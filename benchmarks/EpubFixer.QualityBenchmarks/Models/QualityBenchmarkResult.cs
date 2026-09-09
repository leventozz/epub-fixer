namespace EpubFixer.QualityBenchmarks.Models;

public sealed record QualityBenchmarkResult(
    int KnownErrors,
    int Detected,
    int Missed,
    int CorrectlyFixed,
    int WronglyFixed,
    int Deferred,
    int UnexpectedTextChanges,
    int NonTextChanges);
