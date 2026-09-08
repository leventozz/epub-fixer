using EpubFixer.Core.Epub;
using EpubFixer.Core.Epub.Models;

namespace EpubFixer.Tests;

public sealed class EpubPackageReaderTests
{
    [Fact]
    public void Read_PreservesParagraphBoundaryAndSourceLocation()
    {
        using var epub = TemporaryEpub.Create(
            [
                new TestDocument(
                    "chapter",
                    "chapter.xhtml",
                    Xhtml("<p>Viyana sosyete-</p><p>si cehennemine...</p>"))
            ],
            [new TestSpineItem("chapter")]);

        var package = new EpubPackageReader().Read(epub.Path);
        var stream = package.LogicalText;

        Assert.Equal("Viyana sosyete-si cehennemine...", stream.Text);
        Assert.Equal(2, stream.Segments.Count);
        Assert.Single(stream.Boundaries);
        Assert.Equal(TextBoundaryKind.Paragraph, stream.Boundaries[0].Kind);
        Assert.Equal("OPS/chapter.xhtml", stream.Segments[1].Source.DocumentPath);
        Assert.Equal(0, stream.Segments[0].Source.TextNodeIndex);
        Assert.Equal(1, stream.Segments[1].Source.TextNodeIndex);
        Assert.Equal("si cehennemine...", stream.Segments[1].Source.SourceNode.Data);
        Assert.Equal(0, stream.Segments[1].Source.Start);
        Assert.Equal(stream.Segments[1].Text.Length, stream.Segments[1].Source.Length);

        var secondSegmentStart = stream.Segments[1].LogicalStart;
        Assert.Same(stream.Segments[1], stream.GetSegmentAt(secondSegmentStart));

        var characterSource = stream.GetSourceLocationAt(secondSegmentStart + 1);
        Assert.Same(stream.Segments[1].Source.SourceNode, characterSource.SourceNode);
        Assert.Equal(1, characterSource.Start);
        Assert.Equal(1, characterSource.Length);
        Assert.Equal("Viyana sosyete-\nsi cehennemine...", stream.CreateDebugText());
    }

    [Fact]
    public void Read_PreservesDocumentBoundaryAcrossLinearDocuments()
    {
        using var epub = TemporaryEpub.Create(
            [
                new TestDocument("a", "a.xhtml", Xhtml("<p>İbsen gerçekten bir yazar-</p>")),
                new TestDocument("b", "b.xhtml", Xhtml("<p>mış, tıpkı Strindberg gibi...</p>"))
            ],
            [new TestSpineItem("a"), new TestSpineItem("b")]);

        var stream = new EpubPackageReader().Read(epub.Path).LogicalText;

        Assert.Equal("İbsen gerçekten bir yazar-mış, tıpkı Strindberg gibi...", stream.Text);
        Assert.Single(stream.Boundaries);
        Assert.Equal(TextBoundaryKind.Document, stream.Boundaries[0].Kind);
        Assert.Equal(
            "İbsen gerçekten bir yazar-\n\nmış, tıpkı Strindberg gibi...",
            stream.CreateDebugText());
    }

    [Fact]
    public void Read_UsesSpineOrderInsteadOfArchiveOrFileNameOrder()
    {
        using var epub = TemporaryEpub.Create(
            [
                new TestDocument("alphabeticallyFirst", "Text/a-first.xhtml", Xhtml("<p>A</p>")),
                new TestDocument("spineFirst", "Text/chapter z.xhtml", Xhtml("<p>Z</p>"))
            ],
            [new TestSpineItem("spineFirst"), new TestSpineItem("alphabeticallyFirst")],
            hrefOverrides: new Dictionary<string, string>
            {
                ["spineFirst"] = "Text/chapter%20z.xhtml"
            });

        var package = new EpubPackageReader().Read(epub.Path);

        Assert.Equal(["OPS/Text/chapter z.xhtml", "OPS/Text/a-first.xhtml"], package.SpineDocuments.Select(document => document.Path));
        Assert.Equal("ZA", package.LogicalText.Text);
        Assert.Equal(TextBoundaryKind.Document, Assert.Single(package.LogicalText.Boundaries).Kind);
    }

    [Fact]
    public void Read_KeepsNonLinearDocumentMetadataButExcludesItsText()
    {
        using var epub = TemporaryEpub.Create(
            [
                new TestDocument("a", "a.xhtml", Xhtml("<p>A</p>")),
                new TestDocument("notes", "notes.xhtml", Xhtml("<p>NOTES</p>")),
                new TestDocument("b", "b.xhtml", Xhtml("<p>B</p>"))
            ],
            [
                new TestSpineItem("a"),
                new TestSpineItem("notes", IsLinear: false),
                new TestSpineItem("b")
            ]);

        var package = new EpubPackageReader().Read(epub.Path);

        Assert.Equal(3, package.SpineDocuments.Count);
        Assert.False(package.SpineDocuments[1].IsLinear);
        Assert.Equal("AB", package.LogicalText.Text);
        Assert.Equal(TextBoundaryKind.Document, Assert.Single(package.LogicalText.Boundaries).Kind);
    }

    [Fact]
    public void Read_ExcludesNonReaderNodesAndPreservesMeaningfulInlineWhitespace()
    {
        const string body = """
            <p><span>Visible</span> <em>inline</em><script>hidden script</script><style>hidden style</style><template>hidden template</template> text</p>
            <p>Second</p>
            """;
        using var epub = TemporaryEpub.Create(
            [new TestDocument("chapter", "chapter.xhtml", Xhtml(body))],
            [new TestSpineItem("chapter")]);

        var stream = new EpubPackageReader().Read(epub.Path).LogicalText;

        Assert.Equal("Visible inline textSecond", stream.Text);
        Assert.DoesNotContain("hidden", stream.Text, StringComparison.Ordinal);
        Assert.Equal(3, stream.Boundaries.Count(boundary => boundary.Kind == TextBoundaryKind.TextNode));
        Assert.Equal(TextBoundaryKind.Paragraph, stream.Boundaries[^1].Kind);
    }

    [Fact]
    public void GetSegmentAt_RejectsIndexesOutsideTheCanonicalText()
    {
        using var epub = TemporaryEpub.Create(
            [new TestDocument("chapter", "chapter.xhtml", Xhtml("<p>text</p>"))],
            [new TestSpineItem("chapter")]);
        var stream = new EpubPackageReader().Read(epub.Path).LogicalText;

        Assert.Throws<ArgumentOutOfRangeException>(() => stream.GetSegmentAt(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.GetSegmentAt(stream.CharacterCount));
    }

    private static string Xhtml(string body)
    {
        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <!DOCTYPE html>
            <html xmlns="http://www.w3.org/1999/xhtml">
              <head><title>Test</title><style>hidden style</style></head>
              <body>{body}</body>
            </html>
            """;
    }
}
