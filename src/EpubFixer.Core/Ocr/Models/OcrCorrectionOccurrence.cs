namespace EpubFixer.Core.Ocr.Models;

public sealed record OcrCorrectionOccurrence(
    OcrWordEvidence Source,
    IReadOnlyList<OcrCorrectionCandidate> Proposals);
