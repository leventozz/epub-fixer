using System.Text;
using AngleSharp.Dom;
using EpubFixer.Core.Epub.Models;

namespace EpubFixer.Core.Epub;

public static class LogicalTextStreamBuilder
{
    private static readonly HashSet<string> BlockElementNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "address", "article", "aside", "blockquote", "body", "dd", "div", "dl", "dt",
        "figcaption", "figure", "footer", "form", "h1", "h2", "h3", "h4", "h5", "h6",
        "header", "li", "main", "nav", "ol", "p", "pre", "section", "table", "td", "th",
        "tr", "ul"
    };

    private static readonly HashSet<string> ExcludedElementNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "template"
    };

    public static LogicalTextStream Build(IEnumerable<EpubContentDocument> documents)
    {
        var segments = new List<TextSegment>();
        var boundaries = new List<TextBoundary>();
        var text = new StringBuilder();
        SegmentDraft? previous = null;

        foreach (var document in documents.Where(document => document.IsLinear))
        {
            foreach (var current in ExtractSegments(document))
            {
                if (previous is not null)
                {
                    boundaries.Add(new TextBoundary(
                        segments.Count - 1,
                        segments.Count,
                        GetBoundaryKind(previous, current)));
                }

                var source = new TextSourceLocation(
                    document.Path,
                    current.TextNodeIndex,
                    current.Node,
                    0,
                    current.Node.Data.Length);

                segments.Add(new TextSegment(current.Node.Data, text.Length, source));
                text.Append(current.Node.Data);
                previous = current;
            }
        }

        return new LogicalTextStream(
            Array.AsReadOnly(segments.ToArray()),
            Array.AsReadOnly(boundaries.ToArray()),
            text.ToString());
    }

    private static IReadOnlyList<SegmentDraft> ExtractSegments(EpubContentDocument document)
    {
        var body = document.Document.Body;

        if (body is null)
        {
            return Array.Empty<SegmentDraft>();
        }

        var candidates = EnumerateTextNodes(body)
            .Select((node, index) => new { Node = node, TextNodeIndex = index })
            .Where(item => item.Node.Data.Length > 0 && !HasExcludedAncestor(item.Node))
            .Select(item => new SegmentDraft(
                document,
                item.Node,
                FindNearestBlock(item.Node),
                item.TextNodeIndex))
            .ToArray();

        if (candidates.Length == 0)
        {
            return candidates;
        }

        var previousTextBlocks = new IElement?[candidates.Length];
        var nextTextBlocks = new IElement?[candidates.Length];
        IElement? nearestBlock = null;

        for (var index = 0; index < candidates.Length; index++)
        {
            previousTextBlocks[index] = nearestBlock;

            if (!string.IsNullOrWhiteSpace(candidates[index].Node.Data))
            {
                nearestBlock = candidates[index].Block;
            }
        }

        nearestBlock = null;

        for (var index = candidates.Length - 1; index >= 0; index--)
        {
            nextTextBlocks[index] = nearestBlock;

            if (!string.IsNullOrWhiteSpace(candidates[index].Node.Data))
            {
                nearestBlock = candidates[index].Block;
            }
        }

        return candidates
            .Where((candidate, index) =>
                !string.IsNullOrWhiteSpace(candidate.Node.Data)
                || candidate.Block is not null
                && ReferenceEquals(candidate.Block, previousTextBlocks[index])
                && ReferenceEquals(candidate.Block, nextTextBlocks[index]))
            .ToArray();
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

    private static bool HasExcludedAncestor(INode node)
    {
        for (var ancestor = node.ParentElement; ancestor is not null; ancestor = ancestor.ParentElement)
        {
            if (ExcludedElementNames.Contains(ancestor.LocalName))
            {
                return true;
            }
        }

        return false;
    }

    private static IElement? FindNearestBlock(INode node)
    {
        for (var ancestor = node.ParentElement; ancestor is not null; ancestor = ancestor.ParentElement)
        {
            if (BlockElementNames.Contains(ancestor.LocalName))
            {
                return ancestor;
            }
        }

        return null;
    }

    private static TextBoundaryKind GetBoundaryKind(SegmentDraft previous, SegmentDraft current)
    {
        if (!ReferenceEquals(previous.Document, current.Document))
        {
            return TextBoundaryKind.Document;
        }

        return ReferenceEquals(previous.Block, current.Block)
            ? TextBoundaryKind.TextNode
            : TextBoundaryKind.Paragraph;
    }

    private sealed record SegmentDraft(
        EpubContentDocument Document,
        IText Node,
        IElement? Block,
        int TextNodeIndex);
}
