namespace EpubFixer.Core.Epub.Models;

public sealed record EpubDocumentWriteDiagnostic(
    string Path,
    long OriginalByteCount,
    long OutputByteCount);

public sealed record EpubWriteResult(
    IReadOnlyList<string> ModifiedDocumentPaths,
    IReadOnlyList<EpubDocumentWriteDiagnostic> DocumentDiagnostics);
