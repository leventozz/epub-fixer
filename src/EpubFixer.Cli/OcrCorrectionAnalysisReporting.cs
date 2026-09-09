using System.Text;
using EpubFixer.Core.Ocr.Models;

internal static class OcrCorrectionAnalysisReporting
{
    public static string SerializeMarkdown(OcrCorrectionAnalysisReport report)
    {
        var occurrences = report.Occurrences;
        var withProposal = occurrences.Count(item => item.Proposals.Count > 0);
        var builder = new StringBuilder();
        builder.AppendLine("# OCR Correction Candidate Generation V1");
        builder.AppendLine();
        builder.AppendLine($"- Total OCR anomalies: {occurrences.Count}");
        builder.AppendLine($"- With >=1 proposal: {withProposal}");
        builder.AppendLine($"- Without proposal: {occurrences.Count - withProposal}");
        foreach (var confidence in Enum.GetValues<OcrConfidence>())
            builder.AppendLine($"- {confidence} with proposal: {occurrences.Count(item => item.Source.Confidence == confidence && item.Proposals.Count > 0)}");
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
        var proposals = occurrences.SelectMany(item => item.Proposals).ToArray();
        builder.AppendLine($"- TRmorph-valid proposals: {proposals.Count(item => item.TrMorphValid)}");
        builder.AppendLine($"- BookFrequency > 0 proposals: {proposals.Count(item => item.BookFrequency > 0)}");
        builder.AppendLine($"- Exactly one proposal + TRmorphValid + BookFrequency > 0: {occurrences.Count(item => item.Proposals.Count == 1 && item.Proposals[0].TrMorphValid && item.Proposals[0].BookFrequency > 0)}");
        builder.AppendLine($"- Top-ranked + TRmorphValid + BookFrequency >= 2 + EditDistance <= 2: {occurrences.Count(item => item.Proposals.FirstOrDefault() is { TrMorphValid: true, BookFrequency: >= 2, EditDistance: <= 2 })}");
        builder.AppendLine();
        builder.AppendLine("## Generation reason counts");
        builder.AppendLine();
        foreach (var reason in Enum.GetValues<OcrCorrectionGenerationReason>())
            builder.AppendLine($"- {reason}: {proposals.Count(item => item.GenerationReasons.Contains(reason))}");
        builder.AppendLine();
        builder.AppendLine("## First 100 occurrences");
        builder.AppendLine();
        foreach (var (occurrence, index) in occurrences.Take(100).Select((item, index) => (item, index + 1)))
        {
            var source = occurrence.Source;
            var c = source.Candidate;
            builder.AppendLine($"### {index}. `{Cell(c.Text)}` ({source.Confidence})");
            builder.AppendLine();
            builder.AppendLine($"- Source: {Cell(c.Text)}");
            builder.AppendLine($"- Document/context: {Cell(c.Document)} — {Cell($"{c.ContextBefore}⟦{c.Text}⟧{c.ContextAfter}")}");
            builder.AppendLine($"- Source reasons: {Cell(string.Join(", ", source.DetectionReasons))}");
            builder.AppendLine($"- Proposal count: {occurrence.Proposals.Count}");
            builder.AppendLine();
            builder.AppendLine("| Rank | Proposal | Reasons | Distance | Cost | TRmorph | BookFrequency |");
            builder.AppendLine("|---:|---|---|---:|---:|:---:|---:|");
            foreach (var proposal in occurrence.Proposals)
                builder.AppendLine($"| {proposal.ProposalRank} | {Cell(proposal.ProposedText)} | {Cell(string.Join(", ", proposal.GenerationReasons))} | {proposal.EditDistance} | {proposal.GenerationCost} | {proposal.TrMorphValid} | {proposal.BookFrequency} |");
            builder.AppendLine();
        }
        return builder.ToString();
    }

    private static string Cell(string value) => value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
}
