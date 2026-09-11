using EpubFixer.Core.Lexicon.Models;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Ocr;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Cli.OcrReconstruction;

public sealed class NoisyChannelRegionReconstructor(
    CleanTurkishLexicon cleanLexicon,
    BookLexicon bookLexicon,
    ITurkishMorphologyAnalyzer analyzer,
    OcrEditCostModel? costs = null) : IOcrRegionReconstructor
{
    private readonly OcrEditCostModel costs = costs ?? new();
    public IReadOnlyList<ReconstructionCandidate> Reconstruct(CorruptedTextRegion region, int maxCandidates = 5)
    {
        ArgumentNullException.ThrowIfNull(region);
        if (maxCandidates <= 0) throw new ArgumentOutOfRangeException(nameof(maxCandidates));
        var raw = region.RawText.Normalize();
        var beam = new Dictionary<string, State>(StringComparer.Ordinal) { [raw] = new(raw, 0, 0, []) };
        for (var depth = 0; depth < 4; depth++)
        {
            var expanded = new Dictionary<string, State>(StringComparer.Ordinal);
            foreach (var pair in beam)
                foreach (var next in Expand(pair.Key, pair.Value))
                    if (!expanded.TryGetValue(next.Text, out var old) || next.Cost < old.Cost) expanded[next.Text] = next;
            beam = expanded.OrderBy(x => x.Value.Cost).Take(256).ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        }
        return beam.Select(pair => Score(pair.Key, pair.Value)).Where(x => x is not null).Cast<Scored>()
            .OrderByDescending(x => x.Score).ThenBy(x => x.Cost).ThenByDescending(x => x.Lexical).ThenByDescending(x => x.Morphology).ThenBy(x => x.Text, StringComparer.Ordinal)
            .Take(maxCandidates).Select((x, i) => new ReconstructionCandidate(x.Text, x.Score, i + 1, ReconstructionSource.NoisyChannel, [x.Evidence])).ToArray();
    }

    private IEnumerable<State> Expand(string text, State state)
    {
        yield return new(text, state.Cost, state.Depth + 1, state.Evidence);
        foreach (var (source, replacement) in new[]
        {
            ("i ", "t"), ("ı ", ""), ("1 ", ""), (":,", "s"),
            ("lıi", "bi"), ("-^:", "çe"), ("-ıii", "rü")
        })
        {
            for (var index = text.IndexOf(source, StringComparison.Ordinal); index >= 0; index = text.IndexOf(source, index + 1, StringComparison.Ordinal))
                yield return new(text.Remove(index, source.Length).Insert(index, replacement), state.Cost + costs.ShortFragmentMerge, state.Depth + 1, state.Evidence.Append($"short-fragment merge {source}→{replacement}").ToArray());
        }
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] is '\r' or '\n') yield return new(text.Remove(i, 1), state.Cost + costs.LineBreakDeletion, state.Depth + 1, state.Evidence.Append("line-break deletion").ToArray());
            if (text[i] is ' ' or '\t') yield return new(text.Remove(i, 1), state.Cost + costs.SpaceDeletion, state.Depth + 1, state.Evidence.Append("space deletion").ToArray());
            if (text[i] is '-' or '\u00ad') yield return new(text.Remove(i, 1), state.Cost + costs.HyphenDeletion, state.Depth + 1, state.Evidence.Append("hyphen deletion").ToArray());
            if (text[i] is '^' or ';' or ':' or '<' or '>' or ',') yield return new(text.Remove(i, 1), state.Cost + costs.GarbageDeletion, state.Depth + 1, state.Evidence.Append("garbage deletion").ToArray());
            foreach (var replacement in Replacements(text[i])) yield return new(text[..i] + replacement + text[(i + 1)..], state.Cost + (IsKnown(text[i], replacement) ? costs.KnownGlyphSubstitution : costs.OrdinarySubstitution), state.Depth + 1, state.Evidence.Append($"substitution {text[i]}→{replacement}").ToArray());
        }
    }

    private Scored? Score(string text, State state)
    {
        var normalized = text.Trim();
        if (normalized.Length == 0) return null;
        var lexical = cleanLexicon.Contains(normalized);
        var morphology = analyzer.IsValidWord(normalized);
        var book = bookLexicon.Contains(normalized);
        if (!lexical && !morphology && !book) return null;
        var frequency = Math.Min(0.25, Math.Log10(cleanLexicon.GetFrequency(normalized) + 1) / 24);
        var score = -state.Cost + (lexical ? 1 : 0) + (morphology ? .75 : 0) + (book ? .5 : 0) + frequency;
        return new(normalized, score, state.Cost, lexical, morphology, $"editCost={state.Cost:0.00}; lexical={lexical}; morphology={morphology}; book={book}; frequencyBonus={frequency:0.000}");
    }

    private static IEnumerable<char> Replacements(char c) => c switch { '1' => "liıI", 'l' => "ıi1b", 'ı' => "ilrüöo", 'i' => "ıh", '0' => "oö", '3' => "e", '^' => "şç", 'c' => "e", _ => Array.Empty<char>() };
    private static bool IsKnown(char a, char b) => a is '1' or 'l' or 'ı' or 'i' or '0' or '3' or '^' or 'c';
    private sealed record State(string Text, double Cost, int Depth, IReadOnlyList<string> Evidence);
    private sealed record Scored(string Text, double Score, double Cost, bool Lexical, bool Morphology, string Evidence);
}
