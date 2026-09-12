using EpubFixer.QualityBenchmarks.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using EpubFixer.Core.Epub;
using EpubFixer.TrMorph;

namespace EpubFixer.QualityBenchmarks;

public sealed class QualityBenchmarkApplication
{
    private readonly Func<string, QualityBenchmarkDataset> _load;
    private readonly Func<QualityBenchmarkDataset, QualityBenchmarkResult> _run;
    private readonly QualityBenchmarkGateEvaluator _gateEvaluator;
    private readonly string _gateProfileDescription;

    public QualityBenchmarkApplication()
        : this(CreateDefaultDependencies())
    {
    }

    private QualityBenchmarkApplication(DefaultDependencies dependencies)
        : this(
            directory => new QualityBenchmarkDatasetLoader().Load(directory),
            dataset => new QualityBenchmarkRunner().Run(dataset),
            new QualityBenchmarkGateEvaluator(dependencies.Options),
            dependencies.Description)
    {
    }

    internal QualityBenchmarkApplication(
        Func<string, QualityBenchmarkDataset> load,
        Func<QualityBenchmarkDataset, QualityBenchmarkResult> run,
        QualityBenchmarkGateEvaluator gateEvaluator)
        : this(load, run, gateEvaluator, "injected")
    {
    }

    private QualityBenchmarkApplication(
        Func<string, QualityBenchmarkDataset> load,
        Func<QualityBenchmarkDataset, QualityBenchmarkResult> run,
        QualityBenchmarkGateEvaluator gateEvaluator,
        string gateProfileDescription)
    {
        _load = load;
        _run = run;
        _gateEvaluator = gateEvaluator;
        _gateProfileDescription = gateProfileDescription;
    }

    public int Run(
        string[] args,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args.Length == 2 && string.Equals(args[0], "--propose", StringComparison.OrdinalIgnoreCase))
        {
            return RunPropose(args[1], output, error);
        }

        if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
        {
            error.WriteLine(
                "Usage: dotnet run --project benchmarks/EpubFixer.QualityBenchmarks -- <dataset-directory>");
            error.WriteLine(
                "       dotnet run --project benchmarks/EpubFixer.QualityBenchmarks -- --propose <dataset-directory>");
            return 1;
        }

        try
        {
            var dataset = _load(args[0]);
            var result = _run(dataset);
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

    private static int RunPropose(string datasetDirectory, TextWriter output, TextWriter error)
    {
        try
        {
            var dataset = new QualityBenchmarkDatasetLoader().Load(datasetDirectory);
            var package = new EpubPackageReader().Read(dataset.InputEpubPath);
            using var analyzer = new FomaTurkishMorphologyAnalyzer();
            var proposals = new GroundTruthProposer().Propose(package.LogicalText, analyzer);
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
        var path = QualityBenchmarkGateProfileLoader.FindProfilePath();
        if (path is null)
        {
            return new DefaultDependencies(null, "defaults (profile not found)");
        }

        return new DefaultDependencies(
            QualityBenchmarkGateProfileLoader.LoadCurrent(path),
            $"current from {path}");
    }

    private sealed record DefaultDependencies(QualityBenchmarkGateOptions? Options, string Description);
}

internal static class QualityBenchmarkGateProfileLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static QualityBenchmarkGateOptions? LoadCurrent(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("current", out var current))
        {
            return null;
        }

        return current.Deserialize<QualityBenchmarkGateOptions>(Options);
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
