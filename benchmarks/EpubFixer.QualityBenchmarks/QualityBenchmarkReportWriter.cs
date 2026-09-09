using System.Globalization;
using EpubFixer.QualityBenchmarks.Models;

namespace EpubFixer.QualityBenchmarks;

public static class QualityBenchmarkReportWriter
{
    public static void Write(TextWriter writer, string datasetName, QualityBenchmarkResult result)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(datasetName);
        ArgumentNullException.ThrowIfNull(result);

        writer.WriteLine($"Dataset: {datasetName}");
        writer.WriteLine();
        writer.WriteLine($"Known errors: {result.KnownErrors}");
        writer.WriteLine($"Detected: {result.Detected}");
        writer.WriteLine($"Missed: {result.Missed}");
        writer.WriteLine($"Detection recall: {FormatRate(result.DetectionRecall)}");

        foreach (var occurrence in result.MissedOccurrences)
        {
            writer.WriteLine();
            writer.WriteLine("MISSED");
            writer.WriteLine($"id: {occurrence.Id}");
            writer.WriteLine($"document: {occurrence.DocumentPath}");
            writer.WriteLine($"original: {occurrence.Original}");
            writer.WriteLine($"expected: {occurrence.Expected}");
        }

        writer.WriteLine();
        writer.WriteLine($"Known AutoFixCandidate: {result.KnownAutoFixCandidates}");
        writer.WriteLine($"Known Deferred: {result.KnownDeferred}");
        writer.WriteLine($"Auto-fix coverage: {FormatRate(result.AutoFixCoverage)}");

        foreach (var occurrence in result.KnownDeferredOccurrences)
        {
            writer.WriteLine();
            writer.WriteLine("KNOWN ERROR DEFERRED");
            writer.WriteLine($"id: {occurrence.Id}");
            writer.WriteLine($"original: {occurrence.Original}");
            writer.WriteLine($"expected: {occurrence.Expected}");
            writer.WriteLine($"document: {occurrence.DocumentPath}");
        }

        writer.WriteLine();
        writer.WriteLine("Correction:");
        writer.WriteLine($"  Correctly fixed: {result.CorrectlyFixed}");
        writer.WriteLine($"  Wrongly fixed: {result.WronglyFixed}");
        writer.WriteLine($"  Deferred/still incorrect: {result.Deferred}");

        foreach (var failure in result.CorrectionFailures)
        {
            writer.WriteLine();
            writer.WriteLine("KNOWN ERROR STILL INCORRECT");
            writer.WriteLine($"id: {failure.Occurrence.Id}");
            writer.WriteLine($"document: {failure.Occurrence.DocumentPath}");
            writer.WriteLine($"original: {failure.Occurrence.Original}");
            writer.WriteLine($"expected: {failure.Occurrence.Expected}");
            writer.WriteLine($"observed: {failure.ObservedText}");
            writer.WriteLine($"classification: {failure.Classification}");
        }

        writer.WriteLine();
        writer.WriteLine($"Protected occurrences: {result.ProtectedOccurrences}");
        writer.WriteLine($"Protected safe: {result.ProtectedSafe}");
        writer.WriteLine($"Protected violated: {result.ProtectedViolated}");
        writer.WriteLine($"Protection rate: {FormatRate(result.ProtectionRate)}");
        writer.WriteLine($"Protected changed: {result.ProtectedChanged}");

        writer.WriteLine();
        writer.WriteLine("Integrity:");
        writer.WriteLine($"  Unexpected text changes: {result.UnexpectedTextChanges}");
        writer.WriteLine($"  Non-text changes: {result.NonTextChanges}");

        foreach (var change in result.UnexpectedTextChangeDetails.Take(5))
        {
            writer.WriteLine();
            writer.WriteLine("UNEXPECTED TEXT CHANGE");
            writer.WriteLine($"document: {change.DocumentPath}");
            writer.WriteLine($"region: {change.Region}");
            writer.WriteLine($"before: {change.Before}");
            writer.WriteLine($"after: {change.After}");
            writer.WriteLine($"reason: {change.Reason}");
        }

        foreach (var change in result.NonTextChangeDetails.Take(5))
        {
            writer.WriteLine();
            writer.WriteLine("UNEXPECTED NON-TEXT CHANGE");
            writer.WriteLine($"document: {change.DocumentPath}");
            writer.WriteLine($"region: {change.Region}");
            writer.WriteLine($"mutation: {change.Mutation}");
            writer.WriteLine($"reason: {change.Reason}");
        }

        var omittedTextChanges = result.UnexpectedTextChanges - Math.Min(5, result.UnexpectedTextChanges);
        var omittedNonTextChanges = result.NonTextChanges - Math.Min(5, result.NonTextChanges);
        if (omittedTextChanges > 0 || omittedNonTextChanges > 0)
        {
            writer.WriteLine();
            writer.WriteLine($"Additional integrity diagnostics omitted: {omittedTextChanges + omittedNonTextChanges}");
        }

        foreach (var change in result.ProtectedChanges)
        {
            writer.WriteLine();
            writer.WriteLine("PROTECTED CHANGED");
            writer.WriteLine($"id: {change.Occurrence.Id}");
            writer.WriteLine($"original: {change.Occurrence.Original}");
            writer.WriteLine($"observed: {change.ObservedText}");
        }

        foreach (var violation in result.ProtectedViolations)
        {
            writer.WriteLine();
            writer.WriteLine("PROTECTED VIOLATION");
            writer.WriteLine($"id: {violation.Occurrence.Id}");
            writer.WriteLine($"original: {violation.Occurrence.Original}");
            writer.WriteLine($"decision: {violation.DecisionKind}");
        }
    }

    private static string FormatRate(double? rate) => rate.HasValue
        ? (rate.Value * 100).ToString("F2", CultureInfo.InvariantCulture) + "%"
        : "N/A";
}
