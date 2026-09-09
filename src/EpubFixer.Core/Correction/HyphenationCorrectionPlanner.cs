using EpubFixer.Core.Correction.Models;
using EpubFixer.Core.Decision.Models;
using EpubFixer.Core.Detection.Models;

namespace EpubFixer.Core.Correction;

public sealed class HyphenationCorrectionPlanner
{
    public IReadOnlyList<HyphenationCorrectionPlan> Plan(
        IReadOnlyList<HyphenationDecision> decisions)
    {
        ArgumentNullException.ThrowIfNull(decisions);

        var plans = new List<HyphenationCorrectionPlan>();

        foreach (var decision in decisions)
        {
            if (decision.DecisionKind == HyphenationDecisionKind.Deferred)
            {
                continue;
            }

            if (decision.DecisionKind != HyphenationDecisionKind.AutoFixCandidate)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(decisions),
                    decision.DecisionKind,
                    "Unsupported hyphenation decision kind.");
            }

            var candidate = decision.Evidence.Candidate;
            var correctionKind = candidate.DetectionKind switch
            {
                HyphenationDetectionKind.Inline => HyphenationCorrectionKind.Inline,
                HyphenationDetectionKind.TextNodeBoundary => HyphenationCorrectionKind.TextNode,
                HyphenationDetectionKind.ParagraphBoundary => HyphenationCorrectionKind.CrossParagraph,
                HyphenationDetectionKind.DocumentBoundary => (HyphenationCorrectionKind?)null,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(decisions),
                    candidate.DetectionKind,
                    "Unsupported hyphenation detection kind.")
            };

            if (correctionKind is null)
            {
                continue;
            }

            plans.Add(new HyphenationCorrectionPlan(
                decision,
                correctionKind.Value,
                candidate.UnhyphenatedText,
                candidate.LeftSource,
                candidate.HyphenSource,
                candidate.RightSource));
        }

        return Array.AsReadOnly(plans.ToArray());
    }
}
