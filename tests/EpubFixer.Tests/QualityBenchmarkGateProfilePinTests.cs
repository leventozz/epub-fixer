using System.Text.Json;
using EpubFixer.QualityBenchmarks;
using EpubFixer.QualityBenchmarks.Models;

namespace EpubFixer.Tests;

/// <summary>
/// Pins D80 kural 1: a gate threshold is the measured value of the engine we ship, never a hand
/// picked number. This test does not hardcode any of docs/baselines/quality-gate.json's numbers -
/// it reads current.shippedEngine, pulls that engine's measured counts from ocrStageMeasurement,
/// takes the active thresholds from current, and runs both through the real gate evaluator.
/// If someone later raises a threshold above what legacy actually measures (kural D77 says
/// thresholds may only move up, but never past what is proven), this test goes red - it does not
/// require a benchmark run, so it catches the mistake immediately.
/// </summary>
public sealed class QualityBenchmarkGateProfilePinTests
{
    [Fact]
    public void CurrentProfile_PassesAgainstShippedEngineMeasuredCounts()
    {
        var path = FindRepositoryFile(Path.Combine("docs", "baselines", "quality-gate.json"));
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var current = document.RootElement.GetProperty("current");
        // H5/D92: the thresholds now track the SHIPPED engine, which is hybrid. Before H5 that was
        // legacy; the rule pinned here is unchanged ("a threshold is a measured value of the engine
        // we ship, never a hand-picked number"), only the engine it points at moved.
        var shipped = current.GetProperty("shippedEngine").GetString()!;
        var measurement = current.GetProperty("ocrStageMeasurement").GetProperty(shipped);

        var result = BuildResultFromMeasurement(measurement);
        var options = QualityBenchmarkGateProfileLoader.LoadCurrent(path);
        Assert.NotNull(options);

        var gate = new QualityBenchmarkGateEvaluator(options).Evaluate(result);

        Assert.True(
            gate.Passed,
            $"The shipped engine ({shipped}) must pass the gate's current thresholds - they are " +
            "supposed to be its own measured values (D80 kural 1 / D92), not something stricter. " +
            "Failures: " + string.Join(", ", gate.Failures.Select(failure =>
                $"{failure.Metric} expected {failure.Expected}, actual {failure.Actual}")));
    }

    private static QualityBenchmarkResult BuildResultFromMeasurement(JsonElement measurement)
    {
        var correctlyFixed = measurement.GetProperty("correctlyFixed").GetInt32();
        var wronglyFixed = measurement.GetProperty("wronglyFixed").GetInt32();
        var deferred = measurement.GetProperty("deferred").GetInt32();
        var knownErrors = correctlyFixed + wronglyFixed + deferred;

        var classBreakdowns = measurement.GetProperty("classBreakdown")
            .EnumerateObject()
            .Select(property =>
            {
                var errorClass = Enum.Parse<OcrErrorClass>(property.Name);
                var known = property.Value.GetProperty("known").GetInt32();
                var correct = property.Value.GetProperty("correct").GetInt32();
                var wrong = property.Value.GetProperty("wrong").GetInt32();
                var precision = correct + wrong == 0 ? (double?)null : (double)correct / (correct + wrong);
                var recall = known == 0 ? (double?)null : (double)correct / known;
                return new QualityBenchmarkClassBreakdown(errorClass, known, known, correct, wrong, precision, recall);
            })
            .ToList();

        return new QualityBenchmarkResult(knownErrors, knownErrors, 0, null, [])
        {
            CorrectlyFixed = correctlyFixed,
            WronglyFixed = wronglyFixed,
            Deferred = deferred,
            ClassBreakdowns = classBreakdowns
        };
    }

    private static string FindRepositoryFile(string relativePath)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("Repository file was not found.", relativePath);
    }
}
