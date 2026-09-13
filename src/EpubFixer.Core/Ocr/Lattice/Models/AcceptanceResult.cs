namespace EpubFixer.Core.Ocr.Lattice.Models;

public enum AcceptanceVerdict
{
    Apply,
    Review,
    Leave
}

public sealed record AcceptanceResult(
    AcceptanceVerdict Verdict,
    string? Replacement,
    int LogicalStart,
    int LogicalEndExclusive,
    IReadOnlyList<string> Reasons);
