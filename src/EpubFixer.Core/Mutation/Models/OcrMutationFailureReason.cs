namespace EpubFixer.Core.Mutation.Models;

public enum OcrMutationFailureReason
{
    MissingSelectedProposal,
    MissingSourceLocation,
    DocumentNotFound,
    InvalidSourceRange,
    SourceTextMismatch,
    EmptyOrInvalidReplacement,
    OverlappingMutation,
    ConflictingMutation,
    UnsupportedSourceGeometry,
    ReplacementFailed,
    UnexpectedTextChange
}
