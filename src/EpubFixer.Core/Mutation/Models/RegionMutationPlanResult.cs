namespace EpubFixer.Core.Mutation.Models;

public sealed record RegionMutationPlanResult(
    OcrCorrectionMutationPlan Plan,
    IReadOnlyList<SkippedRegionCorrection> Skipped);
