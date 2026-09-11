using System.Text;
using EpubFixer.Core.Ocr.Models;

internal static class OcrAnalysisReporting
{
    public static string SerializeMarkdown(OcrAnalysisReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# OCR Word Anomaly Analysis");
        builder.AppendLine();
        builder.AppendLine($"- Total word/text occurrences examined: {report.TotalExamined}");
        builder.AppendLine($"- Total OCR anomaly candidates: {report.Candidates.Count}");
        builder.AppendLine($"- HIGH confidence candidates: {report.Candidates.Count(item => item.Confidence == OcrConfidence.High)}");
        builder.AppendLine($"- MEDIUM candidates: {report.Candidates.Count(item => item.Confidence == OcrConfidence.Medium)}");
        builder.AppendLine($"- Evidence-only candidates: {report.Candidates.Count(item => item.Confidence == OcrConfidence.EvidenceOnly)}");
        builder.AppendLine($"- TRmorph invalid total: {report.TrMorphInvalidTotal}");
        builder.AppendLine($"- TRmorph invalid + strong OCR evidence: {report.TrMorphInvalidWithStrongEvidence}");
        builder.AppendLine($"- BaseFormFrequency > 1 candidate count: {report.Candidates.Count(item => item.BaseFormFrequency > 1)}");
        builder.AppendLine($"- Previously suppressed by BaseFormFrequency, now retained candidate count: {report.Candidates.Count(IsPreviouslySuppressedByBaseFormFrequency)}");
        builder.AppendLine($"- Unique apostrophe base forms: {report.UniqueApostropheBaseForms}");
        builder.AppendLine();
        builder.AppendLine("Reason counts overlap: one occurrence may carry multiple reasons, so reason totals do not need to equal candidate total.");
        builder.AppendLine();
        builder.AppendLine("## Detection reasons");
        builder.AppendLine();
        foreach (var reason in Enum.GetValues<OcrDetectionReason>())
            builder.AppendLine($"- {reason}: {report.Candidates.Count(item => item.DetectionReasons.Contains(reason))}");
        builder.AppendLine();
        builder.AppendLine("## First 200 occurrences");
        builder.AppendLine();
        builder.AppendLine("| # | Text / fragment | Document | Frequency | BaseForm | BaseFormFrequency | TRmorph valid | Confidence | Reasons | Logical occurrence context |");
        builder.AppendLine("|---:|---|---|---:|---|---:|:---:|---|---|---|");
        foreach (var (item, index) in report.Candidates.Take(200).Select((item, index) => (item, index + 1)))
        {
            var c = item.Candidate;
            var context = $"{c.ContextBefore}⟦{c.Text}⟧{c.ContextAfter}";
            builder.AppendLine($"| {index} | {Cell(c.Text)} | {Cell(c.Document)} | {item.BookFrequency} | {Cell(item.BaseForm)} | {item.BaseFormFrequency} | {item.TrMorphValid} | {item.Confidence} | {Cell(string.Join(", ", item.DetectionReasons))} | {Cell(context)} |");
        }
        return builder.ToString();
    }

    private static bool IsPreviouslySuppressedByBaseFormFrequency(OcrWordEvidence candidate) =>
        candidate.Confidence == OcrConfidence.EvidenceOnly
        && !candidate.TrMorphValid
        && candidate.BookFrequency == 1
        && candidate.BaseFormFrequency > 1;

    private static string Cell(string value) => value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
}
