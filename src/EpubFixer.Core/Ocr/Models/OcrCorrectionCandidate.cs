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
    int ProposalRank);
