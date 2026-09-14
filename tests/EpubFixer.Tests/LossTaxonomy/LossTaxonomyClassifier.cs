namespace EpubFixer.Tests.LossTaxonomy;

/// <summary>
/// Why the lattice does or does not reach the correct arc when it does not, per
/// docs/phase-5-plan.md section 7.2's "TargetNotInLattice" vs "MatcherMissedTarget" split.
/// </summary>
public enum LatticeUnreachableCause
{
    /// <summary>The span (or the corrupted token within it) is longer than <c>MaxArcLength</c>.</summary>
    ExceedsMaxArcLength,

    /// <summary>The true edit cost to the target exceeds the per-span budget the builder would pass to the matcher.</summary>
    ExceedsBudget,

    /// <summary>
    /// The span's start position is not reachable from the start of the window by ANY arc
    /// (identity, literal, or word). This is not the same as "never a queried start node": a
    /// separator-based start node can still be a dead end if the run-merging in
    /// <c>WordLatticeBuilder.AddIdentityAndLiteralArcs</c> groups it together with an adjacent
    /// non-token character (e.g. a garbage glyph immediately after a space becomes ONE literal
    /// arc spanning both, skipping the node in between) - a word arc queried from such a node is
    /// still built, but can never appear in any complete decoded path (empirically confirmed via
    /// docs/baselines/odun-kesmek.loss-taxonomy.json's "^iddetli" case).
    /// </summary>
    PositionUnreachable,

    /// <summary>
    /// The span itself is not one <c>WordLatticeBuilder.IsMatchableSpan</c> would ever pass to
    /// the matcher (starts/ends in whitespace, or has fewer than two alphanumeric characters) -
    /// so no arc for exactly this span could ever exist, independent of budget or vocabulary.
    /// </summary>
    SpanNotMatchable,

    /// <summary>The span is queryable and within budget, but <see cref="EpubFixer.Core.Ocr.Lattice.ILexiconMatcher"/> did not return the target.</summary>
    MatcherMissedTarget
}

/// <summary>
/// The facts a <see cref="LossTaxonomyProbe"/> establishes about a single loss case, in the
/// exact order docs/phase-5-plan.md section 7.2 attributes a first-failure reason. Every field
/// is a plain, dependency-free flag so the attribution ORDER (not just each branch's own
/// correctness) is unit-testable without the real book, the real matcher, or the real gate.
/// </summary>
public sealed record LossProbeSignals(
    bool RegionDetected,
    bool WindowBuilt,
    bool WindowBudgetExceeded,
    bool TargetReachableInLattice,
    LatticeUnreachableCause? UnreachableCause,
    bool TargetIsTopDecoderRank,
    bool GateVerdictAppliedUnexpectedly,
    string? GateReason);

/// <summary>Reason codes from docs/phase-5-plan.md section 7.2.</summary>
public static class LossReasonCodes
{
    public const string RegionNotDetected = "RegionNotDetected";
    public const string WindowSkippedTooLong = "WindowSkippedTooLong";

    /// <summary>
    /// Not one of the six codes the plan enumerates (odun-kesmek's raw run measured zero
    /// BudgetExceeded regions - docs/baselines/odun-kesmek.lattice.json). Kept distinct from
    /// WindowSkippedTooLong rather than folded into it, so a case that DOES hit this path is
    /// visible as a deviation from the plan instead of silently misfiled (rule 4.3: an
    /// unattributable case is a finding, not something to smooth over).
    /// </summary>
    public const string WindowBudgetExceeded = "WindowBudgetExceeded";

    public const string TargetNotInLattice = "TargetNotInLattice";
    public const string MatcherMissedTarget = "MatcherMissedTarget";
    public const string DecoderRankedOther = "DecoderRankedOther";

    /// <summary>
    /// Not a plan code either: it means the target was top-1 and the gate accepted it, which
    /// would contradict the case being a loss at all. Zero occurrences is the expected/required
    /// outcome (see LossTaxonomyBaselineTests) - if this ever fires it means a loss case was
    /// matched to the wrong region and must be investigated, not attributed.
    /// </summary>
    public const string UnexpectedGateApply = "UnexpectedGateApply";

    public const string GateRejectedPrefix = "GateRejected:";
}

/// <summary>
/// Pure decision logic for R5.0b (docs/phase-5-plan.md section 7.2): given where a single loss
/// case's evaluation through the lattice pipeline first diverges from success, attributes it to
/// exactly one reason code. Contains no I/O and touches none of the real pipeline types, so the
/// checking ORDER - which is the entire point of "first failure point in the chain" - is
/// verifiable in isolation from the slow, real-book probe that supplies these signals.
/// </summary>
public static class LossTaxonomyClassifier
{
    public static string Classify(LossProbeSignals signals)
    {
        ArgumentNullException.ThrowIfNull(signals);

        if (!signals.RegionDetected)
        {
            return LossReasonCodes.RegionNotDetected;
        }

        if (!signals.WindowBuilt)
        {
            return signals.WindowBudgetExceeded
                ? LossReasonCodes.WindowBudgetExceeded
                : LossReasonCodes.WindowSkippedTooLong;
        }

        if (!signals.TargetReachableInLattice)
        {
            return signals.UnreachableCause == LatticeUnreachableCause.MatcherMissedTarget
                ? LossReasonCodes.MatcherMissedTarget
                : LossReasonCodes.TargetNotInLattice;
        }

        if (!signals.TargetIsTopDecoderRank)
        {
            return LossReasonCodes.DecoderRankedOther;
        }

        if (signals.GateVerdictAppliedUnexpectedly)
        {
            return LossReasonCodes.UnexpectedGateApply;
        }

        return LossReasonCodes.GateRejectedPrefix + (signals.GateReason ?? "Unknown");
    }
}
