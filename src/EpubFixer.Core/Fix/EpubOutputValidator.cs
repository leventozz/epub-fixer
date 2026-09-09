using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using EpubFixer.Core.Decision.Models;
using EpubFixer.Core.Detection.Models;
using EpubFixer.Core.Epub;
using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Fix.Models;
using EpubFixer.Core.Morphology;

namespace EpubFixer.Core.Fix;

internal sealed class EpubOutputValidator
{
    private const string MimetypePath = "mimetype";
    private const string MimetypeContent = "application/epub+zip";

    public EpubValidationResult Validate(
        string inputPath,
        string outputPath,
        byte[] inputHashBefore,
        EpubPackage expectedPackage,
        HyphenationPipelineState expectedFinalState,
        EpubWriteResult writeResult,
        ITurkishMorphologyAnalyzer morphologyAnalyzer)
    {
        var inputHashAfter = SHA256.HashData(File.ReadAllBytes(inputPath));

        if (!inputHashBefore.SequenceEqual(inputHashAfter))
        {
            throw new InvalidDataException("The source EPUB changed while the output was being created.");
        }

        var outputHash = SHA256.HashData(File.ReadAllBytes(outputPath));

        if (inputHashBefore.SequenceEqual(outputHash))
        {
            throw new InvalidDataException("The output EPUB hash is identical to the input EPUB hash.");
        }

        var outputPackage = new EpubPackageReader().Read(outputPath);
        ValidateSpine(expectedPackage, outputPackage);

        if (!string.Equals(
                LogicalTextStreamBuilder.Build(expectedPackage.SpineDocuments).Text,
                outputPackage.LogicalText.Text,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The logical text rebuilt from the output EPUB differs from the corrected Working DOM.");
        }

        var outputFinalState = HyphenationPipeline.Analyze(outputPackage.LogicalText);
        var outputFinalV2State = HyphenationPipeline.AnalyzeV2(
            outputPackage.LogicalText,
            morphologyAnalyzer);
        var expectedCandidates = expectedFinalState.Candidates.Select(CreateCandidateSignature).ToArray();
        var outputCandidates = outputFinalState.Candidates.Select(CreateCandidateSignature).ToArray();

        if (!expectedCandidates.SequenceEqual(outputCandidates))
        {
            throw new InvalidDataException(
                "The output EPUB produces a different hyphenation candidate state after reload.");
        }

        if (outputFinalV2State.Decisions.Any(decision =>
                decision.DecisionKind == HyphenationDecisionKind.AutoFixCandidate
                && decision.Evidence.Candidate.DetectionKind != HyphenationDetectionKind.DocumentBoundary))
        {
            throw new InvalidDataException(
                "AutoFixCandidate decisions remain after reloading the output EPUB.");
        }

        var inventory = ValidateInventoryAndResources(
            inputPath,
            outputPath,
            writeResult.ModifiedDocumentPaths);
        var mimetypeValid = ValidateMimetype(outputPath, inventory.HasMimetype);

        return new EpubValidationResult(
            Convert.ToHexString(inputHashAfter),
            Convert.ToHexString(outputHash),
            new EpubIntegrityResult(
                inventory.EntryCount,
                inventory.UntouchedEntryCount,
                writeResult.ModifiedDocumentPaths,
                ResourceInventoryMatches: true,
                UntouchedResourcesMatch: true,
                MimetypePackagingValid: mimetypeValid,
                ReadBackValidated: true),
            outputFinalState.Candidates.Count,
            outputFinalV2State.Decisions.Count(decision =>
                decision.DecisionKind == HyphenationDecisionKind.AutoFixCandidate));
    }

    private static void ValidateSpine(EpubPackage expected, EpubPackage actual)
    {
        if (!string.Equals(expected.Version, actual.Version, StringComparison.Ordinal)
            || expected.SpineDocuments.Count != actual.SpineDocuments.Count)
        {
            throw new InvalidDataException("The output EPUB package or spine differs from the input package.");
        }

        for (var index = 0; index < expected.SpineDocuments.Count; index++)
        {
            var expectedDocument = expected.SpineDocuments[index];
            var actualDocument = actual.SpineDocuments[index];

            if (!string.Equals(expectedDocument.Path, actualDocument.Path, StringComparison.Ordinal)
                || expectedDocument.IsLinear != actualDocument.IsLinear
                || actualDocument.Document.DocumentElement is null)
            {
                throw new InvalidDataException(
                    $"The output EPUB spine document at index {index} is invalid or has changed.");
            }
        }
    }

    private static InventoryValidation ValidateInventoryAndResources(
        string inputPath,
        string outputPath,
        IReadOnlyList<string> modifiedPaths)
    {
        using var inputFile = File.OpenRead(inputPath);
        using var outputFile = File.OpenRead(outputPath);
        using var inputArchive = new ZipArchive(inputFile, ZipArchiveMode.Read);
        using var outputArchive = new ZipArchive(outputFile, ZipArchiveMode.Read);

        var inputNames = inputArchive.Entries.Select(entry => entry.FullName).ToArray();
        var outputNames = outputArchive.Entries.Select(entry => entry.FullName).ToArray();

        if (inputNames.Length != outputNames.Length
            || !inputNames.OrderBy(name => name, StringComparer.Ordinal)
                .SequenceEqual(outputNames.OrderBy(name => name, StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new InvalidDataException("The output EPUB ZIP entry inventory differs from the input EPUB.");
        }

        var modified = modifiedPaths.ToHashSet(StringComparer.Ordinal);
        var outputByName = outputArchive.Entries.ToDictionary(
            entry => entry.FullName,
            StringComparer.Ordinal);
        var untouchedCount = 0;

        foreach (var inputEntry in inputArchive.Entries)
        {
            if (modified.Contains(inputEntry.FullName))
            {
                continue;
            }

            untouchedCount++;
            using var inputStream = inputEntry.Open();
            using var outputStream = outputByName[inputEntry.FullName].Open();
            var inputHash = SHA256.HashData(inputStream);
            var outputHash = SHA256.HashData(outputStream);

            if (!inputHash.SequenceEqual(outputHash))
            {
                throw new InvalidDataException(
                    $"Untouched EPUB resource '{inputEntry.FullName}' changed in the output archive.");
            }
        }

        return new InventoryValidation(
            inputNames.Length,
            untouchedCount,
            inputNames.Contains(MimetypePath, StringComparer.Ordinal));
    }

    private static bool ValidateMimetype(string outputPath, bool hasMimetype)
    {
        if (!hasMimetype)
        {
            return true;
        }

        using (var file = File.OpenRead(outputPath))
        using (var archive = new ZipArchive(file, ZipArchiveMode.Read))
        {
            var firstEntry = archive.Entries.FirstOrDefault();

            if (firstEntry is null
                || !string.Equals(firstEntry.FullName, MimetypePath, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The EPUB mimetype entry is not the first ZIP entry.");
            }

            using var entryStream = firstEntry.Open();
            using var reader = new StreamReader(entryStream, Encoding.ASCII, false);

            if (!string.Equals(reader.ReadToEnd(), MimetypeContent, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The EPUB mimetype entry has invalid content.");
            }
        }

        using var rawFile = File.OpenRead(outputPath);
        Span<byte> header = stackalloc byte[30];
        rawFile.ReadExactly(header);

        if (BitConverter.ToUInt32(header[..4]) != 0x04034b50
            || BitConverter.ToUInt16(header.Slice(8, 2)) != 0)
        {
            throw new InvalidDataException("The EPUB mimetype entry is compressed or has an invalid ZIP header.");
        }

        var fileNameLength = BitConverter.ToUInt16(header.Slice(26, 2));
        var fileNameBytes = new byte[fileNameLength];
        rawFile.ReadExactly(fileNameBytes);

        if (!string.Equals(Encoding.UTF8.GetString(fileNameBytes), MimetypePath, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The first local ZIP entry is not the EPUB mimetype entry.");
        }

        return true;
    }

    private static CandidateSignature CreateCandidateSignature(HyphenationCandidate candidate)
    {
        return new CandidateSignature(
            candidate.DetectionKind,
            candidate.LeftPart,
            candidate.RightPart,
            candidate.UnhyphenatedText,
            CreateSourceSignature(candidate.LeftSource),
            CreateSourceSignature(candidate.HyphenSource),
            CreateSourceSignature(candidate.RightSource));
    }

    private static SourceSignature CreateSourceSignature(TextSourceLocation source)
    {
        return new SourceSignature(
            source.DocumentPath,
            source.TextNodeIndex,
            source.Start,
            source.Length);
    }

    private sealed record InventoryValidation(
        int EntryCount,
        int UntouchedEntryCount,
        bool HasMimetype);

    private sealed record CandidateSignature(
        HyphenationDetectionKind DetectionKind,
        string LeftPart,
        string RightPart,
        string UnhyphenatedText,
        SourceSignature Left,
        SourceSignature Hyphen,
        SourceSignature Right);

    private sealed record SourceSignature(
        string DocumentPath,
        int TextNodeIndex,
        int Start,
        int Length);
}

internal sealed record EpubValidationResult(
    string InputSha256,
    string OutputSha256,
    EpubIntegrityResult Integrity,
    int RemainingCandidateCount,
    int RemainingAutoFixCandidateCount);
