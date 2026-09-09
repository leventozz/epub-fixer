using EpubFixer.QualityBenchmarks;
using EpubFixer.QualityBenchmarks.Models;

if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project benchmarks/EpubFixer.QualityBenchmarks -- <dataset-directory>");
    return 1;
}

try
{
    var dataset = new QualityBenchmarkDatasetLoader().Load(args[0]);
    var result = new QualityBenchmarkRunner().Run(dataset);

    Console.WriteLine($"Dataset: {dataset.Name}");
    Console.WriteLine();
    Console.WriteLine($"Known errors: {result.KnownErrors}");
    Console.WriteLine($"Detected: {result.Detected}");
    Console.WriteLine($"Missed: {result.Missed}");
    Console.WriteLine($"Detection recall: {FormatDetectionRecall(result.DetectionRecall)}");

    foreach (var missedOccurrence in result.MissedOccurrences)
    {
        PrintMissedOccurrence(missedOccurrence);
    }

    return 0;
}
catch (Exception exception) when (exception is ArgumentException
    or IOException
    or InvalidDataException
    or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"Error: {exception.Message}");
    return 2;
}

static string FormatDetectionRecall(double? detectionRecall)
{
    return detectionRecall.HasValue
        ? (detectionRecall.Value * 100).ToString(
            "F2",
            System.Globalization.CultureInfo.InvariantCulture) + "%"
        : "N/A";
}

static void PrintMissedOccurrence(KnownErrorOccurrence occurrence)
{
    Console.WriteLine();
    Console.WriteLine("MISSED");
    Console.WriteLine($"id: {occurrence.Id}");
    Console.WriteLine($"document: {occurrence.DocumentPath}");
    Console.WriteLine($"original: {occurrence.Original}");
    Console.WriteLine($"expected: {occurrence.Expected}");
}
