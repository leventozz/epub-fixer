using EpubFixer.QualityBenchmarks.Models;

namespace EpubFixer.QualityBenchmarks;

public sealed class QualityBenchmarkApplication
{
    private readonly Func<string, QualityBenchmarkDataset> _load;
    private readonly Func<QualityBenchmarkDataset, QualityBenchmarkResult> _run;
    private readonly QualityBenchmarkGateEvaluator _gateEvaluator;

    public QualityBenchmarkApplication()
        : this(
            directory => new QualityBenchmarkDatasetLoader().Load(directory),
            dataset => new QualityBenchmarkRunner().Run(dataset),
            new QualityBenchmarkGateEvaluator())
    {
    }

    internal QualityBenchmarkApplication(
        Func<string, QualityBenchmarkDataset> load,
        Func<QualityBenchmarkDataset, QualityBenchmarkResult> run,
        QualityBenchmarkGateEvaluator gateEvaluator)
    {
        _load = load;
        _run = run;
        _gateEvaluator = gateEvaluator;
    }

    public int Run(
        string[] args,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
        {
            error.WriteLine(
                "Usage: dotnet run --project benchmarks/EpubFixer.QualityBenchmarks -- <dataset-directory>");
            return 1;
        }

        try
        {
            var dataset = _load(args[0]);
            var result = _run(dataset);
            QualityBenchmarkReportWriter.Write(output, dataset.Name, result);

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
