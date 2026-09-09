using System.Text;
using EpubFixer.Core.Ocr.Models;

internal static class OcrCorrectionAnalysisReporting
{
    public static string SerializeMarkdown(OcrCorrectionAnalysisReport report)
    {
        var occurrences = report.Occurrences;
        var withProposal = occurrences.Count(item => item.Proposals.Count > 0);
        var proposals = occurrences.SelectMany(item => item.Proposals).ToArray();
        var builder = new StringBuilder();
        builder.AppendLine("# OCR Correction Candidate Generation V1.1");
        builder.AppendLine();
        builder.AppendLine($"- Total OCR anomalies: {occurrences.Count}");
        builder.AppendLine($"- With >=1 proposal: {withProposal}");
        builder.AppendLine($"- Without proposal: {occurrences.Count - withProposal}");
        foreach (var confidence in Enum.GetValues<OcrConfidence>())
            builder.AppendLine($"- {confidence} with proposal: {Coverage(occurrences, confidence)}");
        builder.AppendLine();
        builder.AppendLine("## V1 → V1.1 proposal coverage");
        builder.AppendLine();
        builder.AppendLine("| Metric | V1 | V1.1 | Delta |");
        builder.AppendLine("|---|---:|---:|---:|");
        AppendComparison(builder, "With proposal", 640, withProposal);
        AppendComparison(builder, "Without proposal", 253, occurrences.Count - withProposal);
        AppendComparison(builder, "HIGH coverage", 137, Coverage(occurrences, OcrConfidence.High));
        AppendComparison(builder, "MEDIUM coverage", 80, Coverage(occurrences, OcrConfidence.Medium));
        AppendComparison(builder, "EvidenceOnly coverage", 423, Coverage(occurrences, OcrConfidence.EvidenceOnly));
        builder.AppendLine();
        builder.AppendLine("## Proposal count distribution");
        builder.AppendLine();
        builder.AppendLine($"- 0: {occurrences.Count(item => item.Proposals.Count == 0)}");
        builder.AppendLine($"- 1: {occurrences.Count(item => item.Proposals.Count == 1)}");
        builder.AppendLine($"- 2-5: {occurrences.Count(item => item.Proposals.Count is >= 2 and <= 5)}");
        builder.AppendLine($"- 6-10: {occurrences.Count(item => item.Proposals.Count is >= 6 and <= 10)}");
        builder.AppendLine();
        builder.AppendLine("## Proposal metrics");
        builder.AppendLine();
        builder.AppendLine($"- TRmorph-valid proposals: {proposals.Count(item => item.TrMorphValid)}");
        builder.AppendLine($"- BookFrequency > 0 proposals: {proposals.Count(item => item.BookFrequency > 0)}");
        builder.AppendLine($"- Exactly one proposal + TRmorphValid + BookFrequency > 0: {occurrences.Count(item => item.Proposals.Count == 1 && item.Proposals[0].TrMorphValid && item.Proposals[0].BookFrequency > 0)}");
        builder.AppendLine($"- Top-ranked + TRmorphValid + BookFrequency >= 2 + EditDistance <= 2: {occurrences.Count(item => item.Proposals.FirstOrDefault() is { TrMorphValid: true, BookFrequency: >= 2, EditDistance: <= 2 })}");
        builder.AppendLine($"- Multi-step structural proposals count: {proposals.Count(item => item.StructuralTransformationCount > 1)}");
        builder.AppendLine($"- TRmorph-valid proposals produced only after multi-step composition: {proposals.Count(item => item.StructuralTransformationCount > 1 && item.TrMorphValid)}");
        builder.AppendLine($"- AdjacentFragmentComposition proposal count: {proposals.Count(IsAdjacent)}");
        builder.AppendLine($"- TRmorph-valid adjacent composite proposal count: {proposals.Count(item => IsAdjacent(item) && item.TrMorphValid)}");
        builder.AppendLine($"- EvidenceOnly candidates suppressed from RareInBook because BaseFormFrequency > 1: {report.SourceAnalysis.RareInBookSuppressedOccurrences.Count}");
        builder.AppendLine($"- Unique apostrophe base forms: {report.SourceAnalysis.UniqueApostropheBaseForms}");
        builder.AppendLine();
        builder.AppendLine("## Generation reason counts");
        builder.AppendLine();
        foreach (var reason in Enum.GetValues<OcrCorrectionGenerationReason>())
            builder.AppendLine($"- {reason}: {proposals.Count(item => item.GenerationReasons.Contains(reason))}");
        builder.AppendLine();
        builder.AppendLine("## First 100 occurrences");
        builder.AppendLine();
        foreach (var (occurrence, index) in occurrences.Take(100).Select((item, index) => (item, index + 1)))
            AppendOccurrence(builder, occurrence, index);

        builder.AppendLine("## Target Regression Examples");
        builder.AppendLine();
        foreach (var target in report.TargetRegressionExamples)
            AppendTarget(builder, target);
        return builder.ToString();
    }

    private static void AppendOccurrence(StringBuilder builder, OcrCorrectionOccurrence occurrence, int index)
    {
        var source = occurrence.Source;
        var candidate = source.Candidate;
        builder.AppendLine($"### {index}. `{Cell(candidate.Text)}` ({source.Confidence})");
        builder.AppendLine();
        builder.AppendLine($"- Source: {Cell(candidate.Text)}");
        builder.AppendLine($"- Working source: {Cell(occurrence.WorkingSource.Text)}");
        builder.AppendLine($"- Lexical core: {Cell(occurrence.LexicalCore)}");
        builder.AppendLine($"- Prefix punctuation: {Cell(occurrence.PrefixPunctuation)}");
        builder.AppendLine($"- Suffix punctuation: {Cell(occurrence.SuffixPunctuation)}");
        builder.AppendLine($"- Document/context: {Cell(candidate.Document)} — {Cell($"{candidate.ContextBefore}⟦{candidate.Text}⟧{candidate.ContextAfter}")}");
        builder.AppendLine($"- Source reasons: {Cell(string.Join(", ", source.DetectionReasons))}");
        builder.AppendLine($"- Exact BookFrequency: {source.BookFrequency}");
        builder.AppendLine($"- BaseForm: {Cell(source.BaseForm)}");
        builder.AppendLine($"- BaseFormFrequency: {source.BaseFormFrequency}");
        builder.AppendLine($"- Proposal count: {occurrence.Proposals.Count}");
        builder.AppendLine();
        AppendProposalTable(builder, occurrence.Proposals);
    }

    private static void AppendTarget(StringBuilder builder, OcrTargetRegressionExample target)
    {
        builder.AppendLine($"### `{Cell(target.Query)}`");
        builder.AppendLine();
        if (!target.Found)
        {
            builder.AppendLine("Not found in current post-hyphenation logical stream");
            builder.AppendLine();
            return;
        }
        builder.AppendLine($"- Source: {Cell(target.Source)}");
        builder.AppendLine($"- Context: {Cell(target.Context)}");
        builder.AppendLine($"- Source reasons: {Cell(string.Join(", ", target.SourceReasons))}");
        builder.AppendLine($"- Exact BookFrequency: {target.ExactBookFrequency}");
        builder.AppendLine($"- BaseForm: {Cell(target.BaseForm)}");
        builder.AppendLine($"- BaseFormFrequency: {target.BaseFormFrequency}");
        builder.AppendLine($"- Proposal count: {target.Proposals.Count}");
        builder.AppendLine();
        AppendProposalTable(builder, target.Proposals);
    }

    private static void AppendProposalTable(StringBuilder builder, IReadOnlyList<OcrCorrectionCandidate> proposals)
    {
        builder.AppendLine("| Rank | Proposal | Generation reasons | Distance | Cost | Structural steps | Source spans | Partial | TRmorph | BookFrequency |");
        builder.AppendLine("|---:|---|---|---:|---:|---:|---:|:---:|:---:|---:|");
        foreach (var proposal in proposals)
            builder.AppendLine($"| {proposal.ProposalRank} | {Cell(proposal.ProposedText)} | {Cell(string.Join(", ", proposal.GenerationReasons))} | {proposal.EditDistance} | {proposal.GenerationCost} | {proposal.StructuralTransformationCount} | {proposal.SourceSpanCount} | {proposal.IsPartialStructuralRepair} | {proposal.TrMorphValid} | {proposal.BookFrequency} |");
        builder.AppendLine();
    }

    private static int Coverage(IEnumerable<OcrCorrectionOccurrence> occurrences, OcrConfidence confidence) =>
        occurrences.Count(item => item.Source.Confidence == confidence && item.Proposals.Count > 0);

    private static bool IsAdjacent(OcrCorrectionCandidate candidate) =>
        candidate.GenerationReasons.Contains(OcrCorrectionGenerationReason.AdjacentFragmentComposition);

    private static void AppendComparison(StringBuilder builder, string metric, int baseline, int current) =>
        builder.AppendLine($"| {metric} | {baseline} | {current} | {current - baseline:+#;-#;0} |");

    private static string Cell(string value) => value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
}
