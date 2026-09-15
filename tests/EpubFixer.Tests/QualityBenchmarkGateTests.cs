using EpubFixer.Core.Ocr;
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

    [Fact]
    public void Evaluate_DefaultOptionsUseRoadmapThresholds()
    {
        var gate = new QualityBenchmarkGateEvaluator()
            .Evaluate(CleanResult() with { KnownErrors = 200, CorrectlyFixed = 97, WronglyFixed = 3, Deferred = 100 });

        Assert.False(gate.Passed);
        Assert.Contains(gate.Failures, failure => failure.Metric == "Recall" && failure.Expected == "60.00%");
        Assert.Contains(gate.Failures, failure => failure.Metric == "Precision" && failure.Expected == "98.00%");
    }

    [Theory]
    [InlineData("ProtectedViolated")]
    [InlineData("ProtectedChanged")]
    [InlineData("UnexpectedTextChanges")]
    [InlineData("NonTextChanges")]
    public void Evaluate_NonZeroCountFails(string metric)
    {
        var result = CleanResult();
        result = metric switch
        {
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

    [Fact]
    public void Evaluate_DetectionRateImperfectDoesNotFail()
    {
        var result = CleanResult() with { DetectionRecall = 0.5d, AutoFixCoverage = 0.5d, ProtectionRate = 0.5d };

        var gate = new QualityBenchmarkGateEvaluator().Evaluate(result);

        Assert.True(gate.Passed);
    }

    [Fact]
    public void Evaluate_PrecisionBelowThresholdFails()
    {
        var gate = new QualityBenchmarkGateEvaluator().Evaluate(CleanResult() with { WronglyFixed = 1 });
        Assert.False(gate.Passed);
        Assert.Contains(gate.Failures, failure => failure.Metric == "Precision");
    }

    [Fact]
    public void Evaluate_PrecisionAtThresholdPasses()
    {
        var result = CleanResult() with { CorrectlyFixed = 98, WronglyFixed = 2 };

        var gate = new QualityBenchmarkGateEvaluator(new QualityBenchmarkGateOptions(MinimumPrecision: 0.98, MinimumRecall: 0))
            .Evaluate(result);

        Assert.True(gate.Passed);
    }

    [Fact]
    public void Evaluate_RecallBelowThresholdFails()
    {
        var gate = new QualityBenchmarkGateEvaluator().Evaluate(CleanResult() with { CorrectlyFixed = 0, Deferred = 1 });
        Assert.False(gate.Passed);
        Assert.Contains(gate.Failures, failure => failure.Metric == "Recall");
    }

    [Fact]
    public void Evaluate_ProtectedViolatedAlwaysFailsEvenWithCustomOptions()
    {
        var gate = new QualityBenchmarkGateEvaluator(new QualityBenchmarkGateOptions(0, 0))
            .Evaluate(CleanResult() with { ProtectedViolated = 1 });

        Assert.False(gate.Passed);
        Assert.Contains(gate.Failures, failure => failure.Metric == "ProtectedViolated");
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
    public void Evaluate_NullPrecisionDoesNotFail()
    {
        var result = CleanResult() with { CorrectlyFixed = 0, WronglyFixed = 0 };

        var gate = new QualityBenchmarkGateEvaluator(new QualityBenchmarkGateOptions(MinimumPrecision: 0.98, MinimumRecall: 0))
            .Evaluate(result);

        Assert.True(gate.Passed);
    }

    [Fact]
    public void Evaluate_ClassRecallBelowThresholdFails()
    {
        var result = CleanResult() with
        {
            ClassBreakdowns = [new QualityBenchmarkClassBreakdown(OcrErrorClass.Hyphenation, 2, 2, 1, 0, 1.0, 0.5)]
        };
        var gate = new QualityBenchmarkGateEvaluator(new QualityBenchmarkGateOptions(
            MinimumPrecision: 0,
            MinimumRecall: 0)
        {
            ClassRecallThresholds = [new QualityBenchmarkClassGateThreshold(OcrErrorClass.Hyphenation, 1.0)]
        }).Evaluate(result);

        Assert.False(gate.Passed);
        Assert.Contains(gate.Failures, failure => failure.Metric == "ClassRecall:Hyphenation");
    }

    [Fact]
    public void Evaluate_ClassRecallAtThresholdPasses()
    {
        var result = CleanResult() with
        {
            ClassBreakdowns = [new QualityBenchmarkClassBreakdown(OcrErrorClass.Hyphenation, 2, 2, 2, 0, 1.0, 1.0)]
        };
        var gate = new QualityBenchmarkGateEvaluator(new QualityBenchmarkGateOptions(
            MinimumPrecision: 0,
            MinimumRecall: 0)
        {
            ClassRecallThresholds = [new QualityBenchmarkClassGateThreshold(OcrErrorClass.Hyphenation, 1.0)]
        }).Evaluate(result);

        Assert.True(gate.Passed);
    }

    [Fact]
    public void Evaluate_ClassRecallThreshold_ClassAbsentFromBreakdownsDoesNotFail()
    {
        // A class with a threshold but zero ground-truth records in this dataset run has no
        // breakdown entry at all (CreateClassBreakdowns groups only classes that are present).
        // Absence must not be treated as a recall of zero.
        var result = CleanResult() with { ClassBreakdowns = [] };
        var gate = new QualityBenchmarkGateEvaluator(new QualityBenchmarkGateOptions(
            MinimumPrecision: 0,
            MinimumRecall: 0)
        {
            ClassRecallThresholds = [new QualityBenchmarkClassGateThreshold(OcrErrorClass.GlyphConfusion, 0.5)]
        }).Evaluate(result);

        Assert.True(gate.Passed);
    }

    [Fact]
    public void GateOptions_ClassRecallThresholds_LoadedFromProfileFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"quality-gate-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """
            {
              "current": {
                "minimumPrecision": 0.98,
                "minimumRecall": 0.6,
                "classRecallThresholds": [
                  { "errorClass": "Hyphenation", "minimumRecall": 1.0 }
                ]
              }
            }
            """);
        try
        {
            var options = QualityBenchmarkGateProfileLoader.LoadCurrent(path);

            Assert.NotNull(options);
            var threshold = Assert.Single(options!.ClassRecallThresholds);
            Assert.Equal(OcrErrorClass.Hyphenation, threshold.ErrorClass);
            Assert.Equal(1.0, threshold.MinimumRecall);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void GateOptions_LoadedFromProfileFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"quality-gate-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """
            {
              "current": { "minimumPrecision": 0.91, "minimumRecall": 0.42 }
            }
            """);
        try
        {
            var options = QualityBenchmarkGateProfileLoader.LoadCurrent(path);

            Assert.NotNull(options);
            Assert.Equal(0.91, options.MinimumPrecision);
            Assert.Equal(0.42, options.MinimumRecall);
        }
        finally
        {
            File.Delete(path);
        }
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

    [Theory]
    [InlineData(new[] { "dataset" }, "Measure", "dataset", "legacy")]
    [InlineData(new[] { "--ocr-engine", "legacy", "dataset" }, "Measure", "dataset", "legacy")]
    [InlineData(new[] { "--ocr-engine", "lattice", "dataset" }, "Measure", "dataset", "lattice")]
    [InlineData(new[] { "--ocr-engine", "LATTICE", "dataset" }, "Measure", "dataset", "lattice")]
    [InlineData(new[] { "--ocr-engine", "hybrid", "dataset" }, "Measure", "dataset", "hybrid")]
    [InlineData(new[] { "--propose", "dataset" }, "Propose", "dataset", null)]
    [InlineData(new string[0], "Usage", null, null)]
    [InlineData(new[] { "--ocr-engine", "unknown-engine", "dataset" }, "Usage", null, null)]
    [InlineData(new[] { "--ocr-engine", "lattice" }, "Usage", null, null)]
    public void ParseArguments_RecognizesOcrEngineFlag(
        string[] args,
        string expectedMode,
        string? expectedDataset,
        string? expectedEngine)
    {
        var parsed = QualityBenchmarkApplication.ParseArguments(args);

        Assert.Equal(expectedMode, parsed.Mode.ToString());
        Assert.Equal(expectedDataset, parsed.DatasetDirectory);
        Assert.Equal(expectedEngine, parsed.OcrEngine);
    }

    [Fact]
    public void Application_OcrEngineFlag_PassesLatticePlannerToRunner()
    {
        var dataset = new QualityBenchmarkDataset(
            "synthetic",
            "dataset",
            "input.epub",
            "ground-truth.json",
            new GroundTruthDocument(1, []));
        IOcrCorrectionPlanner? capturedPlanner = null;
        var app = new QualityBenchmarkApplication(
            _ => dataset,
            (_, planner) =>
            {
                capturedPlanner = planner;
                return CleanResult();
            },
            new QualityBenchmarkGateEvaluator());
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = app.Run(["--ocr-engine", "lattice", "dataset"], output, error);

        Assert.Equal(0, exitCode);
        Assert.NotNull(capturedPlanner);
    }

    [Fact]
    public void Application_OcrEngineFlagHybrid_PassesCompositePlannerToRunner()
    {
        var dataset = new QualityBenchmarkDataset(
            "synthetic",
            "dataset",
            "input.epub",
            "ground-truth.json",
            new GroundTruthDocument(1, []));
        IOcrCorrectionPlanner? capturedPlanner = null;
        var app = new QualityBenchmarkApplication(
            _ => dataset,
            (_, planner) =>
            {
                capturedPlanner = planner;
                return CleanResult();
            },
            new QualityBenchmarkGateEvaluator());
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = app.Run(["--ocr-engine", "hybrid", "dataset"], output, error);

        Assert.Equal(0, exitCode);
        Assert.IsType<CompositeOcrCorrectionPlanner>(capturedPlanner);
    }

    [Fact]
    public void Application_OcrEngineFlagDefaultsToLegacy_PassesNullPlanner()
    {
        var dataset = new QualityBenchmarkDataset(
            "synthetic",
            "dataset",
            "input.epub",
            "ground-truth.json",
            new GroundTruthDocument(1, []));
        IOcrCorrectionPlanner? capturedPlanner = new LegacyOcrCorrectionPlanner();
        var app = new QualityBenchmarkApplication(
            _ => dataset,
            (_, planner) =>
            {
                capturedPlanner = planner;
                return CleanResult();
            },
            new QualityBenchmarkGateEvaluator());
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = app.Run(["dataset"], output, error);

        Assert.Equal(0, exitCode);
        Assert.Null(capturedPlanner);
    }

    [Fact]
    public void Application_UnknownOcrEngineValue_IsUsageError()
    {
        var app = new QualityBenchmarkApplication();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = app.Run(["--ocr-engine", "made-up", "dataset"], output, error);

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
