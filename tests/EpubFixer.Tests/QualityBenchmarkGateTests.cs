using EpubFixer.QualityBenchmarks;
using EpubFixer.QualityBenchmarks.Models;

namespace EpubFixer.Tests;

public sealed class QualityBenchmarkGateTests
{
    [Fact]
    public void Evaluate_CleanResultPasses()
    {
        var gate = new QualityBenchmarkGateEvaluator().Evaluate(CleanResult());

        Assert.True(gate.Passed);
        Assert.Empty(gate.Failures);
    }

    [Theory]
    [InlineData("Missed")]
    [InlineData("KnownDeferred")]
    [InlineData("WronglyFixed")]
    [InlineData("Deferred")]
    [InlineData("ProtectedViolated")]
    [InlineData("ProtectedChanged")]
    [InlineData("UnexpectedTextChanges")]
    [InlineData("NonTextChanges")]
    public void Evaluate_NonZeroCountFails(string metric)
    {
        var result = CleanResult();
        result = metric switch
        {
            "Missed" => result with { Missed = 1 },
            "KnownDeferred" => result with { KnownDeferred = 1 },
            "WronglyFixed" => result with { WronglyFixed = 1 },
            "Deferred" => result with { Deferred = 1 },
            "ProtectedViolated" => result with { ProtectedViolated = 1 },
            "ProtectedChanged" => result with { ProtectedChanged = 1 },
            "UnexpectedTextChanges" => result with { UnexpectedTextChanges = 1 },
            "NonTextChanges" => result with { NonTextChanges = 1 },
            _ => throw new ArgumentOutOfRangeException(nameof(metric))
        };

        var gate = new QualityBenchmarkGateEvaluator().Evaluate(result);

        Assert.False(gate.Passed);
        Assert.Contains(gate.Failures, failure => failure.Metric == metric);
    }

    [Theory]
    [InlineData("DetectionRecall")]
    [InlineData("AutoFixCoverage")]
    [InlineData("ProtectionRate")]
    public void Evaluate_ImperfectRateFails(string metric)
    {
        var result = metric switch
        {
            "DetectionRecall" => CleanResult() with { DetectionRecall = 0.5d },
            "AutoFixCoverage" => CleanResult() with { AutoFixCoverage = 0.5d },
            "ProtectionRate" => CleanResult() with { ProtectionRate = 0.5d },
            _ => throw new ArgumentOutOfRangeException(nameof(metric))
        };

        var gate = new QualityBenchmarkGateEvaluator().Evaluate(result);

        Assert.False(gate.Passed);
        var failure = Assert.Single(gate.Failures);
        Assert.Equal(metric, failure.Metric);
        Assert.Equal("100.00%", failure.Expected);
        Assert.Equal("50.00%", failure.Actual);
    }

    [Fact]
    public void Evaluate_NullRatesDoNotFailWhenCountsAreClean()
    {
        var result = CleanResult() with
        {
            DetectionRecall = null,
            AutoFixCoverage = null,
            ProtectionRate = null
        };

        var gate = new QualityBenchmarkGateEvaluator().Evaluate(result);

        Assert.True(gate.Passed);
    }

    [Fact]
    public void GateReportWriter_PrintsPassAndFailureDetails()
    {
        using var writer = new StringWriter();
        QualityBenchmarkGateReportWriter.Write(
            writer,
            new QualityBenchmarkGateResult(false,
                [new QualityBenchmarkGateFailure("WronglyFixed", "0", "1")]));

        var text = writer.ToString();
        Assert.Contains("Quality gate: FAIL", text);
        Assert.Contains("- WronglyFixed expected 0, actual 1", text);
    }

    [Fact]
    public void Application_UsageErrorReturnsOne()
    {
        var app = new QualityBenchmarkApplication();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = app.Run([], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("Usage:", error.ToString());
    }

    [Fact]
    public void Application_GateFailureReturnsOne()
    {
        var dataset = new QualityBenchmarkDataset(
            "synthetic",
            "dataset",
            "input.epub",
            "ground-truth.json",
            new GroundTruthDocument(1, []));
        var failed = CleanResult() with { WronglyFixed = 1 };
        var app = new QualityBenchmarkApplication(
            _ => dataset,
            _ => failed,
            new QualityBenchmarkGateEvaluator());
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = app.Run(["dataset"], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("Quality gate: FAIL", output.ToString());
    }

    private static QualityBenchmarkResult CleanResult() => new(1, 1, 0, 1d, [])
    {
        KnownAutoFixCandidates = 1,
        KnownDeferred = 0,
        AutoFixCoverage = 1d,
        CorrectlyFixed = 1,
        WronglyFixed = 0,
        Deferred = 0,
        ProtectedOccurrences = 1,
        ProtectedSafe = 1,
        ProtectedViolated = 0,
        ProtectionRate = 1d,
        ProtectedChanged = 0,
        UnexpectedTextChanges = 0,
        NonTextChanges = 0
    };
}
