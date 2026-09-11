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

    [Theory]
    [InlineData("kepaze", "cenaze")]
    [InlineData("kepazesi", "cenazesi")]
    [InlineData("ze", "ve")]
    public void EvidenceOnly_ConservativeGuardsDowngradeRiskyCorrections(string sourceText, string proposalText)
    {
        var decision = Evaluate(Occurrence(sourceText, OcrConfidence.EvidenceOnly, Proposal(proposalText, 500, editDistance: sourceText == "ze" ? 1 : 2)));

        Assert.Equal(OcrCorrectionDecisionKind.Review, decision.DecisionKind);
        Assert.Null(decision.SelectedProposal);
    }

    [Fact]
    public void EvidenceOnly_FiveToOneDominanceIsRequired()
    {
        var decision = Evaluate(Occurrence("ornek", OcrConfidence.EvidenceOnly,
            Proposal("örnek", 25), Proposal("ornek", 5)));

        Assert.Equal(OcrCorrectionDecisionKind.AutoFixCandidate, decision.DecisionKind);
    }

    [Fact]
    public void SameApostropheBase_IsReviewOnly()
    {
        var decision = Evaluate(Occurrence("Joana'mn", OcrConfidence.EvidenceOnly,
            Proposal("Joana'nın", 100, editDistance: 2)));

        Assert.Equal(OcrCorrectionDecisionKind.Review, decision.DecisionKind);
        Assert.Contains(OcrCorrectionDecisionReason.SameApostropheBaseReviewOnly, decision.DecisionReasons);
    }

    [Fact]
    public void CompositeRepairOwnsConsumedStandaloneOccurrence()
    {
        var first = Candidate("ilgi-1");
        var second = Candidate("iydi") with { LogicalStart = 7 };
        var firstEvidence = new OcrWordEvidence(first, 0, first.Text, 0, false,
            [OcrDetectionReason.SuspiciousCharacter], OcrConfidence.High);
        var secondEvidence = new OcrWordEvidence(second, 1, second.Text, 1, false,
            [OcrDetectionReason.MorphologyInvalid, OcrDetectionReason.RareInBook], OcrConfidence.EvidenceOnly);
        var composite = new OcrCorrectionCandidate(first, OcrConfidence.High, "ilgiliydi",
            [OcrCorrectionGenerationReason.AdjacentFragmentComposition], 1, 1, true, 10, 1,
            [first, second], 1, false);
        var standalone = new OcrCorrectionCandidate(second, OcrConfidence.EvidenceOnly, "iyi",
            [OcrCorrectionGenerationReason.BookLexiconNeighbor], 1, 1, true, 99, 1, [second], 0, false);
        var occurrences = new[]
        {
            new OcrCorrectionOccurrence(firstEvidence, first, "", first.Text, "", [composite]),
            new OcrCorrectionOccurrence(secondEvidence, second, "", second.Text, "", [standalone])
        };
        var report = new OcrCorrectionAnalysisReport(
            new OcrAnalysisReport(2, 2, 0, [firstEvidence, secondEvidence], [], 0), occurrences, []);

        var decisions = new OcrCorrectionDecisionEvaluator().Evaluate(report).Decisions;

        Assert.Equal(OcrCorrectionDecisionKind.AutoFixCandidate, decisions[0].DecisionKind);
        Assert.Equal(OcrCorrectionDecisionKind.Review, decisions[1].DecisionKind);
        Assert.Contains(OcrCorrectionDecisionReason.ConsumedByCompositeRepair, decisions[1].DecisionReasons);
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

    [Fact]
    public void TitleCaseStructuralProperNameCompetitor_DowngradesToReview()
    {
        var decision = Evaluate(TitleCaseStructuralOccurrence("Bıırg",
            ProposalFrom("Bıırg", "Berg", true, 1, 2, 2),
            ProposalFrom("Bıırg", "Burg", false, 403, 2, 1)));

        Assert.Equal(OcrCorrectionDecisionKind.Review, decision.DecisionKind);
        Assert.Null(decision.SelectedProposal);
        Assert.Equal("Berg", decision.ProvisionalSelectedProposal!.Proposal.ProposedText);
        Assert.Contains(OcrCorrectionDecisionReason.ProperNameStructuralAmbiguity, decision.DecisionReasons);
        Assert.Contains(decision.CompetingProposals, item => item.Proposal.ProposedText == "Burg");
    }

    [Fact]
    public void LowercaseStructuralGeometry_DoesNotUseTitleCaseGuard()
    {
        var decision = Evaluate(TitleCaseStructuralOccurrence("bıırg",
            ProposalFrom("bıırg", "berg", true, 1, 2, 2),
            ProposalFrom("bıırg", "burg", false, 403, 2, 1)));

        Assert.Equal(OcrCorrectionDecisionKind.AutoFixCandidate, decision.DecisionKind);
        Assert.Equal("berg", decision.SelectedProposal!.Proposal.ProposedText);
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
        var evidence = new OcrWordEvidence(candidate, 1, source, 2, false,
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

    private static OcrCorrectionOccurrence TitleCaseStructuralOccurrence(string source, params OcrCorrectionCandidate[] proposals)
    {
        var candidate = Candidate(source);
        var evidence = new OcrWordEvidence(candidate, 0, source, 0, false,
            [OcrDetectionReason.SuspiciousCharacterSequence, OcrDetectionReason.MorphologyInvalid], OcrConfidence.Medium);
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

    private static OcrCorrectionCandidate ProposalFrom(
        string source,
        string text,
        bool trMorphValid,
        int frequency,
        int editDistance,
        int cost) =>
        new(Candidate(source), OcrConfidence.Medium, text,
            [OcrCorrectionGenerationReason.StructuralNormalization, OcrCorrectionGenerationReason.GlyphSubstitution,
             OcrCorrectionGenerationReason.BookLexiconNeighbor], editDistance, cost, trMorphValid, frequency, 1,
            [Candidate(source)], 0, false);
}
