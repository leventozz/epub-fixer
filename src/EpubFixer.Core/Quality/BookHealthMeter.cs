using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Tokenization;

namespace EpubFixer.Core.Quality;

// Definition version 1: WordTokenizer total tokens; NFC + tr-TR lowercase normalization;
// unresolved when neither the full token nor an apostrophe stem is recognized; suspicious
// raw words are counted with SuspiciousTokenRules.
public sealed class BookHealthMeter(IWordRecognizer recognizer, int worstExampleCount = 20) : IBookHealthMeter
{
    public BookHealth Measure(LogicalTextStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (worstExampleCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(worstExampleCount));
        }

        var tokens = new WordTokenizer().Tokenize(stream);
        var unresolved = new List<string>();

        foreach (var token in tokens)
        {
            if (!IsResolvable(token.Text))
            {
                unresolved.Add(token.Text);
            }
        }

        var worst = unresolved
            .GroupBy(token => token, StringComparer.Ordinal)
            .Select(group => new { Token = group.Key, Count = group.Count() })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Token, StringComparer.Ordinal)
            .Take(worstExampleCount)
            .Select(item => item.Token)
            .ToArray();

        var total = tokens.Count;
        return new BookHealth(
            total,
            unresolved.Count,
            SuspiciousTokenRules.CountSuspiciousRawWords(stream),
            total == 0 ? 0 : unresolved.Count * 1000.0 / total,
            worst);
    }

    private bool IsResolvable(string token)
    {
        var normalized = TurkishWordNormalizer.Normalize(token);
        if (recognizer.IsRecognized(normalized))
        {
            return true;
        }

        var apostrophe = normalized.IndexOfAny(['\'', '’']);
        return apostrophe > 0 && recognizer.IsRecognized(normalized[..apostrophe]);
    }
}
