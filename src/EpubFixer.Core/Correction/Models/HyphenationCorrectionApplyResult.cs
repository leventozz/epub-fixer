namespace EpubFixer.Core.Correction.Models;

public sealed record HyphenationCorrectionApplyResult(
    int AppliedCount,
    int SkippedCount);
