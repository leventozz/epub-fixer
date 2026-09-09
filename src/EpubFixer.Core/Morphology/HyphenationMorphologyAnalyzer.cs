using EpubFixer.Core.Evidence.Models;
using EpubFixer.Core.Morphology.Models;

namespace EpubFixer.Core.Morphology;

public sealed class HyphenationMorphologyAnalyzer
{
    public IReadOnlyList<HyphenationMorphologyEvidence> Analyze(
        IReadOnlyList<HyphenationEvidence> evidence,
        ITurkishMorphologyAnalyzer analyzer)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(analyzer);

        var result = new HyphenationMorphologyEvidence[evidence.Count];
        for (var index = 0; index < evidence.Count; index++)
        {
            var item = evidence[index];
            var context = item.Context;
            var clean = !context.HasAdjacentHyphen && !context.HasAdjacentSuspiciousCharacter;
            result[index] = new HyphenationMorphologyEvidence(
                item.Candidate,
                item.UnhyphenatedOccurrenceCount,
                context.HasAdjacentHyphen,
                context.HasAdjacentSuspiciousCharacter,
                clean,
                analyzer.IsValidWord(item.Candidate.LeftPart + item.Candidate.RightPart))
            {
                Evidence = item
            };
        }

        return Array.AsReadOnly(result);
    }
}
