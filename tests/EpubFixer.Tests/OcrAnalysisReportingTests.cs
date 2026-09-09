using EpubFixer.Core.Epub;
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

    private static string Xhtml(string body) =>
        $"<?xml version=\"1.0\"?><html xmlns=\"http://www.w3.org/1999/xhtml\"><body>{body}</body></html>";

    private sealed class ValidMorphologyAnalyzer : ITurkishMorphologyAnalyzer
    {
        public bool IsValidWord(string word) => true;
    }
}
