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
            ValidateOccurrence(groundTruth.KnownErrors[occurrenceIndex], occurrenceIndex, sources);
        }
    }

    private static void ValidateOccurrence(
        KnownErrorOccurrence occurrence,
        int occurrenceIndex,
        IReadOnlyDictionary<SourceKey, SourceSegment> sources)
    {
        var original = new StringBuilder();
        var previousSourceOrder = -1;
        var containsOccurrenceDocument = false;

        for (var spanIndex = 0; spanIndex < occurrence.SourceSpans.Count; spanIndex++)
        {
            var span = occurrence.SourceSpans[spanIndex];
            var field = $"knownErrors[{occurrenceIndex}].sourceSpans[{spanIndex}]";
            var key = new SourceKey(span.DocumentPath, span.TextNodeIndex);

            if (!sources.TryGetValue(key, out var source))
            {
                throw InvalidSourceSpan(
                    occurrence,
                    field,
                    "documentPath/textNodeIndex does not resolve to a logical text source");
            }

            if (source.Order <= previousSourceOrder)
            {
                throw InvalidSourceSpan(
                    occurrence,
                    field,
                    "spans must be canonical, non-overlapping, and in logical source order");
            }

            var sourceStart = source.Segment.Source.Start;
            var sourceEnd = (long)sourceStart + source.Segment.Source.Length;
            var spanEnd = (long)span.Start + span.Length;

            if (span.Start < sourceStart || spanEnd > sourceEnd)
            {
                throw InvalidSourceSpan(
                    occurrence,
                    field,
                    "start/length is outside the referenced text node");
            }

            original.Append(
                source.Segment.Text,
                span.Start - sourceStart,
                span.Length);
            previousSourceOrder = source.Order;
            containsOccurrenceDocument |= string.Equals(
                occurrence.DocumentPath,
                span.DocumentPath,
                StringComparison.Ordinal);
        }

        if (!containsOccurrenceDocument)
        {
            throw InvalidSourceSpan(
                occurrence,
                $"knownErrors[{occurrenceIndex}].documentPath",
                "does not identify any source span document");
        }

        if (!string.Equals(original.ToString(), occurrence.Original, StringComparison.Ordinal))
        {
            throw InvalidSourceSpan(
                occurrence,
                $"knownErrors[{occurrenceIndex}].sourceSpans",
                "referenced source text does not equal original");
        }
    }

    private static InvalidDataException InvalidSourceSpan(
        KnownErrorOccurrence occurrence,
        string field,
        string detail)
    {
        return new InvalidDataException(
            $"Ground truth source span is invalid for '{occurrence.Id}' ({field}: {detail}).");
    }

    private sealed record SourceKey(string DocumentPath, int TextNodeIndex);

    private sealed record SourceSegment(TextSegment Segment, int Order);
}
