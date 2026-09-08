using EpubFixer.Core.Detection.Models;
using EpubFixer.Core.Evidence.Models;
using EpubFixer.Core.Lexicon.Models;

namespace EpubFixer.Core.Evidence;

public sealed class HyphenationEvidenceEvaluator
{
    public IReadOnlyList<HyphenationEvidence> Evaluate(
        IReadOnlyList<HyphenationCandidate> candidates,
        BookLexicon lexicon)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(lexicon);

        var evidence = new HyphenationEvidence[candidates.Count];

        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            var unhyphenatedText = candidate.UnhyphenatedText;

            evidence[index] = new HyphenationEvidence(
                candidate,
                lexicon.GetCount(unhyphenatedText),
                lexicon.Contains(unhyphenatedText));
        }

        return Array.AsReadOnly(evidence);
    }
}
