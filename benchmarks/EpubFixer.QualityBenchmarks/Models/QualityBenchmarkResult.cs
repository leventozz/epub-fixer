namespace EpubFixer.QualityBenchmarks.Models;

using EpubFixer.Core.Decision.Models;

public sealed record QualityBenchmarkResult(
    int KnownErrors,
    int Detected,
    int Missed,
    double? DetectionRecall,
    IReadOnlyList<KnownErrorOccurrence> MissedOccurrences)
{
    public int ProtectedOccurrences { get; init; }

    public int ProtectedSafe { get; init; }

    public int ProtectedViolated { get; init; }

    public double? ProtectionRate { get; init; }

    public IReadOnlyList<ProtectedOccurrenceViolation> ProtectedViolations { get; init; } = [];

    public int KnownAutoFixCandidates { get; init; }

    public int KnownDeferred { get; init; }

    public double? AutoFixCoverage { get; init; }

    public IReadOnlyList<KnownErrorOccurrence> KnownDeferredOccurrences { get; init; } = [];

    public int CorrectlyFixed { get; init; }

    public int WronglyFixed { get; init; }

    public int Deferred { get; init; }

    public IReadOnlyList<KnownErrorCorrectionFailure> CorrectionFailures { get; init; } = [];

    public int ProtectedChanged { get; init; }

    public IReadOnlyList<ProtectedOccurrenceChange> ProtectedChanges { get; init; } = [];

    public int UnexpectedTextChanges { get; init; }

    public int NonTextChanges { get; init; }
}

public sealed record ProtectedOccurrenceViolation(
    ProtectedOccurrence Occurrence,
    HyphenationDecisionKind DecisionKind);

public sealed record KnownErrorCorrectionFailure(
    KnownErrorOccurrence Occurrence,
    string Classification,
    string ObservedText);

public sealed record ProtectedOccurrenceChange(
    ProtectedOccurrence Occurrence,
    string ObservedText);
