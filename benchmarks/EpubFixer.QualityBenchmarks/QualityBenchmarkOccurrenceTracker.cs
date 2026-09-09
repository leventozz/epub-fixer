using AngleSharp.Dom;
using EpubFixer.Core.Epub.Models;
using EpubFixer.QualityBenchmarks.Models;

namespace EpubFixer.QualityBenchmarks;

internal sealed class QualityBenchmarkOccurrenceTracker
{
    private QualityBenchmarkOccurrenceTracker(
        string id,
        string original,
        string? expected,
        int spanCount,
        IRange range,
        IReadOnlyList<TrackedSourceSpan> spans)
    {
        Id = id;
        Original = original;
        Expected = expected;
        SpanCount = spanCount;
        Range = range;
        Spans = spans;
    }

    public string Id { get; }

    public string Original { get; }

    public string? Expected { get; }

    public int SpanCount { get; }

    public IRange Range { get; }

    private IReadOnlyList<TrackedSourceSpan> Spans { get; }

    public static IReadOnlyList<QualityBenchmarkOccurrenceTracker> CreateKnown(
        IReadOnlyList<KnownErrorOccurrence> occurrences,
        LogicalTextStream logicalText)
    {
        return occurrences
            .Select(occurrence => Create(
                occurrence.Id,
                occurrence.Original,
                occurrence.Expected,
                occurrence.SourceSpans,
                logicalText))
            .ToArray();
    }

    public static IReadOnlyList<QualityBenchmarkOccurrenceTracker> CreateProtected(
        IReadOnlyList<ProtectedOccurrence> occurrences,
        LogicalTextStream logicalText)
    {
        return occurrences
            .Select(occurrence => Create(
                occurrence.Id,
                occurrence.Original,
                expected: null,
                occurrence.SourceSpans,
                logicalText))
            .ToArray();
    }

    public string ReadObservedText()
    {
        var text = string.Concat(Spans.Select(ReadCurrentSpan));

        if (Expected is not null && text.StartsWith(Expected, StringComparison.Ordinal))
        {
            return Expected;
        }

        if (text.StartsWith(Original, StringComparison.Ordinal))
        {
            return Original;
        }

        return text;
    }

    public void Detach()
    {
        Range.Detach();
    }

    private static QualityBenchmarkOccurrenceTracker Create(
        string id,
        string original,
        string? expected,
        IReadOnlyList<GroundTruthSourceSpan> spans,
        LogicalTextStream logicalText)
    {
        if (spans.Count == 0)
        {
            throw new InvalidDataException($"Occurrence '{id}' has no source spans.");
        }

        var segments = spans
            .Select(span => logicalText.Segments.Single(segment =>
                string.Equals(
                    segment.Source.DocumentPath,
                    span.DocumentPath,
                    StringComparison.Ordinal)
                && segment.Source.TextNodeIndex == span.TextNodeIndex))
            .ToArray();
        var first = spans[0];
        var last = spans[^1];
        var firstSource = segments[0].Source;
        var lastSource = segments[^1].Source;
        var document = firstSource.SourceNode.Owner!;
        var range = document.CreateRange();
        range.StartWith(
            firstSource.SourceNode,
            first.Start - firstSource.Start);
        range.EndWith(
            lastSource.SourceNode,
            last.Start + last.Length - lastSource.Start);
        var trackedSpans = spans
            .Zip(segments)
            .Select(item => new TrackedSourceSpan(
                item.First.Start - item.Second.Source.Start,
                item.First.Length,
                item.Second.Source.SourceNode,
                item.Second.Source.SourceNode.Data))
            .ToArray();

        return new QualityBenchmarkOccurrenceTracker(
            id,
            original,
            expected,
            spans.Count,
            range,
            trackedSpans);
    }

    private static string ReadCurrentSpan(TrackedSourceSpan span)
    {
        var data = span.SourceNode.Data;
        var start = MapOffset(span.OriginalText, data, span.Start);
        var sourceText = span.OriginalText.Substring(
            span.Start,
            Math.Min(span.Length, span.OriginalText.Length - span.Start));
        var correctedText = RemoveSingleHyphen(sourceText);

        if (correctedText is not null
            && StartsWithAt(data, start, correctedText))
        {
            return correctedText;
        }

        if (StartsWithAt(data, start, sourceText))
        {
            return sourceText;
        }

        var end = MapOffset(span.OriginalText, data, span.Start + span.Length);
        if (end < start)
        {
            return string.Empty;
        }

        start = Math.Clamp(start, 0, data.Length);
        end = Math.Clamp(end, start, data.Length);
        return data[start..end];
    }

    private static string? RemoveSingleHyphen(string text)
    {
        var index = text.IndexOf('-');
        return index >= 0 && index == text.LastIndexOf('-')
            ? text.Remove(index, 1)
            : null;
    }

    private static bool StartsWithAt(string text, int start, string value)
    {
        return start >= 0
            && start <= text.Length
            && text.AsSpan(start).StartsWith(value.AsSpan(), StringComparison.Ordinal);
    }

    private static int MapOffset(string before, string after, int offset)
    {
        var beforeIndex = 0;
        var afterIndex = 0;
        offset = Math.Clamp(offset, 0, before.Length);

        while (beforeIndex < offset)
        {
            if (afterIndex < after.Length && before[beforeIndex] == after[afterIndex])
            {
                beforeIndex++;
                afterIndex++;
            }
            else if (beforeIndex + 1 < offset
                && afterIndex < after.Length
                && before[beforeIndex + 1] == after[afterIndex])
            {
                beforeIndex++;
            }
            else if (afterIndex + 1 < after.Length
                && before[beforeIndex] == after[afterIndex + 1])
            {
                afterIndex++;
            }
            else
            {
                beforeIndex++;
                if (afterIndex < after.Length)
                {
                    afterIndex++;
                }
            }
        }

        return afterIndex;
    }

    private sealed record TrackedSourceSpan(
        int Start,
        int Length,
        IText SourceNode,
        string OriginalText);
}
