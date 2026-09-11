using EpubFixer.Core.Decision.Models;
using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Mutation.Models;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Core.Mutation;

public sealed class OcrCorrectionMutationPlanner
{
    public OcrCorrectionMutationPlan Create(
        IReadOnlyList<OcrCorrectionDecision> decisions,
        LogicalTextStream stream)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(stream);
        var occurrences = decisions.Select(item => item.SourceOccurrence)
            .ToDictionary(item => item.Source.Candidate.LogicalStart, item => item);
        var mutations = new List<OcrCorrectionMutation>();
        var failures = new List<OcrMutationFailure>();

        foreach (var decision in decisions.Where(item => item.DecisionKind == OcrCorrectionDecisionKind.AutoFixCandidate))
        {
            var proposal = decision.SelectedProposal?.Proposal;
            if (proposal is null)
            {
                failures.Add(new(OcrMutationFailureReason.MissingSelectedProposal, "AutoFix decision has no selected proposal."));
                continue;
            }
            if (string.IsNullOrEmpty(proposal.ProposedText))
            {
                failures.Add(new(OcrMutationFailureReason.EmptyOrInvalidReplacement, "Replacement text is empty."));
                continue;
            }

            var consumed = proposal.ConsumedSources;
            if (consumed.Count == 0 || !occurrences.TryGetValue(decision.SourceOccurrence.Source.Candidate.LogicalStart, out var first))
            {
                failures.Add(new(OcrMutationFailureReason.MissingSourceLocation, "AutoFix source occurrence is missing."));
                continue;
            }
            var firstRange = LexicalRange(first.WorkingSource, first.PrefixPunctuation, first.SuffixPunctuation);
            var lastOccurrence = first;
            foreach (var source in consumed.Skip(1))
            {
                lastOccurrence = occurrences.Values.FirstOrDefault(item => item.Source.Candidate.LogicalStart == source.LogicalStart) ?? lastOccurrence;
            }
            var lastRange = LexicalRange(lastOccurrence.WorkingSource, lastOccurrence.PrefixPunctuation, lastOccurrence.SuffixPunctuation);
            var logicalStart = firstRange.Start;
            var logicalEnd = lastRange.Start + lastRange.Length;
            if (logicalStart < 0 || logicalEnd <= logicalStart || logicalEnd > stream.Text.Length)
            {
                failures.Add(new(OcrMutationFailureReason.UnsupportedSourceGeometry, "Source geometry is not a valid logical range."));
                continue;
            }
            var spans = new List<OcrMutationSourceSpan>();
            for (var index = logicalStart; index < logicalEnd; index++)
            {
                var location = stream.GetSourceLocationAt(index);
                var expected = stream.Text[index].ToString();
                if (spans.Count > 0 && spans[^1].DocumentPath == location.DocumentPath && spans[^1].TextNodeIndex == location.TextNodeIndex && spans[^1].Start + spans[^1].Length == location.Start)
                    spans[^1] = spans[^1] with { Length = spans[^1].Length + 1, ExpectedText = spans[^1].ExpectedText + expected };
                else spans.Add(new(location.DocumentPath, location.TextNodeIndex, location.Start, 1, expected));
            }
            mutations.Add(new(decision.SourceOccurrence.Source.Candidate.Document, logicalStart, logicalEnd - logicalStart, stream.Text[logicalStart..logicalEnd], proposal.ProposedText, decision, spans));
        }

        var normalized = new List<OcrCorrectionMutation>();
        foreach (var mutation in mutations.OrderBy(item => item.DocumentPath, StringComparer.Ordinal).ThenBy(item => item.LogicalStart))
        {
            var duplicate = normalized.FirstOrDefault(item => SameGeometry(item, mutation));
            if (duplicate is not null)
            {
                if (!string.Equals(duplicate.ReplacementText, mutation.ReplacementText, StringComparison.Ordinal))
                    failures.Add(new(OcrMutationFailureReason.ConflictingMutation, "Mutations have identical geometry but different replacements.", mutation));
                continue;
            }
            if (normalized.Any(item => string.Equals(item.DocumentPath, mutation.DocumentPath, StringComparison.Ordinal) && item.LogicalStart < mutation.LogicalStart + mutation.LogicalLength && mutation.LogicalStart < item.LogicalStart + item.LogicalLength))
                failures.Add(new(OcrMutationFailureReason.OverlappingMutation, "Mutations overlap in logical source geometry.", mutation));
            normalized.Add(mutation);
        }
        return new(stream.Text, normalized.AsReadOnly(), failures.AsReadOnly());
    }

    private static (int Start, int Length) LexicalRange(OcrWordCandidate source, string prefix, string suffix) =>
        (source.LogicalStart + prefix.Length, source.Text.Length - prefix.Length - suffix.Length);

    private static bool SameGeometry(OcrCorrectionMutation first, OcrCorrectionMutation second) =>
        string.Equals(first.DocumentPath, second.DocumentPath, StringComparison.Ordinal)
        && first.SourceSpans.SequenceEqual(second.SourceSpans);
}
