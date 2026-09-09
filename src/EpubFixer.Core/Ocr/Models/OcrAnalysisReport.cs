namespace EpubFixer.Core.Ocr.Models;

public sealed class OcrAnalysisReport
{
    public OcrAnalysisReport(
        int totalExamined,
        int trMorphInvalidTotal,
        int trMorphInvalidWithStrongEvidence,
        IReadOnlyList<OcrWordEvidence> candidates)
    {
        TotalExamined = totalExamined;
        TrMorphInvalidTotal = trMorphInvalidTotal;
        TrMorphInvalidWithStrongEvidence = trMorphInvalidWithStrongEvidence;
        Candidates = candidates;
    }

    public int TotalExamined { get; }
    public int TrMorphInvalidTotal { get; }
    public int TrMorphInvalidWithStrongEvidence { get; }
    public IReadOnlyList<OcrWordEvidence> Candidates { get; }
}
