using EpubFixer.Core.Decision.Models;

internal static class HyphenationDecisionReporting
{
    public static HyphenationDecisionSummary CreateSummary(
        IReadOnlyList<HyphenationDecision> decisions)
    {
        ArgumentNullException.ThrowIfNull(decisions);

        var autoFixCandidateCount = decisions.Count(
            item => item.DecisionKind == HyphenationDecisionKind.AutoFixCandidate);
        var transformations = decisions
            .Where(item => item.DecisionKind == HyphenationDecisionKind.AutoFixCandidate)
            .GroupBy(item => new HyphenationDecisionCandidateKey(
                item.Evidence.Candidate.LeftPart,
                item.Evidence.Candidate.RightPart,
                item.Evidence.Candidate.UnhyphenatedText))
            .Select(group => new HyphenationDecisionAggregate(
                group.Key,
                group.First().Evidence.UnhyphenatedOccurrenceCount,
                group.Count()))
            .OrderByDescending(item => item.LexiconCount)
            .ThenByDescending(item => item.CandidateOccurrences)
            .ThenBy(item => item.Key.LeftPart, StringComparer.Ordinal)
            .ThenBy(item => item.Key.RightPart, StringComparer.Ordinal)
            .ThenBy(item => item.Key.UnhyphenatedText, StringComparer.Ordinal)
            .ToArray();

        return new HyphenationDecisionSummary(
            decisions.Count,
            autoFixCandidateCount,
            decisions.Count - autoFixCandidateCount,
            Array.AsReadOnly(transformations));
    }

    public static void Print(TextWriter writer, HyphenationDecisionSummary summary)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(summary);

        writer.WriteLine();
        writer.WriteLine("Hyphenation decisions");
        writer.WriteLine();
        writer.WriteLine($"Total decisions: {summary.TotalDecisions}");
        writer.WriteLine($"AutoFixCandidate: {summary.AutoFixCandidate}");
        writer.WriteLine($"Deferred: {summary.Deferred}");

        if (summary.AutoFixCandidateTransformations.Count == 0)
        {
            return;
        }

        writer.WriteLine();
        writer.WriteLine(
            $"AutoFixCandidate transformations ({summary.AutoFixCandidateTransformations.Count} unique)");

        foreach (var item in summary.AutoFixCandidateTransformations)
        {
            writer.WriteLine();
            writer.WriteLine(
                $"{item.Key.LeftPart}-{item.Key.RightPart} -> {item.Key.UnhyphenatedText}");
            writer.WriteLine($"Lexicon count: {item.LexiconCount}");
            writer.WriteLine($"Candidate occurrences: {item.CandidateOccurrences}");
        }
    }
}

internal sealed record HyphenationDecisionSummary(
    int TotalDecisions,
    int AutoFixCandidate,
    int Deferred,
    IReadOnlyList<HyphenationDecisionAggregate> AutoFixCandidateTransformations);

internal sealed record HyphenationDecisionCandidateKey(
    string LeftPart,
    string RightPart,
    string UnhyphenatedText);

internal sealed record HyphenationDecisionAggregate(
    HyphenationDecisionCandidateKey Key,
    int LexiconCount,
    int CandidateOccurrences);
