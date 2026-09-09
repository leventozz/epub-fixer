using EpubFixer.Core.Epub;
using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Ocr;

namespace EpubFixer.Tests;

public sealed class OcrAnalysisReportingTests
{
    [Fact]
    public void SerializeMarkdown_ShowsMarkedCombinedLogicalContextAndEscapesPipes()
    {
        using var epub = TemporaryEpub.Create(
            [new TestDocument("chapter", "chapter.xhtml", Xhtml("<p>önce | ()nlar | sonra</p>"))],
            [new TestSpineItem("chapter")]);
        var report = new OcrAnomalyDetector().Analyze(
            new EpubPackageReader().Read(epub.Path).LogicalText,
            new ValidMorphologyAnalyzer());

        var markdown = OcrAnalysisReporting.SerializeMarkdown(report);

        Assert.Contains("| Logical occurrence context |", markdown);
        Assert.Contains("önce \\| ⟦()nlar⟧ \\| sonra", markdown);
        Assert.DoesNotContain("| Context before | Context after |", markdown);
    }

    [Fact]
    public void CorrectionReport_ShowsV11MetricsTargetsAndExplicitMissingTargets()
    {
        using var epub = TemporaryEpub.Create(
            [new TestDocument("chapter", "chapter.xhtml", Xhtml("<p>Eiles Eiles'e Eiles'le y1pranmışt1.</p>"))],
            [new TestSpineItem("chapter")]);
        var stream = new EpubPackageReader().Read(epub.Path).LogicalText;
        var analyzer = new SelectiveMorphologyAnalyzer(["yıpranmıştı"]);
        var source = new OcrAnomalyDetector().Analyze(stream, analyzer);
        var report = new OcrCorrectionCandidateGenerator().Generate(
            source, stream, new BookLexiconBuilder().Build(stream), analyzer);

        var markdown = OcrCorrectionAnalysisReporting.SerializeMarkdown(report);

        Assert.Contains("# OCR Correction Candidate Generation V1.1", markdown);
        Assert.Contains("## V1 → V1.1 proposal coverage", markdown);
        Assert.Contains("Multi-step structural proposals count", markdown);
        Assert.Contains("## Target Regression Examples", markdown);
        Assert.Contains("### `Eiles`", markdown);
        Assert.Contains("- BaseFormFrequency: 3", markdown);
        Assert.Contains("### `y1pranmışt1`", markdown);
        Assert.Contains("| yıpranmıştı |", markdown);
        Assert.Contains("Not found in current post-hyphenation logical stream", markdown);
    }

    private static string Xhtml(string body) =>
        $"<?xml version=\"1.0\"?><html xmlns=\"http://www.w3.org/1999/xhtml\"><body>{body}</body></html>";

    private sealed class ValidMorphologyAnalyzer : ITurkishMorphologyAnalyzer
    {
        public bool IsValidWord(string word) => true;
    }

    private sealed class SelectiveMorphologyAnalyzer(IEnumerable<string> valid) : ITurkishMorphologyAnalyzer
    {
        private readonly HashSet<string> _valid = valid.ToHashSet(StringComparer.Ordinal);
        public bool IsValidWord(string word) => _valid.Contains(word);
    }
}
