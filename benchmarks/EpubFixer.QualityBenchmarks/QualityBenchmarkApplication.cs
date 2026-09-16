using EpubFixer.QualityBenchmarks.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using EpubFixer.Adapters.Ocr;
using EpubFixer.Core.Epub;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Ocr;
using EpubFixer.TrMorph;

namespace EpubFixer.QualityBenchmarks;

public sealed class QualityBenchmarkApplication
{
    private readonly Func<string, QualityBenchmarkDataset> _load;
    private readonly Func<QualityBenchmarkDataset, IOcrCorrectionPlanner?, QualityBenchmarkResult> _run;
    private readonly QualityBenchmarkGateEvaluator _gateEvaluator;
    private readonly string _gateProfileDescription;

    public QualityBenchmarkApplication()
        : this(CreateDefaultDependencies())
    {
    }

    private QualityBenchmarkApplication(DefaultDependencies dependencies)
        : this(
            directory => new QualityBenchmarkDatasetLoader().Load(directory),
            (dataset, planner) => new QualityBenchmarkRunner(planner).Run(dataset),
            new QualityBenchmarkGateEvaluator(dependencies.Options),
            dependencies.Description)
    {
        _gateProfileFailure = dependencies.Failure;
    }

    /// <summary>Test seam for G3: an application whose gate profile could not be loaded.</summary>
    internal static QualityBenchmarkApplication WithUnusableGateProfile(string failure) =>
        new(new DefaultDependencies(null, "unusable", failure));

    internal QualityBenchmarkApplication(
        Func<string, QualityBenchmarkDataset> load,
        Func<QualityBenchmarkDataset, QualityBenchmarkResult> run,
        QualityBenchmarkGateEvaluator gateEvaluator)
        : this(load, (dataset, _) => run(dataset), gateEvaluator, "injected")
    {
    }

    internal QualityBenchmarkApplication(
        Func<string, QualityBenchmarkDataset> load,
        Func<QualityBenchmarkDataset, IOcrCorrectionPlanner?, QualityBenchmarkResult> run,
        QualityBenchmarkGateEvaluator gateEvaluator)
        : this(load, run, gateEvaluator, "injected")
    {
    }

    private QualityBenchmarkApplication(
        Func<string, QualityBenchmarkDataset> load,
        Func<QualityBenchmarkDataset, IOcrCorrectionPlanner?, QualityBenchmarkResult> run,
        QualityBenchmarkGateEvaluator gateEvaluator,
        string gateProfileDescription)
    {
        _load = load;
        _run = run;
        _gateEvaluator = gateEvaluator;
        _gateProfileDescription = gateProfileDescription;
    }

    private readonly string? _gateProfileFailure;

    public int Run(
        string[] args,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        var parsed = ParseArguments(args);
        if (parsed.Mode == BenchmarkMode.Usage)
        {
            PrintUsage(error);
            return 1;
        }

        if (parsed.Mode == BenchmarkMode.Propose)
        {
            return RunPropose(parsed.DatasetDirectory!, output, error);
        }

        // G3: refuse before doing any work. Running with a fallback profile would produce a PASS
        // that means nothing, and a meaningless PASS is worse than no answer.
        if (_gateProfileFailure is not null)
        {
            error.WriteLine($"Error: {_gateProfileFailure}");
            return 2;
        }

        try
        {
            var dataset = _load(parsed.DatasetDirectory!);
            var planner = OcrPlannerFactory.Resolve(parsed.OcrEngine!);
            var result = _run(dataset, planner);
            QualityBenchmarkReportWriter.Write(output, dataset.Name, result);
            output.WriteLine();
            output.WriteLine($"Quality gate profile: {_gateProfileDescription}");

            var gate = _gateEvaluator.Evaluate(result);
            QualityBenchmarkGateReportWriter.Write(output, gate);
            return gate.Passed ? 0 : 1;
        }
        catch (Exception exception) when (exception is ArgumentException
            or IOException
            or InvalidDataException
            or UnauthorizedAccessException)
        {
            error.WriteLine($"Error: {exception.Message}");
            return 2;
        }
    }

    private static void PrintUsage(TextWriter error)
    {
        error.WriteLine(
            "Usage: dotnet run --project benchmarks/EpubFixer.QualityBenchmarks -- <dataset-directory>");
        error.WriteLine(
            "       dotnet run --project benchmarks/EpubFixer.QualityBenchmarks -- --propose <dataset-directory>");
        error.WriteLine(
            $"       dotnet run --project benchmarks/EpubFixer.QualityBenchmarks -- --ocr-engine {string.Join('|', OcrPlannerFactory.KnownEngineNames)} <dataset-directory>");
    }

    internal enum BenchmarkMode { Usage, Propose, Measure }

    internal readonly record struct ParsedArguments(BenchmarkMode Mode, string? DatasetDirectory, string? OcrEngine);

    internal static ParsedArguments ParseArguments(string[] args)
    {
        if (args.Length == 2 && string.Equals(args[0], "--propose", StringComparison.OrdinalIgnoreCase))
        {
            return new ParsedArguments(BenchmarkMode.Propose, args[1], null);
        }

        if (args.Length == 1 && !string.IsNullOrWhiteSpace(args[0]))
        {
            return new ParsedArguments(BenchmarkMode.Measure, args[0], "hybrid");
        }

        if (args.Length == 3
            && string.Equals(args[0], "--ocr-engine", StringComparison.OrdinalIgnoreCase)
            && OcrPlannerFactory.IsKnownEngine(args[1])
            && !string.IsNullOrWhiteSpace(args[2]))
        {
            return new ParsedArguments(BenchmarkMode.Measure, args[2], args[1].ToLowerInvariant());
        }

        return new ParsedArguments(BenchmarkMode.Usage, null, null);
    }

    private static int RunPropose(string datasetDirectory, TextWriter output, TextWriter error)
    {
        try
        {
            var dataset = new QualityBenchmarkDatasetLoader().Load(datasetDirectory);
            var package = new EpubPackageReader().Read(dataset.InputEpubPath);
            using var analyzer = new FomaTurkishMorphologyAnalyzer();
            var detector = new OcrRegionDetector();
            var builder = new BatchMorphologyOracleBuilder(analyzer);
            var oracle = builder.Build(
                new OcrAnomalyDetector().EnumerateMorphologyQueries(package.LogicalText)
                    .Concat(detector.EnumerateMorphologyQueries(package.LogicalText.Text)));
            var proposals = new GroundTruthProposer().Propose(package.LogicalText, oracle);
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };
            options.Converters.Add(new JsonStringEnumConverter());
            output.WriteLine(JsonSerializer.Serialize(proposals, options));
            return 0;
        }
        catch (Exception exception) when (exception is ArgumentException
            or IOException
            or InvalidDataException
            or UnauthorizedAccessException
            or TurkishMorphologyException)
        {
            error.WriteLine($"Error: {exception.Message}");
            return 2;
        }
    }

    private static DefaultDependencies CreateDefaultDependencies()
    {
        // G3: the failure is carried, not thrown, so Run reports it the same clean way every other
        // failure is reported (message on stderr, exit code 2) instead of a constructor stack trace.
        var path = QualityBenchmarkGateProfileLoader.FindProfilePath();
        if (path is null)
        {
            return new DefaultDependencies(
                null,
                "unusable",
                "Quality gate profile 'docs/baselines/quality-gate.json' was not found from the current "
                + "directory or the binary's location. Refusing to run: without it the gate would fall "
                + "back to weaker defaults and report PASS against thresholds nobody chose (G3).");
        }

        try
        {
            return new DefaultDependencies(
                QualityBenchmarkGateProfileLoader.LoadCurrent(path),
                $"current from {path}",
                null);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return new DefaultDependencies(null, "unusable", exception.Message);
        }
    }

    private sealed record DefaultDependencies(QualityBenchmarkGateOptions? Options, string Description, string? Failure);
}

internal static class QualityBenchmarkGateProfileLoader
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    /// <summary>
    /// Reads the active profile. G3: every failure below used to return <see langword="null"/>, and
    /// the evaluator then fell back to its own defaults - precision 98%, recall 60%, no class
    /// floors. A single typo in the profile silently dropped the recall bar and the run still
    /// reported PASS. A gate that quietly weakens itself is not a gate, so these now throw.
    /// The two aggregate thresholds must be declared EXPLICITLY: the options record carries
    /// defaults for them, so an absent property is indistinguishable from a deliberate value.
    /// </summary>
    public static QualityBenchmarkGateOptions LoadCurrent(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidDataException($"Quality gate profile '{path}' was not found.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(path));
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Quality gate profile '{path}' is not valid JSON: {exception.Message}", exception);
        }

        using (document)
        {
            if (!document.RootElement.TryGetProperty("current", out var current))
            {
                throw new InvalidDataException($"Quality gate profile '{path}' has no 'current' node.");
            }

            foreach (var required in new[] { "minimumPrecision", "minimumRecall" })
            {
                if (!current.TryGetProperty(required, out _))
                {
                    throw new InvalidDataException(
                        $"Quality gate profile '{path}' is missing 'current.{required}'. Both aggregate "
                        + "thresholds must be declared explicitly - an absent one would silently take the "
                        + "evaluator's own weaker default and hide a regression (G3).");
                }
            }

            return current.Deserialize<QualityBenchmarkGateOptions>(Options)
                ?? throw new InvalidDataException($"Quality gate profile '{path}' has an unreadable 'current' node.");
        }
    }

    public static string? FindProfilePath()
    {
        foreach (var root in EnumerateSearchRoots())
        {
            for (var directory = new DirectoryInfo(root); directory is not null; directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, "docs", "baselines", "quality-gate.json");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateSearchRoots()
    {
        yield return Directory.GetCurrentDirectory();
        yield return AppContext.BaseDirectory;
    }
}

public static class QualityBenchmarkGateReportWriter
{
    public static void Write(TextWriter writer, QualityBenchmarkGateResult gate)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(gate);

        writer.WriteLine();
        writer.WriteLine($"Quality gate: {(gate.Passed ? "PASS" : "FAIL")}");
        if (!gate.Passed)
        {
            writer.WriteLine("Gate failures:");
            foreach (var failure in gate.Failures)
            {
                writer.WriteLine($"- {failure.Metric} expected {failure.Expected}, actual {failure.Actual}");
            }
        }
    }
}
