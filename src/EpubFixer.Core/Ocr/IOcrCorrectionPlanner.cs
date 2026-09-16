using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Mutation.Models;

namespace EpubFixer.Core.Ocr;

public sealed record OcrCorrectionPlanResult(
    OcrCorrectionMutationPlan Plan,
    OcrCorrectionEngine Engine,
    IReadOnlyList<string> Diagnostics);

public interface IOcrCorrectionPlanner
{
    OcrCorrectionPlanResult CreatePlan(LogicalTextStream stream, IMorphologyOracleBuilder oracleBuilder);
}
