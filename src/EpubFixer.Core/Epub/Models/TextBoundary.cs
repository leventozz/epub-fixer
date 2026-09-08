namespace EpubFixer.Core.Epub.Models;

public sealed record TextBoundary(
    int BeforeSegmentIndex,
    int AfterSegmentIndex,
    TextBoundaryKind Kind);
