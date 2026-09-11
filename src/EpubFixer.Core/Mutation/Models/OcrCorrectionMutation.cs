using EpubFixer.Core.Decision.Models;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Core.Mutation.Models;

public sealed record OcrCorrectionMutation(
    string DocumentPath,
    int LogicalStart,
    int LogicalLength,
    string OriginalSourceText,
    string ReplacementText,
    OcrCorrectionDecision Decision,
    IReadOnlyList<OcrMutationSourceSpan> SourceSpans)
{
    public string DecisionRule => Decision.DecisionReasons.FirstOrDefault().ToString();
    public OcrConfidence Confidence => Decision.SourceOccurrence.Source.Confidence;
    public bool IsMultiSource => Decision.SelectedProposal?.Proposal.ConsumesMultipleOccurrences == true;
}
