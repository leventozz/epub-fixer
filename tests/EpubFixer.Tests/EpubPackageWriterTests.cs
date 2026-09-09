using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using AngleSharp.Dom;
using EpubFixer.Core.Epub;

namespace EpubFixer.Tests;

public sealed class EpubPackageWriterTests
{
    [Fact]
    public void Write_PersistsInlineCorrectionAndPreservesUntouchedEntries()
    {
        var image = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
        var css = Encoding.UTF8.GetBytes("p { color: black; }");
        using var epub = TemporaryEpub.Create(
            [new TestDocument("chapter", "chapter.xhtml", Xhtml("<p>haya-tım</p>"))],
            [new TestSpineItem("chapter")],
            additionalEntries: new Dictionary<string, byte[]>
            {
                ["OPS/image.bin"] = image,
                ["OPS/main.css"] = css
            });
        var outputPath = CreateOutputPath();
        var inputHashBefore = SHA256.HashData(File.ReadAllBytes(epub.Path));

        try
        {
            var package = new EpubPackageReader().Read(epub.Path);
            var sourceNode = Assert.Single(package.LogicalText.Segments).Source.SourceNode;
            sourceNode.Delete(sourceNode.Data.IndexOf('-', StringComparison.Ordinal), 1);

            var result = new EpubPackageWriter().Write(epub.Path, outputPath, package);
            var reloaded = new EpubPackageReader().Read(outputPath);

            Assert.Equal("hayatım", reloaded.LogicalText.Text);
            Assert.Equal(["OPS/chapter.xhtml"], result.ModifiedDocumentPaths);
            Assert.Equal(inputHashBefore, SHA256.HashData(File.ReadAllBytes(epub.Path)));
            Assert.Equal(GetEntryNames(epub.Path), GetEntryNames(outputPath));
            Assert.Equal(GetEntryHash(epub.Path, "OPS/image.bin"), GetEntryHash(outputPath, "OPS/image.bin"));
            Assert.Equal(GetEntryHash(epub.Path, "OPS/main.css"), GetEntryHash(outputPath, "OPS/main.css"));
            Assert.Equal(GetEntryHash(epub.Path, "OPS/package.opf"), GetEntryHash(outputPath, "OPS/package.opf"));
            AssertMimetypeIsFirstAndStored(outputPath);

            var xhtml = ReadEntryText(outputPath, "OPS/chapter.xhtml");
            Assert.StartsWith("<?xml", xhtml, StringComparison.Ordinal);
            Assert.False(xhtml.StartsWith("<!--?xml", StringComparison.Ordinal));
            Assert.Contains("encoding=\"utf-8\"", xhtml, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void Write_PersistsCrossParagraphMergeWithoutLosingInlineMarkup()
    {
        using var epub = TemporaryEpub.Create(
            [new TestDocument(
                "chapter",
                "chapter.xhtml",
                Xhtml("<p>haya-</p><p><em>tım</em> güzel</p>"))],
            [new TestSpineItem("chapter")]);
        var outputPath = CreateOutputPath();

        try
        {
            var package = new EpubPackageReader().Read(epub.Path);
            var paragraphs = package.SpineDocuments[0].Document.QuerySelectorAll("p").ToArray();
            var leftText = Assert.IsAssignableFrom<IText>(paragraphs[0].LastChild);
            leftText.Delete(leftText.Data.Length - 1, 1);

            foreach (var child in paragraphs[1].ChildNodes.ToArray())
            {
                paragraphs[0].AppendChild(child);
            }

            paragraphs[1].Remove();

            new EpubPackageWriter().Write(epub.Path, outputPath, package);
            var reloaded = new EpubPackageReader().Read(outputPath);
            var outputDocument = reloaded.SpineDocuments[0].Document;

            Assert.Single(outputDocument.QuerySelectorAll("p"));
            Assert.Equal("hayatım güzel", reloaded.LogicalText.Text);
            Assert.Equal("tım", Assert.Single(outputDocument.QuerySelectorAll("em")).TextContent);
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void Write_RejectsUnexpectedStructuralRoundTripDifferenceWithoutCreatingOutput()
    {
        using var epub = TemporaryEpub.Create(
            [new TestDocument("chapter", "chapter.xhtml", Xhtml("<p>text</p>"))],
            [new TestSpineItem("chapter")]);
        var outputPath = CreateOutputPath();
        var package = new EpubPackageReader().Read(epub.Path);
        var paragraph = Assert.Single(package.SpineDocuments[0].Document.QuerySelectorAll("p"));
        paragraph.AppendChild(package.SpineDocuments[0].Document.CreateTextNode("more"));

        var exception = Assert.Throws<InvalidDataException>(() =>
            new EpubPackageWriter().Write(epub.Path, outputPath, package));

        Assert.Contains("changed the DOM structure", exception.Message, StringComparison.Ordinal);
        Assert.Contains("OPS/chapter.xhtml", exception.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(outputPath));
    }

    private static string Xhtml(string body)
    {
        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <!DOCTYPE html>
            <html xmlns="http://www.w3.org/1999/xhtml">
              <head><title></title></head>
              <body>{body}</body>
            </html>
            """;
    }

    private static string CreateOutputPath()
    {
        return Path.Combine(Path.GetTempPath(), $"epubfixer-output-{Guid.NewGuid():N}.epub");
    }

    private static string[] GetEntryNames(string path)
    {
        using var file = File.OpenRead(path);
        using var archive = new ZipArchive(file, ZipArchiveMode.Read);
        return archive.Entries.Select(entry => entry.FullName).Order(StringComparer.Ordinal).ToArray();
    }

    private static byte[] GetEntryHash(string path, string entryPath)
    {
        using var file = File.OpenRead(path);
        using var archive = new ZipArchive(file, ZipArchiveMode.Read);
        using var stream = archive.GetEntry(entryPath)!.Open();
        return SHA256.HashData(stream);
    }

    private static string ReadEntryText(string path, string entryPath)
    {
        using var file = File.OpenRead(path);
        using var archive = new ZipArchive(file, ZipArchiveMode.Read);
        using var stream = archive.GetEntry(entryPath)!.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static void AssertMimetypeIsFirstAndStored(string path)
    {
        using (var file = File.OpenRead(path))
        using (var archive = new ZipArchive(file, ZipArchiveMode.Read))
        {
            Assert.Equal("mimetype", archive.Entries[0].FullName);
            Assert.Equal("application/epub+zip", ReadEntryText(path, "mimetype"));
        }

        var bytes = File.ReadAllBytes(path);
        Assert.Equal(0x04034b50u, BitConverter.ToUInt32(bytes, 0));
        Assert.Equal(0, BitConverter.ToUInt16(bytes, 8));
        var nameLength = BitConverter.ToUInt16(bytes, 26);
        Assert.Equal("mimetype", Encoding.UTF8.GetString(bytes, 30, nameLength));
    }
}
