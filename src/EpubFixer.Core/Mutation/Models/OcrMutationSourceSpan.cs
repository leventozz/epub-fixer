namespace EpubFixer.Core.Mutation.Models;

public sealed record OcrMutationSourceSpan(
    string DocumentPath,
    int TextNodeIndex,
    int Start,
    int Length,
    string ExpectedText);
