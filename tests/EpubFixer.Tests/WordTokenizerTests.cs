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
    public void Tokenize_TextNodeBoundaryContinuesToken()
    {
        using var epub = CreateSingleDocumentEpub(
            "<p><span>gittim</span><span>sonra</span></p>");

        var stream = ReadStream(epub);
        var tokens = new WordTokenizer().Tokenize(stream);

        Assert.Equal("gittimsonra", stream.Text);
        Assert.Equal(TextBoundaryKind.TextNode, Assert.Single(stream.Boundaries).Kind);
        Assert.Equal(["gittimsonra"], tokens.Select(token => token.Text));
        Assert.Equal(2, Assert.Single(tokens).Sources.Count);
    }

    [Fact]
    public void Tokenize_JoinsTurkishWordAcrossInlineElementAndPreservesSources()
    {
        using var epub = CreateSingleDocumentEpub("<p><sup>İ</sup>nsan</p>");
        var stream = ReadStream(epub);

        var token = Assert.Single(new WordTokenizer().Tokenize(stream));

        Assert.Equal("İnsan", token.Text);
        Assert.Equal(0, token.LogicalStart);
        Assert.Equal(5, token.Length);
        Assert.Equal(2, token.Sources.Count);
        Assert.Equal(["İ", "nsan"], token.Sources.Select(SourceText));
        Assert.Equal([0, 1], token.Sources.Select(source => source.TextNodeIndex));
        Assert.Equal([(0, 1), (0, 4)], token.Sources.Select(source => (source.Start, source.Length)));
        Assert.NotSame(token.Sources[0].SourceNode, token.Sources[1].SourceNode);
    }

    [Fact]
    public void Tokenize_JoinsWordAcrossMultipleInlineBoundaries()
    {
        using var epub = CreateSingleDocumentEpub("<p>Auer<span>sber</span>ger</p>");

        var token = Assert.Single(Tokenize(epub));

        Assert.Equal("Auersberger", token.Text);
        Assert.Equal(["Auer", "sber", "ger"], token.Sources.Select(SourceText));
        Assert.Equal([(0, 4), (0, 4), (0, 3)], token.Sources.Select(source => (source.Start, source.Length)));
    }

    [Fact]
    public void Tokenize_WhitespaceAndPunctuationAcrossTextNodesEndTokens()
    {
        using var epub = CreateSingleDocumentEpub(
            "<p><span>gittim</span> <span>sonra</span>,<span>bugün</span></p>");

        var tokens = Tokenize(epub);

        Assert.Equal(["gittim", "sonra", "bugün"], tokens.Select(token => token.Text));
    }

    [Fact]
    public void Tokenize_PreservesInternalApostrophesAcrossTextNodes()
    {
        using var epub = CreateSingleDocumentEpub(
            "<p>Sokağı<span>'na</span> Auersbergerler’<span>e</span></p>");

        var tokens = Tokenize(epub);

        Assert.Equal(["Sokağı'na", "Auersbergerler’e"], tokens.Select(token => token.Text));
        Assert.All(tokens, token => Assert.Equal(2, token.Sources.Count));
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
        var source = Assert.Single(token.Sources);
        Assert.Equal(segment.Source.DocumentPath, source.DocumentPath);
        Assert.Equal(segment.Source.TextNodeIndex, source.TextNodeIndex);
        Assert.Same(segment.Source.SourceNode, source.SourceNode);
        Assert.Equal((5, 9), (source.Start, source.Length));
        Assert.Equal(token.Text, SourceText(source));
        Assert.Equal(originalText, stream.Text);
        Assert.Equal(originalNodeText, segment.Source.SourceNode.Data);
    }

    private static string SourceText(TextSourceLocation source)
    {
        return source.SourceNode.Data.Substring(source.Start, source.Length);
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
