namespace EpubFixer.QualityBenchmarks.Models;

public enum OcrErrorClass
{
    Unclassified = 0,
    Hyphenation,
    GlyphConfusion,
    SpuriousSpace,
    MissingSpace,
    GarbageInsertion,
    Fragmentation,
    Mixed
}
