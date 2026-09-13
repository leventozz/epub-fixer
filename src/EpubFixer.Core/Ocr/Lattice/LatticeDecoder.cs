using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Ocr.Lattice.Models;

namespace EpubFixer.Core.Ocr.Lattice;

public sealed class LatticeDecoder(
    ILanguageModel languageModel,
    LatticeOptions options,
    string? previousWord = "<s>") : ILatticeDecoder
{
    public IReadOnlyList<DecodedPath> Decode(WordLattice lattice, int kBest = 3)
    {
        ArgumentNullException.ThrowIfNull(lattice);
        if (kBest <= 0) throw new ArgumentOutOfRangeException(nameof(kBest));
        if (lattice.Outcome != LatticeBuildOutcome.Built)
        {
            return Array.Empty<DecodedPath>();
        }

        var byStart = lattice.Arcs
            .Where(arc => arc.From < arc.To)
            .GroupBy(arc => arc.From)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(arc => arc.To).ThenBy(arc => arc.Kind).ThenBy(arc => arc.Word, StringComparer.Ordinal).ToArray());
        var rankLimit = Math.Max(1, kBest);
        var nextId = 0;
        var states = new List<PathState>[lattice.Window.Length + 1];
        states[0] = [new PathState(0, previousWord, null, nextId++)];

        for (var index = 0; index < states.Length; index++)
        {
            var current = states[index];
            if (current is null || !byStart.TryGetValue(index, out var arcs))
            {
                continue;
            }

            current = Deduplicate(current, rankLimit);
            states[index] = current;
            foreach (var state in current)
            {
                foreach (var arc in arcs)
                {
                    var next = Append(state, arc, nextId++);
                    states[arc.To] ??= [];
                    states[arc.To].Add(next);
                }
            }
        }

        return Deduplicate(states[^1] ?? [], rankLimit)
            .Select(BuildPath)
            .GroupBy(path => path.Text, StringComparer.Ordinal)
            .Select(group => group.OrderBy(path => path.Cost).ThenBy(path => path.Text, StringComparer.Ordinal).First())
            .OrderBy(state => state.Cost)
            .ThenBy(state => state.Text, StringComparer.Ordinal)
            .Take(kBest)
            .ToArray();
    }

    private PathState Append(PathState state, LatticeArc arc, int stateId)
    {
        var lmCost = 0.0;
        var previous = state.PreviousWord;
        if (arc.Kind is LatticeArcKind.Word or LatticeArcKind.Identity)
        {
            lmCost = options.Lambda * -languageModel.LogProbability(arc.Word, previous);
            previous = arc.Word;
        }

        return new PathState(
            state.Cost + arc.Cost + lmCost,
            previous,
            new Backpointer(state, arc),
            stateId);
    }

    private static DecodedPath BuildPath(PathState state)
    {
        var arcs = new List<LatticeArc>();
        for (var cursor = state.Backpointer; cursor is not null; cursor = cursor.Previous.Backpointer)
        {
            arcs.Add(cursor.Arc);
        }

        arcs.Reverse();
        return new DecodedPath(string.Concat(arcs.Select(arc => arc.Word)), state.Cost, arcs);
    }

    private static List<PathState> Deduplicate(IEnumerable<PathState> states, int rankLimit) =>
        states
            .GroupBy(state => state.PreviousWord, StringComparer.Ordinal)
            .SelectMany(group => group
                .OrderBy(state => state.Cost)
                .ThenBy(state => state.StateId)
                .Take(rankLimit))
            .OrderBy(state => state.Cost)
            .ThenBy(state => state.StateId)
            .ToList();

    private sealed record PathState(double Cost, string? PreviousWord, Backpointer? Backpointer, int StateId);

    private sealed record Backpointer(PathState Previous, LatticeArc Arc);
}
