using EpubFixer.QualityBenchmarks.Models;

namespace EpubFixer.QualityBenchmarks;

internal sealed class QualityBenchmarkProtectedMutationEvaluator
{
    public ProtectedMutationBenchmarkResult Evaluate(
        IReadOnlyList<ProtectedOccurrence> occurrences,
        IReadOnlyList<QualityBenchmarkOccurrenceTracker> trackers)
    {
        ArgumentNullException.ThrowIfNull(occurrences);
        ArgumentNullException.ThrowIfNull(trackers);

        var changes = new List<ProtectedOccurrenceChange>();
        for (var index = 0; index < occurrences.Count; index++)
        {
            var observed = trackers[index].ReadObservedText();
            if (!string.Equals(observed, occurrences[index].Original, StringComparison.Ordinal))
            {
                changes.Add(new ProtectedOccurrenceChange(occurrences[index], observed));
            }
        }

        return new ProtectedMutationBenchmarkResult(
            changes.Count,
            Array.AsReadOnly(changes.ToArray()));
    }
}

internal sealed record ProtectedMutationBenchmarkResult(
    int ProtectedChanged,
    IReadOnlyList<ProtectedOccurrenceChange> Changes);
