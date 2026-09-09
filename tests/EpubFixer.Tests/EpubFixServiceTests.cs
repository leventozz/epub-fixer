using System.Security.Cryptography;
using EpubFixer.Core.Fix;
using EpubFixer.Core.Correction.Models;
using EpubFixer.Core.Fix.Models;
using EpubFixer.TrMorph;

namespace EpubFixer.Tests;

public sealed class EpubFixServiceTests
{
    [Fact]
    public void Fix_RealEpubWritesValidatedOutputWithoutChangingInput()
    {
        var inputPath = Path.Combine(
            AppContext.BaseDirectory,
            "test-data",
            "Odun Kesmek_recognized.epub");
        var outputPath = Path.Combine(
            Path.GetTempPath(),
            $"odun-kesmek-fixed-{Guid.NewGuid():N}.epub");
        var inputHashBefore = SHA256.HashData(File.ReadAllBytes(inputPath));

        try
        {
            using var analyzer = new FomaTurkishMorphologyAnalyzer();
            var result = new EpubFixService(analyzer).Fix(inputPath, outputPath);

            Assert.Equal(448, result.OriginalCandidateCount);
            Assert.Equal(148, result.OriginalAutoFixCandidateCount);
            Assert.Equal(129, result.InlineApplyResult.AppliedCount);
            Assert.Equal(0, result.InlineApplyResult.SkippedCount);
            Assert.Equal(19, result.CrossParagraphApplyResult.AppliedCount);
            Assert.Equal(0, result.CrossParagraphApplyResult.SkippedCount);
            Assert.Equal(215, result.RemainingCandidateCount);
            Assert.Equal(1, result.RemainingAutoFixCandidateCount);
            Assert.Equal(86, result.V2AutoFixCandidateCount);
            Assert.Equal(new V2DetectionKindCounts(56, 29, 1, 0), result.V2DetectionKinds);
            Assert.Equal(85, result.V2PlannedCount);
            Assert.Equal(new HyphenationCorrectionApplyResult(56, 0), result.V2InlineApplyResult);
            Assert.Equal(new HyphenationCorrectionApplyResult(29, 0), result.V2CrossParagraphApplyResult);
            Assert.Equal(1, result.V2UnsupportedDocumentBoundaryCount);
            Assert.Equal(["main-3.xhtml", "main-4.xhtml", "main.xhtml"], result.WriteResult.ModifiedDocumentPaths);
            Assert.Equal(16, result.Integrity.EntryCount);
            Assert.Equal(13, result.Integrity.UntouchedEntryCount);
            Assert.True(result.Integrity.ResourceInventoryMatches);
            Assert.True(result.Integrity.UntouchedResourcesMatch);
            Assert.True(result.Integrity.MimetypePackagingValid);
            Assert.True(result.Integrity.ReadBackValidated);
            Assert.Equal(inputHashBefore, SHA256.HashData(File.ReadAllBytes(inputPath)));
            Assert.NotEqual(result.InputSha256, result.OutputSha256);
            Assert.True(File.Exists(outputPath));
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void Fix_RejectsInputAndOutputAtSamePath()
    {
        using var epub = TemporaryEpub.Create(
            [new TestDocument("chapter", "chapter.xhtml", Xhtml("<p>text</p>"))],
            [new TestSpineItem("chapter")]);
        var inputHash = SHA256.HashData(File.ReadAllBytes(epub.Path));

        using var analyzer = new FomaTurkishMorphologyAnalyzer();
        Assert.Throws<ArgumentException>(() =>
            new EpubFixService(analyzer).Fix(epub.Path, epub.Path));
        Assert.Equal(inputHash, SHA256.HashData(File.ReadAllBytes(epub.Path)));
    }

    [Fact]
    public void Fix_RejectsExistingOutput()
    {
        using var epub = TemporaryEpub.Create(
            [new TestDocument("chapter", "chapter.xhtml", Xhtml("<p>text</p>"))],
            [new TestSpineItem("chapter")]);
        var outputPath = Path.Combine(Path.GetTempPath(), $"existing-{Guid.NewGuid():N}.epub");
        File.WriteAllText(outputPath, "do not replace");

        try
        {
            using var analyzer = new FomaTurkishMorphologyAnalyzer();
            Assert.Throws<IOException>(() =>
                new EpubFixService(analyzer).Fix(epub.Path, outputPath));
            Assert.Equal("do not replace", File.ReadAllText(outputPath));
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    private static string Xhtml(string body)
    {
        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <!DOCTYPE html>
            <html xmlns="http://www.w3.org/1999/xhtml">
              <head><title></title></head>
              <body>{body}</body>
            </html>
            """;
    }
}
