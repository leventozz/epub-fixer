using EpubFixer.Core.Decision.Models;
using EpubFixer.QualityBenchmarks.Models;

namespace EpubFixer.QualityBenchmarks;

public sealed class QualityBenchmarkKnownDecisionEvaluator
{
    public KnownDecisionBenchmarkResult Evaluate(
        IReadOnlyList<KnownErrorOccurrence> knownErrors,
        IReadOnlyList<HyphenationDecision> decisions)
    {
        ArgumentNullException.ThrowIfNull(knownErrors);
        ArgumentNullException.ThrowIfNull(decisions);

        var consumedDecisions = new bool[decisions.Count];
        var deferredOccurrences = new List<KnownErrorOccurrence>();
        var autoFixCount = 0;

        foreach (var knownError in knownErrors)
        {
            var decisionIndex = FindMatch(knownError, decisions, consumedDecisions);
            if (decisionIndex < 0)
            {
                continue;
            }

            consumedDecisions[decisionIndex] = true;
            if (decisions[decisionIndex].DecisionKind == HyphenationDecisionKind.AutoFixCandidate)
            {
                autoFixCount++;
            }
            else
            {
                deferredOccurrences.Add(knownError);
            }
        }

        double? coverage = knownErrors.Count == 0
            ? null
            : (double)autoFixCount / knownErrors.Count;

        return new KnownDecisionBenchmarkResult(
            autoFixCount,
            deferredOccurrences.Count,
            coverage,
            Array.AsReadOnly(deferredOccurrences.ToArray()));
    }

    private static int FindMatch(
        KnownErrorOccurrence knownError,
        IReadOnlyList<HyphenationDecision> decisions,
        IReadOnlyList<bool> consumedDecisions)
    {
        for (var index = 0; index < decisions.Count; index++)
        {
            var candidate = decisions[index].Evidence.Candidate;
            if (!consumedDecisions[index]
                && QualityBenchmarkMatcher.IsMatch(knownError, candidate))
            {
                return index;
            }
        }

        return -1;
    }
}

public sealed record KnownDecisionBenchmarkResult(
    int KnownAutoFixCandidates,
    int KnownDeferred,
    double? AutoFixCoverage,
    IReadOnlyList<KnownErrorOccurrence> DeferredOccurrences);
