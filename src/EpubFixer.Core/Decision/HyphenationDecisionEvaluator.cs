using EpubFixer.Core.Decision.Models;
using EpubFixer.Core.Evidence.Models;

namespace EpubFixer.Core.Decision;

public sealed class HyphenationDecisionEvaluator
{
    private const int MinimumAutoFixLexiconCount = 10;

    public IReadOnlyList<HyphenationDecision> Evaluate(
        IReadOnlyList<HyphenationEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        var decisions = new HyphenationDecision[evidence.Count];

        for (var index = 0; index < evidence.Count; index++)
        {
            var item = evidence[index];
            var decisionKind = item.UnhyphenatedOccurrenceCount >= MinimumAutoFixLexiconCount
                && !item.Context.HasAdjacentHyphen
                && !item.Context.HasAdjacentSuspiciousCharacter
                    ? HyphenationDecisionKind.AutoFixCandidate
                    : HyphenationDecisionKind.Deferred;

            decisions[index] = new HyphenationDecision(item, decisionKind);
        }

        return Array.AsReadOnly(decisions);
    }
}
