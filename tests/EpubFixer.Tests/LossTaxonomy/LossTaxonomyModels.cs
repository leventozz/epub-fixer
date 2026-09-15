namespace EpubFixer.Tests.LossTaxonomy;

/// <summary>One entry from docs/baselines/odun-kesmek.engine-diff.json's "loss.items" array.</summary>
public sealed record LossCase(
    string DocumentPath,
    int LogicalStart,
    string Original,
    string Replacement,
    string DecisionRule);

/// <summary>
/// The full diagnosis of one <see cref="LossCase"/>: the fields docs/phase-5-plan.md@adc202d section 7.2
/// requires (original, expected, lossReason, gateReason, validToken, targetInLattice, arcLength),
/// plus a few extra fields kept for methodology transparency (rule 4.3 - measurements are
/// reported, not asserted from memory).
/// </summary>
public sealed record LossCaseDiagnosis(
    string DocumentPath,
    int LogicalStart,
    string Original,
    string Expected,
    string DecisionRuleLegacy,
    string LossReason,
    string? GateReason,
    string? ValidToken,
    bool TargetInLattice,
    int? ArcLength,
    bool? MatcherFoundTargetForSpan,
    bool? TargetInVocabulary,
    string? DecoderTopReplacement,
    string? Notes);

public sealed record LossTaxonomyReport(
    string Dataset,
    string MeasuredOn,
    string Methodology,
    int TotalLosses,
    int UnattributedCount,
    IReadOnlyDictionary<string, int> ReasonHistogram,
    IReadOnlyDictionary<string, int> GateRejectedReasonHistogram,
    OriginalTokenIsValidHypothesisSummary OriginalTokenIsValidHypothesis,
    IReadOnlyList<LossCaseDiagnosis> Cases);

/// <summary>
/// The result of testing the 6.4 hypothesis: for every GateRejected:OriginalTokenIsValid case,
/// is the token the gate found valid exactly the clean word left over after a garbage glyph
/// (^ ; : &lt; &gt; ,) is stripped from the corrupted original?
/// </summary>
public sealed record OriginalTokenIsValidHypothesisSummary(
    int CaseCount,
    int ConfirmedCount,
    int RefutedCount,
    IReadOnlyList<string> RefutedExamples);
