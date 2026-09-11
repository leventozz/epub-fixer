using AngleSharp.Dom;
using EpubFixer.Core.Epub;
using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Mutation.Models;

namespace EpubFixer.Core.Mutation;

public sealed class OcrCorrectionMutationApplier
{
    public OcrMutationResult Apply(EpubPackage package, OcrCorrectionMutationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.IsValid)
            return FailureResult(plan, plan.Failures);

        var documents = package.SpineDocuments.ToDictionary(item => item.Path, StringComparer.Ordinal);
        var failures = new List<OcrMutationFailure>();
        var edits = new List<Edit>();
        foreach (var mutation in plan.Mutations)
        {
            if (!documents.TryGetValue(mutation.DocumentPath, out var document))
            {
                failures.Add(new(OcrMutationFailureReason.DocumentNotFound, $"Document '{mutation.DocumentPath}' was not found.", mutation));
                continue;
            }
            foreach (var span in mutation.SourceSpans)
            {
                var node = EnumerateTextNodes(document.Document.Body!).ElementAtOrDefault(span.TextNodeIndex);
                if (node is null)
                {
                    failures.Add(new(OcrMutationFailureReason.MissingSourceLocation, "Mapped text node was not found.", mutation));
                    continue;
                }
                if (span.Start < 0 || span.Length <= 0 || span.Start + span.Length > node.Data.Length)
                {
                    failures.Add(new(OcrMutationFailureReason.InvalidSourceRange, "Mapped source range is invalid.", mutation));
                    continue;
                }
                var actual = node.Data.Substring(span.Start, span.Length);
                if (!string.Equals(actual, span.ExpectedText, StringComparison.Ordinal))
                {
                    failures.Add(new(OcrMutationFailureReason.SourceTextMismatch, $"Expected '{span.ExpectedText}' but found '{actual}'.", mutation));
                    continue;
                }
                edits.Add(new(node, span.Start, span.Length, ReferenceEquals(span, mutation.SourceSpans[0]) ? mutation.ReplacementText : string.Empty));
            }
        }
        if (failures.Count > 0)
            return FailureResult(plan, failures);

        var originals = new Dictionary<IText, string>(ReferenceEqualityComparer.Instance);
        foreach (var group in edits.GroupBy(item => item.Node))
            originals[group.Key] = group.Key.Data;
        try
        {
            foreach (var group in edits.GroupBy(item => item.Node))
                foreach (var edit in group.OrderByDescending(item => item.Start))
                    edit.Node.Data = edit.Node.Data.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.Replacement);

            var rebuilt = LogicalTextStreamBuilder.Build(package.SpineDocuments);
            var expected = plan.SnapshotText;
            foreach (var mutation in plan.Mutations.OrderByDescending(item => item.LogicalStart))
                expected = expected.Remove(mutation.LogicalStart, mutation.LogicalLength).Insert(mutation.LogicalStart, mutation.ReplacementText);
            if (!string.Equals(expected, rebuilt.Text, StringComparison.Ordinal))
            {
                foreach (var original in originals) original.Key.Data = original.Value;
                return FailureResult(plan, [new(OcrMutationFailureReason.UnexpectedTextChange, "Rebuilt logical text differs from planned result.")]);
            }
        }
        catch (Exception exception)
        {
            foreach (var original in originals) original.Key.Data = original.Value;
            return FailureResult(plan, [new(OcrMutationFailureReason.ReplacementFailed, exception.Message)]);
        }

        var changedDocuments = plan.Mutations.Select(item => item.DocumentPath).Distinct(StringComparer.Ordinal).Count();
        return new(plan.Mutations.Count, plan.Mutations.Count, [], plan.Mutations.Count(item => !item.IsMultiSource), plan.Mutations.Count(item => item.IsMultiSource), changedDocuments, 0, 0, 0)
        { AppliedMutations = plan.Mutations };
    }

    private static OcrMutationResult FailureResult(OcrCorrectionMutationPlan plan, IReadOnlyList<OcrMutationFailure> failures) =>
        new(plan.Mutations.Count, 0, failures, plan.Mutations.Count(item => !item.IsMultiSource), plan.Mutations.Count(item => item.IsMultiSource), 0, 0, 0, failures.Count(item => item.Reason == OcrMutationFailureReason.UnexpectedTextChange));

    private static IEnumerable<IText> EnumerateTextNodes(INode node)
    {
        foreach (var child in node.ChildNodes)
        {
            if (child is IText text) yield return text;
            foreach (var descendant in EnumerateTextNodes(child)) yield return descendant;
        }
    }

    private sealed record Edit(IText Node, int Start, int Length, string Replacement);
}
