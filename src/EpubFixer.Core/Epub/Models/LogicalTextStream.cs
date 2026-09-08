using System.Text;

namespace EpubFixer.Core.Epub.Models;

public sealed class LogicalTextStream
{
    internal LogicalTextStream(
        IReadOnlyList<TextSegment> segments,
        IReadOnlyList<TextBoundary> boundaries,
        string text)
    {
        Segments = segments;
        Boundaries = boundaries;
        Text = text;
    }

    public IReadOnlyList<TextSegment> Segments { get; }

    public IReadOnlyList<TextBoundary> Boundaries { get; }

    public string Text { get; }

    public int CharacterCount => Text.Length;

    public TextSegment GetSegmentAt(int logicalCharacterIndex)
    {
        if ((uint)logicalCharacterIndex >= (uint)CharacterCount)
        {
            throw new ArgumentOutOfRangeException(nameof(logicalCharacterIndex));
        }

        var lower = 0;
        var upper = Segments.Count - 1;

        while (lower <= upper)
        {
            var middle = lower + ((upper - lower) / 2);
            var segment = Segments[middle];

            if (logicalCharacterIndex < segment.LogicalStart)
            {
                upper = middle - 1;
            }
            else if (logicalCharacterIndex >= segment.LogicalStart + segment.Length)
            {
                lower = middle + 1;
            }
            else
            {
                return segment;
            }
        }

        throw new InvalidOperationException("The logical text stream contains an unmapped character.");
    }

    public TextSourceLocation GetSourceLocationAt(int logicalCharacterIndex)
    {
        var segment = GetSegmentAt(logicalCharacterIndex);
        var offsetInSegment = logicalCharacterIndex - segment.LogicalStart;

        return segment.Source with
        {
            Start = segment.Source.Start + offsetInSegment,
            Length = 1
        };
    }

    public string CreateDebugText()
    {
        if (Segments.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(Text.Length + Boundaries.Count);
        builder.Append(Segments[0].Text);

        for (var index = 1; index < Segments.Count; index++)
        {
            builder.Append(Boundaries[index - 1].Kind switch
            {
                TextBoundaryKind.Paragraph => "\n",
                TextBoundaryKind.Document => "\n\n",
                _ => string.Empty
            });
            builder.Append(Segments[index].Text);
        }

        return builder.ToString();
    }
}
