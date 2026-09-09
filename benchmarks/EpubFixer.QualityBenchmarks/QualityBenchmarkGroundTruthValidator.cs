using System.Text;
using EpubFixer.Core.Epub.Models;
using EpubFixer.QualityBenchmarks.Models;

namespace EpubFixer.QualityBenchmarks;

public sealed class QualityBenchmarkGroundTruthValidator
{
    public void Validate(GroundTruthDocument groundTruth, LogicalTextStream logicalText)
    {
        ArgumentNullException.ThrowIfNull(groundTruth);
        ArgumentNullException.ThrowIfNull(logicalText);

        var sources = logicalText.Segments
            .Select((segment, order) => new SourceSegment(segment, order))
            .ToDictionary(
                item => new SourceKey(
                    item.Segment.Source.DocumentPath,
                    item.Segment.Source.TextNodeIndex));

        for (var occurrenceIndex = 0;
             occurrenceIndex < groundTruth.KnownErrors.Count;
             occurrenceIndex++)
        {
            var occurrence = groundTruth.KnownErrors[occurrenceIndex];
            ValidateOccurrence(
                occurrence.Id,
                occurrence.DocumentPath,
                occurrence.Original,
                occurrence.SourceSpans,
                $"knownErrors[{occurrenceIndex}]",
                sources);
        }

        for (var occurrenceIndex = 0;
             occurrenceIndex < groundTruth.ProtectedOccurrences.Count;
             occurrenceIndex++)
        {
            var occurrence = groundTruth.ProtectedOccurrences[occurrenceIndex];
            ValidateOccurrence(
                occurrence.Id,
                occurrence.DocumentPath,
                occurrence.Original,
                occurrence.SourceSpans,
                $"protectedOccurrences[{occurrenceIndex}]",
                sources);
        }
    }

    private static void ValidateOccurrence(
        string occurrenceId,
        string occurrenceDocumentPath,
        string occurrenceOriginal,
        IReadOnlyList<GroundTruthSourceSpan> sourceSpans,
        string fieldPrefix,
        IReadOnlyDictionary<SourceKey, SourceSegment> sources)
    {
        var original = new StringBuilder();
        var previousSourceOrder = -1;
        var containsOccurrenceDocument = false;

        for (var spanIndex = 0; spanIndex < sourceSpans.Count; spanIndex++)
        {
            var span = sourceSpans[spanIndex];
            var field = $"{fieldPrefix}.sourceSpans[{spanIndex}]";
            var key = new SourceKey(span.DocumentPath, span.TextNodeIndex);

            if (!sources.TryGetValue(key, out var source))
            {
                throw InvalidSourceSpan(
                    occurrenceId,
                    field,
                    "documentPath/textNodeIndex does not resolve to a logical text source");
            }

            if (source.Order <= previousSourceOrder)
            {
                throw InvalidSourceSpan(
                    occurrenceId,
                    field,
                    "spans must be canonical, non-overlapping, and in logical source order");
            }

            var sourceStart = source.Segment.Source.Start;
            var sourceEnd = (long)sourceStart + source.Segment.Source.Length;
            var spanEnd = (long)span.Start + span.Length;

            if (span.Start < sourceStart || spanEnd > sourceEnd)
            {
                throw InvalidSourceSpan(
                    occurrenceId,
                    field,
                    "start/length is outside the referenced text node");
            }

            original.Append(
                source.Segment.Text,
                span.Start - sourceStart,
                span.Length);
            previousSourceOrder = source.Order;
            containsOccurrenceDocument |= string.Equals(
                occurrenceDocumentPath,
                span.DocumentPath,
                StringComparison.Ordinal);
        }

        if (!containsOccurrenceDocument)
        {
            throw InvalidSourceSpan(
                occurrenceId,
                $"{fieldPrefix}.documentPath",
                "does not identify any source span document");
        }

        if (!string.Equals(original.ToString(), occurrenceOriginal, StringComparison.Ordinal))
        {
            throw InvalidSourceSpan(
                occurrenceId,
                $"{fieldPrefix}.sourceSpans",
                "referenced source text does not equal original");
        }
    }

    private static InvalidDataException InvalidSourceSpan(
        string occurrenceId,
        string field,
        string detail)
    {
        return new InvalidDataException(
            $"Ground truth source span is invalid for '{occurrenceId}' ({field}: {detail}).");
    }

    private sealed record SourceKey(string DocumentPath, int TextNodeIndex);

    private sealed record SourceSegment(TextSegment Segment, int Order);
}
