using System.Reflection;
using EpubFixer.Core.Epub;
using EpubFixer.QualityBenchmarks;

namespace EpubFixer.Tests;

public sealed class QualityBenchmarkTests
{
    [Fact]
    public void Load_ParsesValidGroundTruthJson()
    {
        using var dataset = TemporaryQualityBenchmarkDataset.Create(ValidGroundTruthJson);

        var result = new QualityBenchmarkDatasetLoader().Load(dataset.Path);

        Assert.Equal(dataset.Name, result.Name);
        var occurrence = Assert.Single(result.GroundTruth.KnownErrors);
        Assert.Equal("error-1", occurrence.Id);
        Assert.Equal("main-3.xhtml", occurrence.DocumentPath);
        Assert.Equal("Auersber-ger", occurrence.Original);
        Assert.Equal("Auersberger", occurrence.Expected);
        var span = Assert.Single(occurrence.SourceSpans);
        Assert.Equal(70, span.TextNodeIndex);
        Assert.Equal(480, span.Start);
        Assert.Equal(12, span.Length);
    }

    [Fact]
    public void Load_PreservesSeparateOccurrencesWithTheSameOriginalText()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "knownErrors": [
                {
                  "id": "error-1",
                  "documentPath": "main-3.xhtml",
                  "original": "Auersber-ger",
                  "expected": "Auersberger",
                  "sourceSpans": [
                    { "documentPath": "main-3.xhtml", "textNodeIndex": 70, "start": 480, "length": 12 }
                  ]
                },
                {
                  "id": "error-2",
                  "documentPath": "main-3.xhtml",
                  "original": "Auersber-ger",
                  "expected": "Auersberger",
                  "sourceSpans": [
                    { "documentPath": "main-3.xhtml", "textNodeIndex": 78, "start": 2104, "length": 12 }
                  ]
                }
              ]
            }
            """;
        using var dataset = TemporaryQualityBenchmarkDataset.Create(json);

        var occurrences = new QualityBenchmarkDatasetLoader()
            .Load(dataset.Path)
            .GroundTruth.KnownErrors;

        Assert.Equal(2, occurrences.Count);
        Assert.Equal(["error-1", "error-2"], occurrences.Select(item => item.Id));
        Assert.Equal([480, 2104], occurrences.Select(item => item.SourceSpans[0].Start));
    }

    [Fact]
    public void Load_ReportsMissingInputEpub()
    {
        using var dataset = TemporaryQualityBenchmarkDataset.Create(
            ValidGroundTruthJson,
            createInputEpub: false);

        var exception = Assert.Throws<FileNotFoundException>(
            () => new QualityBenchmarkDatasetLoader().Load(dataset.Path));

        Assert.Contains("Input EPUB was not found", exception.Message);
        Assert.EndsWith("input.epub", exception.FileName, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_ReportsMissingGroundTruth()
    {
        using var dataset = TemporaryQualityBenchmarkDataset.Create(groundTruthJson: null);

        var exception = Assert.Throws<FileNotFoundException>(
            () => new QualityBenchmarkDatasetLoader().Load(dataset.Path));

        Assert.Contains("Ground truth file was not found", exception.Message);
        Assert.EndsWith("ground-truth.json", exception.FileName, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_ReportsEmptyGroundTruth()
    {
        using var dataset = TemporaryQualityBenchmarkDataset.Create(string.Empty);

        var exception = Assert.Throws<InvalidDataException>(
            () => new QualityBenchmarkDatasetLoader().Load(dataset.Path));

        Assert.Contains("Ground truth file is empty", exception.Message);
    }

    [Fact]
    public void Load_ReportsMalformedGroundTruth()
    {
        using var dataset = TemporaryQualityBenchmarkDataset.Create("{ not-json }");

        var exception = Assert.Throws<InvalidDataException>(
            () => new QualityBenchmarkDatasetLoader().Load(dataset.Path));

        Assert.Contains("Ground truth JSON is invalid", exception.Message);
        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public void ProductionAssemblies_DoNotReferenceQualityBenchmarks()
    {
        var productionAssemblies = new[]
        {
            typeof(EpubPackageReader).Assembly,
            Assembly.Load("EpubFixer.Cli")
        };

        foreach (var assembly in productionAssemblies)
        {
            Assert.DoesNotContain(
                assembly.GetReferencedAssemblies(),
                reference => string.Equals(
                    reference.Name,
                    "EpubFixer.QualityBenchmarks",
                    StringComparison.Ordinal));
        }
    }

    private const string ValidGroundTruthJson = """
        {
          "schemaVersion": 1,
          "knownErrors": [
            {
              "id": "error-1",
              "documentPath": "main-3.xhtml",
              "original": "Auersber-ger",
              "expected": "Auersberger",
              "sourceSpans": [
                {
                  "documentPath": "main-3.xhtml",
                  "textNodeIndex": 70,
                  "start": 480,
                  "length": 12
                }
              ]
            }
          ]
        }
        """;
}

internal sealed class TemporaryQualityBenchmarkDataset : IDisposable
{
    private TemporaryQualityBenchmarkDataset(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public string Name => System.IO.Path.GetFileName(Path);

    public static TemporaryQualityBenchmarkDataset Create(
        string? groundTruthJson,
        bool createInputEpub = true)
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"epubfixer-quality-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);

        if (createInputEpub)
        {
            File.WriteAllBytes(System.IO.Path.Combine(path, "input.epub"), []);
        }

        if (groundTruthJson is not null)
        {
            File.WriteAllText(System.IO.Path.Combine(path, "ground-truth.json"), groundTruthJson);
        }

        return new TemporaryQualityBenchmarkDataset(path);
    }

    public void Dispose()
    {
        Directory.Delete(Path, recursive: true);
    }
}
