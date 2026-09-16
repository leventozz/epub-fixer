using EpubFixer.Core.Lexicon.Models;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Ocr;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Cli.OcrReconstruction;

public sealed class CurrentRegionReconstructor(BookLexicon bookLexicon, ITurkishMorphologyAnalyzer analyzer)
    : IOcrRegionReconstructor
{
    public IReadOnlyList<ReconstructionCandidate> Reconstruct(CorruptedTextRegion region, int maxCandidates = 5)
    {
        ArgumentNullException.ThrowIfNull(region);
        if (maxCandidates <= 0) throw new ArgumentOutOfRangeException(nameof(maxCandidates));
        var states = new Dictionary<string, (int Cost, HashSet<string> Reasons)>(StringComparer.Ordinal)
        { [Core(region.RawText)] = (0, []) };
        for (var depth = 0; depth < 3; depth++)
        {
            foreach (var item in states.Values.ToArray())
            {
                foreach (var next in Expand(item, region))
                {
                    if (!states.TryGetValue(next.Text, out var old) || next.Cost < old.Cost)
                        states[next.Text] = (next.Cost, next.Reasons);
                }
            }
        }
        var candidates = new List<(string Text, double Score, int Cost, bool Morph, int Frequency, string Evidence)>();
        foreach (var state in states)
        foreach (var entry in bookLexicon.Entries)
        {
            var distance = Distance(state.Key, entry.Key);
            var limit = state.Key.EnumerateRunes().Count() <= 4 ? 1 : 2;
            if (distance > limit) continue;
            var morph = analyzer.IsValidWord(entry.Key);
            var score = entry.Value + (morph ? 1_000_000 : 0) - distance * 10_000 - state.Value.Cost;
            candidates.Add((entry.Key, score, distance + state.Value.Cost, morph, entry.Value, $"bookFrequency={entry.Value}; morphology={morph}; editDistance={distance}; structuralCost={state.Value.Cost}"));
        }
        return candidates
            .GroupBy(item => item.Text, StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(x => x.Score).First())
            .OrderByDescending(x => x.Score).ThenBy(x => x.Cost).ThenByDescending(x => x.Morph).ThenBy(x => x.Text, StringComparer.Ordinal)
            .Take(maxCandidates)
            .Select((x, i) => new ReconstructionCandidate(x.Text, x.Score, i + 1, ReconstructionSource.Current, [x.Evidence]))
            .ToArray();
    }

    private static string Core(string value) => value.Trim(".,:;!?()[]{}\"“”«»".ToCharArray());

    private static IEnumerable<(string Text, int Cost, HashSet<string> Reasons)> Expand((int Cost, HashSet<string> Reasons) state, CorruptedTextRegion region)
    {
        var text = Core(region.RawText);
        foreach (var value in new[] { text.Replace(" ", "", StringComparison.Ordinal), text.Replace("-", "", StringComparison.Ordinal), text.Replace("\r", "", StringComparison.Ordinal).Replace("\n", "", StringComparison.Ordinal) })
            if (!string.Equals(value, text, StringComparison.Ordinal)) yield return (value, state.Cost + 1, ["structural normalization"]);
        foreach (var (from, replacements) in new[] { ('1', "liıI"), ('0', "oöOÖ"), ('3', "eE"), ('^', "şç"), ('ı', "ilr"), ('i', "ı"), ('l', "ıi") })
            for (var index = 0; index < text.Length; index++) if (text[index] == from)
                foreach (var replacement in replacements) yield return (text[..index] + replacement + text[(index + 1)..], state.Cost + 1, ["glyph substitution"]);
        for (var index = 0; index < text.Length; index++) if (text[index] is ':' or ';' or '<' or '>' or '^' or ',')
            yield return (text.Remove(index, 1), state.Cost + 1, ["garbage removal"]);
    }

    private static int Distance(string a, string b)
    {
        var row = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++) { var next = new int[b.Length + 1]; next[0] = i; for (var j = 1; j <= b.Length; j++) next[j] = Math.Min(Math.Min(next[j - 1] + 1, row[j] + 1), row[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1)); row = next; }
        return row[^1];
    }
}
