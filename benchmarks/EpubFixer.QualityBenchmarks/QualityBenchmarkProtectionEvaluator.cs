using EpubFixer.Core.Decision.Models;
using EpubFixer.QualityBenchmarks.Models;

namespace EpubFixer.QualityBenchmarks;

public sealed class QualityBenchmarkProtectionEvaluator
{
    public ProtectionBenchmarkResult Evaluate(
        IReadOnlyList<ProtectedOccurrence> protectedOccurrences,
        IReadOnlyList<HyphenationDecision> decisions)
    {
        ArgumentNullException.ThrowIfNull(protectedOccurrences);
        ArgumentNullException.ThrowIfNull(decisions);

        var consumedDecisions = new bool[decisions.Count];
        var violations = new List<ProtectedOccurrenceViolation>();

        foreach (var occurrence in protectedOccurrences)
        {
            var decisionIndex = FindMatch(occurrence, decisions, consumedDecisions);

            if (decisionIndex < 0)
            {
                continue;
            }

            consumedDecisions[decisionIndex] = true;
            var decision = decisions[decisionIndex];

            if (decision.DecisionKind == HyphenationDecisionKind.AutoFixCandidate)
            {
                violations.Add(new ProtectedOccurrenceViolation(
                    occurrence,
                    decision.DecisionKind));
            }
        }

        var protectedCount = protectedOccurrences.Count;
        var violatedCount = violations.Count;
        var safeCount = protectedCount - violatedCount;
        double? protectionRate = protectedCount == 0
            ? null
            : (double)safeCount / protectedCount;

        return new ProtectionBenchmarkResult(
            protectedCount,
            safeCount,
            violatedCount,
            protectionRate,
            Array.AsReadOnly(violations.ToArray()));
    }

    private static int FindMatch(
        ProtectedOccurrence occurrence,
        IReadOnlyList<HyphenationDecision> decisions,
        IReadOnlyList<bool> consumedDecisions)
    {
        for (var index = 0; index < decisions.Count; index++)
        {
            if (!consumedDecisions[index]
                && IsMatch(occurrence, decisions[index]))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsMatch(
        ProtectedOccurrence occurrence,
        HyphenationDecision decision)
    {
        var candidate = decision.Evidence.Candidate;

        if (!string.Equals(
                occurrence.DocumentPath,
                candidate.HyphenSource.DocumentPath,
                StringComparison.Ordinal)
            || !string.Equals(
                occurrence.Original,
                candidate.LeftPart + "-" + candidate.RightPart,
                StringComparison.Ordinal))
        {
            return false;
        }

        var candidateSpans = QualityBenchmarkMatcher.CreateCanonicalSourceSpans(candidate);

        if (occurrence.SourceSpans.Count != candidateSpans.Count)
        {
            return false;
        }

        for (var index = 0; index < candidateSpans.Count; index++)
        {
            if (occurrence.SourceSpans[index] != candidateSpans[index])
            {
                return false;
            }
        }

        return true;
    }
}

public sealed record ProtectionBenchmarkResult(
    int ProtectedOccurrences,
    int ProtectedSafe,
    int ProtectedViolated,
    double? ProtectionRate,
    IReadOnlyList<ProtectedOccurrenceViolation> Violations);
