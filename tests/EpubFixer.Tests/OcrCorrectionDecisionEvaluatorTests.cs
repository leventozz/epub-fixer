using EpubFixer.Core.Decision;
using EpubFixer.Core.Decision.Models;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Tests;

public sealed class OcrCorrectionDecisionEvaluatorTests
{
    [Theory]
    [InlineData("J3arış", OcrCasePattern.TitleCase)]
    [InlineData("()yuncu", OcrCasePattern.Lowercase)]
    [InlineData("BAR3", OcrCasePattern.Uppercase)]
    [InlineData("123", OcrCasePattern.NoLetters)]
    public void CasePattern_IgnoresNonLetters(string value, OcrCasePattern expected) =>
        Assert.Equal(expected, OcrCorrectionDecisionEvaluator.GetCasePattern(value));

    [Fact]
    public void EvidenceOnly_RequiresDominance()
    {
        var source = Occurrence("onlarm", OcrConfidence.EvidenceOnly,
            Proposal("onları", 60), Proposal("onlar", 59), Proposal("onlara", 53));

        var decision = Evaluate(source);

        Assert.Equal(OcrCorrectionDecisionKind.Review, decision.DecisionKind);
        Assert.Contains(OcrCorrectionDecisionReason.InsufficientFrequencyDominance, decision.DecisionReasons);
    }

    [Fact]
    public void EvidenceOnly_LowercaseDominantProposalIsAutoFix()
    {
        var source = Occurrence("liyatro", OcrConfidence.EvidenceOnly, Proposal("tiyatro", 37));

        var decision = Evaluate(source);

        Assert.Equal(OcrCorrectionDecisionKind.AutoFixCandidate, decision.DecisionKind);
        Assert.Equal("tiyatro", decision.SelectedProposal!.Proposal.ProposedText);
    }

    [Fact]
    public void ProperNameRisk_BlocksGenericFuzzyAutoFix()
    {
        var source = Occurrence("Metis", OcrConfidence.EvidenceOnly, Proposal("Mitos", 6));

        var decision = Evaluate(source);

        Assert.Equal(OcrCorrectionDecisionKind.Review, decision.DecisionKind);
        Assert.Contains(OcrCorrectionDecisionReason.ProperNameRisk, decision.DecisionReasons);
    }

    [Fact]
    public void StructuralWorkingSpanCanOverrideEvidenceOnlyClassification()
    {
        var sourceCandidate = Candidate("iddetli");
        var working = Candidate("^iddetli");
        var evidence = new OcrWordEvidence(sourceCandidate, 0, "iddetli", 0, false,
            [OcrDetectionReason.MorphologyInvalid, OcrDetectionReason.RareInBook], OcrConfidence.EvidenceOnly);
        var proposal = Proposal("şiddetli", 0, [OcrCorrectionGenerationReason.StructuralNormalization, OcrCorrectionGenerationReason.GlyphSubstitution]);
        var occurrence = new OcrCorrectionOccurrence(evidence, working, "", "iddetli", "", [proposal]);

        var decision = Evaluate(occurrence);

        Assert.Equal(OcrCorrectionDecisionKind.AutoFixCandidate, decision.DecisionKind);
        Assert.Equal("şiddetli", decision.SelectedProposal!.Proposal.ProposedText);
    }

    [Fact]
    public void ProposalOrderDoesNotChangeDecision()
    {
        var first = Evaluate(Occurrence("liyatro", OcrConfidence.EvidenceOnly, Proposal("tiyatro", 37), Proposal("tiyatro", 37, editDistance: 2)));
        var second = Evaluate(Occurrence("liyatro", OcrConfidence.EvidenceOnly, Proposal("tiyatro", 37, editDistance: 2), Proposal("tiyatro", 37)));

        Assert.Equal(first.DecisionKind, second.DecisionKind);
        Assert.Equal(OcrCorrectionDecisionKind.Review, first.DecisionKind);
        Assert.Null(first.SelectedProposal);
        Assert.Null(second.SelectedProposal);
    }

    [Fact]
    public void StructuralApostropheException_AllowsAlignedCaseWithInvalidMorphology()
    {
        var source = StructuralApostropheOccurrence("l<ilb'de", Proposal("Kilb'de", 30,
            [OcrCorrectionGenerationReason.StructuralNormalization, OcrCorrectionGenerationReason.GarbageRemoval,
             OcrCorrectionGenerationReason.BookLexiconNeighbor], editDistance: 2));

        var decision = Evaluate(source);

        Assert.Equal(OcrCorrectionDecisionKind.AutoFixCandidate, decision.DecisionKind);
        Assert.True(decision.SelectedProposal!.CaseCompatible);
        Assert.Contains(OcrCorrectionDecisionReason.CaseCompatible, decision.DecisionReasons);
    }

    [Fact]
    public void StructuralApostropheException_RequiresApostropheAndSuffixAndUniqueSafety()
    {
        var noApostrophe = Evaluate(StructuralApostropheOccurrence("l<ilbde", Proposal("Kilbde", 30,
            [OcrCorrectionGenerationReason.StructuralNormalization, OcrCorrectionGenerationReason.GarbageRemoval,
             OcrCorrectionGenerationReason.BookLexiconNeighbor], editDistance: 2)));
        Assert.NotEqual(OcrCorrectionDecisionKind.AutoFixCandidate, noApostrophe.DecisionKind);

        var changedSuffix = Evaluate(StructuralApostropheOccurrence("l<ilb'de", Proposal("Kilb'e", 30,
            [OcrCorrectionGenerationReason.StructuralNormalization, OcrCorrectionGenerationReason.GarbageRemoval,
             OcrCorrectionGenerationReason.BookLexiconNeighbor], editDistance: 2)));
        Assert.NotEqual(OcrCorrectionDecisionKind.AutoFixCandidate, changedSuffix.DecisionKind);

        var tied = Evaluate(StructuralApostropheOccurrence("l<ilb'de",
            Proposal("Kilb'de", 30, [OcrCorrectionGenerationReason.StructuralNormalization, OcrCorrectionGenerationReason.GarbageRemoval, OcrCorrectionGenerationReason.BookLexiconNeighbor], editDistance: 2),
            Proposal("Kilbb'de", 30, [OcrCorrectionGenerationReason.StructuralNormalization, OcrCorrectionGenerationReason.GarbageRemoval, OcrCorrectionGenerationReason.BookLexiconNeighbor], editDistance: 2)));
        Assert.Equal(OcrCorrectionDecisionKind.Review, tied.DecisionKind);
    }

    private static OcrCorrectionDecision Evaluate(OcrCorrectionOccurrence occurrence)
    {
        var analysis = new OcrCorrectionAnalysisReport(
            new OcrAnalysisReport(1, 1, 0, [occurrence.Source], [], 0), [occurrence], []);
        return Assert.Single(new OcrCorrectionDecisionEvaluator().Evaluate(analysis).Decisions);
    }

    private static OcrCorrectionOccurrence Occurrence(string source, OcrConfidence confidence, params OcrCorrectionCandidate[] proposals)
    {
        var candidate = Candidate(source);
        var evidence = new OcrWordEvidence(candidate, 1, source, 1, false,
            [OcrDetectionReason.MorphologyInvalid, OcrDetectionReason.RareInBook], confidence);
        return new OcrCorrectionOccurrence(evidence, candidate, "", source, "", proposals);
    }

    private static OcrCorrectionOccurrence StructuralApostropheOccurrence(string source, params OcrCorrectionCandidate[] proposals)
    {
        var candidate = Candidate(source);
        var evidence = new OcrWordEvidence(candidate, 0, source, 0, false,
            [OcrDetectionReason.SuspiciousCharacter, OcrDetectionReason.MorphologyInvalid], OcrConfidence.High);
        return new OcrCorrectionOccurrence(evidence, candidate, "", source, "", proposals);
    }

    private static OcrWordCandidate Candidate(string text) => new(text, 0, [], "test.xhtml", "", "");

    private static OcrCorrectionCandidate Proposal(
        string text,
        int frequency,
        IReadOnlyList<OcrCorrectionGenerationReason>? reasons = null,
        int editDistance = 1) =>
        new(Candidate("source"), OcrConfidence.EvidenceOnly, text, reasons ?? [OcrCorrectionGenerationReason.BookLexiconNeighbor],
            editDistance, editDistance, true, frequency, 1, [Candidate("source")], 0, false);
}
