using EpubFixer.Core.Ocr.Lattice.Models;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Core.Ocr.Lattice;

public sealed record LatticeWindow(string Window, int WindowOffset, LatticeBuildOutcome Outcome)
{
    public static LatticeWindow Select(CorruptedTextRegion region, string fullText, LatticeOptions options)
    {
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(fullText);
        ArgumentNullException.ThrowIfNull(options);

        if ((uint)region.Start > (uint)fullText.Length
            || (uint)region.EndExclusive > (uint)fullText.Length
            || region.Start > region.EndExclusive)
        {
            throw new ArgumentOutOfRangeException(nameof(region));
        }

        var leftBoundary = FindLeftHardBoundary(fullText, region.Start);
        var rightBoundary = FindRightHardBoundary(fullText, region.EndExclusive);
        var tokens = EnumerateTokens(fullText, leftBoundary, rightBoundary).ToArray();
        var firstToken = Array.FindLastIndex(tokens, token => token.Start < region.Start);
        var lastToken = Array.FindIndex(tokens, token => token.End > region.EndExclusive);

        var windowStart = region.Start;
        var windowEnd = region.EndExclusive;
        if (tokens.Length > 0)
        {
            var regionFirst = Array.FindIndex(tokens, token => token.End > region.Start);
            var regionLast = Array.FindLastIndex(tokens, token => token.Start < region.EndExclusive);
            if (regionFirst >= 0 && regionLast >= regionFirst)
            {
                firstToken = Math.Max(0, regionFirst - options.ContextTokens);
                lastToken = Math.Min(tokens.Length - 1, regionLast + options.ContextTokens);
                windowStart = tokens[firstToken].Start;
                windowEnd = tokens[lastToken].End;
            }
        }

        windowStart = Math.Max(leftBoundary, windowStart);
        windowEnd = Math.Min(rightBoundary, windowEnd);
        var window = fullText[windowStart..windowEnd];
        return window.Length > options.MaxWindowLength
            ? new LatticeWindow(string.Empty, windowStart, LatticeBuildOutcome.SkippedTooLong)
            : new LatticeWindow(window, windowStart, LatticeBuildOutcome.Built);
    }

    private static int FindLeftHardBoundary(string text, int start)
    {
        for (var i = start - 1; i > 0; i--)
        {
            if (text[i] == '\n' && text[i - 1] == '\n')
            {
                return i + 1;
            }
        }

        return 0;
    }

    private static int FindRightHardBoundary(string text, int end)
    {
        for (var i = end; i + 1 < text.Length; i++)
        {
            if (text[i] == '\n' && text[i + 1] == '\n')
            {
                return i;
            }
        }

        return text.Length;
    }

    private static IEnumerable<TokenSpan> EnumerateTokens(string text, int start, int end)
    {
        var tokenStart = -1;
        for (var i = start; i < end; i++)
        {
            if (IsTokenCharacter(text[i]))
            {
                tokenStart = tokenStart < 0 ? i : tokenStart;
                continue;
            }

            if (tokenStart >= 0)
            {
                yield return new TokenSpan(tokenStart, i);
                tokenStart = -1;
            }
        }

        if (tokenStart >= 0)
        {
            yield return new TokenSpan(tokenStart, end);
        }
    }

    private static bool IsTokenCharacter(char value) =>
        char.IsLetterOrDigit(value) || value is '\'' or '’' or '-' or '\u00ad';

    private readonly record struct TokenSpan(int Start, int End);
}
