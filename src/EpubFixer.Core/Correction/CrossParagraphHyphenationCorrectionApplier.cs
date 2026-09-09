using AngleSharp.Dom;
using EpubFixer.Core.Correction.Models;
using EpubFixer.Core.Detection.Models;
using EpubFixer.Core.Epub.Models;

namespace EpubFixer.Core.Correction;

public sealed class CrossParagraphHyphenationCorrectionApplier
{
    public HyphenationCorrectionApplyResult Apply(
        IReadOnlyList<HyphenationCorrectionPlan> plans)
    {
        ArgumentNullException.ThrowIfNull(plans);

        var appliedCount = 0;
        var skippedCount = 0;

        foreach (var plan in plans)
        {
            if (!TryCreateEdit(plan, out var edit))
            {
                skippedCount++;
                continue;
            }

            edit.HyphenNode.Delete(edit.HyphenStart, edit.HyphenLength);

            foreach (var child in edit.RightParagraphChildren)
            {
                edit.LeftParagraph.AppendChild(child);
            }

            foreach (var whitespaceNode in edit.InterveningWhitespaceNodes)
            {
                whitespaceNode.Parent?.RemoveChild(whitespaceNode);
            }

            edit.RightParagraph.Remove();
            appliedCount++;
        }

        return new HyphenationCorrectionApplyResult(appliedCount, skippedCount);
    }

    private static bool TryCreateEdit(
        HyphenationCorrectionPlan plan,
        out CrossParagraphEdit edit)
    {
        edit = null!;

        if (plan is null
            || plan.CorrectionKind != HyphenationCorrectionKind.CrossParagraph)
        {
            return false;
        }

        var candidate = plan.Decision.Evidence.Candidate;

        if (candidate.DetectionKind != HyphenationDetectionKind.ParagraphBoundary)
        {
            return false;
        }

        var left = plan.LeftSource;
        var hyphen = plan.HyphenSource;
        var right = plan.RightSource;
        var leftParagraph = FindContainingParagraph(left.SourceNode);
        var rightParagraph = FindContainingParagraph(right.SourceNode);

        if (leftParagraph is null
            || rightParagraph is null
            || !ReferenceEquals(leftParagraph.Parent, rightParagraph.Parent)
            || !ReferenceEquals(leftParagraph.NextElementSibling, rightParagraph)
            || !HasSameAttributes(leftParagraph, rightParagraph)
            || !ReferenceEquals(left.SourceNode, hyphen.SourceNode)
            || !HasSameSourceIdentity(left, hyphen)
            || !string.Equals(left.DocumentPath, right.DocumentPath, StringComparison.Ordinal)
            || hyphen.Length != 1
            || !IsValidRange(left.SourceNode.Data, left.Start, left.Length)
            || !IsValidRange(hyphen.SourceNode.Data, hyphen.Start, hyphen.Length)
            || !IsValidRange(right.SourceNode.Data, right.Start, right.Length)
            || left.Start + left.Length != hyphen.Start
            || !IsLastTextPosition(leftParagraph, hyphen)
            || !IsFirstTextPosition(rightParagraph, right))
        {
            return false;
        }

        var interveningNodes = GetInterveningNodes(leftParagraph, rightParagraph);

        if (interveningNodes is null
            || interveningNodes.Any(node =>
                node is not IText text || !string.IsNullOrWhiteSpace(text.Data)))
        {
            return false;
        }

        if (!string.Equals(
                left.SourceNode.Data.Substring(left.Start, left.Length),
                candidate.LeftPart,
                StringComparison.Ordinal)
            || !string.Equals(
                hyphen.SourceNode.Data.Substring(hyphen.Start, hyphen.Length),
                "-",
                StringComparison.Ordinal)
            || !string.Equals(
                right.SourceNode.Data.Substring(right.Start, right.Length),
                candidate.RightPart,
                StringComparison.Ordinal)
            || !string.Equals(
                candidate.LeftPart + candidate.RightPart,
                plan.UnhyphenatedText,
                StringComparison.Ordinal))
        {
            return false;
        }

        edit = new CrossParagraphEdit(
            leftParagraph,
            rightParagraph,
            hyphen.SourceNode,
            hyphen.Start,
            hyphen.Length,
            rightParagraph.ChildNodes.ToArray(),
            interveningNodes);
        return true;
    }

    private static IElement? FindContainingParagraph(INode node)
    {
        for (var element = node.ParentElement;
             element is not null;
             element = element.ParentElement)
        {
            if (string.Equals(element.LocalName, "p", StringComparison.OrdinalIgnoreCase))
            {
                return element;
            }
        }

        return null;
    }

    private static INode[]? GetInterveningNodes(
        IElement leftParagraph,
        IElement rightParagraph)
    {
        var nodes = new List<INode>();

        for (var node = leftParagraph.NextSibling;
             node is not null;
             node = node.NextSibling)
        {
            if (ReferenceEquals(node, rightParagraph))
            {
                return nodes.ToArray();
            }

            nodes.Add(node);
        }

        return null;
    }

    private static bool IsLastTextPosition(
        IElement paragraph,
        TextSourceLocation hyphen)
    {
        var textNodes = EnumerateTextNodes(paragraph).ToArray();
        var meaningfulNodes = textNodes
            .Where(node => !string.IsNullOrWhiteSpace(node.Data))
            .ToArray();

        if (meaningfulNodes.Length == 0
            || !ReferenceEquals(meaningfulNodes[^1], hyphen.SourceNode)
            || hyphen.Start + hyphen.Length != hyphen.SourceNode.Data.Length)
        {
            return false;
        }

        var sourceIndex = Array.FindIndex(
            textNodes,
            node => ReferenceEquals(node, hyphen.SourceNode));

        return sourceIndex >= 0
            && textNodes[(sourceIndex + 1)..].All(node => node.Data.Length == 0);
    }

    private static bool IsFirstTextPosition(
        IElement paragraph,
        TextSourceLocation right)
    {
        var textNodes = EnumerateTextNodes(paragraph).ToArray();
        var meaningfulNodes = textNodes
            .Where(node => !string.IsNullOrWhiteSpace(node.Data))
            .ToArray();

        if (meaningfulNodes.Length == 0
            || !ReferenceEquals(meaningfulNodes[0], right.SourceNode)
            || right.Start != 0)
        {
            return false;
        }

        var sourceIndex = Array.FindIndex(
            textNodes,
            node => ReferenceEquals(node, right.SourceNode));

        return sourceIndex >= 0
            && textNodes[..sourceIndex].All(node => node.Data.Length == 0);
    }

    private static IEnumerable<IText> EnumerateTextNodes(INode node)
    {
        foreach (var child in node.ChildNodes)
        {
            if (child is IText text)
            {
                yield return text;
            }

            foreach (var descendant in EnumerateTextNodes(child))
            {
                yield return descendant;
            }
        }
    }

    private static bool HasSameAttributes(IElement left, IElement right)
    {
        var leftAttributes = left.Attributes.ToArray();
        var rightAttributes = right.Attributes.ToArray();

        return leftAttributes.Length == rightAttributes.Length
            && leftAttributes.All(leftAttribute => rightAttributes.Any(rightAttribute =>
                string.Equals(
                    leftAttribute.NamespaceUri,
                    rightAttribute.NamespaceUri,
                    StringComparison.Ordinal)
                && string.Equals(
                    leftAttribute.LocalName,
                    rightAttribute.LocalName,
                    StringComparison.Ordinal)
                && string.Equals(
                    leftAttribute.Value,
                    rightAttribute.Value,
                    StringComparison.Ordinal)));
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

    private sealed record CrossParagraphEdit(
        IElement LeftParagraph,
        IElement RightParagraph,
        IText HyphenNode,
        int HyphenStart,
        int HyphenLength,
        IReadOnlyList<INode> RightParagraphChildren,
        IReadOnlyList<INode> InterveningWhitespaceNodes);
}
