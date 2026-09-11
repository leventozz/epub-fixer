namespace EpubFixer.Core.Mutation.Models;

public sealed record OcrCorrectionMutationPlan(
    string SnapshotText,
    IReadOnlyList<OcrCorrectionMutation> Mutations,
    IReadOnlyList<OcrMutationFailure> Failures)
{
    public bool IsValid => Failures.Count == 0;
}
