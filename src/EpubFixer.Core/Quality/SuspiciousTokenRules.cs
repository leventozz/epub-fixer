using System.Text;
using EpubFixer.Core.Epub.Models;

namespace EpubFixer.Core.Quality;

public static class SuspiciousTokenRules
{
    private static readonly HashSet<char> GarbageGlyphs = ['<', '>', '^', '~', '|', '\\', '_', '§'];
    private static readonly HashSet<char> InnerPunctuation = ['.', ',', ':', ';', '\'', '’', '-', '^'];
    private static readonly HashSet<string> AmbiguousSingles = new(StringComparer.Ordinal)
    {
        "ı", "i", "l", "I", "İ", "1"
    };

    public static int CountSuspiciousRawWords(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var words = ExtractRawWords(text);
        var count = 0;
        for (var index = 0; index < words.Count; index++)
        {
            if (IsSuspicious(words, index))
            {
                count++;
            }
        }

        return count;
    }

    public static int CountSuspiciousRawWords(LogicalTextStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var words = ExtractRawWords(stream);
        var count = 0;

        for (var index = 0; index < words.Count; index++)
        {
            if (IsSuspicious(
                words[index],
                index == 0 ? null : words[index - 1],
                index == words.Count - 1 ? null : words[index + 1]))
            {
                count++;
            }
        }

        return count;
    }

    public static bool IsSuspicious(string rawWord, string? previousRawWord = null, string? nextRawWord = null)
    {
        ArgumentNullException.ThrowIfNull(rawWord);
        var words = new List<string>();
        var index = 0;
        if (previousRawWord is not null)
        {
            words.Add(previousRawWord);
            index++;
        }

        words.Add(rawWord);
        if (nextRawWord is not null)
        {
            words.Add(nextRawWord);
        }

        return IsSuspicious(words, index);
    }

    private static bool IsSuspicious(IReadOnlyList<string> words, int index)
    {
        var word = words[index];
        if (!word.EnumerateRunes().Any(Rune.IsLetter))
        {
            return false;
        }

        if (word.Any(char.IsLetter) && word.Any(char.IsDigit))
        {
            return true;
        }

        if (word.Any(GarbageGlyphs.Contains))
        {
            return true;
        }

        for (var i = 0; i < word.Length - 1; i++)
        {
            if (InnerPunctuation.Contains(word[i]) && InnerPunctuation.Contains(word[i + 1]))
            {
                return true;
            }
        }

        if (AmbiguousSingles.Contains(word)
            && (IsSingleCharacter(words, index - 1) || IsSingleCharacter(words, index + 1)))
        {
            return true;
        }

        return false;
    }

    private static bool IsSingleCharacter(IReadOnlyList<string> words, int index) =>
        index >= 0 && index < words.Count && words[index].EnumerateRunes().Count() == 1;

    private static IReadOnlyList<string> ExtractRawWords(string text)
    {
        var words = new List<string>();
        var start = -1;
        for (var index = 0; index < text.Length; index++)
        {
            if (!char.IsWhiteSpace(text[index]))
            {
                if (start < 0)
                {
                    start = index;
                }

                continue;
            }

            if (start >= 0)
            {
                words.Add(text[start..index]);
                start = -1;
            }
        }

        if (start >= 0)
        {
            words.Add(text[start..]);
        }

        return words;
    }

    private static IReadOnlyList<string> ExtractRawWords(LogicalTextStream stream)
    {
        var words = new List<string>();
        var current = new StringBuilder();

        for (var segmentIndex = 0; segmentIndex < stream.Segments.Count; segmentIndex++)
        {
            AppendRawWords(stream.Segments[segmentIndex].Text, words, current);

            if (segmentIndex < stream.Boundaries.Count
                && stream.Boundaries[segmentIndex].Kind is not TextBoundaryKind.TextNode)
            {
                CompleteCurrent(words, current);
            }
        }

        CompleteCurrent(words, current);
        return words;
    }

    private static void AppendRawWords(string text, ICollection<string> words, StringBuilder current)
    {
        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                CompleteCurrent(words, current);
            }
            else
            {
                current.Append(character);
            }
        }
    }

    private static void CompleteCurrent(ICollection<string> words, StringBuilder current)
    {
        if (current.Length == 0)
        {
            return;
        }

        words.Add(current.ToString());
        current.Clear();
    }
}
