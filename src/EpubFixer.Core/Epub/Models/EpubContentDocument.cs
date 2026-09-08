using AngleSharp.Dom;

namespace EpubFixer.Core.Epub.Models;

public sealed class EpubContentDocument
{
    internal EpubContentDocument(string path, int spineIndex, bool isLinear, IDocument document)
    {
        Path = path;
        SpineIndex = spineIndex;
        IsLinear = isLinear;
        Document = document;
    }

    public string Path { get; }

    public int SpineIndex { get; }

    public bool IsLinear { get; }

    public IDocument Document { get; }
}
