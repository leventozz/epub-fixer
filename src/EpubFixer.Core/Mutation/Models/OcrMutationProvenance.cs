using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Core.Mutation.Models;

public enum OcrCorrectionEngine
{
    Legacy,
    Lattice
}

public sealed record OcrMutationProvenance(
    OcrCorrectionEngine Engine,
    string Rule,
    OcrConfidence? Confidence,
    bool ConsumesMultipleSources);
