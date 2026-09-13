namespace EpubFixer.Core.Ocr.Lattice;

public sealed record LatticeOptions(
    int MaxWindowLength = 48,
    int MaxArcLength = 20,
    int ContextTokens = 1,
    double BudgetBase = 1.0,
    double BudgetPerFourChars = 1.0,
    double BudgetCap = 3.0,
    int MaxMatchesPerSpan = 16,
    int MaxQueriesPerSpan = 24,
    int MaxRegionStates = 200_000,
    double Lambda = 0.5,
    double MaxPathCost = 2.5,
    double MinMargin = 0.75,
    int MaxOrdinarySubstitutions = 1);
