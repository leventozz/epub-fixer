namespace EpubFixer.QualityBenchmarks.Models;

public sealed record GroundTruthDocument(
    int SchemaVersion,
    IReadOnlyList<KnownErrorOccurrence> KnownErrors);

public sealed record KnownErrorOccurrence(
    string Id,
    string DocumentPath,
    string Original,
    string Expected,
    IReadOnlyList<GroundTruthSourceSpan> SourceSpans);

public sealed record GroundTruthSourceSpan(
    string DocumentPath,
    int TextNodeIndex,
    int Start,
    int Length);
