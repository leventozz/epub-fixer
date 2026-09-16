namespace EpubFixer.Core.Lexicon;

public sealed record VocabularyCoverageReport(
    double HeldOutCoverage,
    double TargetCoverage,
    IReadOnlyList<string> MissingTargets,
    int SuspiciousEntries);
