namespace EpubFixer.QualityBenchmarks.Models;

public sealed record QualityBenchmarkGateResult(
    bool Passed,
    IReadOnlyList<QualityBenchmarkGateFailure> Failures);

public sealed record QualityBenchmarkGateFailure(
    string Metric,
    string Expected,
    string Actual);
