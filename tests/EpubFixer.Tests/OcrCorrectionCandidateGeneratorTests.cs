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
            stream,
            new BookLexiconBuilder().Build(stream),
            new ValidMorphologyAnalyzer());

        var occurrence = Assert.Single(result.Occurrences);
        var proposal = Assert.Single(occurrence.Proposals, item => item.ProposedText == "oyuncu");
        Assert.Contains(OcrCorrectionGenerationReason.GlyphSubstitution, proposal.GenerationReasons);
        Assert.Contains(OcrCorrectionGenerationReason.BookLexiconNeighbor, proposal.GenerationReasons);
        Assert.Equal(1, proposal.BookFrequency);
        Assert.True(proposal.TrMorphValid);
        Assert.Equal(1, proposal.GenerationCost);
        Assert.Equal(1, proposal.StructuralTransformationCount);
    }

    [Fact]
    public void Generate_IsRuneSafeAndBounded()
    {
        var report = Analyze("()𐐀𐐁", out var stream);
        var result = new OcrCorrectionCandidateGenerator().Generate(
            report,
            stream,
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
            stream,
            new BookLexiconBuilder().Build(stream),
            new ValidMorphologyAnalyzer());

        var occurrence = Assert.Single(result.Occurrences, item => item.Source.Candidate.Text.StartsWith("y1pranmışt1", StringComparison.Ordinal));
        Assert.Contains(occurrence.Proposals, item => item.ProposedText == "yıpranmıştı");
    }

    [Fact]
    public void Generate_ComposesMultipleGlyphsOnPunctuationFreeCore()
    {
        var analyzer = new RecordingMorphologyAnalyzer(["yıpranmıştı"]);
        var report = Analyze("y1pranmışt1.", out var stream, analyzer);
        analyzer.Clear();

        var result = Generate(report, stream, analyzer);

        var occurrence = Assert.Single(result.Occurrences);
        var proposal = Assert.Single(occurrence.Proposals, item => item.ProposedText == "yıpranmıştı");
        Assert.True(proposal.TrMorphValid);
        Assert.Equal(2, proposal.StructuralTransformationCount);
        Assert.Equal("y1pranmışt1", occurrence.LexicalCore);
        Assert.Equal(".", occurrence.SuffixPunctuation);
        Assert.DoesNotContain(analyzer.Inputs, input => input.EndsWith(".", StringComparison.Ordinal));
    }

    [Fact]
    public void Generate_KeepsApostropheInsideLexicalCore()
    {
        var analyzer = new RecordingMorphologyAnalyzer(["Joana'nın"]);
        var report = Analyze("Joana'1n Joana'nın", out var stream, analyzer);

        var occurrence = Assert.Single(Generate(report, stream, analyzer).Occurrences,
            item => item.Source.Candidate.Text == "Joana'1n");

        Assert.Equal("Joana'1n", occurrence.LexicalCore);
        Assert.Contains(occurrence.Proposals, item => item.ProposedText == "Joana'nın");
    }

    [Fact]
    public void Generate_RetainsBaseFormFamilyCandidateAndFindsBookProposal()
    {
        var analyzer = new RecordingMorphologyAnalyzer(["Joana'nın", "Joana'ya"]);
        var report = Analyze("Joana'mn Joana'nın Joana'ya", out var stream, analyzer);

        var occurrence = Assert.Single(Generate(report, stream, analyzer).Occurrences,
            item => item.Source.Candidate.Text == "Joana'mn");

        Assert.Equal(OcrConfidence.EvidenceOnly, occurrence.Source.Confidence);
        Assert.Equal("Joana", occurrence.Source.BaseForm);
        Assert.Equal(3, occurrence.Source.BaseFormFrequency);
        Assert.Contains(occurrence.Proposals, item => item.ProposedText == "Joana'nın");
    }

    [Fact]
    public void Generate_EnforcesStructuralDepthProposalLimitAndDeterministicOrdering()
    {
        var analyzer = new RecordingMorphologyAnalyzer(["alblcldl"]);
        var report = Analyze("a1b1c1d1", out var stream, analyzer);

        var first = Generate(report, stream, analyzer);
        var second = Generate(report, stream, analyzer);

        Assert.DoesNotContain(first.Occurrences.SelectMany(item => item.Proposals), item => item.ProposedText == "alblcldl");
        Assert.All(first.Occurrences, occurrence => Assert.InRange(occurrence.Proposals.Count, 0, 10));
        Assert.Equal(
            first.Occurrences.SelectMany(item => item.Proposals).Select(item => item.ProposedText),
            second.Occurrences.SelectMany(item => item.Proposals).Select(item => item.ProposedText));
    }

    [Fact]
    public void Generate_ComposesOnlyEligibleImmediatelyAdjacentFragment()
    {
        var analyzer = new RecordingMorphologyAnalyzer(["ilgili", "ilgiliydi"]);
        var report = Analyze("ilgi-1 iydi ilgili", out var stream, analyzer);

        var result = Generate(report, stream, analyzer);

        var occurrence = Assert.Single(result.Occurrences, item => item.Source.Candidate.Text == "ilgi-1");
        var composite = Assert.Single(occurrence.Proposals, item => item.ProposedText == "ilgiliydi");
        Assert.True(composite.TrMorphValid);
        Assert.True(composite.ConsumesMultipleOccurrences);
        Assert.Equal(2, composite.SourceSpanCount);
        Assert.Contains(OcrCorrectionGenerationReason.AdjacentFragmentComposition, composite.GenerationReasons);
        Assert.Contains(occurrence.Proposals, item => item.ProposedText == "ilgili" && item.IsPartialStructuralRepair);
    }

    [Fact]
    public void Generate_DoesNotComposeAcrossNormalTextOrSentenceBoundary()
    {
        var analyzer = new RecordingMorphologyAnalyzer(["ilgiliydi"]);
        var normalReport = Analyze("ilgi-1 normal iydi", out var normalStream, analyzer);
        var sentenceReport = Analyze("ilgi-1. iydi", out var sentenceStream, analyzer);

        Assert.DoesNotContain(Generate(normalReport, normalStream, analyzer).Occurrences.SelectMany(item => item.Proposals), IsAdjacent);
        Assert.DoesNotContain(Generate(sentenceReport, sentenceStream, analyzer).Occurrences.SelectMany(item => item.Proposals), IsAdjacent);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Generate_DoesNotComposeAcrossParagraphOrDocumentBoundary(bool documentBoundary)
    {
        var analyzer = new RecordingMorphologyAnalyzer(["ilgiliydi"]);
        using var epub = documentBoundary
            ? TemporaryEpub.Create(
                [new TestDocument("first", "first.xhtml", Xhtml("<p>ilgi-1</p>")),
                 new TestDocument("second", "second.xhtml", Xhtml("<p>iydi</p>"))],
                [new TestSpineItem("first"), new TestSpineItem("second")])
            : TemporaryEpub.Create(
                [new TestDocument("chapter", "chapter.xhtml", Xhtml("<p>ilgi-1</p><p>iydi</p>"))],
                [new TestSpineItem("chapter")]);
        var stream = new EpubPackageReader().Read(epub.Path).LogicalText;
        var report = new OcrAnomalyDetector().Analyze(stream, analyzer);

        var result = Generate(report, stream, analyzer);

        Assert.DoesNotContain(result.Occurrences.SelectMany(item => item.Proposals), IsAdjacent);
    }

    [Fact]
    public void Generate_ExpandsAttachedCaretAndAppliesGenericGlyphSubstitution()
    {
        var analyzer = new RecordingMorphologyAnalyzer(["şiddetli"]);
        var report = Analyze("bir ^iddetli", out var stream, analyzer);

        var occurrence = Assert.Single(Generate(report, stream, analyzer).Occurrences,
            item => item.Source.Candidate.Text == "iddetli");

        Assert.Equal("^iddetli", occurrence.WorkingSource.Text);
        var proposal = Assert.Single(occurrence.Proposals, item => item.ProposedText == "şiddetli");
        Assert.True(proposal.TrMorphValid);
        Assert.Equal(0, proposal.BookFrequency);
        Assert.Equal(1, proposal.StructuralTransformationCount);
        Assert.Contains(OcrCorrectionGenerationReason.GlyphSubstitution, proposal.GenerationReasons);
    }

    [Theory]
    [InlineData("a^a", "aşa", 1)]
    [InlineData("dü-^ündüm", "düşündüm", 2)]
    public void Generate_CaretSubstitutionIsGenericAndComposesWithExistingStructuralSteps(
        string source,
        string expected,
        int structuralSteps)
    {
        var analyzer = new RecordingMorphologyAnalyzer([expected]);
        var report = Analyze(source, out var stream, analyzer);

        var proposal = Assert.Single(Generate(report, stream, analyzer).Occurrences
            .SelectMany(item => item.Proposals), item => item.ProposedText == expected);

        Assert.True(proposal.TrMorphValid);
        Assert.Equal(structuralSteps, proposal.StructuralTransformationCount);
        Assert.Contains(OcrCorrectionGenerationReason.GlyphSubstitution, proposal.GenerationReasons);
        if (structuralSteps > 1)
            Assert.Contains(OcrCorrectionGenerationReason.HyphenRemoval, proposal.GenerationReasons);
    }

    [Fact]
    public void Generate_ProducesNoProposalWhenNoBoundedNeighborExists()
    {
        var report = Analyze("zzzzzzzzzz", out var stream, ["zzzzzzzzzz"]);
        var result = new OcrCorrectionCandidateGenerator().Generate(
            report,
            stream,
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

    private static OcrAnalysisReport Analyze(string text, out EpubFixer.Core.Epub.Models.LogicalTextStream stream, ITurkishMorphologyAnalyzer analyzer)
    {
        using var epub = TemporaryEpub.Create(
            [new TestDocument("chapter", "chapter.xhtml", Xhtml($"<p>{text}</p>"))],
            [new TestSpineItem("chapter")]);
        stream = new EpubPackageReader().Read(epub.Path).LogicalText;
        return new OcrAnomalyDetector().Analyze(stream, analyzer);
    }

    private static OcrCorrectionAnalysisReport Generate(
        OcrAnalysisReport report,
        EpubFixer.Core.Epub.Models.LogicalTextStream stream,
        ITurkishMorphologyAnalyzer analyzer) =>
        new OcrCorrectionCandidateGenerator().Generate(report, stream,
            new BookLexiconBuilder().Build(stream), analyzer);

    private static bool IsAdjacent(OcrCorrectionCandidate candidate) =>
        candidate.GenerationReasons.Contains(OcrCorrectionGenerationReason.AdjacentFragmentComposition);

    private static string Xhtml(string body) => $"<?xml version=\"1.0\"?><html xmlns=\"http://www.w3.org/1999/xhtml\"><body>{body}</body></html>";

    private sealed class ValidMorphologyAnalyzer : ITurkishMorphologyAnalyzer
    {
        private readonly HashSet<string> _invalid;
        public ValidMorphologyAnalyzer(IEnumerable<string>? invalid = null) => _invalid = (invalid ?? []).ToHashSet(StringComparer.Ordinal);
        public bool IsValidWord(string word) => !_invalid.Contains(word);
    }

    private sealed class RecordingMorphologyAnalyzer(IEnumerable<string> valid) : ITurkishMorphologyAnalyzer
    {
        private readonly HashSet<string> _valid = valid.ToHashSet(StringComparer.Ordinal);
        public List<string> Inputs { get; } = [];
        public bool IsValidWord(string word)
        {
            Inputs.Add(word);
            return _valid.Contains(word);
        }
        public void Clear() => Inputs.Clear();
    }
}
