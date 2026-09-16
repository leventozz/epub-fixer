using EpubFixer.Core.Ocr.Lattice.Models;

namespace EpubFixer.Core.Mutation.Models;

public sealed record RegionCorrection(
    int LogicalStart,
    int LogicalEndExclusive,
    string Replacement,
    AcceptanceResult Acceptance);

public enum RegionCorrectionSkipReason
{
    OutOfRange,
    NoChange,
    Overlapping,
    CrossesDocument,
    EmptyReplacement
}

public sealed record SkippedRegionCorrection(
    RegionCorrection Correction,
    RegionCorrectionSkipReason Reason);
