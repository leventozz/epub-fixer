using EpubFixer.Core.Epub;
using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Tokenization;

namespace EpubFixer.Tests;

public sealed class WordTokenizerTests
{
    [Fact]
    public void Tokenize_ReturnsSeparateWordsFromOneSegment()
    {
        using var epub = CreateSingleDocumentEpub("<p>berjer koltukta</p>");

        var tokens = Tokenize(epub);

        Assert.Equal(["berjer", "koltukta"], tokens.Select(token => token.Text));
    }

    [Fact]
    public void Tokenize_UsesUnicodeLetterRules()
    {
        using var epub = CreateSingleDocumentEpub("<p>çağrı İğdır 𐐀𐐁</p>");

        var tokens = Tokenize(epub);

        Assert.Equal(["çağrı", "İğdır", "𐐀𐐁"], tokens.Select(token => token.Text));
        Assert.Equal(4, tokens[2].Length);
    }

    [Fact]
    public void Tokenize_PreservesOnlyInternalApostrophes()
    {
        using var epub = CreateSingleDocumentEpub(
            "<p>Sokağı'na Auersbergerler’e Gentz'te 'tek' '</p>");

        var tokens = Tokenize(epub);

        Assert.Equal(
            ["Sokağı'na", "Auersbergerler’e", "Gentz'te", "tek"],
            tokens.Select(token => token.Text));
    }

    [Fact]
    public void Tokenize_TextNodeBoundaryEndsToken()
    {
        using var epub = CreateSingleDocumentEpub(
            "<p><span>gittim</span><span>sonra</span></p>");

        var stream = ReadStream(epub);
        var tokens = new WordTokenizer().Tokenize(stream);

        Assert.Equal("gittimsonra", stream.Text);
        Assert.Equal(TextBoundaryKind.TextNode, Assert.Single(stream.Boundaries).Kind);
        Assert.Equal(["gittim", "sonra"], tokens.Select(token => token.Text));
    }

    [Fact]
    public void Tokenize_ParagraphBoundaryEndsToken()
    {
        using var epub = CreateSingleDocumentEpub("<p>geldim</p><p>sonra</p>");

        var stream = ReadStream(epub);
        var tokens = new WordTokenizer().Tokenize(stream);

        Assert.Equal("geldimsonra", stream.Text);
        Assert.Equal(TextBoundaryKind.Paragraph, Assert.Single(stream.Boundaries).Kind);
        Assert.Equal(["geldim", "sonra"], tokens.Select(token => token.Text));
    }

    [Fact]
    public void Tokenize_DocumentBoundaryEndsToken()
    {
        using var epub = TemporaryEpub.Create(
            [
                new TestDocument("a", "a.xhtml", Xhtml("<p>gittim</p>")),
                new TestDocument("b", "b.xhtml", Xhtml("<p>sonra</p>"))
            ],
            [new TestSpineItem("a"), new TestSpineItem("b")]);

        var stream = ReadStream(epub);
        var tokens = new WordTokenizer().Tokenize(stream);

        Assert.Equal("gittimsonra", stream.Text);
        Assert.Equal(TextBoundaryKind.Document, Assert.Single(stream.Boundaries).Kind);
        Assert.Equal(["gittim", "sonra"], tokens.Select(token => token.Text));
    }

    [Fact]
    public void Tokenize_HyphenEndsTokenWithoutJoiningParts()
    {
        using var epub = CreateSingleDocumentEpub("<p>Auersber-ger</p>");

        var tokens = Tokenize(epub);

        Assert.Equal(["Auersber", "ger"], tokens.Select(token => token.Text));
        Assert.DoesNotContain(tokens, token => token.Text == "Auersberger");
    }

    [Fact]
    public void Tokenize_PreservesLogicalAndSourceLocationsWithoutMutation()
    {
        using var epub = CreateSingleDocumentEpub("<p>önce Sokağı'na sonra</p>");
        var stream = ReadStream(epub);
        var originalText = stream.Text;
        var segment = Assert.Single(stream.Segments);
        var originalNodeText = segment.Source.SourceNode.Data;

        var token = new WordTokenizer().Tokenize(stream)[1];

        Assert.Equal("Sokağı'na", token.Text);
        Assert.Equal(5, token.LogicalStart);
        Assert.Equal(9, token.Length);
        Assert.Equal(segment.Source.DocumentPath, token.Source.DocumentPath);
        Assert.Equal(segment.Source.TextNodeIndex, token.Source.TextNodeIndex);
        Assert.Same(segment.Source.SourceNode, token.Source.SourceNode);
        Assert.Equal((5, 9), (token.Source.Start, token.Source.Length));
        Assert.Equal(token.Text, token.Source.SourceNode.Data.Substring(token.Source.Start, token.Source.Length));
        Assert.Equal(originalText, stream.Text);
        Assert.Equal(originalNodeText, segment.Source.SourceNode.Data);
    }

    private static IReadOnlyList<EpubFixer.Core.Tokenization.Models.WordToken> Tokenize(
        TemporaryEpub epub)
    {
        return new WordTokenizer().Tokenize(ReadStream(epub));
    }

    private static LogicalTextStream ReadStream(TemporaryEpub epub)
    {
        return new EpubPackageReader().Read(epub.Path).LogicalText;
    }

    private static TemporaryEpub CreateSingleDocumentEpub(string body)
    {
        return TemporaryEpub.Create(
            [new TestDocument("chapter", "chapter.xhtml", Xhtml(body))],
            [new TestSpineItem("chapter")]);
    }

    private static string Xhtml(string body)
    {
        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <!DOCTYPE html>
            <html xmlns="http://www.w3.org/1999/xhtml">
              <head><title>Test</title></head>
              <body>{body}</body>
            </html>
            """;
    }
}
