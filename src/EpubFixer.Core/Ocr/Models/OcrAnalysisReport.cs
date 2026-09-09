namespace EpubFixer.Core.Ocr.Models;

public sealed class OcrAnalysisReport
{
    public OcrAnalysisReport(
        int totalExamined,
        int trMorphInvalidTotal,
        int trMorphInvalidWithStrongEvidence,
        IReadOnlyList<OcrWordEvidence> candidates,
        IReadOnlyList<OcrWordEvidence> rareInBookSuppressedOccurrences,
        int uniqueApostropheBaseForms)
    {
        TotalExamined = totalExamined;
        TrMorphInvalidTotal = trMorphInvalidTotal;
        TrMorphInvalidWithStrongEvidence = trMorphInvalidWithStrongEvidence;
        Candidates = candidates;
        RareInBookSuppressedOccurrences = rareInBookSuppressedOccurrences;
        UniqueApostropheBaseForms = uniqueApostropheBaseForms;
    }

    public int TotalExamined { get; }
    public int TrMorphInvalidTotal { get; }
    public int TrMorphInvalidWithStrongEvidence { get; }
    public IReadOnlyList<OcrWordEvidence> Candidates { get; }
    public IReadOnlyList<OcrWordEvidence> RareInBookSuppressedOccurrences { get; }
    public int UniqueApostropheBaseForms { get; }
}
