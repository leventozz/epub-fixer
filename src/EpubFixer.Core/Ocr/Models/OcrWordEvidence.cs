namespace EpubFixer.Core.Ocr.Models;

public sealed record OcrWordEvidence(
    OcrWordCandidate Candidate,
    int BookFrequency,
    bool TrMorphValid,
    IReadOnlyList<OcrDetectionReason> DetectionReasons,
    OcrConfidence Confidence);
