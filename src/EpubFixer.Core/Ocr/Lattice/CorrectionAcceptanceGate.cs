using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Lexicon.Models;
using EpubFixer.Core.Ocr.Lattice.Models;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Core.Ocr.Lattice;

public sealed class CorrectionAcceptanceGate(BookVocabulary vocabulary, LatticeOptions options) : ICorrectionAcceptanceGate
{
    private readonly WeightedEditAligner aligner = new();

    public AcceptanceResult Evaluate(CorruptedTextRegion region, WordLattice lattice, IReadOnlyList<DecodedPath> paths)
    {
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(lattice);
        ArgumentNullException.ThrowIfNull(paths);

        if (lattice.Outcome != LatticeBuildOutcome.Built || paths.Count == 0)
        {
            return Leave(lattice, "NoPath");
        }

        var best = paths[0];
        var identity = lattice.Window;
        if (string.Equals(best.Text, identity, StringComparison.Ordinal))
        {
            return Leave(lattice, "NoChange");
        }

        var changed = ChangedSpan(identity, best.Text);
        if (ChangedTokens(lattice.Window, changed.Start, changed.End).Any(IsValidOriginalToken))
        {
            return Leave(lattice, "OriginalTokenIsValid", changed);
        }

        var ordinarySubstitutions = OrdinarySubstitutions(lattice, best);
        if (ordinarySubstitutions is null || ordinarySubstitutions > options.MaxOrdinarySubstitutions)
        {
            return Leave(lattice, "TooManyOrdinaryEdits", changed);
        }

        if (best.EditCost > options.MaxPathCost)
        {
            return Leave(lattice, "CostAboveThreshold", changed);
        }

        var second = paths.Skip(1).FirstOrDefault(path => !string.Equals(path.Text, best.Text, StringComparison.Ordinal));
        if (second is not null && second.Cost - best.Cost < options.MinMargin)
        {
            return Result(AcceptanceVerdict.Review, null, lattice, changed, ["MarginTooSmall"]);
        }

        var replacement = ReplacementFor(identity, best.Text, changed);
        return Result(AcceptanceVerdict.Apply, replacement, lattice, changed, ["Accepted"]);
    }

    private int? OrdinarySubstitutions(WordLattice lattice, DecodedPath path)
    {
        var total = 0;
        foreach (var arc in path.Arcs.Where(arc => arc.Kind == LatticeArcKind.Word))
        {
            var source = lattice.Window.Substring(arc.From, arc.To - arc.From);
            if (!aligner.TryAlign(source, arc.Word, options.BudgetCap, out var alignment))
            {
                return null;
            }

            total += alignment.OrdinarySubstitutions;
        }

        return total;
    }

    private bool IsValidOriginalToken(string token)
    {
        var entry = vocabulary.Find(token);
        return entry is not null
            && (entry.BookCount >= 2 || entry.Source is VocabularySource.Frequency or VocabularySource.Morphology);
    }

    private static IEnumerable<string> ChangedTokens(string text, int start, int end)
    {
        var tokenStart = -1;
        for (var i = start; i < end; i++)
        {
            if (char.IsLetterOrDigit(text[i]) || text[i] is '\'' or '’')
            {
                tokenStart = tokenStart < 0 ? i : tokenStart;
                continue;
            }

            if (tokenStart >= 0)
            {
                yield return text[tokenStart..i];
                tokenStart = -1;
            }
        }

        if (tokenStart >= 0)
        {
            yield return text[tokenStart..end];
        }
    }

    private static ChangeSpan ChangedSpan(string identity, string replacement)
    {
        var prefix = 0;
        while (prefix < identity.Length
            && prefix < replacement.Length
            && identity[prefix] == replacement[prefix])
        {
            prefix++;
        }

        var suffix = 0;
        while (suffix < identity.Length - prefix
            && suffix < replacement.Length - prefix
            && identity[identity.Length - 1 - suffix] == replacement[replacement.Length - 1 - suffix])
        {
            suffix++;
        }

        var start = ExpandLeft(identity, prefix);
        var end = ExpandRight(identity, identity.Length - suffix);
        return new ChangeSpan(start, end, prefix, identity.Length - suffix, replacement.Length - suffix);
    }

    private static string ReplacementFor(string identity, string replacement, ChangeSpan changed)
    {
        var extraLeft = changed.DiffStart - changed.Start;
        var bestStart = Math.Max(0, changed.DiffStart - extraLeft);
        var bestEnd = Math.Min(replacement.Length, changed.ReplacementDiffEnd + (changed.End - changed.IdentityDiffEnd));
        return replacement[bestStart..bestEnd];
    }

    private static int ExpandLeft(string text, int index)
    {
        while (index > 0 && IsTokenCharacter(text[index - 1]))
        {
            index--;
        }

        return index;
    }

    private static int ExpandRight(string text, int index)
    {
        while (index < text.Length && IsTokenCharacter(text[index]))
        {
            index++;
        }

        return index;
    }

    private static bool IsTokenCharacter(char value) =>
        char.IsLetterOrDigit(value) || value is '\'' or '’';

    private static AcceptanceResult Leave(WordLattice lattice, string reason) =>
        Result(AcceptanceVerdict.Leave, null, lattice, new ChangeSpan(0, 0, 0, 0, 0), [reason]);

    private static AcceptanceResult Leave(WordLattice lattice, string reason, ChangeSpan changed) =>
        Result(AcceptanceVerdict.Leave, null, lattice, changed, [reason]);

    private static AcceptanceResult Result(
        AcceptanceVerdict verdict,
        string? replacement,
        WordLattice lattice,
        ChangeSpan changed,
        IReadOnlyList<string> reasons) =>
        new(verdict, replacement, lattice.WindowOffset + changed.Start, lattice.WindowOffset + changed.End, reasons);

    private readonly record struct ChangeSpan(
        int Start,
        int End,
        int DiffStart,
        int IdentityDiffEnd,
        int ReplacementDiffEnd);
}
