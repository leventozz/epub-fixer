using EpubFixer.Core.Detection.Models;
using EpubFixer.Core.Evidence.Models;
using System.Text;

namespace EpubFixer.Core.Morphology.Models;

public sealed record HyphenationMorphologyEvidence(
    HyphenationCandidate Candidate,
    int LexiconCount,
    bool HasAdjacentHyphen,
    bool HasAdjacentSuspiciousCharacter,
    bool IsClean,
    bool TRmorphValid)
{
    public HyphenationEvidence? Evidence { get; init; }

    public int RightFragmentLetterCount => Candidate.RightPart
        .EnumerateRunes()
        .Count(Rune.IsLetter);

    public string Original => $"{Candidate.LeftPart}-{Candidate.RightPart}";
    public string JoinedForm => Candidate.LeftPart + Candidate.RightPart;
}
