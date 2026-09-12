using System.Globalization;
using EpubFixer.QualityBenchmarks.Models;

namespace EpubFixer.QualityBenchmarks;

public sealed class QualityBenchmarkGateEvaluator
{
    private readonly QualityBenchmarkGateOptions options;

    public QualityBenchmarkGateEvaluator(QualityBenchmarkGateOptions? options = null)
    {
        this.options = options ?? new QualityBenchmarkGateOptions();
    }

    public QualityBenchmarkGateResult Evaluate(QualityBenchmarkResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var failures = new List<QualityBenchmarkGateFailure>();
        AddZeroFailure(failures, "ProtectedViolated", result.ProtectedViolated);
        AddZeroFailure(failures, "ProtectedChanged", result.ProtectedChanged);
        AddZeroFailure(failures, "UnexpectedTextChanges", result.UnexpectedTextChanges);
        AddZeroFailure(failures, "NonTextChanges", result.NonTextChanges);

        AddMinimumRateFailure(failures, "Precision", result.Precision, options.MinimumPrecision);
        AddMinimumRateFailure(failures, "Recall", result.Recall, options.MinimumRecall);

        return new QualityBenchmarkGateResult(failures.Count == 0, failures);
    }

    private static void AddZeroFailure(
        ICollection<QualityBenchmarkGateFailure> failures,
        string metric,
        int actual,
        int expected = 0)
    {
        if (actual > expected)
        {
            failures.Add(new QualityBenchmarkGateFailure(metric, expected.ToString(CultureInfo.InvariantCulture), actual.ToString(CultureInfo.InvariantCulture)));
        }
    }

    private static void AddMinimumRateFailure(
        ICollection<QualityBenchmarkGateFailure> failures,
        string metric,
        double? actual,
        double minimum)
    {
        if (actual.HasValue && actual.Value < minimum)
        {
            failures.Add(new QualityBenchmarkGateFailure(
                metric,
                (minimum * 100).ToString("F2", CultureInfo.InvariantCulture) + "%",
                (actual.Value * 100).ToString("F2", CultureInfo.InvariantCulture) + "%"));
        }
    }
}
