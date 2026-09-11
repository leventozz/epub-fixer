using System.Text;
using EpubFixer.Core.Decision;
using EpubFixer.Core.Decision.Models;
using EpubFixer.Core.Ocr.Models;

internal static class OcrDecisionReporting
{
    private static readonly string[] Targets =
    [
        "y1pranmışt1", "^iddetli", "ilgi-1 iydi", "liyatro", "akşaın", "ınetre", "J3arış",
        "Viya-ııa'da", "Avııstıırya'nın", "l<ilb'de", "Joana'mn", "Eiles", "Metis", "Akzente",
        "Stallburg", "Eine", "Stefan", "onlarm", "Lckrarlayan", "ger-^ .ckten", "ço-nıktu",
        "kepaze", "kepazesi", "iydi", "Auersberger'dir", "Schreker'e", "Fritz'inin"
    ];

    public static string SerializeMarkdown(OcrCorrectionDecisionAnalysisReport report)
    {
        var decisions = report.Decisions;
        var audit = BuildAudit(decisions);
        var builder = new StringBuilder();
        builder.AppendLine("# OCR Correction Decision V1.2");
        builder.AppendLine();
        builder.AppendLine($"- Total anomalies: {decisions.Count}");
        builder.AppendLine($"- With proposals: {decisions.Count(item => item.SourceOccurrence.Proposals.Count > 0)}");
        builder.AppendLine($"- Without proposals: {decisions.Count(item => item.SourceOccurrence.Proposals.Count == 0)}");
        foreach (var kind in Enum.GetValues<OcrCorrectionDecisionKind>())
            builder.AppendLine($"- {kind}: {decisions.Count(item => item.DecisionKind == kind)}");
        builder.AppendLine();

        builder.AppendLine("## V1.1 → V1.2 comparison");
        builder.AppendLine();
        builder.AppendLine("| Metric | V1 | V1.1 |");
        builder.AppendLine("|---|---:|---:|");
        AppendComparison(builder, "AutoFixCandidate", 123, decisions.Count(item => item.DecisionKind == OcrCorrectionDecisionKind.AutoFixCandidate));
        AppendComparison(builder, "Review", 381, decisions.Count(item => item.DecisionKind == OcrCorrectionDecisionKind.Review));
        AppendComparison(builder, "Defer", 389, decisions.Count(item => item.DecisionKind == OcrCorrectionDecisionKind.Defer));
        AppendComparison(builder, "HIGH AutoFix", 72, decisions.Count(item => item.DecisionKind == OcrCorrectionDecisionKind.AutoFixCandidate && item.SourceOccurrence.Source.Confidence == OcrConfidence.High));
        AppendComparison(builder, "MEDIUM AutoFix", 38, decisions.Count(item => item.DecisionKind == OcrCorrectionDecisionKind.AutoFixCandidate && item.SourceOccurrence.Source.Confidence == OcrConfidence.Medium));
        AppendComparison(builder, "EvidenceOnly AutoFix", 13, decisions.Count(item => item.DecisionKind == OcrCorrectionDecisionKind.AutoFixCandidate && item.SourceOccurrence.Source.Confidence == OcrConfidence.EvidenceOnly));
        foreach (var rule in new[] { OcrCorrectionDecisionReason.DirectStructuralRepair, OcrCorrectionDecisionReason.StructuralLexiconRepair, OcrCorrectionDecisionReason.AdjacentCompositeRepair, OcrCorrectionDecisionReason.EvidenceOnlyDominantLexicon, OcrCorrectionDecisionReason.SameApostropheBase })
        {
            var oldCount = rule switch { OcrCorrectionDecisionReason.DirectStructuralRepair => 15, OcrCorrectionDecisionReason.StructuralLexiconRepair => 97, OcrCorrectionDecisionReason.AdjacentCompositeRepair => 2, OcrCorrectionDecisionReason.EvidenceOnlyDominantLexicon => 9, _ => 0 };
            AppendComparison(builder, rule.ToString(), oldCount, decisions.Count(item => item.DecisionKind == OcrCorrectionDecisionKind.AutoFixCandidate && item.DecisionReasons.Contains(rule)));
        }
        builder.AppendLine();

        builder.AppendLine("## Confidence distribution");
        builder.AppendLine();
        foreach (var confidence in Enum.GetValues<OcrConfidence>())
            builder.AppendLine($"- {confidence} AutoFix: {decisions.Count(item => item.DecisionKind == OcrCorrectionDecisionKind.AutoFixCandidate && item.SourceOccurrence.Source.Confidence == confidence)}");
        builder.AppendLine();

        builder.AppendLine("## AutoFix rules");
        builder.AppendLine();
        foreach (var rule in audit.Rules)
            builder.AppendLine($"- {rule.Rule}: {rule.Rows.Count} (rows: {string.Join(", ", rule.Rows)})");
        builder.AppendLine();

        builder.AppendLine("## AutoFix Audit Risk Groups");
        builder.AppendLine();
        foreach (var group in audit.RiskGroups)
            builder.AppendLine($"- {group.Name}: {group.Count}");
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
        builder.AppendLine($"- TitleCase Structural Ambiguity Review: {decisions.Count(item => item.DecisionReasons.Contains(OcrCorrectionDecisionReason.ProperNameStructuralAmbiguity))}");
        builder.AppendLine($"- AutoFix apostrophe same-base: {decisions.Count(item => item.DecisionKind == OcrCorrectionDecisionKind.AutoFixCandidate && item.DecisionReasons.Contains(OcrCorrectionDecisionReason.SameApostropheBase))}");
        builder.AppendLine($"- ConsumedByCompositeRepair count: {decisions.Count(item => item.DecisionReasons.Contains(OcrCorrectionDecisionReason.ConsumedByCompositeRepair))}");
        builder.AppendLine($"- EvidenceOnly rejected by short-token guard: {decisions.Count(item => item.DecisionReasons.Contains(OcrCorrectionDecisionReason.EvidenceOnlyTooShort))}");
        builder.AppendLine($"- EvidenceOnly rejected by edit-distance guard: {decisions.Count(item => item.DecisionReasons.Contains(OcrCorrectionDecisionReason.EvidenceOnlyEditDistanceTooHigh))}");
        builder.AppendLine($"- SameApostropheBase downgraded count: {decisions.Count(item => item.DecisionReasons.Contains(OcrCorrectionDecisionReason.SameApostropheBaseReviewOnly))}");
        builder.AppendLine($"- AutoFix selected proposal was not generator rank 1: {decisions.Count(item => item.DecisionKind == OcrCorrectionDecisionKind.AutoFixCandidate && item.SelectedProposal!.Proposal.ProposalRank != 1)}");
        builder.AppendLine();

        builder.AppendLine("## All AutoFix Candidates");
        builder.AppendLine();
        builder.AppendLine("| # | Source | SelectedProposal | SourceConfidence | DecisionRule | DecisionReasons | RiskFlags | TRmorph | BookFrequency | EditDistance | Cost | StructuralSteps | Partial | SourceSpans | CaseCompatible | SameApostropheBase | GeneratorRank |");
        builder.AppendLine("|---:|---|---|---|---|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var row in audit.Rows)
            builder.AppendLine($"| {row.Number} | {Cell(row.Source)} | {Cell(row.SelectedProposal)} | {row.SourceConfidence} | {row.DecisionRule} | {Cell(row.DecisionReasons)} | {Cell(row.RiskFlags)} | {row.TrMorph} | {row.BookFrequency} | {row.EditDistance} | {row.Cost} | {row.StructuralSteps} | {row.Partial} | {row.SourceSpans} | {row.CaseCompatible} | {row.SameApostropheBase} | {row.GeneratorRank} |");
        builder.AppendLine();

        AppendTitleCaseStructuralAudit(builder, decisions);

        builder.AppendLine("## Decision Regression Examples");
        builder.AppendLine();
        foreach (var query in Targets)
            AppendTarget(builder, query, report);
        return builder.ToString();
    }

    private static void AppendTitleCaseStructuralAudit(
        StringBuilder builder,
        IReadOnlyList<OcrCorrectionDecision> decisions)
    {
        builder.AppendLine("## TitleCase Structural AutoFix Audit");
        builder.AppendLine();
        var rows = decisions
            .Where(IsTitleCaseStructuralAudit)
            .OrderBy(item => item.SourceOccurrence.Source.Candidate.LogicalStart)
            .ToArray();
        builder.AppendLine($"Baseline TitleCase structural AutoFix occurrences: {rows.Length}");
        builder.AppendLine();
        foreach (var decision in rows)
        {
            var source = decision.SourceOccurrence.Source;
            var provisional = decision.ProvisionalSelectedProposal ?? decision.SelectedProposal;
            builder.AppendLine($"### `{Cell(decision.SourceOccurrence.WorkingSource.Text)}`");
            builder.AppendLine();
            builder.AppendLine($"- Source: {Cell(source.Candidate.Text)}");
            builder.AppendLine($"- Context: {Cell($"{source.Candidate.ContextBefore}⟦{source.Candidate.Text}⟧{source.Candidate.ContextAfter}")}");
            builder.AppendLine($"- SourceConfidence: {source.Confidence}");
            builder.AppendLine($"- SourceReasons: {Cell(string.Join(", ", source.DetectionReasons))}");
            builder.AppendLine($"- V1.1 provisional decision rule: {StructuralRule(decision)}");
            builder.AppendLine($"- V1.2 final decision: {decision.DecisionKind}");
            builder.AppendLine($"- V1.2 decision reasons: {Cell(string.Join(", ", decision.DecisionReasons))}");
            if (provisional is not null && provisional.Proposal.BookFrequency == 0)
                builder.AppendLine("- Audit flags: TitleCaseZeroFrequency");
            AppendProposal(builder, "V1.1 provisional selected proposal", provisional);
            builder.AppendLine("- All case-compatible competing proposals:");
            foreach (var competitor in decision.SourceOccurrence.Proposals
                .Select(proposal => new OcrCorrectionProposalSafetyEvidence(
                    proposal,
                    OcrCorrectionDecisionEvaluator.GetCasePattern(proposal.ProposedText),
                    IsCaseCompatibleForAudit(decision, proposal),
                    SameApostropheBaseForAudit(source.Candidate.Text, proposal.ProposedText)))
                .Where(item => item.CaseCompatible && (provisional is null || !ReferenceEquals(item.Proposal, provisional.Proposal))))
            {
                AppendProposal(builder, "  - Proposal", competitor);
            }
            builder.AppendLine();
        }
    }

    private static bool IsTitleCaseStructuralAudit(OcrCorrectionDecision decision) =>
        decision.SourceCase == OcrCasePattern.TitleCase
        && (decision.DecisionReasons.Contains(OcrCorrectionDecisionReason.DirectStructuralRepair)
            || decision.DecisionReasons.Contains(OcrCorrectionDecisionReason.StructuralLexiconRepair)
            || decision.DecisionReasons.Contains(OcrCorrectionDecisionReason.AdjacentCompositeRepair)
            || decision.DecisionReasons.Contains(OcrCorrectionDecisionReason.ProperNameStructuralAmbiguity));

    private static string StructuralRule(OcrCorrectionDecision decision) =>
        decision.DecisionReasons.FirstOrDefault(reason => reason is
            OcrCorrectionDecisionReason.DirectStructuralRepair
            or OcrCorrectionDecisionReason.StructuralLexiconRepair
            or OcrCorrectionDecisionReason.AdjacentCompositeRepair
            or OcrCorrectionDecisionReason.ProperNameStructuralAmbiguity).ToString();

    private static bool IsCaseCompatibleForAudit(OcrCorrectionDecision decision, OcrCorrectionCandidate proposal)
    {
        var sourceCase = OcrCorrectionDecisionEvaluator.GetCasePattern(decision.SourceOccurrence.WorkingSource.Text);
        return sourceCase == OcrCorrectionDecisionEvaluator.GetCasePattern(proposal.ProposedText);
    }

    private static bool SameApostropheBaseForAudit(string source, string proposal)
    {
        var sourceIndex = source.IndexOfAny(['\'', '’']);
        var proposalIndex = proposal.IndexOfAny(['\'', '’']);
        return sourceIndex > 0 && proposalIndex > 0
            && string.Equals(source[..sourceIndex], proposal[..proposalIndex], StringComparison.Ordinal);
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
        builder.AppendLine($"  - Structural steps: {proposal.StructuralTransformationCount}");
        builder.AppendLine($"  - Generator rank: {proposal.ProposalRank}");
        builder.AppendLine($"  - Generation reasons: {Cell(string.Join(", ", proposal.GenerationReasons))}");
        builder.AppendLine($"  - Partial: {proposal.IsPartialStructuralRepair}");
        builder.AppendLine($"  - Source spans: {proposal.SourceSpanCount}");
        builder.AppendLine($"  - Case compatible: {evidence.CaseCompatible}");
        builder.AppendLine($"  - Same apostrophe base: {evidence.SameApostropheBase}");
    }

    private static string Cell(string value) => value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");

    private static void AppendComparison(StringBuilder builder, string metric, int v1, int v11) =>
        builder.AppendLine($"| {metric} | {v1} | {v11} |");

    private static Audit BuildAudit(IReadOnlyList<OcrCorrectionDecision> decisions)
    {
        var auto = decisions.Where(item => item.DecisionKind == OcrCorrectionDecisionKind.AutoFixCandidate).ToArray();
        var ordered = auto.OrderBy(item => RiskRank(item)).ThenBy(item => item.SourceOccurrence.Source.Candidate.LogicalStart).ToArray();
        var rows = ordered.Select((decision, index) => AuditRow.Create(index + 1, decision)).ToArray();
        var rules = new[]
        {
            OcrCorrectionDecisionReason.DirectStructuralRepair,
            OcrCorrectionDecisionReason.StructuralLexiconRepair,
            OcrCorrectionDecisionReason.AdjacentCompositeRepair,
            OcrCorrectionDecisionReason.EvidenceOnlyDominantLexicon,
            OcrCorrectionDecisionReason.SameApostropheBase
        }.Select(rule => new AuditRule(rule, rows.Where(row => row.DecisionReasons.Contains(rule.ToString())).Select(row => row.Number).ToArray())).ToArray();
        var riskNames = new[] { "TRmorphInvalid", "EvidenceOnly", "TitleCase", "NonTopRank", "BookFrequencyZero", "TitleCaseZeroFrequency", "MultiSource", "SameApostropheBase" };
        var risks = riskNames.Select(name => new AuditRiskGroup(name, rows.Count(row => row.RiskFlags.Split(',', StringSplitOptions.RemoveEmptyEntries).Contains(name, StringComparer.Ordinal)))).ToArray();
        return new Audit(rows, rules, risks);
    }

    private static int RiskRank(OcrCorrectionDecision decision)
    {
        var proposal = decision.SelectedProposal!.Proposal;
        if (!proposal.TrMorphValid) return 0;
        if (decision.SourceOccurrence.Source.Confidence == OcrConfidence.EvidenceOnly) return 1;
        if (decision.SourceCase == OcrCasePattern.TitleCase) return 2;
        if (proposal.ProposalRank != 1) return 3;
        if (proposal.BookFrequency == 0) return 4;
        return 5;
    }

    private sealed record Audit(IReadOnlyList<AuditRow> Rows, IReadOnlyList<AuditRule> Rules, IReadOnlyList<AuditRiskGroup> RiskGroups);
    private sealed record AuditRule(OcrCorrectionDecisionReason Rule, IReadOnlyList<int> Rows);
    private sealed record AuditRiskGroup(string Name, int Count);
    private sealed record AuditRow(int Number, string Source, string SelectedProposal, OcrConfidence SourceConfidence, string DecisionRule, string DecisionReasons, string RiskFlags, bool TrMorph, int BookFrequency, int EditDistance, int Cost, int StructuralSteps, bool Partial, int SourceSpans, bool CaseCompatible, bool SameApostropheBase, int GeneratorRank)
    {
        public static AuditRow Create(int number, OcrCorrectionDecision decision)
        {
            var selected = decision.SelectedProposal!;
            var proposal = selected.Proposal;
            var flags = new List<string>();
            if (!proposal.TrMorphValid) flags.Add("TRmorphInvalid");
            if (decision.SourceOccurrence.Source.Confidence == OcrConfidence.EvidenceOnly) flags.Add("EvidenceOnly");
            if (decision.SourceCase == OcrCasePattern.TitleCase) flags.Add("TitleCase");
            if (proposal.ProposalRank != 1) flags.Add("NonTopRank");
            if (proposal.BookFrequency == 0) flags.Add("BookFrequencyZero");
            if (decision.SourceCase == OcrCasePattern.TitleCase && proposal.BookFrequency == 0) flags.Add("TitleCaseZeroFrequency");
            if (proposal.ConsumesMultipleOccurrences) flags.Add("MultiSource");
            if (selected.SameApostropheBase) flags.Add("SameApostropheBase");
            var rule = decision.DecisionReasons.FirstOrDefault(item => item is
                OcrCorrectionDecisionReason.DirectStructuralRepair or OcrCorrectionDecisionReason.StructuralLexiconRepair or
                OcrCorrectionDecisionReason.AdjacentCompositeRepair or OcrCorrectionDecisionReason.EvidenceOnlyDominantLexicon or
                OcrCorrectionDecisionReason.SameApostropheBase).ToString();
            return new(number, decision.SourceOccurrence.WorkingSource.Text, proposal.ProposedText, decision.SourceOccurrence.Source.Confidence,
                rule, string.Join(", ", decision.DecisionReasons), string.Join(",", flags), proposal.TrMorphValid, proposal.BookFrequency,
                proposal.EditDistance, proposal.GenerationCost, proposal.StructuralTransformationCount, proposal.IsPartialStructuralRepair,
                proposal.SourceSpanCount, selected.CaseCompatible, selected.SameApostropheBase, proposal.ProposalRank);
        }
    }
}
