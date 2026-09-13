namespace EpubFixer.Core.Ocr.Models;

public enum EditOperationKind
{
    Keep,
    KnownGlyphSubstitution,
    OrdinarySubstitution,
    SpaceDeletion,
    SpaceInsertion,
    HyphenDeletion,
    LineBreakDeletion,
    GarbageDeletion,
    OrdinaryDeletion,
    OrdinaryInsertion
}

public readonly record struct EditOperation(EditOperationKind Kind, int SourceIndex, char Source, char Target);

public sealed record EditAlignment(double Cost, IReadOnlyList<EditOperation> Operations)
{
    public int OrdinarySubstitutions { get; } = Operations.Count(x => x.Kind == EditOperationKind.OrdinarySubstitution);

    public int OrdinaryEdits { get; } = Operations.Count(x =>
        x.Kind is EditOperationKind.OrdinarySubstitution
            or EditOperationKind.OrdinaryDeletion
            or EditOperationKind.OrdinaryInsertion);
}
