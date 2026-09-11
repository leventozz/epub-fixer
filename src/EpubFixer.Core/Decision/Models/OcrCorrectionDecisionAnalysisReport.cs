using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Core.Decision.Models;

public sealed class OcrCorrectionDecisionAnalysisReport
{
    public OcrCorrectionDecisionAnalysisReport(
        OcrCorrectionAnalysisReport sourceAnalysis,
        IReadOnlyList<OcrCorrectionDecision> decisions)
    {
        SourceAnalysis = sourceAnalysis;
        Decisions = decisions;
    }

    public OcrCorrectionAnalysisReport SourceAnalysis { get; }
    public IReadOnlyList<OcrCorrectionDecision> Decisions { get; }
}
