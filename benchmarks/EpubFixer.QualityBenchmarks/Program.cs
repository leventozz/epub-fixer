using EpubFixer.QualityBenchmarks;

if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project benchmarks/EpubFixer.QualityBenchmarks -- <dataset-directory>");
    return 1;
}

try
{
    var dataset = new QualityBenchmarkDatasetLoader().Load(args[0]);

    Console.WriteLine($"Dataset: {dataset.Name}");
    Console.WriteLine();
    Console.WriteLine("Input EPUB: found");
    Console.WriteLine("Ground truth: loaded");
    Console.WriteLine($"Known errors: {dataset.GroundTruth.KnownErrors.Count}");
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
