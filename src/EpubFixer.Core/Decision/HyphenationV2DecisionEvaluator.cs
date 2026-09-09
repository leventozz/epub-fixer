using EpubFixer.Core.Decision.Models;
using EpubFixer.Core.Evidence.Models;
using EpubFixer.Core.Morphology.Models;

namespace EpubFixer.Core.Decision;

public sealed class HyphenationV2DecisionEvaluator
{
    private const int MinimumAutoFixLexiconCount = 10;
    private const int MinimumRightFragmentLetterCount = 2;

    public IReadOnlyList<HyphenationDecision> Evaluate(
        IReadOnlyList<HyphenationEvidence> evidence,
        IReadOnlyList<HyphenationMorphologyEvidence> morphology)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(morphology);
        if (evidence.Count != morphology.Count)
            throw new ArgumentException("Evidence and morphology evidence must have the same count.");

        var decisions = new HyphenationDecision[evidence.Count];
        for (var index = 0; index < evidence.Count; index++)
        {
            var item = evidence[index];
            var morphologyItem = morphology[index];
            var isClean = !item.Context.HasAdjacentHyphen
                && !item.Context.HasAdjacentSuspiciousCharacter;
            var strongLexicon = item.UnhyphenatedOccurrenceCount >= MinimumAutoFixLexiconCount
                && isClean;
            var morphologyFallback = item.UnhyphenatedOccurrenceCount < MinimumAutoFixLexiconCount
                && isClean
                && morphologyItem.TRmorphValid
                && morphologyItem.RightFragmentLetterCount >= MinimumRightFragmentLetterCount;
            var kind = strongLexicon || morphologyFallback
                ? HyphenationDecisionKind.AutoFixCandidate
                : HyphenationDecisionKind.Deferred;
            decisions[index] = new HyphenationDecision(
                item,
                kind,
                strongLexicon
                    ? HyphenationDecisionReason.StrongBookLexicon
                    : morphologyFallback
                        ? HyphenationDecisionReason.TurkishMorphology
                        : null);
        }

        return Array.AsReadOnly(decisions);
    }
}
