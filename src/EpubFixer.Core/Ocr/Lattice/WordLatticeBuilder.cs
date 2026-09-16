using EpubFixer.Core.Ocr.Lattice.Models;
using EpubFixer.Core.Ocr.Models;
using System.Globalization;

namespace EpubFixer.Core.Ocr.Lattice;

public sealed class WordLatticeBuilder(
    ILexiconMatcher matcher,
    IReadOnlyCollection<int>? hardBoundaryOffsets = null) : IWordLatticeBuilder
{
    private static readonly CultureInfo TurkishCulture = new("tr-TR");
    private static readonly OcrEditCostModel Costs = new();
    private static readonly HashSet<char> HardGarbage = ['^', ';', '<', '>', ':'];
    private readonly IReadOnlyCollection<int> hardBoundaryOffsets = hardBoundaryOffsets ?? Array.Empty<int>();

    public WordLattice Build(CorruptedTextRegion region, string fullText, LatticeOptions options)
    {
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(fullText);
        ArgumentNullException.ThrowIfNull(options);

        var selected = LatticeWindow.Select(region, fullText, options, hardBoundaryOffsets);
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

                    arcs.Add(new LatticeArc(from, to, RestoreCase(match.Word, span), match.Cost, LatticeArcKind.Word));
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

            if (inToken)
            {
                AddArc(start, i, window[start..i], 0, LatticeArcKind.Identity);
            }
            else
            {
                AddNonTokenArcs(start, i);
            }

            start = i;
            inToken = nextInToken;
        }

        // A run between two tokens gets two arcs when it carries a hard garbage glyph: keep it
        // (costing RetainedGarbage per glyph) or drop it (costing GarbageDeletion per glyph,
        // emitting a single space so the neighbours stay separated). Both arcs count the SAME
        // characters - charge fewer than you drop and keeping always wins, which is exactly the
        // dead-arc trap H4b hit first time round. Two guards (D91):
        //   - no hard garbage in the run -> both stay free and undeletable, so ordinary
        //     punctuation is never taxed and never welds two sentences together;
        //   - no whitespace in the run -> it sits inside a token, where the word arc already
        //     solves it; a deletion arc there would only add a rival garbage-free-but-wrong path.
        void AddNonTokenArcs(int from, int to)
        {
            var run = window[from..to];
            var deletable = run.Any(HardGarbage.Contains) && run.Any(char.IsWhiteSpace);
            var glyphs = deletable ? run.Count(value => !char.IsWhiteSpace(value)) : 0;

            AddArc(from, to, run, glyphs * Costs.RetainedGarbage, LatticeArcKind.Literal);
            if (deletable)
            {
                AddArc(from, to, " ", glyphs * Costs.GarbageDeletion, LatticeArcKind.GarbageDeletion);
            }
        }

        void AddArc(int from, int to, string word, double cost, LatticeArcKind kind)
        {
            if (from == to)
            {
                return;
            }

            budget.Visit();
            arcs.Add(new LatticeArc(from, to, word, cost, kind));
        }
    }

    private static bool IsMatchableSpan(string span) =>
        span.Length > 0
        && !char.IsWhiteSpace(span[0])
        && !char.IsWhiteSpace(span[^1])
        && span.Count(char.IsLetterOrDigit) >= 2;

    private static bool IsSeparator(char value) =>
        value is ' ' or '\t' or '\r' or '\n' or '-' or '\u00ad';

    private static bool IsTokenCharacter(char value) =>
        char.IsLetterOrDigit(value) || value is '\'' or '’';

    private static string RestoreCase(string word, string span)
    {
        if (word.Length == 0)
        {
            return word;
        }

        var letters = span.Where(char.IsLetter).ToArray();
        if (letters.Length >= 2 && letters.All(char.IsUpper))
        {
            return word.ToUpper(TurkishCulture);
        }

        var firstLetter = span.FirstOrDefault(char.IsLetter);
        return firstLetter != '\0' && char.IsUpper(firstLetter)
            ? char.ToUpper(word[0], TurkishCulture) + word[1..]
            : word;
    }
}
