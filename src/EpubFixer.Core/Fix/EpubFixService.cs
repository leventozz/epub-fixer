using System.Security.Cryptography;
using EpubFixer.Core.Correction;
using EpubFixer.Core.Correction.Models;
using EpubFixer.Core.Decision.Models;
using EpubFixer.Core.Epub;
using EpubFixer.Core.Fix.Models;

namespace EpubFixer.Core.Fix;

public sealed class EpubFixService
{
    public EpubFixResult Fix(string inputPath, string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var fullInputPath = Path.GetFullPath(inputPath);
        var fullOutputPath = Path.GetFullPath(outputPath);
        var pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (string.Equals(fullInputPath, fullOutputPath, pathComparison))
        {
            throw new ArgumentException("Input and output EPUB paths must be different.");
        }

        if (!File.Exists(fullInputPath))
        {
            throw new FileNotFoundException("The EPUB file was not found.", fullInputPath);
        }

        if (File.Exists(fullOutputPath))
        {
            throw new IOException($"The output EPUB already exists: '{fullOutputPath}'.");
        }

        var outputDirectory = Path.GetDirectoryName(fullOutputPath)
            ?? throw new ArgumentException("The output EPUB path has no parent directory.");

        if (!Directory.Exists(outputDirectory))
        {
            throw new DirectoryNotFoundException(
                $"The output directory was not found: '{outputDirectory}'.");
        }

        var inputHashBefore = SHA256.HashData(File.ReadAllBytes(fullInputPath));
        var package = new EpubPackageReader().Read(fullInputPath);
        var original = HyphenationPipeline.Analyze(package.LogicalText);
        var originalAutoFixCount = original.Decisions.Count(decision =>
            decision.DecisionKind == HyphenationDecisionKind.AutoFixCandidate);
        var inlinePlans = original.Plans
            .Where(plan => plan.CorrectionKind == HyphenationCorrectionKind.Inline)
            .ToArray();
        var inlineApplyResult = new HyphenationCorrectionApplier().Apply(inlinePlans);

        var afterInlineStream = LogicalTextStreamBuilder.Build(package.SpineDocuments);
        var afterInline = HyphenationPipeline.Analyze(afterInlineStream);
        var crossParagraphPlans = afterInline.Plans
            .Where(plan => plan.CorrectionKind == HyphenationCorrectionKind.CrossParagraph)
            .ToArray();
        var crossParagraphApplyResult =
            new CrossParagraphHyphenationCorrectionApplier().Apply(crossParagraphPlans);

        var finalStream = LogicalTextStreamBuilder.Build(package.SpineDocuments);
        var finalState = HyphenationPipeline.Analyze(finalStream);
        var temporaryPath = Path.Combine(
            outputDirectory,
            $".{Path.GetFileName(fullOutputPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            var writeResult = new EpubPackageWriter().Write(
                fullInputPath,
                temporaryPath,
                package);
            var validation = new EpubOutputValidator().Validate(
                fullInputPath,
                temporaryPath,
                inputHashBefore,
                package,
                finalState,
                writeResult);

            File.Move(temporaryPath, fullOutputPath, overwrite: false);

            return new EpubFixResult(
                fullInputPath,
                fullOutputPath,
                original.Candidates.Count,
                originalAutoFixCount,
                inlineApplyResult,
                crossParagraphApplyResult,
                validation.RemainingCandidateCount,
                validation.RemainingAutoFixCandidateCount,
                validation.InputSha256,
                validation.OutputSha256,
                writeResult,
                validation.Integrity);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
