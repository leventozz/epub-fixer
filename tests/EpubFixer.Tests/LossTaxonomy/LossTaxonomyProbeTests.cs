using EpubFixer.Core.Ocr.Lattice;
using EpubFixer.Core.Ocr.Lattice.Models;

namespace EpubFixer.Tests.LossTaxonomy;

public sealed class LossTaxonomyProbeTests
{
    private static readonly LossCase SampleCase = new("main-3.xhtml", 509, "zyz", "xyz", "StructuralLexiconRepair");

    [Fact]
    public void DiagnoseRegionNotDetected_ReturnsRegionNotDetectedAndNoLatticeFacts()
    {
        var diagnosis = LossTaxonomyProbe.DiagnoseRegionNotDetected(SampleCase);

        Assert.Equal(LossReasonCodes.RegionNotDetected, diagnosis.LossReason);
        Assert.False(diagnosis.TargetInLattice);
        Assert.Null(diagnosis.ArcLength);
        Assert.Null(diagnosis.GateReason);
        Assert.Equal(SampleCase.Original, diagnosis.Original);
        Assert.Equal(SampleCase.Replacement, diagnosis.Expected);
    }

    [Theory]
    [InlineData(LatticeBuildOutcome.SkippedTooLong, LossReasonCodes.WindowSkippedTooLong)]
    [InlineData(LatticeBuildOutcome.BudgetExceeded, LossReasonCodes.WindowBudgetExceeded)]
    public void DiagnoseWindowNotBuilt_MapsEachOutcomeToItsOwnCode(LatticeBuildOutcome outcome, string expectedReason)
    {
        var diagnosis = LossTaxonomyProbe.DiagnoseWindowNotBuilt(SampleCase, outcome);

        Assert.Equal(expectedReason, diagnosis.LossReason);
        Assert.False(diagnosis.TargetInLattice);
    }

    [Fact]
    public void DiagnoseWindowNotBuilt_RejectsBuiltOutcome()
    {
        Assert.Throws<ArgumentException>(() => LossTaxonomyProbe.DiagnoseWindowNotBuilt(SampleCase, LatticeBuildOutcome.Built));
    }

    [Fact]
    public void DiagnoseBuiltWindow_TargetTopRankAndGateRejectsForValidToken_ExtractsThatToken()
    {
        // window: "koltukta zyz" (indices 0-7 "koltukta", 8 space, 9-11 "zyz").
        // "koltukta" is a book-learned word in LatticeFixture.Vocabulary (BookCount >= 2), so it
        // is exactly the kind of token 6.4's hypothesis says trips OriginalTokenIsValid.
        var arcs = new[]
        {
            new LatticeArc(0, 8, "koltukta", 0, LatticeArcKind.Identity),
            new LatticeArc(8, 9, " ", 0, LatticeArcKind.Literal),
            new LatticeArc(9, 12, "xyz", 0.8, LatticeArcKind.Word)
        };
        var lattice = new WordLattice("koltukta zyz", 500, arcs, LatticeBuildOutcome.Built, 3);
        var paths = new[] { new DecodedPath("koltukta xyz", 0.8, arcs) };
        // Simulates the gate having expanded the changed span across the whole window - the
        // probe reads changed-token bounds straight from the gate's own reported span rather
        // than recomputing ChangedSpan itself (see LossTaxonomyProbe's class comment).
        var gateResult = new AcceptanceResult(AcceptanceVerdict.Leave, null, 500, 512, ["OriginalTokenIsValid"]);
        var lossCase = new LossCase("main-3.xhtml", 509, "zyz", "xyz", "StructuralLexiconRepair");

        var diagnosis = LossTaxonomyProbe.DiagnoseBuiltWindow(
            lossCase, LatticeFixture.Vocabulary, new NeverCalledMatcher(), new LatticeOptions(), lattice, paths, gateResult);

        Assert.Equal("GateRejected:OriginalTokenIsValid", diagnosis.LossReason);
        Assert.Equal("OriginalTokenIsValid", diagnosis.GateReason);
        Assert.Equal("koltukta", diagnosis.ValidToken);
        Assert.True(diagnosis.TargetInLattice);
        Assert.Equal(3, diagnosis.ArcLength);
    }

    [Fact]
    public void DiagnoseBuiltWindow_TargetReachableButNotTopRank_IsDecoderRankedOther()
    {
        var arcs = new[]
        {
            new LatticeArc(0, 8, "koltukta", 0, LatticeArcKind.Identity),
            new LatticeArc(8, 9, " ", 0, LatticeArcKind.Literal),
            new LatticeArc(9, 12, "zyz", 0, LatticeArcKind.Identity),
            new LatticeArc(9, 12, "xyz", 0.8, LatticeArcKind.Word)
        };
        var lattice = new WordLattice("koltukta zyz", 500, arcs, LatticeBuildOutcome.Built, 4);
        var identityPath = new DecodedPath("koltukta zyz", 0, [arcs[0], arcs[1], arcs[2]]);
        var wordPath = new DecodedPath("koltukta xyz", 0.8, [arcs[0], arcs[1], arcs[3]]);
        var gateResult = new AcceptanceResult(AcceptanceVerdict.Leave, null, 509, 512, ["NoChange"]);
        var lossCase = new LossCase("main-3.xhtml", 509, "zyz", "xyz", "StructuralLexiconRepair");

        var diagnosis = LossTaxonomyProbe.DiagnoseBuiltWindow(
            lossCase, LatticeFixture.Vocabulary, new NeverCalledMatcher(), new LatticeOptions(), lattice, [identityPath, wordPath], gateResult);

        Assert.Equal(LossReasonCodes.DecoderRankedOther, diagnosis.LossReason);
        Assert.True(diagnosis.TargetInLattice);
        Assert.Equal("koltukta zyz", diagnosis.DecoderTopReplacement);
    }

    [Fact]
    public void DiagnoseBuiltWindow_TargetLongerThanMaxArcLength_IsTargetNotInLattice()
    {
        // "abcdefgh" (8 chars) starts right after a separator (a valid start node) but exceeds
        // a MaxArcLength of 5, so no arc could ever be queried for the whole token.
        var arcs = new[]
        {
            new LatticeArc(0, 1, "x", 0, LatticeArcKind.Identity),
            new LatticeArc(1, 2, " ", 0, LatticeArcKind.Literal),
            new LatticeArc(2, 10, "abcdefgh", 0, LatticeArcKind.Identity)
        };
        var lattice = new WordLattice("x abcdefgh", 900, arcs, LatticeBuildOutcome.Built, 3);
        var paths = new[] { new DecodedPath("x abcdefgh", 0, arcs) };
        var gateResult = new AcceptanceResult(AcceptanceVerdict.Leave, null, 902, 910, ["NoChange"]);
        var lossCase = new LossCase("main-3.xhtml", 902, "abcdefgh", "ABCDEFGH", "StructuralLexiconRepair");

        var diagnosis = LossTaxonomyProbe.DiagnoseBuiltWindow(
            lossCase, LatticeFixture.Vocabulary, new NeverCalledMatcher(), new LatticeOptions(MaxArcLength: 5), lattice, paths, gateResult);

        Assert.Equal(LossReasonCodes.TargetNotInLattice, diagnosis.LossReason);
        Assert.False(diagnosis.TargetInLattice);
        Assert.Equal(8, diagnosis.ArcLength);
        Assert.Contains("ExceedsMaxArcLength", diagnosis.Notes);
    }

    [Fact]
    public void DiagnoseBuiltWindow_PositionUnreachableFromWindowStart_IsTargetNotInLatticeWithDistinctNote()
    {
        // "abcdefgh" is one unbroken token (a single identity arc from 0 to 8); position 2 is
        // mid-run, so no arc of any kind ever lands there - it cannot be a DP state regardless
        // of what word arcs a matcher call might produce starting from it.
        var arcs = new[] { new LatticeArc(0, 8, "abcdefgh", 0, LatticeArcKind.Identity) };
        var lattice = new WordLattice("abcdefgh", 900, arcs, LatticeBuildOutcome.Built, 1);
        var paths = new[] { new DecodedPath("abcdefgh", 0, arcs) };
        var gateResult = new AcceptanceResult(AcceptanceVerdict.Leave, null, 902, 905, ["NoChange"]);
        // "cde" sits at window[2..5) (0:a 1:b 2:c 3:d 4:e ...).
        var lossCase = new LossCase("main-3.xhtml", 902, "cde", "CDE", "StructuralLexiconRepair");

        var diagnosis = LossTaxonomyProbe.DiagnoseBuiltWindow(
            lossCase, LatticeFixture.Vocabulary, new NeverCalledMatcher(), new LatticeOptions(), lattice, paths, gateResult);

        Assert.Equal(LossReasonCodes.TargetNotInLattice, diagnosis.LossReason);
        Assert.Contains("PositionUnreachable", diagnosis.Notes);
    }

    [Fact]
    public void DiagnoseBuiltWindow_SpanHasFewerThanTwoAlphanumericCharacters_IsTargetNotInLattice()
    {
        // "a" is a single alphanumeric character - WordLatticeBuilder.IsMatchableSpan requires
        // at least two, so this exact span could never be queried against the matcher in the
        // first place (independent of budget or vocabulary).
        var arcs = new[]
        {
            new LatticeArc(0, 1, "x", 0, LatticeArcKind.Identity),
            new LatticeArc(1, 2, " ", 0, LatticeArcKind.Literal),
            new LatticeArc(2, 3, "a", 0, LatticeArcKind.Identity)
        };
        var lattice = new WordLattice("x a", 900, arcs, LatticeBuildOutcome.Built, 3);
        var paths = new[] { new DecodedPath("x a", 0, arcs) };
        var gateResult = new AcceptanceResult(AcceptanceVerdict.Leave, null, 902, 903, ["NoChange"]);
        var lossCase = new LossCase("main-3.xhtml", 902, "a", "xyz", "StructuralLexiconRepair");

        var diagnosis = LossTaxonomyProbe.DiagnoseBuiltWindow(
            lossCase, LatticeFixture.Vocabulary, new NeverCalledMatcher(), new LatticeOptions(), lattice, paths, gateResult);

        Assert.Equal(LossReasonCodes.TargetNotInLattice, diagnosis.LossReason);
        Assert.Contains("SpanNotMatchable", diagnosis.Notes);
    }

    [Fact]
    public void DiagnoseBuiltWindow_MatcherDoesNotReturnAReachableTarget_IsMatcherMissedTarget()
    {
        var arcs = new[]
        {
            new LatticeArc(0, 1, "x", 0, LatticeArcKind.Identity),
            new LatticeArc(1, 2, " ", 0, LatticeArcKind.Literal),
            new LatticeArc(2, 5, "abc", 0, LatticeArcKind.Identity)
        };
        var lattice = new WordLattice("x abc", 900, arcs, LatticeBuildOutcome.Built, 3);
        var paths = new[] { new DecodedPath("x abc", 0, arcs) };
        var gateResult = new AcceptanceResult(AcceptanceVerdict.Leave, null, 902, 905, ["NoChange"]);
        var lossCase = new LossCase("main-3.xhtml", 902, "abc", "abd", "StructuralLexiconRepair");

        // Default costs give a single ordinary substitution a cost of 1.00 and the default
        // budget for a 3-char span is BudgetBase(1.0) - comfortably enough for the aligner to
        // accept "abc"->"abd", so an empty matcher result is a genuine miss, not a budget wall.
        var diagnosis = LossTaxonomyProbe.DiagnoseBuiltWindow(
            lossCase, LatticeFixture.Vocabulary, new EmptyMatcher(), new LatticeOptions(), lattice, paths, gateResult);

        Assert.Equal(LossReasonCodes.MatcherMissedTarget, diagnosis.LossReason);
        Assert.Equal(false, diagnosis.MatcherFoundTargetForSpan);
    }

    [Fact]
    public void DiagnoseBuiltWindow_RequiredEditCostExceedsBudget_IsTargetNotInLattice()
    {
        var arcs = new[]
        {
            new LatticeArc(0, 1, "x", 0, LatticeArcKind.Identity),
            new LatticeArc(1, 2, " ", 0, LatticeArcKind.Literal),
            new LatticeArc(2, 4, "ab", 0, LatticeArcKind.Identity)
        };
        var lattice = new WordLattice("x ab", 900, arcs, LatticeBuildOutcome.Built, 3);
        var paths = new[] { new DecodedPath("x ab", 0, arcs) };
        var gateResult = new AcceptanceResult(AcceptanceVerdict.Leave, null, 902, 904, ["NoChange"]);
        var lossCase = new LossCase("main-3.xhtml", 902, "ab", "xyz", "StructuralLexiconRepair");

        // A zero budget cannot afford even a single Keep-cost-free char plus an insertion.
        var options = new LatticeOptions(BudgetBase: 0, BudgetPerFourChars: 0, BudgetCap: 0);
        var diagnosis = LossTaxonomyProbe.DiagnoseBuiltWindow(
            lossCase, LatticeFixture.Vocabulary, new NeverCalledMatcher(), options, lattice, paths, gateResult);

        Assert.Equal(LossReasonCodes.TargetNotInLattice, diagnosis.LossReason);
        Assert.Contains("ExceedsBudget", diagnosis.Notes);
    }

    [Fact]
    public void DiagnoseBuiltWindow_WindowDoesNotCoverTheLossSpan_IsReportedAsAnAnomalyNotGuessed()
    {
        var lattice = new WordLattice("abc", 900, [new LatticeArc(0, 3, "abc", 0, LatticeArcKind.Identity)], LatticeBuildOutcome.Built, 1);
        var paths = new[] { new DecodedPath("abc", 0, lattice.Arcs) };
        var gateResult = new AcceptanceResult(AcceptanceVerdict.Leave, null, 900, 903, ["NoChange"]);
        // logicalStart 950 is nowhere near this 3-char window.
        var lossCase = new LossCase("main-3.xhtml", 950, "abc", "abd", "StructuralLexiconRepair");

        var diagnosis = LossTaxonomyProbe.DiagnoseBuiltWindow(
            lossCase, LatticeFixture.Vocabulary, new NeverCalledMatcher(), new LatticeOptions(), lattice, paths, gateResult);

        Assert.Contains("ANOMALY", diagnosis.Notes);
        Assert.False(diagnosis.TargetInLattice);
    }

    [Theory]
    [InlineData(";;aşılacak", "aşılacak", true)]
    // "()yuncu" -> "oyuncu": the gate does not find "oyuncu" valid, it finds "yuncu" (a real
    // word) already sitting inside the corrupted text once "()" is set aside - the token the
    // hypothesis is actually about is "yuncu", not the eventual replacement.
    [InlineData("()yuncu", "yuncu", true)]
    // "J3arış": '3' is alphanumeric, so ChangedTokens never splits here at all - there is no
    // garbage-glyph-stripped remainder to confirm the hypothesis against.
    [InlineData("J3arış", "Barış", false)]
    public void MatchesGarbageStripHypothesis_ConfirmsOnlyWhenTokenSurvivesGarbageRemoval(string original, string validToken, bool expected)
    {
        Assert.Equal(expected, LossTaxonomyProbe.MatchesGarbageStripHypothesis(original, validToken));
    }

    [Fact]
    public void ChangedTokens_SplitsOnNonTokenCharactersAndKeepsApostrophes()
    {
        var tokens = LossTaxonomyProbe.ChangedTokens("Auersberger'in ^;test", 0, 21).ToArray();

        Assert.Equal(["Auersberger'in", "test"], tokens);
    }

    [Fact]
    public void IsValidOriginalToken_TrueForBookLearnedWord_FalseForUnknownWord()
    {
        Assert.True(LossTaxonomyProbe.IsValidOriginalToken(LatticeFixture.Vocabulary, "koltukta"));
        Assert.False(LossTaxonomyProbe.IsValidOriginalToken(LatticeFixture.Vocabulary, "zzzznotaword"));
    }

    private sealed class NeverCalledMatcher : ILexiconMatcher
    {
        public IReadOnlyList<LexiconMatch> Match(ReadOnlySpan<char> span, double budget) =>
            throw new InvalidOperationException("The matcher should not be consulted once the target is already reachable in the lattice.");
    }

    private sealed class EmptyMatcher : ILexiconMatcher
    {
        public IReadOnlyList<LexiconMatch> Match(ReadOnlySpan<char> span, double budget) => Array.Empty<LexiconMatch>();
    }
}
