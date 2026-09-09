using AngleSharp.Dom;
using EpubFixer.Core.Correction.Models;
using EpubFixer.Core.Epub.Models;

namespace EpubFixer.Core.Correction;

public sealed class HyphenationCorrectionApplier
{
    public HyphenationCorrectionApplyResult Apply(
        IReadOnlyList<HyphenationCorrectionPlan> plans)
    {
        ArgumentNullException.ThrowIfNull(plans);

        var edits = new List<InlineEdit>();
        var acceptedRanges = new Dictionary<IText, List<SourceRange>>(
            ReferenceEqualityComparer.Instance);
        var skippedCount = 0;

        foreach (var plan in plans)
        {
            if (!TryCreateInlineEdit(plan, out var edit))
            {
                skippedCount++;
                continue;
            }

            if (!acceptedRanges.TryGetValue(edit.Node, out var ranges))
            {
                ranges = [];
                acceptedRanges.Add(edit.Node, ranges);
            }

            var range = new SourceRange(edit.Start, edit.Length);

            if (ranges.Any(existing => existing.Overlaps(range)))
            {
                skippedCount++;
                continue;
            }

            ranges.Add(range);
            edits.Add(edit);
        }

        foreach (var group in edits.GroupBy(edit => edit.Node, ReferenceEqualityComparer.Instance))
        {
            foreach (var edit in group.OrderByDescending(edit => edit.Start))
            {
                edit.Node.Delete(edit.Start, edit.Length);
            }
        }

        return new HyphenationCorrectionApplyResult(edits.Count, skippedCount);
    }

    private static bool TryCreateInlineEdit(
        HyphenationCorrectionPlan plan,
        out InlineEdit edit)
    {
        edit = null!;

        if (plan is null || plan.CorrectionKind != HyphenationCorrectionKind.Inline)
        {
            return false;
        }

        var candidate = plan.Decision.Evidence.Candidate;
        var left = plan.LeftSource;
        var hyphen = plan.HyphenSource;
        var right = plan.RightSource;

        if (!ReferenceEquals(left.SourceNode, hyphen.SourceNode)
            || !ReferenceEquals(hyphen.SourceNode, right.SourceNode)
            || !HasSameSourceIdentity(left, hyphen)
            || !HasSameSourceIdentity(hyphen, right)
            || hyphen.Length != 1
            || !IsValidRange(left.SourceNode.Data, left.Start, left.Length)
            || !IsValidRange(hyphen.SourceNode.Data, hyphen.Start, hyphen.Length)
            || !IsValidRange(right.SourceNode.Data, right.Start, right.Length)
            || left.Start + left.Length != hyphen.Start
            || hyphen.Start + hyphen.Length != right.Start)
        {
            return false;
        }

        var sourceText = left.SourceNode.Data;

        if (!string.Equals(
                sourceText.Substring(left.Start, left.Length),
                candidate.LeftPart,
                StringComparison.Ordinal)
            || !string.Equals(
                sourceText.Substring(hyphen.Start, hyphen.Length),
                "-",
                StringComparison.Ordinal)
            || !string.Equals(
                sourceText.Substring(right.Start, right.Length),
                candidate.RightPart,
                StringComparison.Ordinal)
            || !string.Equals(
                candidate.LeftPart + candidate.RightPart,
                plan.UnhyphenatedText,
                StringComparison.Ordinal))
        {
            return false;
        }

        edit = new InlineEdit(hyphen.SourceNode, hyphen.Start, hyphen.Length);
        return true;
    }

    private static bool HasSameSourceIdentity(
        TextSourceLocation first,
        TextSourceLocation second)
    {
        return string.Equals(first.DocumentPath, second.DocumentPath, StringComparison.Ordinal)
            && first.TextNodeIndex == second.TextNodeIndex;
    }

    private static bool IsValidRange(string text, int start, int length)
    {
        return start >= 0
            && length >= 0
            && start <= text.Length
            && length <= text.Length - start;
    }

    private sealed record InlineEdit(IText Node, int Start, int Length);

    private readonly record struct SourceRange(int Start, int Length)
    {
        private int End => Start + Length;

        public bool Overlaps(SourceRange other)
        {
            return Start < other.End && other.Start < End;
        }
    }
}
