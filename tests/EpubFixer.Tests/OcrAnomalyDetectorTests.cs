using EpubFixer.Core.Epub;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Ocr;

namespace EpubFixer.Tests;

public sealed class OcrAnomalyDetectorTests
{
    [Fact]
    public void Analyze_FindsMinimumStructuralFamilies()
    {
        using var epub = TemporaryEpub.Create(
            [new TestDocument("chapter", "chapter.xhtml", Xhtml("<p>ger-^ .ckten angajma-ııa iıç ınetre Lckrarlayan ı ı ygun ;;.amanda Viya-ııa'da l'l1 Jo-;ına ço-nıktu l&lt;ilb'de e-posta Sankt-Pölten</p>"))],
            [new TestSpineItem("chapter")]);

        var report = new OcrAnomalyDetector().Analyze(
            new EpubPackageReader().Read(epub.Path).LogicalText,
            new FakeMorphologyAnalyzer(["iıç", "ınetre", "Lckrarlayan"]));

        var texts = report.Candidates.Select(item => item.Candidate.Text).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("ger-^ .ckten", texts);
        Assert.Contains("angajma-ııa", texts);
        Assert.Contains("iıç", texts);
        Assert.Contains("ınetre", texts);
        Assert.Contains("Lckrarlayan", texts);
        Assert.Contains("ı ı ygun", texts);
        Assert.Contains(";;.amanda", texts);
        Assert.Contains("Viya-ııa'da", texts);
        Assert.Contains("l'l1", texts);
        Assert.Contains("Jo-;ına", texts);
        Assert.Contains("ço-nıktu", texts);
        Assert.Contains("l<ilb'de", texts);
        Assert.DoesNotContain(report.Candidates, item => item.Candidate.Text is "e-posta" or "Sankt-Pölten");
    }

    private static string Xhtml(string body) => $"<?xml version=\"1.0\"?><html xmlns=\"http://www.w3.org/1999/xhtml\"><body>{body}</body></html>";

    private sealed class FakeMorphologyAnalyzer(IEnumerable<string> invalid) : ITurkishMorphologyAnalyzer
    {
        private readonly HashSet<string> invalid = invalid.ToHashSet(StringComparer.Ordinal);
        public bool IsValidWord(string word) => !invalid.Contains(word);
    }
}
