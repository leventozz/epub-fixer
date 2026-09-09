namespace EpubFixer.Core.Ocr.Models;

public enum OcrCorrectionGenerationReason
{
    StructuralNormalization,
    GarbageRemoval,
    GlyphSubstitution,
    FragmentJoin,
    HyphenRemoval,
    BookLexiconNeighbor,
    AdjacentFragmentComposition
}
