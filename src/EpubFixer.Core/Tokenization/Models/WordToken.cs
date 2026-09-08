using EpubFixer.Core.Epub.Models;

namespace EpubFixer.Core.Tokenization.Models;

public sealed record WordToken(
    string Text,
    int LogicalStart,
    IReadOnlyList<TextSourceLocation> Sources)
{
    public int Length => Text.Length;
}
