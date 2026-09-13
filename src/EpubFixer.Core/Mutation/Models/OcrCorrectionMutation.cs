using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Core.Mutation.Models;

public sealed record OcrCorrectionMutation(
    string DocumentPath,
    int LogicalStart,
    int LogicalLength,
    string OriginalSourceText,
    string ReplacementText,
    OcrMutationProvenance Provenance,
    IReadOnlyList<OcrMutationSourceSpan> SourceSpans)
{
    public string DecisionRule => Provenance.Rule;
    public OcrConfidence? Confidence => Provenance.Confidence;
    public bool IsMultiSource => Provenance.ConsumesMultipleSources;
}
