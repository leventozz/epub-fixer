using EpubFixer.Core.Decision;
using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Mutation;
using EpubFixer.Core.Mutation.Models;

namespace EpubFixer.Core.Ocr;

/// <summary>
/// Wraps today's production OCR correction chain behind <see cref="IOcrCorrectionPlanner"/>.
/// Behavior is unchanged from what <c>EpubFixService</c> called inline before R4.2a.
/// </summary>
public sealed class LegacyOcrCorrectionPlanner : IOcrCorrectionPlanner
{
    public OcrCorrectionPlanResult CreatePlan(LogicalTextStream stream, IMorphologyOracleBuilder oracleBuilder)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(oracleBuilder);

        var analysis = new OcrAnalysisService().AnalyzeCorrections(stream, oracleBuilder);
        var report = new OcrCorrectionDecisionEvaluator().Evaluate(analysis);
        var plan = new OcrCorrectionMutationPlanner().Create(report.Decisions, stream);
        return new(plan, OcrCorrectionEngine.Legacy, []);
    }
}
