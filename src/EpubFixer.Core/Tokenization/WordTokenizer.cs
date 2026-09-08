using System.Buffers;
using System.Text;
using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Tokenization.Models;

namespace EpubFixer.Core.Tokenization;

public sealed class WordTokenizer
{
    public IReadOnlyList<WordToken> Tokenize(LogicalTextStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var tokens = new List<WordToken>();
        TokenAccumulator? currentToken = null;

        for (var segmentIndex = 0; segmentIndex < stream.Segments.Count; segmentIndex++)
        {
            var segment = stream.Segments[segmentIndex];
            var index = 0;

            while (index < segment.Text.Length)
            {
                if (TryDecodeRune(segment.Text, index, out var rune, out var runeLength)
                    && Rune.IsLetter(rune))
                {
                    currentToken ??= new TokenAccumulator(segment.LogicalStart + index);
                    currentToken.Append(segment, index, runeLength);
                    index += runeLength;
                    continue;
                }

                if (currentToken is not null
                    && IsApostrophe(segment.Text[index])
                    && IsFollowedByLetter(stream, segmentIndex, index + 1))
                {
                    currentToken.Append(segment, index, 1);
                    index++;
                    continue;
                }

                CompleteToken(stream, tokens, ref currentToken);
                index += runeLength;
            }

            var boundary = GetBoundaryAfter(stream, segmentIndex);

            if (boundary is null || boundary.Kind is not TextBoundaryKind.TextNode)
            {
                CompleteToken(stream, tokens, ref currentToken);
            }
        }

        return Array.AsReadOnly(tokens.ToArray());
    }

    private static void CompleteToken(
        LogicalTextStream stream,
        ICollection<WordToken> tokens,
        ref TokenAccumulator? currentToken)
    {
        if (currentToken is null)
        {
            return;
        }

        tokens.Add(currentToken.CreateToken(stream));
        currentToken = null;
    }

    private static bool IsFollowedByLetter(
        LogicalTextStream stream,
        int segmentIndex,
        int index)
    {
        var segment = stream.Segments[segmentIndex];

        if (index < segment.Text.Length)
        {
            return TryDecodeRune(segment.Text, index, out var rune, out _)
                && Rune.IsLetter(rune);
        }

        var boundary = GetBoundaryAfter(stream, segmentIndex);

        if (boundary?.Kind is not TextBoundaryKind.TextNode)
        {
            return false;
        }

        var nextSegment = stream.Segments[boundary.AfterSegmentIndex];
        return TryDecodeRune(nextSegment.Text, 0, out var nextRune, out _)
            && Rune.IsLetter(nextRune);
    }

    private static TextBoundary? GetBoundaryAfter(LogicalTextStream stream, int segmentIndex)
    {
        if (segmentIndex >= stream.Segments.Count - 1)
        {
            return null;
        }

        if (segmentIndex >= stream.Boundaries.Count)
        {
            throw new InvalidOperationException("The logical text stream is missing a segment boundary.");
        }

        var boundary = stream.Boundaries[segmentIndex];

        if (boundary.BeforeSegmentIndex != segmentIndex
            || boundary.AfterSegmentIndex != segmentIndex + 1)
        {
            throw new InvalidOperationException("The logical text stream contains a non-adjacent segment boundary.");
        }

        return boundary;
    }

    private static bool TryDecodeRune(
        string text,
        int start,
        out Rune rune,
        out int runeLength)
    {
        if (start >= text.Length)
        {
            rune = default;
            runeLength = 1;
            return false;
        }

        var status = Rune.DecodeFromUtf16(text.AsSpan(start), out rune, out runeLength);

        if (status == OperationStatus.Done)
        {
            return true;
        }

        runeLength = 1;
        return false;
    }

    private static bool IsApostrophe(char character)
    {
        return character is '\'' or '’';
    }

    private sealed class TokenAccumulator(int logicalStart)
    {
        private readonly List<TextSourceLocation> _sources = new();

        public int LogicalStart { get; } = logicalStart;

        public int LogicalEnd { get; private set; } = logicalStart;

        public void Append(TextSegment segment, int start, int length)
        {
            var sourceStart = segment.Source.Start + start;

            if (_sources.Count > 0
                && IsContiguousSource(_sources[^1], segment.Source, sourceStart))
            {
                var previous = _sources[^1];
                _sources[^1] = previous with { Length = previous.Length + length };
            }
            else
            {
                _sources.Add(segment.Source with
                {
                    Start = sourceStart,
                    Length = length
                });
            }

            LogicalEnd = segment.LogicalStart + start + length;
        }

        public WordToken CreateToken(LogicalTextStream stream)
        {
            var length = LogicalEnd - LogicalStart;

            return new WordToken(
                stream.Text.Substring(LogicalStart, length),
                LogicalStart,
                Array.AsReadOnly(_sources.ToArray()));
        }

        private static bool IsContiguousSource(
            TextSourceLocation previous,
            TextSourceLocation current,
            int currentStart)
        {
            return ReferenceEquals(previous.SourceNode, current.SourceNode)
                && previous.DocumentPath == current.DocumentPath
                && previous.TextNodeIndex == current.TextNodeIndex
                && previous.Start + previous.Length == currentStart;
        }
    }
}
