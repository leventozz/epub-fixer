using EpubFixer.Core.Epub;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Ocr;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Tests;

public sealed class OcrAnomalyDetectorTests
{
    [Fact]
    public void Analyze_FindsMinimumStructuralFamilies()
    {
        using var epub = TemporaryEpub.Create(
            [new TestDocument("chapter", "chapter.xhtml", Xhtml("<p>ger-^ .ckten angajma-ııa iıç ınetre Lckrarlayan ı ı ygun ;;.amanda Viya-ııa'da l'l1 Jo-;ına ço-nıktu l&lt;ilb'de ()nlar y1pranmışt1 dü-^ündüm Avııstıırya'nın e-posta Sankt-Pölten</p>"))],
            [new TestSpineItem("chapter")]);

        var report = new OcrAnomalyDetector().Analyze(
            new EpubPackageReader().Read(epub.Path).LogicalText,
            new FakeMorphologyAnalyzer(["iıç", "ınetre", "Lckrarlayan", "nıktu"]));

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
        Assert.Contains("nıktu", texts);
        Assert.Contains("l<ilb'de", texts);
        Assert.Contains("()nlar", texts);
        Assert.Contains("y1pranmışt1", texts);
        Assert.Contains("dü-^ündüm", texts);
        Assert.Contains("Avııstıırya'nın", texts);
        Assert.DoesNotContain(report.Candidates, item => item.Candidate.Text is "e-posta" or "Sankt-Pölten");
    }

    [Theory]
    [InlineData("Sankt-Pölten")]
    [InlineData("Maria-Zeller")]
    [InlineData("Webern-halefi")]
    [InlineData("Reclam-Universalbibliothek")]
    [InlineData("e-posta")]
    public void Analyze_SingleNormalHyphenDoesNotProduceStructuralEvidence(string text)
    {
        var report = Analyze(text);

        Assert.DoesNotContain(report.Candidates, item => item.DetectionReasons.Any(IsStructural));
        Assert.DoesNotContain(report.Candidates, item => item.DetectionReasons.Contains(OcrDetectionReason.SuspiciousPunctuation));
    }

    [Theory]
    [InlineData("angajma-ııa", OcrConfidence.Medium)]
    [InlineData("dü-^ündüm", OcrConfidence.High)]
    [InlineData("Jo-;ına", OcrConfidence.High)]
    public void Analyze_HyphenWithAnotherStructuralSignalRemainsCandidate(string text, OcrConfidence confidence)
    {
        var candidate = Assert.Single(Analyze(text).Candidates, item => item.Candidate.Text == text);

        Assert.Equal(confidence, candidate.Confidence);
        Assert.Contains(OcrDetectionReason.SuspiciousCharacterSequence, candidate.DetectionReasons);
    }

    [Theory]
    [InlineData("&lt;kelime", "<kelime")]
    [InlineData("kel&lt;ime", "kel<ime")]
    [InlineData(";;kelime", ";;kelime")]
    [InlineData("kel^ime", "kel^ime")]
    [InlineData("()nlar", "()nlar")]
    [InlineData("()𐐀𐐁", "()𐐀𐐁")]
    public void Analyze_AttachedStructuralGlyphSequenceIsHigh(string xhtmlText, string expected)
    {
        var candidate = Assert.Single(Analyze(xhtmlText).Candidates, item => item.Candidate.Text == expected);

        Assert.Equal(OcrConfidence.High, candidate.Confidence);
        Assert.Contains(OcrDetectionReason.SuspiciousCharacter, candidate.DetectionReasons);
    }

    [Fact]
    public void Analyze_IndependentParenthesesAndNormalApostrophesAreNotStructural()
    {
        var report = Analyze("(normal ifade) (...) Joana'nın Viyana'da");

        Assert.DoesNotContain(report.Candidates, item => item.DetectionReasons.Any(IsStructural));
    }

    [Fact]
    public void Analyze_InvalidRareWordsRemainEvidenceOnly()
    {
        var report = Analyze("ınetre Lckrarlayan", ["ınetre", "Lckrarlayan"]);

        Assert.All(report.Candidates, item => Assert.Equal(OcrConfidence.EvidenceOnly, item.Confidence));
        Assert.Equal(["ınetre", "Lckrarlayan"], report.Candidates.Select(item => item.Candidate.Text));
    }

    [Fact]
    public void Analyze_BaseFormFrequencyDoesNotSuppressRareEvidenceOnlyCandidates()
    {
        var report = Analyze("Eiles Eiles'e Eiles'le Joana'mn Joana'nın Joana'ya ınetre",
            ["Eiles", "Eiles'e", "Eiles'le", "Joana'mn", "Joana'nın", "Joana'ya", "ınetre"]);

        Assert.Empty(report.RareInBookSuppressedOccurrences);
        var eilesFamily = report.Candidates
            .Where(item => item.Candidate.Text.StartsWith("Eiles", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(3, eilesFamily.Length);
        Assert.All(eilesFamily, item =>
        {
            Assert.Equal("Eiles", item.BaseForm);
            Assert.Equal(3, item.BaseFormFrequency);
            Assert.Equal(OcrConfidence.EvidenceOnly, item.Confidence);
            Assert.Contains(OcrDetectionReason.RareInBook, item.DetectionReasons);
        });
        var joana = Assert.Single(report.Candidates, item => item.Candidate.Text == "Joana'mn");
        Assert.Equal(OcrConfidence.EvidenceOnly, joana.Confidence);
        Assert.Equal("Joana", joana.BaseForm);
        Assert.Equal(3, joana.BaseFormFrequency);
        Assert.Contains(OcrDetectionReason.RareInBook, joana.DetectionReasons);
        Assert.Contains(report.Candidates, item => item.Candidate.Text == "ınetre"
            && item.DetectionReasons.Contains(OcrDetectionReason.RareInBook));
    }

    [Fact]
    public void Analyze_HyphenOnlyOccurrenceCanRemainAsFragmentEvidenceOnly()
    {
        var candidate = Assert.Single(Analyze("ço-nıktu", ["nıktu"]).Candidates);

        Assert.Equal("nıktu", candidate.Candidate.Text);
        Assert.Equal("ço-", candidate.Candidate.ContextBefore);
        Assert.Equal(OcrConfidence.EvidenceOnly, candidate.Confidence);
        Assert.DoesNotContain(OcrDetectionReason.SuspiciousPunctuation, candidate.DetectionReasons);
    }

    [Fact]
    public void Analyze_PreservesSupplementaryUnicodeLetterHandling()
    {
        Assert.Empty(Analyze("𐐀𐐁").Candidates);
    }

    private static OcrAnalysisReport Analyze(string text, IEnumerable<string>? invalid = null)
    {
        using var epub = TemporaryEpub.Create(
            [new TestDocument("chapter", "chapter.xhtml", Xhtml($"<p>{text}</p>"))],
            [new TestSpineItem("chapter")]);

        return new OcrAnomalyDetector().Analyze(
            new EpubPackageReader().Read(epub.Path).LogicalText,
            new FakeMorphologyAnalyzer(invalid ?? []));
    }

    private static bool IsStructural(OcrDetectionReason reason) =>
        reason is not OcrDetectionReason.MorphologyInvalid and not OcrDetectionReason.RareInBook;

    private static string Xhtml(string body) => $"<?xml version=\"1.0\"?><html xmlns=\"http://www.w3.org/1999/xhtml\"><body>{body}</body></html>";

    private sealed class FakeMorphologyAnalyzer(IEnumerable<string> invalid) : ITurkishMorphologyAnalyzer
    {
        private readonly HashSet<string> invalid = invalid.ToHashSet(StringComparer.Ordinal);
        public bool IsValidWord(string word) => !invalid.Contains(word);
    }
}
