namespace EpubFixer.Core.Mutation.Models;

public sealed record OcrMutationFailure(
    OcrMutationFailureReason Reason,
    string Message,
    OcrCorrectionMutation? Mutation = null);
