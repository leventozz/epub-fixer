namespace EpubFixer.Core.Evidence.Models;

public sealed record HyphenationContextEvidence(
    string? PreviousRune,
    string? NextRune,
    bool HasAdjacentHyphen,
    bool HasAdjacentSuspiciousCharacter);
