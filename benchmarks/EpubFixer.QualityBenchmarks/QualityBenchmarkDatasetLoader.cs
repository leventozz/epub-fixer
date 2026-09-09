using System.Text.Json;
using System.Text;
using EpubFixer.QualityBenchmarks.Models;

namespace EpubFixer.QualityBenchmarks;

public sealed class QualityBenchmarkDatasetLoader
{
    public const int SupportedSchemaVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public QualityBenchmarkDataset Load(string datasetDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(datasetDirectory);

        var directoryPath = Path.GetFullPath(datasetDirectory);

        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Dataset directory was not found: {directoryPath}");
        }

        var inputEpubPath = Path.Combine(directoryPath, "input.epub");

        if (!File.Exists(inputEpubPath))
        {
            throw new FileNotFoundException($"Input EPUB was not found: {inputEpubPath}", inputEpubPath);
        }

        var groundTruthPath = Path.Combine(directoryPath, "ground-truth.json");

        if (!File.Exists(groundTruthPath))
        {
            throw new FileNotFoundException(
                $"Ground truth file was not found: {groundTruthPath}",
                groundTruthPath);
        }

        var json = File.ReadAllText(groundTruthPath);

        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException($"Ground truth file is empty: {groundTruthPath}");
        }

        GroundTruthDocument groundTruth;

        try
        {
            groundTruth = JsonSerializer.Deserialize<GroundTruthDocument>(json, SerializerOptions)
                ?? throw new InvalidDataException(
                    $"Ground truth JSON does not contain a document: {groundTruthPath}");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Ground truth JSON is invalid: {groundTruthPath}",
                exception);
        }

        ValidateGroundTruth(groundTruth, groundTruthPath);

        var normalizedDirectoryPath = Path.TrimEndingDirectorySeparator(directoryPath);
        var datasetName = Path.GetFileName(normalizedDirectoryPath);

        return new QualityBenchmarkDataset(
            datasetName,
            normalizedDirectoryPath,
            inputEpubPath,
            groundTruthPath,
            groundTruth);
    }

    private static void ValidateGroundTruth(
        GroundTruthDocument groundTruth,
        string groundTruthPath)
    {
        if (groundTruth.SchemaVersion != SupportedSchemaVersion)
        {
            throw InvalidGroundTruth(
                groundTruthPath,
                $"schemaVersion must be {SupportedSchemaVersion}");
        }

        if (groundTruth.KnownErrors is null)
        {
            throw InvalidGroundTruth(groundTruthPath, "knownErrors is required");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var occurrenceLocations = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < groundTruth.KnownErrors.Count; index++)
        {
            var occurrence = groundTruth.KnownErrors[index];
            var fieldPrefix = $"knownErrors[{index}]";

            if (occurrence is null)
            {
                throw InvalidGroundTruth(groundTruthPath, $"{fieldPrefix} must not be null");
            }

            RequireText(occurrence.Id, $"{fieldPrefix}.id", groundTruthPath);

            if (!ids.Add(occurrence.Id))
            {
                throw InvalidGroundTruth(
                    groundTruthPath,
                    $"{fieldPrefix}.id must be unique; duplicate value '{occurrence.Id}'");
            }

            RequireText(occurrence.DocumentPath, $"{fieldPrefix}.documentPath", groundTruthPath);
            RequireText(occurrence.Original, $"{fieldPrefix}.original", groundTruthPath);
            RequireText(occurrence.Expected, $"{fieldPrefix}.expected", groundTruthPath);

            if (occurrence.SourceSpans is null || occurrence.SourceSpans.Count == 0)
            {
                throw InvalidGroundTruth(
                    groundTruthPath,
                    $"{fieldPrefix}.sourceSpans must contain at least one span");
            }

            for (var spanIndex = 0; spanIndex < occurrence.SourceSpans.Count; spanIndex++)
            {
                var span = occurrence.SourceSpans[spanIndex];
                var spanPrefix = $"{fieldPrefix}.sourceSpans[{spanIndex}]";

                if (span is null)
                {
                    throw InvalidGroundTruth(groundTruthPath, $"{spanPrefix} must not be null");
                }

                RequireText(span.DocumentPath, $"{spanPrefix}.documentPath", groundTruthPath);

                if (span.TextNodeIndex < 0 || span.Start < 0 || span.Length <= 0)
                {
                    throw InvalidGroundTruth(
                        groundTruthPath,
                        $"{spanPrefix} requires textNodeIndex and start >= 0, and length > 0");
                }
            }

            if (!occurrenceLocations.Add(CreateOccurrenceLocationKey(occurrence)))
            {
                throw InvalidGroundTruth(
                    groundTruthPath,
                    $"{fieldPrefix} duplicates an exact source occurrence");
            }
        }

        if (groundTruth.ProtectedOccurrences is null)
        {
            throw InvalidGroundTruth(groundTruthPath, "protectedOccurrences must not be null");
        }

        for (var index = 0; index < groundTruth.ProtectedOccurrences.Count; index++)
        {
            var occurrence = groundTruth.ProtectedOccurrences[index];
            var fieldPrefix = $"protectedOccurrences[{index}]";

            if (occurrence is null)
            {
                throw InvalidGroundTruth(groundTruthPath, $"{fieldPrefix} must not be null");
            }

            RequireText(occurrence.Id, $"{fieldPrefix}.id", groundTruthPath);

            if (!ids.Add(occurrence.Id))
            {
                throw InvalidGroundTruth(
                    groundTruthPath,
                    $"{fieldPrefix}.id must be unique; duplicate value '{occurrence.Id}'");
            }

            RequireText(occurrence.DocumentPath, $"{fieldPrefix}.documentPath", groundTruthPath);
            RequireText(occurrence.Original, $"{fieldPrefix}.original", groundTruthPath);

            if (occurrence.SourceSpans is null || occurrence.SourceSpans.Count == 0)
            {
                throw InvalidGroundTruth(
                    groundTruthPath,
                    $"{fieldPrefix}.sourceSpans must contain at least one span");
            }

            for (var spanIndex = 0; spanIndex < occurrence.SourceSpans.Count; spanIndex++)
            {
                var span = occurrence.SourceSpans[spanIndex];
                var spanPrefix = $"{fieldPrefix}.sourceSpans[{spanIndex}]";

                if (span is null)
                {
                    throw InvalidGroundTruth(groundTruthPath, $"{spanPrefix} must not be null");
                }

                RequireText(span.DocumentPath, $"{spanPrefix}.documentPath", groundTruthPath);

                if (span.TextNodeIndex < 0 || span.Start < 0 || span.Length <= 0)
                {
                    throw InvalidGroundTruth(
                        groundTruthPath,
                        $"{spanPrefix} requires textNodeIndex and start >= 0, and length > 0");
                }
            }

            if (!occurrenceLocations.Add(CreateOccurrenceLocationKey(occurrence)))
            {
                throw InvalidGroundTruth(
                    groundTruthPath,
                    $"{fieldPrefix} duplicates an exact source occurrence");
            }
        }
    }

    private static string CreateOccurrenceLocationKey(KnownErrorOccurrence occurrence)
    {
        return CreateOccurrenceLocationKey(
            occurrence.DocumentPath,
            occurrence.SourceSpans);
    }

    private static string CreateOccurrenceLocationKey(ProtectedOccurrence occurrence)
    {
        return CreateOccurrenceLocationKey(
            occurrence.DocumentPath,
            occurrence.SourceSpans);
    }

    private static string CreateOccurrenceLocationKey(
        string documentPath,
        IReadOnlyList<GroundTruthSourceSpan> sourceSpans)
    {
        var builder = new StringBuilder(documentPath);

        foreach (var span in sourceSpans)
        {
            builder.Append('\0');
            builder.Append(span.DocumentPath);
            builder.Append('\0');
            builder.Append(span.TextNodeIndex);
            builder.Append('\0');
            builder.Append(span.Start);
            builder.Append('\0');
            builder.Append(span.Length);
        }

        return builder.ToString();
    }

    private static void RequireText(string? value, string field, string groundTruthPath)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw InvalidGroundTruth(groundTruthPath, $"{field} is required");
        }
    }

    private static InvalidDataException InvalidGroundTruth(string path, string detail)
    {
        return new InvalidDataException($"Ground truth is invalid ({detail}): {path}");
    }
}
