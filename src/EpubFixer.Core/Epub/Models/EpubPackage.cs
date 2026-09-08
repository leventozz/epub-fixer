namespace EpubFixer.Core.Epub.Models;

public sealed class EpubPackage
{
    internal EpubPackage(
        string version,
        IReadOnlyList<EpubContentDocument> spineDocuments,
        LogicalTextStream logicalText)
    {
        Version = version;
        SpineDocuments = spineDocuments;
        LogicalText = logicalText;
    }

    public string Version { get; }

    public IReadOnlyList<EpubContentDocument> SpineDocuments { get; }

    public LogicalTextStream LogicalText { get; }
}
