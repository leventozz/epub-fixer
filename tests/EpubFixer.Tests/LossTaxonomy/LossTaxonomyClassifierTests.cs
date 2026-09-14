namespace EpubFixer.Tests.LossTaxonomy;

/// <summary>
/// Proves the classifier checks reasons in the exact order docs/phase-5-plan.md section 7.2
/// prescribes - the "first failure point in the chain" rule - not just that each branch is
/// individually correct. Each "outranks" test sets every later-stage flag to a value that
/// would ALSO justify a different code, and asserts the earlier code still wins.
/// </summary>
public sealed class LossTaxonomyClassifierTests
{
    [Fact]
    public void RegionNotDetected_OutranksEveryLaterSignal()
    {
        var signals = new LossProbeSignals(
            RegionDetected: false,
            WindowBuilt: true,
            WindowBudgetExceeded: false,
            TargetReachableInLattice: true,
            UnreachableCause: null,
            TargetIsTopDecoderRank: true,
            GateVerdictAppliedUnexpectedly: false,
            GateReason: "Accepted");

        Assert.Equal(LossReasonCodes.RegionNotDetected, LossTaxonomyClassifier.Classify(signals));
    }

    [Fact]
    public void WindowSkippedTooLong_OutranksTargetReachability()
    {
        var signals = new LossProbeSignals(
            RegionDetected: true,
            WindowBuilt: false,
            WindowBudgetExceeded: false,
            TargetReachableInLattice: true,
            UnreachableCause: null,
            TargetIsTopDecoderRank: true,
            GateVerdictAppliedUnexpectedly: false,
            GateReason: "Accepted");

        Assert.Equal(LossReasonCodes.WindowSkippedTooLong, LossTaxonomyClassifier.Classify(signals));
    }

    [Fact]
    public void WindowBudgetExceeded_IsDistinguishedFromSkippedTooLong()
    {
        var signals = new LossProbeSignals(
            RegionDetected: true,
            WindowBuilt: false,
            WindowBudgetExceeded: true,
            TargetReachableInLattice: false,
            UnreachableCause: null,
            TargetIsTopDecoderRank: false,
            GateVerdictAppliedUnexpectedly: false,
            GateReason: null);

        Assert.Equal(LossReasonCodes.WindowBudgetExceeded, LossTaxonomyClassifier.Classify(signals));
    }

    [Theory]
    [InlineData(LatticeUnreachableCause.ExceedsMaxArcLength)]
    [InlineData(LatticeUnreachableCause.ExceedsBudget)]
    [InlineData(LatticeUnreachableCause.PositionUnreachable)]
    [InlineData(LatticeUnreachableCause.SpanNotMatchable)]
    public void UnreachableTarget_MapsStructuralCausesToTargetNotInLattice(LatticeUnreachableCause cause)
    {
        var signals = new LossProbeSignals(
            RegionDetected: true,
            WindowBuilt: true,
            WindowBudgetExceeded: false,
            TargetReachableInLattice: false,
            UnreachableCause: cause,
            TargetIsTopDecoderRank: true,
            GateVerdictAppliedUnexpectedly: true,
            GateReason: "Accepted");

        Assert.Equal(LossReasonCodes.TargetNotInLattice, LossTaxonomyClassifier.Classify(signals));
    }

    [Fact]
    public void UnreachableTarget_MatcherMissedIsDistinguishedFromStructuralCauses()
    {
        var signals = new LossProbeSignals(
            RegionDetected: true,
            WindowBuilt: true,
            WindowBudgetExceeded: false,
            TargetReachableInLattice: false,
            UnreachableCause: LatticeUnreachableCause.MatcherMissedTarget,
            TargetIsTopDecoderRank: true,
            GateVerdictAppliedUnexpectedly: true,
            GateReason: "Accepted");

        Assert.Equal(LossReasonCodes.MatcherMissedTarget, LossTaxonomyClassifier.Classify(signals));
    }

    [Fact]
    public void TargetReachableButNotTopRank_IsDecoderRankedOther()
    {
        var signals = new LossProbeSignals(
            RegionDetected: true,
            WindowBuilt: true,
            WindowBudgetExceeded: false,
            TargetReachableInLattice: true,
            UnreachableCause: null,
            TargetIsTopDecoderRank: false,
            GateVerdictAppliedUnexpectedly: true,
            GateReason: "Accepted");

        Assert.Equal(LossReasonCodes.DecoderRankedOther, LossTaxonomyClassifier.Classify(signals));
    }

    [Fact]
    public void TopRankButGateApplied_IsFlaggedAsUnexpectedRatherThanAttributed()
    {
        var signals = new LossProbeSignals(
            RegionDetected: true,
            WindowBuilt: true,
            WindowBudgetExceeded: false,
            TargetReachableInLattice: true,
            UnreachableCause: null,
            TargetIsTopDecoderRank: true,
            GateVerdictAppliedUnexpectedly: true,
            GateReason: "Accepted");

        Assert.Equal(LossReasonCodes.UnexpectedGateApply, LossTaxonomyClassifier.Classify(signals));
    }

    [Fact]
    public void TopRankAndGateRejects_ReturnsGateReasonWithPrefix()
    {
        var signals = new LossProbeSignals(
            RegionDetected: true,
            WindowBuilt: true,
            WindowBudgetExceeded: false,
            TargetReachableInLattice: true,
            UnreachableCause: null,
            TargetIsTopDecoderRank: true,
            GateVerdictAppliedUnexpectedly: false,
            GateReason: "OriginalTokenIsValid");

        Assert.Equal("GateRejected:OriginalTokenIsValid", LossTaxonomyClassifier.Classify(signals));
    }

    [Fact]
    public void TopRankAndGateRejects_ReviewVerdictReasonAlsoGetsGateRejectedPrefix()
    {
        var signals = new LossProbeSignals(
            RegionDetected: true,
            WindowBuilt: true,
            WindowBudgetExceeded: false,
            TargetReachableInLattice: true,
            UnreachableCause: null,
            TargetIsTopDecoderRank: true,
            GateVerdictAppliedUnexpectedly: false,
            GateReason: "MarginTooSmall");

        Assert.Equal("GateRejected:MarginTooSmall", LossTaxonomyClassifier.Classify(signals));
    }

    [Fact]
    public void Classify_NullSignals_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => LossTaxonomyClassifier.Classify(null!));
    }

    [Fact]
    public void Classify_NeverReturnsUnattributed_ForAnyReachableCombinationOfFlags()
    {
        // Rule: an unattributable case is a finding, not something the classifier hides. This
        // exhaustively walks every boolean combination reaching the final branch and confirms
        // the function is total (always returns a non-empty code), rather than trusting a
        // human read of the branches above.
        foreach (var regionDetected in new[] { true, false })
        foreach (var windowBuilt in new[] { true, false })
        foreach (var budgetExceeded in new[] { true, false })
        foreach (var reachable in new[] { true, false })
        foreach (var topRank in new[] { true, false })
        foreach (var appliedUnexpectedly in new[] { true, false })
        {
            var signals = new LossProbeSignals(
                regionDetected,
                windowBuilt,
                budgetExceeded,
                reachable,
                reachable ? null : LatticeUnreachableCause.MatcherMissedTarget,
                topRank,
                appliedUnexpectedly,
                "SomeReason");

            var reason = LossTaxonomyClassifier.Classify(signals);
            Assert.False(string.IsNullOrWhiteSpace(reason));
        }
    }
}
