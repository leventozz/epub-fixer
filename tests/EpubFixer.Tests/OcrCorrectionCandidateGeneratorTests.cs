using EpubFixer.Core.Epub;
using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Ocr;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Tests;

public sealed class OcrCorrectionCandidateGeneratorTests
{
    [Fact]
    public void Generate_UsesOnlyDetectorOccurrences_AndMergesDuplicateReasons()
    {
        var report = Analyze("oyuncu ()yuncu", out var stream);
        var result = new OcrCorrectionCandidateGenerator().Generate(
            report,
            new BookLexiconBuilder().Build(stream),
            new ValidMorphologyAnalyzer());

        var occurrence = Assert.Single(result.Occurrences);
        var proposal = Assert.Single(occurrence.Proposals, item => item.ProposedText == "oyuncu");
        Assert.Contains(OcrCorrectionGenerationReason.GlyphSubstitution, proposal.GenerationReasons);
        Assert.Contains(OcrCorrectionGenerationReason.BookLexiconNeighbor, proposal.GenerationReasons);
        Assert.Equal(1, proposal.BookFrequency);
        Assert.True(proposal.TrMorphValid);
    }

    [Fact]
    public void Generate_IsRuneSafeAndBounded()
    {
        var report = Analyze("()𐐀𐐁", out var stream);
        var result = new OcrCorrectionCandidateGenerator().Generate(
            report,
            new BookLexiconBuilder().Build(stream),
            new ValidMorphologyAnalyzer());

        Assert.All(result.Occurrences, occurrence => Assert.InRange(occurrence.Proposals.Count, 0, 10));
        Assert.All(result.Occurrences.SelectMany(item => item.Proposals), item => Assert.InRange(item.EditDistance, 0, 2));
    }

    [Fact]
    public void Generate_ChainsDigitGlyphsAndLexiconNeighbor()
    {
        var report = Analyze("y1pranmışt1 yıpranmıştı", out var stream);
        var result = new OcrCorrectionCandidateGenerator().Generate(
            report,
            new BookLexiconBuilder().Build(stream),
            new ValidMorphologyAnalyzer());

        var occurrence = Assert.Single(result.Occurrences, item => item.Source.Candidate.Text.StartsWith("y1pranmışt1", StringComparison.Ordinal));
        Assert.Contains(occurrence.Proposals, item => item.ProposedText == "yıpranmıştı");
    }

    [Fact]
    public void Generate_ProducesNoProposalWhenNoBoundedNeighborExists()
    {
        var report = Analyze("zzzzzzzzzz", out var stream, ["zzzzzzzzzz"]);
        var result = new OcrCorrectionCandidateGenerator().Generate(
            report,
            new BookLexiconBuilder().Build(stream),
            new ValidMorphologyAnalyzer());

        Assert.Single(result.Occurrences);
        Assert.Empty(result.Occurrences[0].Proposals);
    }

    private static OcrAnalysisReport Analyze(string text, out EpubFixer.Core.Epub.Models.LogicalTextStream stream, IEnumerable<string>? invalid = null)
    {
        using var epub = TemporaryEpub.Create(
            [new TestDocument("chapter", "chapter.xhtml", Xhtml($"<p>{text}</p>"))],
            [new TestSpineItem("chapter")]);
        stream = new EpubPackageReader().Read(epub.Path).LogicalText;
        return new OcrAnomalyDetector().Analyze(stream, new ValidMorphologyAnalyzer(invalid));
    }

    private static string Xhtml(string body) => $"<?xml version=\"1.0\"?><html xmlns=\"http://www.w3.org/1999/xhtml\"><body>{body}</body></html>";

    private sealed class ValidMorphologyAnalyzer : ITurkishMorphologyAnalyzer
    {
        private readonly HashSet<string> _invalid;
        public ValidMorphologyAnalyzer(IEnumerable<string>? invalid = null) => _invalid = (invalid ?? []).ToHashSet(StringComparer.Ordinal);
        public bool IsValidWord(string word) => !_invalid.Contains(word);
    }
}
