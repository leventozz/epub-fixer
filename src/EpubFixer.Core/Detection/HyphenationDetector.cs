using System.Buffers;
using System.Text;
using EpubFixer.Core.Detection.Models;
using EpubFixer.Core.Epub.Models;

namespace EpubFixer.Core.Detection;

public sealed class HyphenationDetector
{
    public IReadOnlyList<HyphenationCandidate> Detect(LogicalTextStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var detected = new List<DetectedCandidate>();

        foreach (var segment in stream.Segments)
        {
            DetectInline(segment, detected);
        }

        foreach (var boundary in stream.Boundaries)
        {
            DetectAcrossBoundary(stream, boundary, detected);
        }

        var candidates = detected
            .OrderBy(item => item.LogicalHyphenIndex)
            .Select(item => item.Candidate)
            .ToArray();

        return Array.AsReadOnly(candidates);
    }

    private static void DetectInline(TextSegment segment, ICollection<DetectedCandidate> detected)
    {
        for (var hyphenIndex = 0; hyphenIndex < segment.Text.Length; hyphenIndex++)
        {
            if (segment.Text[hyphenIndex] != '-')
            {
                continue;
            }

            if (!TryFindLetterRunBefore(segment.Text, hyphenIndex, out var leftStart)
                || !TryFindLetterRunAfter(segment.Text, hyphenIndex + 1, out var rightEnd))
            {
                continue;
            }

            detected.Add(new DetectedCandidate(
                CreateCandidate(
                    segment,
                    leftStart,
                    hyphenIndex - leftStart,
                    segment,
                    hyphenIndex,
                    segment,
                    hyphenIndex + 1,
                    rightEnd - hyphenIndex - 1,
                    HyphenationDetectionKind.Inline),
                segment.LogicalStart + hyphenIndex));
        }
    }

    private static void DetectAcrossBoundary(
        LogicalTextStream stream,
        TextBoundary boundary,
        ICollection<DetectedCandidate> detected)
    {
        var leftSegment = stream.Segments[boundary.BeforeSegmentIndex];
        var rightSegment = stream.Segments[boundary.AfterSegmentIndex];
        var hyphenIndex = leftSegment.Text.Length - 1;

        if (hyphenIndex < 0
            || leftSegment.Text[hyphenIndex] != '-'
            || !TryFindLetterRunBefore(leftSegment.Text, hyphenIndex, out var leftStart)
            || !TryFindLetterRunAfter(rightSegment.Text, 0, out var rightEnd))
        {
            return;
        }

        detected.Add(new DetectedCandidate(
            CreateCandidate(
                leftSegment,
                leftStart,
                hyphenIndex - leftStart,
                leftSegment,
                hyphenIndex,
                rightSegment,
                0,
                rightEnd,
                MapDetectionKind(boundary.Kind)),
            leftSegment.LogicalStart + hyphenIndex));
    }

    private static HyphenationCandidate CreateCandidate(
        TextSegment leftSegment,
        int leftStart,
        int leftLength,
        TextSegment hyphenSegment,
        int hyphenStart,
        TextSegment rightSegment,
        int rightStart,
        int rightLength,
        HyphenationDetectionKind detectionKind)
    {
        var leftPart = leftSegment.Text.Substring(leftStart, leftLength);
        var rightPart = rightSegment.Text.Substring(rightStart, rightLength);

        return new HyphenationCandidate(
            leftPart,
            rightPart,
            leftPart + rightPart,
            detectionKind,
            CreateSourceLocation(leftSegment, leftStart, leftLength),
            CreateSourceLocation(hyphenSegment, hyphenStart, 1),
            CreateSourceLocation(rightSegment, rightStart, rightLength));
    }

    private static TextSourceLocation CreateSourceLocation(TextSegment segment, int start, int length)
    {
        return segment.Source with
        {
            Start = segment.Source.Start + start,
            Length = length
        };
    }

    private static bool TryFindLetterRunBefore(string text, int endExclusive, out int start)
    {
        start = endExclusive;

        if (!TryDecodePreviousLetter(text, start, out var runeLength))
        {
            return false;
        }

        start -= runeLength;

        while (TryDecodePreviousLetter(text, start, out runeLength))
        {
            start -= runeLength;
        }

        return true;
    }

    private static bool TryFindLetterRunAfter(string text, int start, out int endExclusive)
    {
        endExclusive = start;

        if (!TryDecodeNextLetter(text, endExclusive, out var runeLength))
        {
            return false;
        }

        endExclusive += runeLength;

        while (TryDecodeNextLetter(text, endExclusive, out runeLength))
        {
            endExclusive += runeLength;
        }

        return true;
    }

    private static bool TryDecodePreviousLetter(string text, int endExclusive, out int runeLength)
    {
        runeLength = 0;

        if (endExclusive <= 0)
        {
            return false;
        }

        var status = Rune.DecodeLastFromUtf16(text.AsSpan(0, endExclusive), out var rune, out runeLength);
        return status == OperationStatus.Done && Rune.IsLetter(rune);
    }

    private static bool TryDecodeNextLetter(string text, int start, out int runeLength)
    {
        runeLength = 0;

        if (start >= text.Length)
        {
            return false;
        }

        var status = Rune.DecodeFromUtf16(text.AsSpan(start), out var rune, out runeLength);
        return status == OperationStatus.Done && Rune.IsLetter(rune);
    }

    private static HyphenationDetectionKind MapDetectionKind(TextBoundaryKind boundaryKind)
    {
        return boundaryKind switch
        {
            TextBoundaryKind.TextNode => HyphenationDetectionKind.TextNodeBoundary,
            TextBoundaryKind.Paragraph => HyphenationDetectionKind.ParagraphBoundary,
            TextBoundaryKind.Document => HyphenationDetectionKind.DocumentBoundary,
            _ => throw new ArgumentOutOfRangeException(nameof(boundaryKind), boundaryKind, null)
        };
    }

    private sealed record DetectedCandidate(
        HyphenationCandidate Candidate,
        int LogicalHyphenIndex);
}
