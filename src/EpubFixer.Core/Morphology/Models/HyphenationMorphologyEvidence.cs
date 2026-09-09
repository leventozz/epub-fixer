using EpubFixer.Core.Detection.Models;

namespace EpubFixer.Core.Morphology.Models;

public sealed record HyphenationMorphologyEvidence(
    HyphenationCandidate Candidate,
    int LexiconCount,
    bool HasAdjacentHyphen,
    bool HasAdjacentSuspiciousCharacter,
    bool IsClean,
    bool TRmorphValid)
{
    public string Original => $"{Candidate.LeftPart}-{Candidate.RightPart}";
    public string JoinedForm => Candidate.LeftPart + Candidate.RightPart;
}
