using EpubFixer.QualityBenchmarks.Models;

namespace EpubFixer.QualityBenchmarks;

internal sealed class QualityBenchmarkCorrectionEvaluator
{
    public CorrectionBenchmarkResult Evaluate(
        IReadOnlyList<KnownErrorOccurrence> occurrences,
        IReadOnlyList<QualityBenchmarkOccurrenceTracker> trackers)
    {
        ArgumentNullException.ThrowIfNull(occurrences);
        ArgumentNullException.ThrowIfNull(trackers);

        var correctlyFixed = 0;
        var wronglyFixed = 0;
        var deferred = 0;
        var failures = new List<KnownErrorCorrectionFailure>();

        for (var index = 0; index < occurrences.Count; index++)
        {
            var occurrence = occurrences[index];
            var observed = trackers[index].ReadObservedText();

            if (string.Equals(observed, occurrence.Expected, StringComparison.Ordinal))
            {
                correctlyFixed++;
                continue;
            }

            var classification = string.Equals(
                observed,
                occurrence.Original,
                StringComparison.Ordinal)
                ? "Deferred"
                : "WronglyFixed";

            if (classification == "Deferred")
            {
                deferred++;
            }
            else
            {
                wronglyFixed++;
            }

            failures.Add(new KnownErrorCorrectionFailure(
                occurrence,
                classification,
                observed));
        }

        return new CorrectionBenchmarkResult(
            correctlyFixed,
            wronglyFixed,
            deferred,
            Array.AsReadOnly(failures.ToArray()));
    }
}

internal sealed record CorrectionBenchmarkResult(
    int CorrectlyFixed,
    int WronglyFixed,
    int Deferred,
    IReadOnlyList<KnownErrorCorrectionFailure> Failures);
