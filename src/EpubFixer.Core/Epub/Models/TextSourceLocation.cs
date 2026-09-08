using AngleSharp.Dom;

namespace EpubFixer.Core.Epub.Models;

public sealed record TextSourceLocation(
    string DocumentPath,
    IText SourceNode,
    int Start,
    int Length);
