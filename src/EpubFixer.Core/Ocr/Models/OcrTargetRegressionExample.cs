namespace EpubFixer.Core.Ocr.Models;

public sealed record OcrTargetRegressionExample(
    string Query,
    bool Found,
    string Source,
    string Context,
    IReadOnlyList<OcrDetectionReason> SourceReasons,
    int ExactBookFrequency,
    string BaseForm,
    int BaseFormFrequency,
    IReadOnlyList<OcrCorrectionCandidate> Proposals);
