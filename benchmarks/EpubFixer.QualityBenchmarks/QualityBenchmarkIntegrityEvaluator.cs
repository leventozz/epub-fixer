using AngleSharp.Dom;
using EpubFixer.Core.Correction.Models;
using EpubFixer.Core.Epub.Models;
using EpubFixer.QualityBenchmarks.Models;

namespace EpubFixer.QualityBenchmarks;

/// <summary>
/// Audits the DOM mutations made by the production appliers. The expected
/// state is deliberately modelled from source plans, rather than by running
/// the production applier a second time.
/// </summary>
internal sealed class QualityBenchmarkIntegrityEvaluator
{
    public DomSnapshot Capture(IReadOnlyList<EpubContentDocument> documents)
    {
        var nodes = new Dictionary<INode, NodeState>(ReferenceEqualityComparer.Instance);
        foreach (var document in documents)
        {
            CaptureNode(document.Document, document.Path, "/", null, nodes);
        }

        return new DomSnapshot(nodes);
    }

    public IntegrityBenchmarkResult Audit(
        DomSnapshot before,
        IReadOnlyList<EpubContentDocument> documents,
        IReadOnlyList<HyphenationCorrectionPlan> allowedPlans,
        string phase)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(allowedPlans);

        var current = new Dictionary<INode, NodeState>(ReferenceEqualityComparer.Instance);
        foreach (var document in documents)
        {
            CaptureNode(document.Document, document.Path, "/", null, current);
        }

        var allowed = AllowedMutationSet.Create(before, allowedPlans);
        var textChanges = new List<UnexpectedTextChange>();
        var nonTextChanges = new List<NonTextChange>();

        foreach (var pair in before.Nodes)
        {
            var node = pair.Key;
            var oldState = pair.Value;
            if (!current.TryGetValue(node, out var newState))
            {
                if (allowed.IsAllowedRemoval(node, oldState))
                {
                    continue;
                }

                AddNonText(nonTextChanges, oldState, phase, "removed", "Unrelated DOM node was removed.");
                if (node is IText && !string.IsNullOrEmpty(oldState.Value))
                {
                    AddText(textChanges, oldState, oldState.Value, string.Empty, phase,
                        "Text node was removed outside an expected cross-paragraph merge.");
                }

                continue;
            }

            if (!ReferenceEquals(oldState.Parent, newState.Parent)
                && !allowed.IsAllowedMove(node, newState.Parent))
            {
                AddNonText(nonTextChanges, oldState, phase, "moved", "Node moved outside an expected cross-paragraph merge.");
            }

            if (node is IElement)
            {
                if (!string.Equals(oldState.Attributes, newState.Attributes, StringComparison.Ordinal))
                {
                    AddNonText(nonTextChanges, oldState, phase, "attribute", "Element attributes changed unexpectedly.");
                }
            }

            if (node is IText
                && !string.Equals(oldState.Value, newState.Value, StringComparison.Ordinal)
                && !allowed.IsAllowedText(node, newState.Value))
            {
                    AddText(textChanges, oldState, oldState.Value, newState.Value, phase,
                        $"Text changed outside an expected known-error correction (before length {oldState.Value.Length}, after length {newState.Value.Length}).");
            }
        }

        foreach (var pair in current)
        {
            if (before.Nodes.ContainsKey(pair.Key))
            {
                continue;
            }

            AddNonText(nonTextChanges, pair.Value, phase, "added", "Unrelated DOM node was added.");
            if (pair.Key is IText && !string.IsNullOrEmpty(pair.Value.Value))
            {
                AddText(textChanges, pair.Value, string.Empty, pair.Value.Value, phase,
                    "Text node was added unexpectedly.");
            }
        }

        return new IntegrityBenchmarkResult(textChanges, nonTextChanges);
    }

    private static void CaptureNode(
        INode node,
        string documentPath,
        string path,
        INode? parent,
        IDictionary<INode, NodeState> nodes)
    {
        var state = new NodeState(
            node,
            documentPath,
            path,
            parent,
            node.NodeName,
            node is IText text ? text.Data : node.NodeValue ?? string.Empty,
            node is IElement element ? AttributeFingerprint(element) : string.Empty,
            node.ChildNodes.ToArray());
        nodes[node] = state;

        for (var index = 0; index < node.ChildNodes.Length; index++)
        {
            CaptureNode(node.ChildNodes[index], documentPath,
                $"{path}{node.ChildNodes[index].NodeName}[{index}]/", node, nodes);
        }
    }

    private static string AttributeFingerprint(IElement element)
    {
        return string.Join("\u001f", element.Attributes
            .Select(attribute => string.Join("\u001e",
                attribute.NamespaceUri ?? string.Empty,
                attribute.Prefix ?? string.Empty,
                attribute.LocalName,
                attribute.Value))
            .OrderBy(value => value, StringComparer.Ordinal));
    }

    private static void AddText(
        ICollection<UnexpectedTextChange> changes,
        NodeState state,
        string before,
        string after,
        string phase,
        string reason)
    {
        changes.Add(new UnexpectedTextChange(
            state.DocumentPath,
            $"{phase}:{state.Path}",
            before,
            after,
            reason));
    }

    private static void AddNonText(
        ICollection<NonTextChange> changes,
        NodeState state,
        string phase,
        string mutation,
        string reason)
    {
        changes.Add(new NonTextChange(state.DocumentPath, $"{phase}:{state.Path}", mutation, reason));
    }

    internal sealed record DomSnapshot(IReadOnlyDictionary<INode, NodeState> Nodes);

    internal sealed record NodeState(
        INode Node,
        string DocumentPath,
        string Path,
        INode? Parent,
        string NodeName,
        string Value,
        string Attributes,
        IReadOnlyList<INode> Children);

    internal sealed record IntegrityBenchmarkResult(
        IReadOnlyList<UnexpectedTextChange> TextChanges,
        IReadOnlyList<NonTextChange> NonTextChanges);

    private sealed class AllowedMutationSet
    {
        private readonly HashSet<INode> _allowedRemoved = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<INode, HashSet<INode?>> _allowedParents = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<INode, HashSet<int>> _allowedHyphenPositions = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<INode, HashSet<string>> _allowedSuffixes = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<INode, string> _originalText = new(ReferenceEqualityComparer.Instance);
        private readonly HashSet<INode> _crossLeftNodes = new(ReferenceEqualityComparer.Instance);

        public static AllowedMutationSet Create(
            DomSnapshot before,
            IReadOnlyList<HyphenationCorrectionPlan> plans)
        {
            var result = new AllowedMutationSet();
            foreach (var plan in plans)
            {
                var left = plan.LeftSource.SourceNode;
                var hyphen = plan.HyphenSource.SourceNode;
                var right = plan.RightSource.SourceNode;
                result.AllowHyphenDeletion(before, hyphen, plan.HyphenSource.Start);
            }

            foreach (var plan in plans)
            {
                var left = plan.LeftSource.SourceNode;
                var right = plan.RightSource.SourceNode;

                if (plan.CorrectionKind != HyphenationCorrectionKind.CrossParagraph)
                {
                    continue;
                }

                var leftParagraph = FindParagraph(before, left);
                var rightParagraph = FindParagraph(before, right);
                if (leftParagraph is null || rightParagraph is null)
                {
                    continue;
                }
                result._crossLeftNodes.Add(left);

                result._allowedRemoved.Add(rightParagraph);
                foreach (var child in before.Nodes[rightParagraph].Children)
                {
                    result.AllowMove(child, leftParagraph);
                }

                var leftParent = before.Nodes[leftParagraph].Parent;
                var siblings = leftParent is not null && before.Nodes.TryGetValue(leftParent, out var parentState)
                    ? parentState.Children
                    : Array.Empty<INode>();
                var rightIndex = Array.IndexOf(siblings.ToArray(), rightParagraph);
                var leftIndex = Array.IndexOf(siblings.ToArray(), leftParagraph);
                for (var index = leftIndex + 1; index >= 0 && index < rightIndex; index++)
                {
                    var node = siblings[index];
                    if (node is IText text && string.IsNullOrWhiteSpace(text.Data))
                    {
                        result._allowedRemoved.Add(node);
                    }
                }

                // The production applier coalesces direct text children after a
                // paragraph merge. Permit exactly the moved text as a suffix.
                if (before.Nodes[left].Parent == leftParagraph
                    && left is IText)
                {
                    foreach (var child in before.Nodes[rightParagraph].Children.OfType<IText>())
                    {
                        foreach (var variant in result.GetTextVariants(child, before))
                        {
                            result.AllowAppend(left, variant);
                        }
                        result._allowedRemoved.Add(child);
                    }
                    var subtreeText = GetSubtreeText(before, rightParagraph);
                    if (subtreeText.Length > 0)
                    {
                        result.AllowAppend(left, subtreeText);
                    }
                }
            }

            return result;
        }

        public bool IsAllowedRemoval(INode node, NodeState oldState)
        {
            return _allowedRemoved.Contains(node);
        }

        public bool IsAllowedMove(INode node, INode? parent)
        {
            return _allowedParents.TryGetValue(node, out var parents) && parents.Contains(parent);
        }

        public bool IsAllowedText(INode node, string actual)
        {
            if (!_originalText.TryGetValue(node, out var original)
                || !_allowedHyphenPositions.TryGetValue(node, out var positions))
            {
                return false;
            }

            IEnumerable<string> candidateSuffixes = _allowedSuffixes.TryGetValue(node, out var suffixes)
                ? suffixes
                : new[] { string.Empty };
            foreach (var suffixValue in candidateSuffixes)
            {
                var suffix = suffixValue;
                var candidate = actual;
                var allowArbitrarySuffix = _crossLeftNodes.Contains(node) && suffix.Length == 0;
                if (!allowArbitrarySuffix && !candidate.EndsWith(suffix, StringComparison.Ordinal))
                {
                    continue;
                }

                if (suffix.Length > 0)
                {
                    candidate = candidate[..^suffix.Length];
                }
                var beforeIndex = 0;
                var afterIndex = 0;
                var valid = true;
                while (beforeIndex < original.Length)
                {
                    if (afterIndex < candidate.Length && original[beforeIndex] == candidate[afterIndex])
                    {
                        beforeIndex++;
                        afterIndex++;
                    }
                    else if (original[beforeIndex] == '-' && positions.Contains(beforeIndex))
                    {
                        beforeIndex++;
                    }
                    else
                    {
                        valid = false;
                        break;
                    }
                }

                if (valid && (afterIndex == candidate.Length || allowArbitrarySuffix))
                {
                    return true;
                }
            }

            return false;
        }

        private void AllowHyphenDeletion(DomSnapshot before, INode node, int sourceStart)
        {
            if (!before.Nodes.TryGetValue(node, out var state)
                || sourceStart < 0
                || sourceStart >= state.Value.Length
                || state.Value[sourceStart] != '-')
            {
                return;
            }

            _originalText[node] = state.Value;
            if (!_allowedHyphenPositions.TryGetValue(node, out var positions))
            {
                positions = [];
                _allowedHyphenPositions[node] = positions;
            }
            positions.Add(sourceStart);
        }

        private void AllowAppend(INode node, string suffix)
        {
            if (!_allowedSuffixes.TryGetValue(node, out var values))
            {
                values = [string.Empty];
                _allowedSuffixes[node] = values;
            }

            foreach (var value in values.ToArray())
            {
                values.Add(value + suffix);
            }
        }

        private IEnumerable<string> GetTextVariants(INode node, DomSnapshot before)
        {
            if (!before.Nodes.TryGetValue(node, out var state)
                || !_allowedHyphenPositions.TryGetValue(node, out var positions)
                || positions.Count == 0)
            {
                return [before.Nodes[node].Value];
            }

            var variants = new HashSet<string>(StringComparer.Ordinal) { state.Value };
            foreach (var position in positions.OrderByDescending(position => position))
            {
                foreach (var value in variants.ToArray())
                {
                    if (position < value.Length && value[position] == '-')
                    {
                        variants.Add(value.Remove(position, 1));
                    }
                }
            }
            return variants;
        }

        private static string GetSubtreeText(DomSnapshot before, INode node)
        {
            if (!before.Nodes.TryGetValue(node, out var state))
            {
                return string.Empty;
            }

            return node is IText
                ? state.Value
                : string.Concat(state.Children.Select(child => GetSubtreeText(before, child)));
        }

        private void AllowMove(INode node, INode parent)
        {
            if (!_allowedParents.TryGetValue(node, out var parents))
            {
                parents = new HashSet<INode?>(ReferenceEqualityComparer.Instance);
                _allowedParents[node] = parents;
            }
            parents.Add(parent);
        }

        private static IElement? FindParagraph(DomSnapshot snapshot, INode node)
        {
            var parent = snapshot.Nodes.TryGetValue(node, out var state) ? state.Parent : null;
            while (parent is not null)
            {
                if (parent is IElement element
                    && string.Equals(element.LocalName, "p", StringComparison.OrdinalIgnoreCase))
                {
                    return element;
                }

                parent = snapshot.Nodes.TryGetValue(parent, out var parentState)
                    ? parentState.Parent
                    : null;
            }
            return null;
        }
    }
}
