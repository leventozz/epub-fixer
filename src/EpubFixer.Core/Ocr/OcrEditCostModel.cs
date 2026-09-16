namespace EpubFixer.Core.Ocr;

public sealed record OcrEditCostModel(
    double Keep = 0,
    double LineBreakDeletion = 0.15,
    double GarbageDeletion = 0.20,
    double KnownGlyphSubstitution = 0.25,
    double HyphenDeletion = 0.25,
    double SpaceDeletion = 0.30,
    double ShortFragmentMerge = 0.40,
    double SpaceInsertion = 0.70,
    double OrdinarySubstitution = 1.00,
    double OrdinaryDeletion = 1.00,
    double OrdinaryInsertion = 1.00,
    double LocalPairContraction = 1.00,
    // What it costs to LEAVE a hard garbage glyph in place rather than drop it. Without this the
    // lattice's literal arc is free, so no deletion can ever win and the garbage is carried into
    // the output (H4b measured exactly that). Must exceed GarbageDeletion or dropping is never
    // preferred. Chosen, not calibrated (D34): the smallest step above the 0.20 already in use.
    // Appended at the end - this is a positional record and existing call sites construct it so.
    double RetainedGarbage = 0.25);
