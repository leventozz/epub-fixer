namespace EpubFixer.QualityBenchmarks.Models;

public sealed record GroundTruthDocument(
    int SchemaVersion,
    IReadOnlyList<KnownErrorOccurrence> KnownErrors)
{
    public IReadOnlyList<ProtectedOccurrence> ProtectedOccurrences { get; init; } = [];
}

public sealed record KnownErrorOccurrence(
    string Id,
    string DocumentPath,
    string Original,
    string Expected,
    IReadOnlyList<GroundTruthSourceSpan> SourceSpans);

public sealed record ProtectedOccurrence(
    string Id,
    string DocumentPath,
    string Original,
    IReadOnlyList<GroundTruthSourceSpan> SourceSpans);

public sealed record GroundTruthSourceSpan(
    string DocumentPath,
    int TextNodeIndex,
    int Start,
    int Length);
