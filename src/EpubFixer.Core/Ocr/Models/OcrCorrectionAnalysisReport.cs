namespace EpubFixer.Core.Ocr.Models;

public sealed class OcrCorrectionAnalysisReport
{
    public OcrCorrectionAnalysisReport(
        OcrAnalysisReport sourceAnalysis,
        IReadOnlyList<OcrCorrectionOccurrence> occurrences,
        IReadOnlyList<OcrTargetRegressionExample> targetRegressionExamples)
    {
        SourceAnalysis = sourceAnalysis;
        Occurrences = occurrences;
        TargetRegressionExamples = targetRegressionExamples;
    }

    public OcrAnalysisReport SourceAnalysis { get; }
    public IReadOnlyList<OcrCorrectionOccurrence> Occurrences { get; }
    public IReadOnlyList<OcrTargetRegressionExample> TargetRegressionExamples { get; }
}
