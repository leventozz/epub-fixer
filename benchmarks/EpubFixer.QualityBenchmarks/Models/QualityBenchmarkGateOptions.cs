namespace EpubFixer.QualityBenchmarks.Models;

public sealed record QualityBenchmarkGateOptions(
    double MinimumPrecision = 0.98,
    double MinimumRecall = 0.60)
{
    /// <summary>
    /// Per-<see cref="OcrErrorClass"/> minimum recall floors (R5.0a, D76). A class with no
    /// entry here has no floor - the aggregate Precision/Recall gate above is still the only
    /// thing protecting it. This fazda thresholds are added deliberately sparsely: only classes
    /// with enough ground-truth records to make a floor meaningful get one (kural 3.4/D63 - a
    /// threshold set on a handful of records is false confidence, not a gate).
    /// </summary>
    public IReadOnlyList<QualityBenchmarkClassGateThreshold> ClassRecallThresholds { get; init; } = [];
}

/// <summary>Minimum recall a single OCR error class must clear for the gate to pass.</summary>
public sealed record QualityBenchmarkClassGateThreshold(OcrErrorClass ErrorClass, double MinimumRecall);
