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
        writer.WriteLine($"Protected occurrences: {result.ProtectedOccurrences}");
        writer.WriteLine($"Protected safe: {result.ProtectedSafe}");
        writer.WriteLine($"Protected violated: {result.ProtectedViolated}");
        writer.WriteLine($"Protection rate: {FormatRate(result.ProtectionRate)}");

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
