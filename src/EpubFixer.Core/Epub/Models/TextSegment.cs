namespace EpubFixer.Core.Epub.Models;

public sealed record TextSegment(
    string Text,
    int LogicalStart,
    TextSourceLocation Source)
{
    public int Length => Text.Length;
}
