using EpubFixer.Core.Correction.Models;
using EpubFixer.Core.Decision.Models;
using EpubFixer.Core.Detection.Models;

internal static class HyphenationCorrectionReporting
{
    public static HyphenationCorrectionSummary CreateSummary(
        IReadOnlyList<HyphenationDecision> decisions,
        IReadOnlyList<HyphenationCorrectionPlan> plans)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(plans);

        var autoFixCandidates = decisions
            .Where(decision => decision.DecisionKind == HyphenationDecisionKind.AutoFixCandidate)
            .ToArray();
        var documentCount = autoFixCandidates.Count(decision =>
            decision.Evidence.Candidate.DetectionKind == HyphenationDetectionKind.DocumentBoundary);
        var expectedPlanCount = autoFixCandidates.Length - documentCount;

        if (plans.Count != expectedPlanCount)
        {
            throw new InvalidOperationException(
                $"Correction plan count {plans.Count} does not match the expected count "
                + $"{expectedPlanCount} after excluding {documentCount} document-boundary decisions.");
        }

        var inlineCount = CountPlans(plans, HyphenationCorrectionKind.Inline);
        var textNodeCount = CountPlans(plans, HyphenationCorrectionKind.TextNode);
        var crossParagraphCount = CountPlans(plans, HyphenationCorrectionKind.CrossParagraph);

        if (inlineCount + textNodeCount + crossParagraphCount != plans.Count)
        {
            throw new InvalidOperationException("Correction plans contain an unsupported correction kind.");
        }

        return new HyphenationCorrectionSummary(
            autoFixCandidates.Length,
            plans.Count,
            inlineCount,
            crossParagraphCount,
            textNodeCount,
            documentCount);
    }

    public static void Print(TextWriter writer, HyphenationCorrectionSummary summary)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(summary);

        writer.WriteLine();
        writer.WriteLine("Hyphenation correction plans");
        writer.WriteLine();
        writer.WriteLine($"AutoFixCandidate: {summary.AutoFixCandidate}");
        writer.WriteLine($"Correction plans: {summary.CorrectionPlans}");
        writer.WriteLine($"Inline: {summary.Inline}");
        writer.WriteLine($"CrossParagraph: {summary.CrossParagraph}");
        writer.WriteLine($"TextNode: {summary.TextNode}");
        writer.WriteLine($"Document: {summary.Document}");

        var unplannedCount = summary.AutoFixCandidate - summary.CorrectionPlans;

        if (unplannedCount > 0)
        {
            writer.WriteLine(
                $"Unplanned AutoFixCandidate: {unplannedCount} "
                + $"(DocumentBoundary: {summary.Document})");
        }
    }

    private static int CountPlans(
        IReadOnlyList<HyphenationCorrectionPlan> plans,
        HyphenationCorrectionKind kind)
    {
        return plans.Count(plan => plan.CorrectionKind == kind);
    }
}

internal sealed record HyphenationCorrectionSummary(
    int AutoFixCandidate,
    int CorrectionPlans,
    int Inline,
    int CrossParagraph,
    int TextNode,
    int Document);
