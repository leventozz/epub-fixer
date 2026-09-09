using AngleSharp.Dom;
using EpubFixer.Core.Epub;

namespace EpubFixer.Core.Epub.Models;

public sealed class EpubContentDocument
{
    internal EpubContentDocument(
        string path,
        int spineIndex,
        bool isLinear,
        IDocument document,
        string? xmlDeclaration)
    {
        Path = path;
        SpineIndex = spineIndex;
        IsLinear = isLinear;
        Document = document;
        XmlDeclaration = xmlDeclaration;
        OriginalStructureFingerprint = DomStructure.GetFingerprint(document);
    }

    public string Path { get; }

    public int SpineIndex { get; }

    public bool IsLinear { get; }

    public IDocument Document { get; }

    internal string? XmlDeclaration { get; }

    internal string OriginalStructureFingerprint { get; }
}
