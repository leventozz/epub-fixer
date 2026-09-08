using EpubFixer.Core.Epub.Models;

namespace EpubFixer.Core.Tokenization.Models;

public sealed record WordToken(
    string Text,
    int LogicalStart,
    TextSourceLocation Source)
{
    public int Length => Text.Length;
}
