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
            var before = all.Take(i).Where(x => !Excluded(x, regions)).TakeLast(2).Select(x => Normalize(x.Value)).ToArray();
            var after = all.Skip(i + 1).Where(x => !Excluded(x, regions)).Take(2).Select(x => Normalize(x.Value)).ToArray();
            tokens.Add(new IndexedToken(m.Value, Normalize(m.Value), m.Index, m.Length, text[..m.Index].Count(c => c == '\n'), tokens.Count, before, after));
        }
        var entries = new Dictionary<string, List<Occurrence>>(StringComparer.Ordinal);
        foreach (var token in tokens)
            for (var length = 2; length <= token.Normalized.Length; length++)
            {
                var key = token.Normalized[..length];
                if (!entries.TryGetValue(key, out var list)) entries[key] = list = new();
                list.Add(new Occurrence(token, tokens.Count(x => x.Normalized == token.Normalized), length != token.Normalized.Length));
            }
        var words = entries.ToDictionary(x => x.Key, x => (IReadOnlyList<Occurrence>)x.Value.AsReadOnly(), StringComparer.Ordinal);
        return new BookContextIndex(tokens.AsReadOnly(), words);
    }
    private static bool Excluded(Match m, IReadOnlyList<CorruptedTextRegion> rs) => rs.Any(r => r.Start < m.Index + m.Length && r.EndExclusive > m.Index);
    public OcrBookContextMatch? FindBest(string candidate, IReadOnlyList<string> previous, IReadOnlyList<string> next)
    {
        var e = FindEvidence(candidate, previous, next); if (e is null) return null;
        var prefix = byWord.TryGetValue(Normalize(candidate.Trim()), out var occurrences) && occurrences.Any(x => x.PrefixOnly);
        return new OcrBookContextMatch(prefix, e.LeftMatches, e.RightMatches, e.BigramMatches > 0, e.LeftMatches > 0 && e.RightMatches > 0, e.Frequency)
        { BigramMatches = e.BigramMatches, TrigramMatches = e.TrigramMatches, PhraseMatches = e.PhraseMatches, ConsensusSupport = e.ConsensusSupport };
    }
    public OcrBookContextEvidence? FindEvidence(string candidate, IReadOnlyList<string> previous, IReadOnlyList<string> next)
    {
        Interlocked.Increment(ref lookups); if (!byWord.TryGetValue(Normalize(candidate.Trim()), out var items)) return null;
        var left = previous.Select(Normalize).ToArray(); var right = next.Select(Normalize).ToArray();
        var lm = items.Max(x => Overlap(left, x.Token.Previous)); var rm = items.Max(x => Overlap(right, x.Token.Next));
        var bi = items.Count(x => (left.Length > 0 && x.Token.Next.FirstOrDefault() == left[0]) || (right.Length > 0 && x.Token.Previous.LastOrDefault() == right[0]));
        var tri = items.Count(x => (left.Length > 1 && x.Token.Next.Take(2).SequenceEqual(left.Take(2)))
            || (right.Length > 1 && x.Token.Previous.TakeLast(2).SequenceEqual(right.Take(2)))
            || (left.Length > 0 && right.Length > 0 && x.Token.Previous.LastOrDefault() == left[0] && x.Token.Next.FirstOrDefault() == right[0]));
        var phrase = items.Count(x => lm > 0 && rm > 0);
        return new OcrBookContextEvidence(items.Count, lm, rm, bi, tri, phrase, Math.Min(1, (lm + rm + bi + tri + phrase) / 10d));
    }
    private static int Overlap(IReadOnlyList<string> a, IReadOnlyList<string> b) => a.Count(x => b.Contains(x, StringComparer.Ordinal));
    private static string Normalize(string s) => s.Normalize().ToLower(Turkish);
    private sealed record Occurrence(IndexedToken Token, int Count, bool PrefixOnly);
}
internal sealed record IndexedToken(string Text, string Normalized, int Start, int Length, int Paragraph, int Position, IReadOnlyList<string> Previous, IReadOnlyList<string> Next);
