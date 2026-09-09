namespace EpubFixer.Core.Ocr.Models;

public sealed record OcrCorrectionCandidate(
    OcrWordCandidate Source,
    OcrConfidence SourceConfidence,
    string ProposedText,
    IReadOnlyList<OcrCorrectionGenerationReason> GenerationReasons,
    int EditDistance,
    int GenerationCost,
    bool TrMorphValid,
    int BookFrequency,
    int ProposalRank,
    IReadOnlyList<OcrWordCandidate> ConsumedSources,
    int StructuralTransformationCount,
    bool IsPartialStructuralRepair)
{
    public int SourceSpanCount => ConsumedSources.Count;
    public bool ConsumesMultipleOccurrences => SourceSpanCount > 1;
}
