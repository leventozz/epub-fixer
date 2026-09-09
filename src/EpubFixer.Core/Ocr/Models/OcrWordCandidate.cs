using EpubFixer.Core.Epub.Models;

namespace EpubFixer.Core.Ocr.Models;

public sealed record OcrWordCandidate(
    string Text,
    int LogicalStart,
    IReadOnlyList<TextSourceLocation> Sources,
    string Document,
    string ContextBefore,
    string ContextAfter);
