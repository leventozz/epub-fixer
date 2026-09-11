namespace EpubFixer.Core.Ocr.Models;

public sealed record CorruptedTextRegion(
    string RawText,
    int Start,
    int EndExclusive,
    IReadOnlyList<string> LogicalFragments,
    string ContextBefore,
    string ContextAfter,
    IReadOnlyList<OcrRegionDetectionReason> DetectionReasons)
{
    public int Length => EndExclusive - Start;
}
