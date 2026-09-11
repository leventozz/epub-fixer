namespace EpubFixer.Core.Ocr.Models;

public enum OcrRegionDetectionReason
{
    SuspiciousLineBreak,
    ShortFragment,
    MorphologyInvalid,
    SuspiciousPunctuation,
    EmbeddedGarbageGlyph,
    MalformedHyphenContinuation,
    IsolatedOcrGlyph
}
