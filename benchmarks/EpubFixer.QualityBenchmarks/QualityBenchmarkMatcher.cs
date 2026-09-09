using EpubFixer.Core.Detection.Models;
using EpubFixer.Core.Epub.Models;
using EpubFixer.QualityBenchmarks.Models;

namespace EpubFixer.QualityBenchmarks;

public sealed class QualityBenchmarkMatcher
{
    public QualityBenchmarkResult Match(
        IReadOnlyList<KnownErrorOccurrence> knownErrors,
        IReadOnlyList<HyphenationCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(knownErrors);
        ArgumentNullException.ThrowIfNull(candidates);

        var consumedCandidates = new bool[candidates.Count];
        var missedOccurrences = new List<KnownErrorOccurrence>();

        foreach (var knownError in knownErrors)
        {
            var candidateIndex = FindMatch(knownError, candidates, consumedCandidates);

            if (candidateIndex < 0)
            {
                missedOccurrences.Add(knownError);
                continue;
            }

            consumedCandidates[candidateIndex] = true;
        }

        var knownErrorCount = knownErrors.Count;
        var missedCount = missedOccurrences.Count;
        var detectedCount = knownErrorCount - missedCount;
        double? detectionRecall = knownErrorCount == 0
            ? null
            : (double)detectedCount / knownErrorCount;

        return new QualityBenchmarkResult(
            knownErrorCount,
            detectedCount,
            missedCount,
            detectionRecall,
            Array.AsReadOnly(missedOccurrences.ToArray()));
    }

    private static int FindMatch(
        KnownErrorOccurrence knownError,
        IReadOnlyList<HyphenationCandidate> candidates,
        IReadOnlyList<bool> consumedCandidates)
    {
        for (var index = 0; index < candidates.Count; index++)
        {
            if (!consumedCandidates[index] && IsMatch(knownError, candidates[index]))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsMatch(
        KnownErrorOccurrence knownError,
        HyphenationCandidate candidate)
    {
        if (!string.Equals(
                knownError.DocumentPath,
                candidate.HyphenSource.DocumentPath,
                StringComparison.Ordinal)
            || !string.Equals(
                knownError.Original,
                candidate.LeftPart + "-" + candidate.RightPart,
                StringComparison.Ordinal)
            || !string.Equals(
                knownError.Expected,
                candidate.UnhyphenatedText,
                StringComparison.Ordinal))
        {
            return false;
        }

        var candidateSpans = CreateCanonicalSourceSpans(candidate);

        if (knownError.SourceSpans.Count != candidateSpans.Count)
        {
            return false;
        }

        for (var index = 0; index < candidateSpans.Count; index++)
        {
            if (knownError.SourceSpans[index] != candidateSpans[index])
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyList<GroundTruthSourceSpan> CreateCanonicalSourceSpans(
        HyphenationCandidate candidate)
    {
        var locations = new[]
        {
            candidate.LeftSource,
            candidate.HyphenSource,
            candidate.RightSource
        };
        var spans = new List<GroundTruthSourceSpan>(locations.Length);

        foreach (var location in locations)
        {
            if (spans.Count > 0 && CanMerge(spans[^1], location))
            {
                var previous = spans[^1];
                spans[^1] = previous with { Length = previous.Length + location.Length };
                continue;
            }

            spans.Add(new GroundTruthSourceSpan(
                location.DocumentPath,
                location.TextNodeIndex,
                location.Start,
                location.Length));
        }

        return spans;
    }

    private static bool CanMerge(
        GroundTruthSourceSpan previous,
        TextSourceLocation current)
    {
        return string.Equals(
                previous.DocumentPath,
                current.DocumentPath,
                StringComparison.Ordinal)
            && previous.TextNodeIndex == current.TextNodeIndex
            && previous.Start + previous.Length == current.Start;
    }
}
