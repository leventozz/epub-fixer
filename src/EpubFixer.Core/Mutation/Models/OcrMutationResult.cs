namespace EpubFixer.Core.Mutation.Models;

public sealed record OcrMutationResult(
    int PlannedCount,
    int AppliedCount,
    IReadOnlyList<OcrMutationFailure> Failures,
    int SingleSourceCount,
    int MultiSourceCount,
    int DocumentsChanged,
    int ReviewOccurrencesChanged,
    int DeferOccurrencesChanged,
    int UnexpectedTextChanges)
{
    public IReadOnlyList<OcrCorrectionMutation> AppliedMutations { get; init; } = [];
    public int ConflictCount => Failures.Count(item => item.Reason is OcrMutationFailureReason.OverlappingMutation or OcrMutationFailureReason.ConflictingMutation);
    public bool Succeeded => Failures.Count == 0 && AppliedCount == PlannedCount;
}
