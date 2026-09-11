namespace EpubFixer.Cli.OcrReconstruction;

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
    double LocalPairContraction = 1.00);
