namespace EpubFixer.QualityBenchmarks.Models;

public sealed record QualityBenchmarkGateOptions(
    double MinimumPrecision = 0.98,
    double MinimumRecall = 0.60);
