namespace EpubFixer.QualityBenchmarks.Models;

public sealed record QualityBenchmarkResult(
    int KnownErrors,
    int Detected,
    int Missed,
    double? DetectionRecall,
    IReadOnlyList<KnownErrorOccurrence> MissedOccurrences)
{
    public int CorrectlyFixed { get; init; }

    public int WronglyFixed { get; init; }

    public int Deferred { get; init; }

    public int UnexpectedTextChanges { get; init; }

    public int NonTextChanges { get; init; }
}
