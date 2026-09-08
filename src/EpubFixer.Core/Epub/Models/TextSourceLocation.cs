using AngleSharp.Dom;

namespace EpubFixer.Core.Epub.Models;

public sealed record TextSourceLocation(
    string DocumentPath,
    int TextNodeIndex,
    IText SourceNode,
    int Start,
    int Length);
