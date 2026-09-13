using EpubFixer.Core.Ocr.Lattice.Models;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Core.Ocr.Lattice;

public sealed class WordLatticeBuilder(ILexiconMatcher matcher) : IWordLatticeBuilder
{
    public WordLattice Build(CorruptedTextRegion region, string fullText, LatticeOptions options)
    {
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(fullText);
        ArgumentNullException.ThrowIfNull(options);

        var selected = LatticeWindow.Select(region, fullText, options);
        if (selected.Outcome != LatticeBuildOutcome.Built)
        {
            return new WordLattice(selected.Window, selected.WindowOffset, Array.Empty<LatticeArc>(), selected.Outcome, 0);
        }

        var budget = new SearchBudget(options.MaxRegionStates);
        var arcs = new List<LatticeArc>();
        AddIdentityAndLiteralArcs(selected.Window, arcs, budget);
        if (budget.Exceeded)
        {
            return BudgetExceeded(selected, budget);
        }

        var visitedNodes = new HashSet<int> { 0 };
        var work = new SortedSet<int>(StartNodes(selected.Window));
        while (work.Count > 0)
        {
            var from = work.Min;
            work.Remove(from);
            if (from >= selected.Window.Length)
            {
                continue;
            }

            var max = Math.Min(selected.Window.Length, from + options.MaxArcLength);
            for (var to = from + 1; to <= max; to++)
            {
                var span = selected.Window[from..to];
                if (!IsMatchableSpan(span))
                {
                    continue;
                }

                budget.Visit();
                if (budget.Exceeded)
                {
                    return BudgetExceeded(selected, budget);
                }

                foreach (var match in matcher.Match(span, BudgetFor(span, options)).Take(options.MaxMatchesPerSpan))
                {
                    budget.Visit();
                    if (budget.Exceeded)
                    {
                        return BudgetExceeded(selected, budget);
                    }

                    arcs.Add(new LatticeArc(from, to, match.Word, match.Cost, LatticeArcKind.Word));
                    if (visitedNodes.Add(to))
                    {
                        work.Add(to);
                    }
                }
            }
        }

        return new WordLattice(
            selected.Window,
            selected.WindowOffset,
            SortArcs(arcs),
            LatticeBuildOutcome.Built,
            (int)budget.Visited);
    }

    private static WordLattice BudgetExceeded(LatticeWindow selected, SearchBudget budget) =>
        new(selected.Window, selected.WindowOffset, Array.Empty<LatticeArc>(), LatticeBuildOutcome.BudgetExceeded, (int)budget.Visited);

    private static double BudgetFor(string span, LatticeOptions options) =>
        Math.Min(options.BudgetCap, options.BudgetBase + options.BudgetPerFourChars * (span.Length / 4));

    private static IReadOnlyList<LatticeArc> SortArcs(IEnumerable<LatticeArc> arcs) =>
        arcs
            .Distinct()
            .OrderBy(arc => arc.From)
            .ThenBy(arc => arc.To)
            .ThenBy(arc => arc.Kind)
            .ThenBy(arc => arc.Word, StringComparer.Ordinal)
            .ThenBy(arc => arc.Cost)
            .ToArray();

    private static IEnumerable<int> StartNodes(string window)
    {
        yield return 0;
        for (var i = 1; i <= window.Length; i++)
        {
            if (IsSeparator(window[i - 1]))
            {
                yield return i;
            }
        }
    }

    private static void AddIdentityAndLiteralArcs(string window, List<LatticeArc> arcs, SearchBudget budget)
    {
        var start = 0;
        var inToken = window.Length > 0 && IsTokenCharacter(window[0]);
        for (var i = 1; i <= window.Length; i++)
        {
            var nextInToken = i < window.Length && IsTokenCharacter(window[i]);
            if (i < window.Length && nextInToken == inToken)
            {
                continue;
            }

            AddArc(start, i, inToken ? LatticeArcKind.Identity : LatticeArcKind.Literal);
            start = i;
            inToken = nextInToken;
        }

        void AddArc(int from, int to, LatticeArcKind kind)
        {
            if (from == to)
            {
                return;
            }

            budget.Visit();
            arcs.Add(new LatticeArc(from, to, window[from..to], 0, kind));
        }
    }

    private static bool IsMatchableSpan(string span) =>
        span.Length > 0
        && !char.IsWhiteSpace(span[0])
        && !char.IsWhiteSpace(span[^1])
        && span.Any(char.IsLetterOrDigit);

    private static bool IsSeparator(char value) =>
        value is ' ' or '\t' or '\r' or '\n' or '-' or '\u00ad';

    private static bool IsTokenCharacter(char value) =>
        char.IsLetterOrDigit(value) || value is '\'' or '’';
}
