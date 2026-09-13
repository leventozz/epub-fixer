using AngleSharp.Dom;
using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Mutation.Models;
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

    private IReadOnlyList<TrackedSourceSpan> Spans { get; set; }

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

    public void Resync(
        IReadOnlyDictionary<IText, string> beforeOcrText,
        IReadOnlyDictionary<(string DocumentPath, int TextNodeIndex), IText> mutationNodesByLocation,
        IReadOnlyList<OcrCorrectionMutation> mutations)
    {
        ArgumentNullException.ThrowIfNull(beforeOcrText);
        ArgumentNullException.ThrowIfNull(mutationNodesByLocation);
        ArgumentNullException.ThrowIfNull(mutations);

        Spans = Spans
            .Select(span => ResyncSpan(span, beforeOcrText, mutationNodesByLocation, mutations))
            .ToArray();
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
                item.First.DocumentPath,
                item.First.TextNodeIndex,
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
        return ResolveSpan(span, span.SourceNode.Data).Text;
    }

    private static (int Start, string Text) ResolveSpan(TrackedSourceSpan span, string data)
    {
        var start = MapOffset(span.OriginalText, data, span.Start);
        var sourceText = span.OriginalText.Substring(
            span.Start,
            Math.Min(span.Length, span.OriginalText.Length - span.Start));
        var correctedText = RemoveSingleHyphen(sourceText);

        if (correctedText is not null
            && StartsWithAt(data, start, correctedText))
        {
            return (start, correctedText);
        }

        if (StartsWithAt(data, start, sourceText))
        {
            return (start, sourceText);
        }

        var end = MapOffset(span.OriginalText, data, span.Start + span.Length);
        if (end < start)
        {
            return (start, string.Empty);
        }

        start = Math.Clamp(start, 0, data.Length);
        end = Math.Clamp(end, start, data.Length);
        return (start, data[start..end]);
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

    private static TrackedSourceSpan ResyncSpan(
        TrackedSourceSpan tracked,
        IReadOnlyDictionary<IText, string> beforeOcrText,
        IReadOnlyDictionary<(string DocumentPath, int TextNodeIndex), IText> mutationNodesByLocation,
        IReadOnlyList<OcrCorrectionMutation> mutations)
    {
        // tracked.Start/OriginalText were captured before the hyphenation stages ran.
        // Resolve against the node's text as it stood right before the OCR stage first -
        // that reuses the existing hyphen-tolerant MapOffset/RemoveSingleHyphen matching
        // (unchanged) to land on the actual post-hyphenation start and length. Only from
        // that resolved position can the OCR mutation's exact geometry be applied - the
        // node's post-hyphenation text is the same coordinate space the OCR mutation spans
        // (built from that stage's LogicalTextStream) use.
        var beforeText = beforeOcrText.TryGetValue(tracked.SourceNode, out var value)
            ? value
            : tracked.OriginalText;
        var resolved = ResolveSpan(tracked, beforeText);
        var resolvedStart = resolved.Start;
        var resolvedLength = resolved.Text.Length;
        var resolvedEnd = resolvedStart + resolvedLength;

        var delta = 0;
        var intersects = false;

        foreach (var mutation in mutations)
        {
            for (var index = 0; index < mutation.SourceSpans.Count; index++)
            {
                var source = mutation.SourceSpans[index];

                // A mutation's TextNodeIndex is positional in the LogicalTextStream
                // it was planned against (built after hyphenation ran, which can
                // remove/merge text nodes). The tracker's TextNodeIndex is positional
                // in the pristine, pre-hyphenation stream. The same index number can
                // therefore refer to two different physical nodes - resolve the
                // mutation span to its actual node object and compare by reference.
                if (!mutationNodesByLocation.TryGetValue(
                        (source.DocumentPath, source.TextNodeIndex),
                        out var sourceNode)
                    || !ReferenceEquals(sourceNode, tracked.SourceNode))
                {
                    continue;
                }

                var sourceEnd = source.Start + source.Length;
                if (source.Start < resolvedEnd && resolvedStart < sourceEnd)
                {
                    intersects = true;
                    continue;
                }

                if (sourceEnd <= resolvedStart)
                {
                    var replacementLength = index == 0 ? mutation.ReplacementText.Length : 0;
                    delta += replacementLength - source.Length;
                }
            }
        }

        if (!intersects && delta == 0)
        {
            // No OCR mutation touched this node at or before the tracked span: the
            // node's current data is identical to its pre-OCR data here, so the
            // existing (untouched) MapOffset/RemoveSingleHyphen read keeps working.
            return tracked;
        }

        // A mutation intersecting the span itself never contributes to delta (see the
        // loop above) - its content genuinely changed and is read as-is below. Any
        // other, strictly preceding mutation in the same node still shifts position.
        return tracked with
        {
            Start = resolvedStart + delta,
            Length = resolvedLength,
            OriginalText = tracked.SourceNode.Data
        };
    }

    private sealed record TrackedSourceSpan(
        string DocumentPath,
        int TextNodeIndex,
        int Start,
        int Length,
        IText SourceNode,
        string OriginalText);
}
