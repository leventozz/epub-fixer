using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Mutation.Models;

namespace EpubFixer.Core.Mutation;

/// <summary>
/// Converts region-based OCR corrections (produced by a statistical engine such as the
/// lattice reconstructor) into the same <see cref="OcrCorrectionMutationPlan"/> the legacy,
/// decision-based <see cref="OcrCorrectionMutationPlanner"/> produces. A correction that
/// cannot be safely applied (out of range, unchanged, crossing a document boundary, or
/// overlapping an already-accepted correction) is skipped rather than failing the whole
/// plan (D55) - the produced plan's <see cref="OcrCorrectionMutationPlan.Failures"/> is
/// therefore always empty and <see cref="OcrCorrectionMutationPlan.IsValid"/> always true.
/// </summary>
public sealed class RegionMutationPlanner
{
    public RegionMutationPlanResult Create(
        IReadOnlyList<RegionCorrection> corrections,
        LogicalTextStream stream)
    {
        ArgumentNullException.ThrowIfNull(corrections);
        ArgumentNullException.ThrowIfNull(stream);

        var candidates = new List<(RegionCorrection Correction, string DocumentPath, IReadOnlyList<OcrMutationSourceSpan> Spans)>();
        var skipped = new List<SkippedRegionCorrection>();

        foreach (var correction in corrections)
        {
            if (correction.LogicalStart < 0
                || correction.LogicalEndExclusive <= correction.LogicalStart
                || correction.LogicalEndExclusive > stream.Text.Length)
            {
                skipped.Add(new(correction, RegionCorrectionSkipReason.OutOfRange));
                continue;
            }

            if (string.IsNullOrEmpty(correction.Replacement))
            {
                skipped.Add(new(correction, RegionCorrectionSkipReason.EmptyReplacement));
                continue;
            }

            var originalText = stream.Text[correction.LogicalStart..correction.LogicalEndExclusive];
            if (string.Equals(originalText, correction.Replacement, StringComparison.Ordinal))
            {
                skipped.Add(new(correction, RegionCorrectionSkipReason.NoChange));
                continue;
            }

            var firstLocation = stream.GetSourceLocationAt(correction.LogicalStart);
            var lastLocation = stream.GetSourceLocationAt(correction.LogicalEndExclusive - 1);
            if (!string.Equals(firstLocation.DocumentPath, lastLocation.DocumentPath, StringComparison.Ordinal))
            {
                skipped.Add(new(correction, RegionCorrectionSkipReason.CrossesDocument));
                continue;
            }

            var spans = RegionMutationGeometry.BuildSourceSpans(
                stream,
                correction.LogicalStart,
                correction.LogicalEndExclusive);
            candidates.Add((correction, firstLocation.DocumentPath, spans));
        }

        var accepted = new List<OcrCorrectionMutation>();
        foreach (var candidate in candidates
            .OrderBy(item => item.DocumentPath, StringComparer.Ordinal)
            .ThenBy(item => item.Correction.LogicalStart)
            .ThenBy(item => item.Correction.LogicalEndExclusive)
            .ThenBy(item => item.Correction.Replacement, StringComparer.Ordinal))
        {
            var overlaps = accepted.Any(item =>
                string.Equals(item.DocumentPath, candidate.DocumentPath, StringComparison.Ordinal)
                && item.LogicalStart < candidate.Correction.LogicalEndExclusive
                && candidate.Correction.LogicalStart < item.LogicalStart + item.LogicalLength);
            if (overlaps)
            {
                skipped.Add(new(candidate.Correction, RegionCorrectionSkipReason.Overlapping));
                continue;
            }

            accepted.Add(new(
                candidate.DocumentPath,
                candidate.Correction.LogicalStart,
                candidate.Correction.LogicalEndExclusive - candidate.Correction.LogicalStart,
                stream.Text[candidate.Correction.LogicalStart..candidate.Correction.LogicalEndExclusive],
                candidate.Correction.Replacement,
                BuildProvenance(candidate.Correction, stream),
                candidate.Spans));
        }

        var plan = new OcrCorrectionMutationPlan(
            stream.Text,
            accepted
                .OrderBy(item => item.DocumentPath, StringComparer.Ordinal)
                .ThenBy(item => item.LogicalStart)
                .ToArray(),
            []);
        return new(plan, skipped);
    }

    private static OcrMutationProvenance BuildProvenance(RegionCorrection correction, LogicalTextStream stream)
    {
        var originalText = stream.Text[correction.LogicalStart..correction.LogicalEndExclusive];
        return new(
            OcrCorrectionEngine.Lattice,
            string.Join("; ", correction.Acceptance.Reasons),
            null,
            originalText.Any(char.IsWhiteSpace));
    }
}
