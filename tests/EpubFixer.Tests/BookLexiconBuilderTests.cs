using EpubFixer.Core.Epub;
using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Lexicon.Models;

namespace EpubFixer.Tests;

public sealed class BookLexiconBuilderTests
{
    [Fact]
    public void Build_CountsRepeatedWords()
    {
        using var epub = CreateSingleDocumentEpub("<p>Joana Joana Viyana</p>");

        var lexicon = BuildLexicon(epub);

        Assert.Equal(2, lexicon.GetCount("Joana"));
        Assert.Equal(1, lexicon.GetCount("Viyana"));
    }

    [Fact]
    public void Build_CountsTokenAcrossTextNodeBoundaries()
    {
        using var epub = CreateSingleDocumentEpub(
            "<p>Auer<span>sber</span>ger Auersberger</p>");

        var lexicon = BuildLexicon(epub);

        Assert.Equal(2, lexicon.GetCount("Auersberger"));
    }

    [Fact]
    public void Build_CountsApostropheFormsSeparately()
    {
        using var epub = CreateSingleDocumentEpub(
            "<p>Sokağı'na Sokağı'na Sokağı</p>");

        var lexicon = BuildLexicon(epub);

        Assert.Equal(2, lexicon.GetCount("Sokağı'na"));
        Assert.Equal(1, lexicon.GetCount("Sokağı"));
    }

    [Fact]
    public void Build_DoesNotCreateUnhyphenatedEntry()
    {
        using var epub = CreateSingleDocumentEpub("<p>Auersber-ger</p>");

        var lexicon = BuildLexicon(epub);

        Assert.False(lexicon.Contains("Auersberger"));
        Assert.Equal(0, lexicon.GetCount("Auersberger"));
    }

    [Fact]
    public void Build_PreservesTokenCase()
    {
        using var epub = CreateSingleDocumentEpub("<p>Joana joana</p>");

        var lexicon = BuildLexicon(epub);

        Assert.Equal(1, lexicon.GetCount("Joana"));
        Assert.Equal(1, lexicon.GetCount("joana"));
        Assert.False(lexicon.Contains("JOANA"));
    }

    private static BookLexicon BuildLexicon(TemporaryEpub epub)
    {
        var stream = ReadStream(epub);
        return new BookLexiconBuilder().Build(stream);
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
