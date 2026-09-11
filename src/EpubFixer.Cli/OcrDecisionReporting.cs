using System.Text;
using EpubFixer.Core.Decision.Models;
using EpubFixer.Core.Ocr.Models;

internal static class OcrDecisionReporting
{
    private static readonly string[] Targets =
    [
        "y1pranmışt1", "^iddetli", "ilgi-1 iydi", "liyatro", "akşaın", "ınetre", "J3arış",
        "Viya-ııa'da", "Avııstıırya'nın", "l<ilb'de", "Joana'mn", "Eiles", "Metis", "Akzente",
        "Stallburg", "Eine", "Stefan", "onlarm", "Lckrarlayan", "ger-^ .ckten", "ço-nıktu"
    ];

    public static string SerializeMarkdown(OcrCorrectionDecisionAnalysisReport report)
    {
        var decisions = report.Decisions;
        var proposals = decisions.SelectMany(item => item.SourceOccurrence.Proposals).ToArray();
        var builder = new StringBuilder();
        builder.AppendLine("# OCR Correction Decision V1");
        builder.AppendLine();
        builder.AppendLine($"- Total anomalies: {decisions.Count}");
        builder.AppendLine($"- With proposals: {decisions.Count(item => item.SourceOccurrence.Proposals.Count > 0)}");
        builder.AppendLine($"- Without proposals: {decisions.Count(item => item.SourceOccurrence.Proposals.Count == 0)}");
        foreach (var kind in Enum.GetValues<OcrCorrectionDecisionKind>())
            builder.AppendLine($"- {kind}: {decisions.Count(item => item.DecisionKind == kind)}");
        builder.AppendLine();

        builder.AppendLine("## Confidence distribution");
        builder.AppendLine();
        foreach (var confidence in Enum.GetValues<OcrConfidence>())
            builder.AppendLine($"- {confidence} AutoFix: {decisions.Count(item => item.DecisionKind == OcrCorrectionDecisionKind.AutoFixCandidate && item.SourceOccurrence.Source.Confidence == confidence)}");
        builder.AppendLine();

        builder.AppendLine("## AutoFix rules");
        builder.AppendLine();
        foreach (var reason in new[]
        {
            OcrCorrectionDecisionReason.DirectStructuralRepair,
            OcrCorrectionDecisionReason.StructuralLexiconRepair,
            OcrCorrectionDecisionReason.AdjacentCompositeRepair,
            OcrCorrectionDecisionReason.EvidenceOnlyDominantLexicon,
            OcrCorrectionDecisionReason.SameApostropheBase
        })
            builder.AppendLine($"- {reason}: {decisions.Count(item => item.DecisionKind == OcrCorrectionDecisionKind.AutoFixCandidate && item.DecisionReasons.Contains(reason))}");
        builder.AppendLine();

        builder.AppendLine("## Review and Defer reasons");
        builder.AppendLine();
        foreach (var reason in Enum.GetValues<OcrCorrectionDecisionReason>())
        {
            var count = decisions.Count(item => item.DecisionKind != OcrCorrectionDecisionKind.AutoFixCandidate && item.DecisionReasons.Contains(reason));
            if (count > 0) builder.AppendLine($"- {reason}: {count}");
        }
        builder.AppendLine();

        var selected = decisions.Where(item => item.DecisionKind == OcrCorrectionDecisionKind.AutoFixCandidate)
            .Select(item => item.SelectedProposal!).ToArray();
        builder.AppendLine("## Safety metrics");
        builder.AppendLine();
        builder.AppendLine($"- AutoFix selected proposal TRmorph-valid: {selected.Count(item => item.Proposal.TrMorphValid)}");
        builder.AppendLine($"- AutoFix selected proposal BookFrequency=0: {selected.Count(item => item.Proposal.BookFrequency == 0)}");
        builder.AppendLine($"- AutoFix using multi-source span: {selected.Count(item => item.Proposal.ConsumesMultipleOccurrences)}");
        builder.AppendLine($"- AutoFix EvidenceOnly: {decisions.Count(item => item.DecisionKind == OcrCorrectionDecisionKind.AutoFixCandidate && item.SourceOccurrence.Source.Confidence == OcrConfidence.EvidenceOnly)}");
        builder.AppendLine($"- AutoFix TitleCase: {decisions.Count(item => item.DecisionKind == OcrCorrectionDecisionKind.AutoFixCandidate && item.SourceCase == OcrCasePattern.TitleCase)}");
        builder.AppendLine($"- AutoFix apostrophe same-base: {decisions.Count(item => item.DecisionKind == OcrCorrectionDecisionKind.AutoFixCandidate && item.DecisionReasons.Contains(OcrCorrectionDecisionReason.SameApostropheBase))}");
        builder.AppendLine($"- AutoFix selected proposal was not generator rank 1: {decisions.Count(item => item.DecisionKind == OcrCorrectionDecisionKind.AutoFixCandidate && item.SelectedProposal!.Proposal.ProposalRank != 1)}");
        builder.AppendLine();

        builder.AppendLine("## Decision Regression Examples");
        builder.AppendLine();
        foreach (var query in Targets)
            AppendTarget(builder, query, report);
        return builder.ToString();
    }

    private static void AppendTarget(StringBuilder builder, string query, OcrCorrectionDecisionAnalysisReport report)
    {
        var sourceTarget = report.SourceAnalysis.TargetRegressionExamples.FirstOrDefault(item => item.Query == query);
        var decision = report.Decisions.FirstOrDefault(item =>
            string.Equals(item.SourceOccurrence.Source.Candidate.Text, query, StringComparison.Ordinal)
            || string.Equals(item.SourceOccurrence.WorkingSource.Text, query, StringComparison.Ordinal)
            || sourceTarget is not null && sourceTarget.Proposals.Any(proposal => item.SourceOccurrence.Proposals.Contains(proposal)));
        builder.AppendLine($"### `{Cell(query)}`");
        builder.AppendLine();
        if (decision is null)
        {
            builder.AppendLine("- Not found");
            builder.AppendLine();
            return;
        }
        var source = decision.SourceOccurrence.Source;
        builder.AppendLine($"- Source: {Cell(source.Candidate.Text)}");
        builder.AppendLine($"- Source confidence: {source.Confidence}");
        builder.AppendLine($"- Source reasons: {Cell(string.Join(", ", source.DetectionReasons))}");
        builder.AppendLine($"- Selected decision: {decision.DecisionKind}");
        builder.AppendLine($"- Decision reasons: {Cell(string.Join(", ", decision.DecisionReasons))}");
        AppendProposal(builder, "Selected proposal", decision.SelectedProposal);
        builder.AppendLine("- Competing safe proposals:");
        foreach (var proposal in decision.CompetingProposals)
            AppendProposal(builder, "  - Proposal", proposal);
        builder.AppendLine();
    }

    private static void AppendProposal(StringBuilder builder, string label, OcrCorrectionProposalSafetyEvidence? evidence)
    {
        if (evidence is null)
        {
            builder.AppendLine($"- {label}: none");
            return;
        }
        var proposal = evidence.Proposal;
        builder.AppendLine($"- {label}: {Cell(proposal.ProposedText)}");
        builder.AppendLine($"  - TRmorph: {proposal.TrMorphValid}");
        builder.AppendLine($"  - BookFrequency: {proposal.BookFrequency}");
        builder.AppendLine($"  - EditDistance: {proposal.EditDistance}");
        builder.AppendLine($"  - Cost: {proposal.GenerationCost}");
        builder.AppendLine($"  - Generation reasons: {Cell(string.Join(", ", proposal.GenerationReasons))}");
        builder.AppendLine($"  - Partial: {proposal.IsPartialStructuralRepair}");
        builder.AppendLine($"  - Source spans: {proposal.SourceSpanCount}");
        builder.AppendLine($"  - Case compatible: {evidence.CaseCompatible}");
        builder.AppendLine($"  - Same apostrophe base: {evidence.SameApostropheBase}");
    }

    private static string Cell(string value) => value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
}
