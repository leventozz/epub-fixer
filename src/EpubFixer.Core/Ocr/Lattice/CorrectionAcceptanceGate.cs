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

        var replacement = ReplacementFor(identity, best.Text, changed);
        var changedSource = identity[changed.Start..changed.End];
        var ordinaryEdits = OrdinaryEdits(lattice, best);
        if (ordinaryEdits is null || ordinaryEdits > options.MaxOrdinarySubstitutions)
        {
            return Leave(lattice, "TooManyOrdinaryEdits", changed);
        }

        var regionRisk = UnsafeReplacementReason(region.RawText, replacement, ordinaryEdits.Value);
        if (regionRisk is not null)
        {
            return Leave(lattice, regionRisk, changed);
        }

        if (IsShortOrDisproportionateChange(changedSource, replacement))
        {
            return Leave(lattice, "UnsafeLengthChange", changed);
        }

        var riskyArcReason = UnsafeWordArcReason(lattice, best);
        if (riskyArcReason is not null)
        {
            return Leave(lattice, riskyArcReason, changed);
        }

        if (ordinaryEdits > 0 && HasProperNameRisk(changedSource, replacement))
        {
            return Leave(lattice, "ProperNameRisk", changed);
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

        return Result(AcceptanceVerdict.Apply, replacement, lattice, changed, ["Accepted"]);
    }

    private int? OrdinaryEdits(WordLattice lattice, DecodedPath path)
    {
        var total = 0;
        foreach (var arc in path.Arcs.Where(arc => arc.Kind == LatticeArcKind.Word))
        {
            var source = lattice.Window.Substring(arc.From, arc.To - arc.From);
            if (!aligner.TryAlign(source, arc.Word, options.BudgetCap, out var alignment))
            {
                return null;
            }

            total += alignment.OrdinaryEdits;
        }

        return total;
    }

    private string? UnsafeWordArcReason(WordLattice lattice, DecodedPath path)
    {
        foreach (var arc in path.Arcs.Where(arc => arc.Kind == LatticeArcKind.Word))
        {
            var source = lattice.Window.Substring(arc.From, arc.To - arc.From);
            if (!aligner.TryAlign(source, arc.Word, options.BudgetCap, out var alignment))
            {
                return "TooManyOrdinaryEdits";
            }

            if (alignment.OrdinaryEdits > 0 && HasProperNameRisk(source, arc.Word))
            {
                return "ProperNameRisk";
            }
        }

        return null;
    }

    private static string? UnsafeReplacementReason(string rawText, string replacement, int ordinaryEdits)
    {
        if (HasSuspiciousReplacementShape(replacement))
        {
            return "SuspiciousReplacement";
        }

        if (HasApostropheStemRisk(rawText, replacement))
        {
            return "ProperNameRisk";
        }

        if (IsShortOrDisproportionateChange(rawText, replacement))
        {
            return "UnsafeLengthChange";
        }

        return ordinaryEdits > 0 && HasProperNameRisk(rawText, replacement)
            ? "ProperNameRisk"
            : null;
    }

    private static bool HasSuspiciousReplacementShape(string replacement)
    {
        var normalized = replacement.ToLowerInvariant();
        return normalized.Contains("ıe", StringComparison.Ordinal)
            || normalized.Contains("ie", StringComparison.Ordinal);
    }

    private static bool HasApostropheStemRisk(string source, string replacement)
    {
        if (!TryStemBeforeApostrophe(source, out var sourceStem)
            || !TryStemBeforeApostrophe(replacement, out var replacementStem))
        {
            return false;
        }

        return !string.Equals(CanonicalStem(sourceStem), CanonicalStem(replacementStem), StringComparison.Ordinal);
    }

    private static bool TryStemBeforeApostrophe(string value, out string stem)
    {
        var index = value.IndexOfAny(['\'', '’']);
        if (index < 0)
        {
            stem = string.Empty;
            return false;
        }

        stem = value[..index];
        return true;
    }

    private static string CanonicalStem(string value) =>
        new(value
            .Where(character => char.IsLetterOrDigit(character))
            .ToArray());

    private static bool IsShortOrDisproportionateChange(string source, string replacement)
    {
        var sourceLength = AlphanumericLength(source);
        var replacementLength = AlphanumericLength(replacement);
        if (sourceLength < 3)
        {
            return true;
        }

        if (replacementLength == 0)
        {
            return true;
        }

        return replacementLength > sourceLength * 1.5
            || replacementLength < sourceLength * 0.75;
    }

    private static bool HasProperNameRisk(string source, string replacement)
    {
        if (source.Any(char.IsUpper))
        {
            return true;
        }

        return source.Contains('\'') || source.Contains('’') || replacement.Contains('\'') || replacement.Contains('’');
    }

    private static int AlphanumericLength(string value) =>
        value.Count(char.IsLetterOrDigit);

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
