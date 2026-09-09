using System.Globalization;
using EpubFixer.QualityBenchmarks.Models;

namespace EpubFixer.QualityBenchmarks;

public sealed class QualityBenchmarkGateEvaluator
{
    public QualityBenchmarkGateResult Evaluate(QualityBenchmarkResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var failures = new List<QualityBenchmarkGateFailure>();
        AddZeroFailure(failures, "Missed", result.Missed);
        AddZeroFailure(failures, "KnownDeferred", result.KnownDeferred);
        AddZeroFailure(failures, "WronglyFixed", result.WronglyFixed);
        AddZeroFailure(failures, "Deferred", result.Deferred);
        AddZeroFailure(failures, "ProtectedViolated", result.ProtectedViolated);
        AddZeroFailure(failures, "ProtectedChanged", result.ProtectedChanged);
        AddZeroFailure(failures, "UnexpectedTextChanges", result.UnexpectedTextChanges);
        AddZeroFailure(failures, "NonTextChanges", result.NonTextChanges);

        AddPerfectRateFailure(failures, "DetectionRecall", result.DetectionRecall);
        AddPerfectRateFailure(failures, "AutoFixCoverage", result.AutoFixCoverage);
        AddPerfectRateFailure(failures, "ProtectionRate", result.ProtectionRate);

        return new QualityBenchmarkGateResult(failures.Count == 0, failures);
    }

    private static void AddZeroFailure(
        ICollection<QualityBenchmarkGateFailure> failures,
        string metric,
        int actual)
    {
        if (actual != 0)
        {
            failures.Add(new QualityBenchmarkGateFailure(metric, "0", actual.ToString(CultureInfo.InvariantCulture)));
        }
    }

    private static void AddPerfectRateFailure(
        ICollection<QualityBenchmarkGateFailure> failures,
        string metric,
        double? actual)
    {
        if (actual.HasValue && actual.Value != 1d)
        {
            failures.Add(new QualityBenchmarkGateFailure(
                metric,
                "100.00%",
                (actual.Value * 100).ToString("F2", CultureInfo.InvariantCulture) + "%"));
        }
    }
}
