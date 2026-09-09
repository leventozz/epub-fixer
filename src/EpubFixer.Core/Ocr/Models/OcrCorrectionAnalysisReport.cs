namespace EpubFixer.Core.Ocr.Models;

public sealed class OcrCorrectionAnalysisReport
{
    public OcrCorrectionAnalysisReport(
        OcrAnalysisReport sourceAnalysis,
        IReadOnlyList<OcrCorrectionOccurrence> occurrences)
    {
        SourceAnalysis = sourceAnalysis;
        Occurrences = occurrences;
    }

    public OcrAnalysisReport SourceAnalysis { get; }
    public IReadOnlyList<OcrCorrectionOccurrence> Occurrences { get; }
}
