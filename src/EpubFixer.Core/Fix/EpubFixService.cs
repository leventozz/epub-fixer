using System.Security.Cryptography;
using EpubFixer.Core.Correction;
using EpubFixer.Core.Correction.Models;
using EpubFixer.Core.Decision.Models;
using EpubFixer.Core.Detection.Models;
using EpubFixer.Core.Epub;
using EpubFixer.Core.Fix.Models;
using EpubFixer.Core.Morphology;

namespace EpubFixer.Core.Fix;

public sealed class EpubFixService
{
    private readonly ITurkishMorphologyAnalyzer morphologyAnalyzer;

    public EpubFixService(ITurkishMorphologyAnalyzer morphologyAnalyzer)
    {
        this.morphologyAnalyzer = morphologyAnalyzer
            ?? throw new ArgumentNullException(nameof(morphologyAnalyzer));
    }

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

        var afterV1Stream = LogicalTextStreamBuilder.Build(package.SpineDocuments);
        var v2 = HyphenationPipeline.AnalyzeV2(afterV1Stream, morphologyAnalyzer);
        var v2Auto = v2.Decisions.Where(d => d.DecisionKind == HyphenationDecisionKind.AutoFixCandidate).ToArray();
        var v2InlinePlans = v2.Plans.Where(p => p.CorrectionKind == HyphenationCorrectionKind.Inline).ToArray();
        var approvedV2Cross = v2.Plans
            .Where(p => p.CorrectionKind == HyphenationCorrectionKind.CrossParagraph)
            .Select(p => p.Decision.Evidence.Candidate)
            .ToArray();
        var v2InlineResult = new HyphenationCorrectionApplier().Apply(v2InlinePlans);

        var afterV2Inline = LogicalTextStreamBuilder.Build(package.SpineDocuments);
        var refreshedV2 = HyphenationPipeline.AnalyzeV2(afterV2Inline, morphologyAnalyzer);
        var v2CrossPlans = refreshedV2.Plans
            .Where(p => p.CorrectionKind == HyphenationCorrectionKind.CrossParagraph)
            .Where(p => approvedV2Cross.Any(candidate =>
                ReferenceEquals(candidate.LeftSource.SourceNode, p.LeftSource.SourceNode)
                && ReferenceEquals(candidate.HyphenSource.SourceNode, p.HyphenSource.SourceNode)
                && ReferenceEquals(candidate.RightSource.SourceNode, p.RightSource.SourceNode)
                && string.Equals(candidate.LeftPart, p.Decision.Evidence.Candidate.LeftPart, StringComparison.Ordinal)
                && string.Equals(candidate.RightPart, p.Decision.Evidence.Candidate.RightPart, StringComparison.Ordinal)
                && string.Equals(candidate.UnhyphenatedText, p.UnhyphenatedText, StringComparison.Ordinal)))
            .ToArray();
        var v2CrossResult = new CrossParagraphHyphenationCorrectionApplier().Apply(v2CrossPlans);

        var finalStream = LogicalTextStreamBuilder.Build(package.SpineDocuments);
        var finalState = HyphenationPipeline.Analyze(finalStream);
        var v2Kinds = new V2DetectionKindCounts(
            v2Auto.Count(d => d.Evidence.Candidate.DetectionKind == HyphenationDetectionKind.Inline),
            v2Auto.Count(d => d.Evidence.Candidate.DetectionKind == HyphenationDetectionKind.ParagraphBoundary),
            v2Auto.Count(d => d.Evidence.Candidate.DetectionKind == HyphenationDetectionKind.DocumentBoundary),
            v2Auto.Count(d => d.Evidence.Candidate.DetectionKind is not HyphenationDetectionKind.Inline
                and not HyphenationDetectionKind.ParagraphBoundary
                and not HyphenationDetectionKind.DocumentBoundary));
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
                writeResult,
                morphologyAnalyzer);

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
                validation.Integrity,
                v2Auto.Length,
                v2Kinds,
                v2InlinePlans.Length + v2CrossPlans.Length,
                v2InlineResult,
                v2CrossResult,
                v2Kinds.DocumentBoundary);
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
