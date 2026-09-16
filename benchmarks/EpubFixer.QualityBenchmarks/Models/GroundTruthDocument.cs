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
    IReadOnlyList<GroundTruthSourceSpan> SourceSpans)
{
    public OcrErrorClass ErrorClass { get; init; } = OcrErrorClass.Unclassified;

    /// <summary>
    /// schemaVersion 3+: who established <see cref="Expected"/> for this occurrence (e.g.
    /// "manual" - read against the book's own surrounding sentence, per docs/phase-5-plan.md@adc202d
    /// section 7.4 / D71). Absent on records written before schemaVersion 3.
    /// </summary>
    public string? VerifiedBy { get; init; }

    /// <summary>
    /// schemaVersion 3+: what the legacy OCR engine actually proposed for this occurrence, when
    /// that differs from <see cref="Expected"/> - i.e. legacy's own proposal was wrong and a
    /// human corrected it during verification (D71). Absent when there is nothing to flag: either
    /// legacy's proposal already matched <see cref="Expected"/>, or legacy made no proposal here
    /// at all (e.g. an occurrence only the lattice engine corrected).
    /// </summary>
    public string? LegacyProposal { get; init; }

    /// <summary>
    /// schemaVersion 3+: how this occurrence was found - "engine-diff" (drawn from
    /// docs/baselines/odun-kesmek.engine-diff.json's loss/gain/conflict lists), "auto" (drawn from
    /// a lattice decision report such as a Review/Apply verdict), or "manual" (found by direct
    /// inspection, not sourced from an engine run). Absent on records written before
    /// schemaVersion 3.
    /// </summary>
    public string? Source { get; init; }
}

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
