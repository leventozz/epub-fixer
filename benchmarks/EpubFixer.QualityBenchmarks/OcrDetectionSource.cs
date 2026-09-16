namespace EpubFixer.QualityBenchmarks;

public sealed record OcrDetectionSource(string DocumentPath, int TextNodeIndex, int Start, int EndExclusive);
