namespace EpubFixer.Core.Ocr.Models;

public sealed record OcrWordEvidence(
    OcrWordCandidate Candidate,
    int BookFrequency,
    string BaseForm,
    int BaseFormFrequency,
    bool TrMorphValid,
    IReadOnlyList<OcrDetectionReason> DetectionReasons,
    OcrConfidence Confidence);
