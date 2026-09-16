namespace EpubFixer.Core.Ocr.Models;

public sealed record ReconstructionCandidate(
    string Text,
    double Score,
    int Rank,
    ReconstructionSource Source,
    IReadOnlyList<string> Evidence);
