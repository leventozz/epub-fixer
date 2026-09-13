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
        var states = new List<PathState>[lattice.Window.Length + 1];
        states[0] = [new PathState(string.Empty, 0, previousWord, [])];

        for (var index = 0; index < states.Length; index++)
        {
            var current = states[index];
            if (current is null || !byStart.TryGetValue(index, out var arcs))
            {
                continue;
            }

            foreach (var state in current)
            {
                foreach (var arc in arcs)
                {
                    var next = Append(state, arc);
                    states[arc.To] ??= [];
                    states[arc.To].Add(next);
                    states[arc.To] = Prune(states[arc.To], Math.Max(kBest, 8));
                }
            }
        }

        return (states[^1] ?? [])
            .GroupBy(state => state.Text, StringComparer.Ordinal)
            .Select(group => group.OrderBy(state => state.Cost).ThenBy(state => state.Text, StringComparer.Ordinal).First())
            .OrderBy(state => state.Cost)
            .ThenBy(state => state.Text, StringComparer.Ordinal)
            .Take(kBest)
            .Select(state => new DecodedPath(state.Text, state.Cost, state.Arcs))
            .ToArray();
    }

    private PathState Append(PathState state, LatticeArc arc)
    {
        var lmCost = 0.0;
        var previous = state.PreviousWord;
        if (arc.Kind is LatticeArcKind.Word or LatticeArcKind.Identity)
        {
            lmCost = options.Lambda * -languageModel.LogProbability(arc.Word, previous);
            previous = arc.Word;
        }

        return new PathState(
            state.Text + arc.Word,
            state.Cost + arc.Cost + lmCost,
            previous,
            state.Arcs.Append(arc).ToArray());
    }

    private static List<PathState> Prune(IEnumerable<PathState> states, int limit) =>
        states
            .GroupBy(state => (state.Text, state.PreviousWord), StateKeyComparer.Instance)
            .Select(group => group.OrderBy(state => state.Cost).ThenBy(state => state.Text, StringComparer.Ordinal).First())
            .OrderBy(state => state.Cost)
            .ThenBy(state => state.Text, StringComparer.Ordinal)
            .Take(limit)
            .ToList();

    private sealed record PathState(string Text, double Cost, string? PreviousWord, IReadOnlyList<LatticeArc> Arcs);

    private sealed class StateKeyComparer : IEqualityComparer<(string Text, string? PreviousWord)>
    {
        public static StateKeyComparer Instance { get; } = new();

        public bool Equals((string Text, string? PreviousWord) x, (string Text, string? PreviousWord) y) =>
            string.Equals(x.Text, y.Text, StringComparison.Ordinal)
            && string.Equals(x.PreviousWord, y.PreviousWord, StringComparison.Ordinal);

        public int GetHashCode((string Text, string? PreviousWord) obj) =>
            HashCode.Combine(StringComparer.Ordinal.GetHashCode(obj.Text), obj.PreviousWord is null ? 0 : StringComparer.Ordinal.GetHashCode(obj.PreviousWord));
    }
}
