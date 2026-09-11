using System.Text.RegularExpressions;
using EpubFixer.Core.Ocr;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Cli.OcrReconstruction;

internal sealed class BookContextIndex : IOcrBookContextLookup
{
    private readonly Dictionary<string, List<Occurrence>> occurrences = new(StringComparer.Ordinal);
    private int lookups;
    public int LookupCount => lookups;

    private BookContextIndex() { }

    public static BookContextIndex Build(string text, IReadOnlyList<CorruptedTextRegion> regions)
    {
        var index = new BookContextIndex();
        var matches = Regex.Matches(text, "[^\\s\\p{P}]+", RegexOptions.CultureInvariant);
        var words = matches.Select(m => m.Value).ToArray();
        foreach (Match match in matches)
        {
            if (regions.Any(r => r.Start < match.Index + match.Length && r.EndExclusive > match.Index)) continue;
            var wordIndex = matches.Cast<Match>().TakeWhile(x => x.Index < match.Index).Count();
            var before = words.Skip(Math.Max(0, wordIndex - 4)).Take(Math.Min(4, wordIndex)).ToArray();
            var after = words.Skip(wordIndex + 1).Take(4).ToArray();
            var normalized = match.Value.Normalize().ToLower(new System.Globalization.CultureInfo("tr-TR"));
            for (var length = 2; length <= normalized.Length; length++)
            {
                var key = normalized[..length];
                if (!index.occurrences.TryGetValue(key, out var list)) index.occurrences[key] = list = new();
                list.Add(new(length != normalized.Length, before, after));
            }
        }
        return index;
    }

    public OcrBookContextMatch? FindBest(string candidate, IReadOnlyList<string> previous, IReadOnlyList<string> next)
    {
        lookups++;
        var key = candidate.Trim().Normalize().ToLower(new System.Globalization.CultureInfo("tr-TR"));
        if (key.Length < 2 || !occurrences.TryGetValue(key, out var items)) return null;
        OcrBookContextMatch? best = null;
        foreach (var item in items)
        {
            var left = Overlap(previous, item.Before);
            var right = Overlap(next, item.After);
            var ordered = OrderedPair(next, item.After);
            var both = left > 0 && right > 0;
            var current = new OcrBookContextMatch(item.PrefixOnly, left, right, ordered, both, items.Count);
            if (best is null || Value(current) > Value(best)) best = current;
        }
        return best;
    }

    private static int Overlap(IReadOnlyList<string> expected, IReadOnlyList<string> actual) =>
        expected.Count(word => actual.Contains(word, StringComparer.OrdinalIgnoreCase));

    private static bool OrderedPair(IReadOnlyList<string> expected, IReadOnlyList<string> actual)
    {
        for (var i = 0; i + 1 < expected.Count; i++)
            for (var j = 0; j + 1 < actual.Count; j++)
                if (string.Equals(expected[i], actual[j], StringComparison.OrdinalIgnoreCase)
                    && string.Equals(expected[i + 1], actual[j + 1], StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static double Value(OcrBookContextMatch x) => x.LeftMatches + x.RightMatches + (x.OrderedPair ? 2 : 0) + (x.BothSides ? 2 : 0);
    private sealed record Occurrence(bool PrefixOnly, IReadOnlyList<string> Before, IReadOnlyList<string> After);
}
