using System.Globalization;
using System.Text.RegularExpressions;
using EpubFixer.Core.Ocr;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Cli.OcrReconstruction;

internal sealed class BookContextIndex : IOcrBookContextLookup
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    private readonly IReadOnlyDictionary<string, IReadOnlyList<Occurrence>> byWord;
    private int lookups;
    public int LookupCount => lookups;
    public IReadOnlyList<IndexedToken> Tokens { get; }
    private BookContextIndex(IReadOnlyList<IndexedToken> tokens, Dictionary<string, IReadOnlyList<Occurrence>> words)
    { Tokens = tokens; byWord = words; }

    public static BookContextIndex Build(string text, IReadOnlyList<CorruptedTextRegion> regions)
    {
        var all = Regex.Matches(text, "[^\\s\\p{P}]+", RegexOptions.CultureInvariant).Cast<Match>().ToArray();
        var tokens = new List<IndexedToken>();
        for (var i = 0; i < all.Length; i++)
        {
            var m = all[i];
            if (regions.Any(r => r.Start < m.Index + m.Length && r.EndExclusive > m.Index)) continue;
            // Keep damaged neighboring tokens as context evidence; only the
            // indexed candidate occurrence itself is excluded from the corpus.
            var before = all.Take(i).TakeLast(4).Select(x => Normalize(x.Value)).ToArray();
            var after = all.Skip(i + 1).Take(4).Select(x => Normalize(x.Value)).ToArray();
            tokens.Add(new IndexedToken(m.Value, Normalize(m.Value), m.Index, m.Length, text[..m.Index].Count(c => c == '\n'), tokens.Count, before, after));
        }
        var entries = new Dictionary<string, List<Occurrence>>(StringComparer.Ordinal);
        foreach (var token in tokens)
            for (var length = 2; length <= token.Normalized.Length; length++)
            {
                var key = token.Normalized[..length];
                if (!entries.TryGetValue(key, out var list)) entries[key] = list = new();
                list.Add(new Occurrence(token, length == token.Normalized.Length ? tokens.Count(x => x.Normalized == token.Normalized) : 0, length != token.Normalized.Length));
            }
        var words = entries.ToDictionary(x => x.Key, x => (IReadOnlyList<Occurrence>)x.Value.AsReadOnly(), StringComparer.Ordinal);
        return new BookContextIndex(tokens.AsReadOnly(), words);
    }
    private static bool Excluded(Match m, IReadOnlyList<CorruptedTextRegion> rs) => rs.Any(r => r.Start < m.Index + m.Length && r.EndExclusive > m.Index);
    public OcrBookContextMatch? FindBest(string candidate, IReadOnlyList<string> previous, IReadOnlyList<string> next)
    {
        var e = FindEvidence(candidate, previous, next); if (e is null) return null;
        var prefix = byWord.TryGetValue(Normalize(candidate.Trim()), out var occurrences)
            && occurrences.OrderByDescending(x => Support(x, previous, next)).FirstOrDefault()?.PrefixOnly == true;
        return new OcrBookContextMatch(prefix, e.LeftMatches, e.RightMatches, e.BigramMatches > 0, e.LeftMatches > 0 && e.RightMatches > 0, e.Frequency)
        { BigramMatches = e.BigramMatches, TrigramMatches = e.TrigramMatches, PhraseMatches = e.PhraseMatches, ConsensusSupport = e.ConsensusSupport };
    }
    public OcrBookContextEvidence? FindEvidence(string candidate, IReadOnlyList<string> previous, IReadOnlyList<string> next)
    {
        Interlocked.Increment(ref lookups); if (!byWord.TryGetValue(Normalize(candidate.Trim()), out var items)) return null;
        var left = previous.Select(Normalize).ToArray(); var right = next.Select(Normalize).ToArray();
        var scored = items.Select(item => (Item: item, Left: Overlap(left, item.Token.Previous), Right: Overlap(right, item.Token.Next)))
            .Select(x => (x.Item, x.Left, x.Right,
                Bigram: (left.Length > 0 && x.Item.Token.Next.FirstOrDefault() == left[0]) || (right.Length > 0 && x.Item.Token.Previous.LastOrDefault() == right[0]),
                Trigram: (left.Length > 1 && x.Item.Token.Next.Take(2).SequenceEqual(left.Take(2)))
                    || (right.Length > 1 && x.Item.Token.Previous.TakeLast(2).SequenceEqual(right.Take(2)))
                    || (left.Length > 0 && right.Length > 0 && x.Item.Token.Previous.LastOrDefault() == left[0] && x.Item.Token.Next.FirstOrDefault() == right[0])))
            .Select(x => (x.Item, x.Left, x.Right, x.Bigram, x.Trigram, Phrase: x.Left > 0 && x.Right > 0))
            .OrderByDescending(x => Support(x.Item, x.Left, x.Right, x.Bigram, x.Trigram, x.Phrase)).ThenByDescending(x => !x.Item.PrefixOnly)
            .First();
        var coherent = scored.Phrase || scored.Bigram || scored.Trigram || scored.Left > 0 || scored.Right > 0;
        return new OcrBookContextEvidence(scored.Item.Count, scored.Left, scored.Right, scored.Bigram ? 1 : 0, scored.Trigram ? 1 : 0, scored.Phrase ? 1 : 0,
            coherent ? Math.Min(1, (scored.Left + scored.Right + (scored.Bigram ? 1 : 0) + (scored.Trigram ? 1 : 0) + (scored.Phrase ? 1 : 0)) / 10d) : 0);
    }
    private static int Support(Occurrence item, IReadOnlyList<string> previous, IReadOnlyList<string> next)
        => Support(item, Overlap(previous.Select(Normalize).ToArray(), item.Token.Previous), Overlap(next.Select(Normalize).ToArray(), item.Token.Next), false, false, false);
    private static int Support(Occurrence item, int left, int right, bool bigram, bool trigram, bool phrase)
        => left + right + (bigram ? 2 : 0) + (trigram ? 2 : 0) + (phrase ? 3 : 0) + (item.PrefixOnly ? 0 : 1);
    private static int Overlap(IReadOnlyList<string> a, IReadOnlyList<string> b) => a.Count(x => b.Contains(x, StringComparer.Ordinal));
    private static string Normalize(string s) => s.Normalize().ToLower(Turkish);
    private sealed record Occurrence(IndexedToken Token, int Count, bool PrefixOnly);
}
internal sealed record IndexedToken(string Text, string Normalized, int Start, int Length, int Paragraph, int Position, IReadOnlyList<string> Previous, IReadOnlyList<string> Next);
