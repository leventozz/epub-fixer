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

        foreach (var segment in stream.Segments)
        {
            TokenizeSegment(segment, tokens);
        }

        return Array.AsReadOnly(tokens.ToArray());
    }

    private static void TokenizeSegment(TextSegment segment, ICollection<WordToken> tokens)
    {
        var index = 0;

        while (index < segment.Text.Length)
        {
            if (!TryDecodeRune(segment.Text, index, out var rune, out var runeLength)
                || !Rune.IsLetter(rune))
            {
                index += runeLength;
                continue;
            }

            var tokenStart = index;
            index += runeLength;

            while (index < segment.Text.Length)
            {
                if (TryDecodeRune(segment.Text, index, out rune, out runeLength)
                    && Rune.IsLetter(rune))
                {
                    index += runeLength;
                    continue;
                }

                if (IsApostrophe(segment.Text[index])
                    && TryDecodeRune(segment.Text, index + 1, out var nextRune, out _)
                    && Rune.IsLetter(nextRune))
                {
                    index++;
                    continue;
                }

                break;
            }

            var tokenLength = index - tokenStart;
            var source = segment.Source with
            {
                Start = segment.Source.Start + tokenStart,
                Length = tokenLength
            };

            tokens.Add(new WordToken(
                segment.Text.Substring(tokenStart, tokenLength),
                segment.LogicalStart + tokenStart,
                source));
        }
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
}
