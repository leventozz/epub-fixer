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

    internal NoisyChannelDiagnosticTrace Diagnose(CorruptedTextRegion region, string expected)
    {
        var raw = region.RawText.Normalize();
        var expectedNormalized = expected.Normalize();
        var beam = new Dictionary<string, State>(StringComparer.Ordinal) { [raw] = new(raw, 0, 0, []) };
        var depthRows = new List<DiagnosticDepthRow>();
        var allStates = new Dictionary<string, State>(StringComparer.Ordinal) { [raw] = beam[raw] };
        var generated = false;
        var pruned = false;
        for (var depth = 0; depth < 4; depth++)
        {
            var expanded = new Dictionary<string, State>(StringComparer.Ordinal);
            foreach (var pair in beam)
                foreach (var next in Expand(pair.Key, pair.Value))
                    if (!expanded.TryGetValue(next.Text, out var old) || next.Cost < old.Cost) expanded[next.Text] = next;
            generated |= expanded.ContainsKey(expectedNormalized);
            foreach (var item in expanded)
                if (!allStates.TryGetValue(item.Key, out var old) || item.Value.Cost < old.Cost) allStates[item.Key] = item.Value;
            var retained = expanded.OrderBy(x => x.Value.Cost).ThenBy(x => x.Key, StringComparer.Ordinal).Take(256)
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
            pruned |= expanded.ContainsKey(expectedNormalized) && !retained.ContainsKey(expectedNormalized);
            depthRows.Add(new(depth + 1, expanded.Count, retained.Count, expanded.Count - retained.Count,
                retained.Count == 0 ? null : retained.Values.Min(x => x.Cost), expanded.ContainsKey(expectedNormalized), retained.ContainsKey(expectedNormalized),
                retained.Values.OrderBy(x => x.Cost).ThenBy(x => x.Text, StringComparer.Ordinal).Take(8).Select(x => x.Text).ToArray()));
            beam = retained;
        }
        var path = FindMinimumPath(raw, expectedNormalized);
        var evaluated = path is not null
            ? Score(expectedNormalized, new State(expectedNormalized, path.Cost, path.Steps.Count, path.Steps.Select(x => x.Operation).ToArray()))
            : Score(expectedNormalized, new State(expectedNormalized, double.NaN, 0, []));
        var final = beam.TryGetValue(expectedNormalized, out var finalState) ? Score(expectedNormalized, finalState) : null;
        var closest = allStates.Values.Select(x => new DiagnosticState(x.Text, x.Cost, x.Depth, Distance(x.Text, expectedNormalized)))
            .OrderBy(x => x.Distance).ThenBy(x => x.Cost).ThenBy(x => x.Text, StringComparer.Ordinal).Take(5).ToArray();
        var reachableWithinDecoder = path is not null && path.Steps.Count <= 4;
        var classification = final is not null ? "GeneratedAndVisible"
            : pruned || reachableWithinDecoder ? "GeneratedThenPruned" : generated ? "GeneratedButFiltered" : "NotGenerated";
        if (final is not null && beam.OrderByDescending(x => Score(x.Key, x.Value)?.Score ?? double.MinValue).Take(5).All(x => x.Key != expectedNormalized)) classification = "GeneratedButRankedBelowTop5";
        var subreason = classification switch
        {
            "GeneratedThenPruned" => "BeamWidthPruned",
            "NotGenerated" when path is null => "MissingEditOperation",
            "NotGenerated" => path is not null ? "EditDepthExceeded" : "MissingEditOperation",
            "GeneratedButRankedBelowTop5" => "ScoreTooLow",
            "GeneratedButFiltered" => evaluated is null ? "LexiconRejected" : "Other",
            _ => "Other"
        };
        return new(raw, expectedNormalized, generated, pruned || reachableWithinDecoder, classification, subreason, depthRows, closest, path?.Steps,
            evaluated is null ? null : new(evaluated.Score, evaluated.Cost, evaluated.Lexical, evaluated.Morphology, bookLexicon.Contains(expectedNormalized), cleanLexicon.GetFrequency(expectedNormalized)),
            final is not null);
    }

    private DiagnosticPathResult? FindMinimumPath(string source, string target)
    {
        var queue = new PriorityQueue<State, double>();
        var best = new Dictionary<string, double>(StringComparer.Ordinal) { [source] = 0 };
        queue.Enqueue(new(source, 0, 0, []), 0);
        while (queue.TryDequeue(out var current, out _))
        {
            if (current.Text == target) return new(current.Evidence.Select((x, i) => new DiagnosticPathStep(i + 1, x)).ToArray(), current.Cost);
            foreach (var next in Expand(current.Text, current))
            {
                if (next.Text == current.Text || best.TryGetValue(next.Text, out var old) && old <= next.Cost) continue;
                best[next.Text] = next.Cost;
                queue.Enqueue(next, next.Cost);
            }
        }
        return null;
    }

    private static int Distance(string a, string b)
    {
        var row = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++) { var prev = row[0]; row[0] = i; for (var j = 1; j <= b.Length; j++) { var old = row[j]; row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), prev + (a[i - 1] == b[j - 1] ? 0 : 1)); prev = old; } }
        return row[^1];
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

internal sealed record NoisyChannelDiagnosticTrace(string Initial, string Expected, bool Generated, bool Pruned, string Classification, string Subreason, IReadOnlyList<DiagnosticDepthRow> Depths, IReadOnlyList<DiagnosticState> ClosestStates, IReadOnlyList<DiagnosticPathStep>? MinimumPath, DiagnosticEvaluation? Evaluation, bool Visible);
internal sealed record DiagnosticDepthRow(int Depth, int Generated, int Retained, int Pruned, double? BestCost, bool ExpectedGenerated, bool ExpectedRetained, IReadOnlyList<string> BestStates);
internal sealed record DiagnosticState(string Text, double Cost, int Depth, int Distance);
internal sealed record DiagnosticPathStep(int Number, string Operation);
internal sealed record DiagnosticPathResult(IReadOnlyList<DiagnosticPathStep> Steps, double Cost);
internal sealed record DiagnosticEvaluation(double Score, double Cost, bool Clean, bool Morphology, bool Book, long CleanFrequency);
