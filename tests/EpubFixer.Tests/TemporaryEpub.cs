using System.IO.Compression;
using System.Text;

namespace EpubFixer.Tests;

internal sealed class TemporaryEpub : IDisposable
{
    private TemporaryEpub(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public static TemporaryEpub Create(
        IReadOnlyList<TestDocument> documentsInArchiveOrder,
        IReadOnlyList<TestSpineItem> spine,
        IReadOnlyDictionary<string, string>? hrefOverrides = null)
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"epubfixer-{Guid.NewGuid():N}.epub");

        using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
        {
            WriteEntry(archive, "mimetype", "application/epub+zip", CompressionLevel.NoCompression);
            WriteEntry(archive, "META-INF/container.xml", ContainerXml);
            WriteEntry(archive, "OPS/package.opf", CreatePackageDocument(documentsInArchiveOrder, spine, hrefOverrides));

            foreach (var document in documentsInArchiveOrder)
            {
                WriteEntry(archive, $"OPS/{document.Href}", document.Content);
            }
        }

        return new TemporaryEpub(path);
    }

    public void Dispose()
    {
        File.Delete(Path);
    }

    private static string CreatePackageDocument(
        IReadOnlyList<TestDocument> documents,
        IReadOnlyList<TestSpineItem> spine,
        IReadOnlyDictionary<string, string>? hrefOverrides)
    {
        var manifest = string.Join(
            "",
            documents.Select(document =>
            {
                var href = hrefOverrides?.GetValueOrDefault(document.Id) ?? document.Href;
                return $"<item id=\"{document.Id}\" href=\"{href}\" media-type=\"application/xhtml+xml\"/>";
            }));
        var itemReferences = string.Join(
            "",
            spine.Select(item => $"<itemref idref=\"{item.Id}\"{(item.IsLinear ? "" : " linear=\"no\"")}/>"));

        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://www.idpf.org/2007/opf" version="2.0">
              <manifest>{manifest}</manifest>
              <spine toc="ncx">{itemReferences}</spine>
            </package>
            """;
    }

    private static void WriteEntry(
        ZipArchive archive,
        string path,
        string content,
        CompressionLevel compressionLevel = CompressionLevel.Optimal)
    {
        var entry = archive.CreateEntry(path, compressionLevel);

        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }

    private const string ContainerXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <container xmlns="urn:oasis:names:tc:opendocument:xmlns:container" version="1.0">
          <rootfiles>
            <rootfile full-path="OPS/package.opf" media-type="application/oebps-package+xml"/>
          </rootfiles>
        </container>
        """;
}

internal sealed record TestDocument(string Id, string Href, string Content);

internal sealed record TestSpineItem(string Id, bool IsLinear = true);
