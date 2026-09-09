namespace EpubFixer.Core.Ocr.Models;

public enum OcrDetectionReason
{
    SuspiciousCharacter,
    EmbeddedDigit,
    SuspiciousPunctuation,
    IsolatedLetterFragmentation,
    SuspiciousCharacterSequence,
    MorphologyInvalid,
    RareInBook
}
