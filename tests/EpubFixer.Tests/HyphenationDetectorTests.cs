using EpubFixer.Core.Detection;
using EpubFixer.Core.Detection.Models;

namespace EpubFixer.Tests;

public sealed class HyphenationDetectorTests
{
    [Theory]
    [InlineData("Auersber-ger", "Auersber", "ger", "Auersberger")]
    [InlineData("Jo-ana", "Jo", "ana", "Joana")]
    [InlineData("Viya-na", "Viya", "na", "Viyana")]
    public void Detect_FindsInlineCandidate(
        string text,
        string expectedLeft,
        string expectedRight,
        string expectedUnhyphenated)
    {
        using var epub = CreateSingleDocumentEpub($"<p>{text}</p>");
        var stream = ReadStream(epub);

        var candidate = Assert.Single(new HyphenationDetector().Detect(stream));

        Assert.Equal(expectedLeft, candidate.LeftPart);
        Assert.Equal(expectedRight, candidate.RightPart);
        Assert.Equal(expectedUnhyphenated, candidate.UnhyphenatedText);
        Assert.Equal(HyphenationDetectionKind.Inline, candidate.DetectionKind);
    }

    [Fact]
    public void Detect_MapsInlineSourceRangesToTheOriginalTextNode()
    {
        using var epub = CreateSingleDocumentEpub("<p>Auersber-ger</p>");
        var stream = ReadStream(epub);
        var segment = Assert.Single(stream.Segments);

        var candidate = Assert.Single(new HyphenationDetector().Detect(stream));

        Assert.Same(segment.Source.SourceNode, candidate.LeftSource.SourceNode);
        Assert.Same(segment.Source.SourceNode, candidate.HyphenSource.SourceNode);
        Assert.Same(segment.Source.SourceNode, candidate.RightSource.SourceNode);
        Assert.Equal((0, 8), (candidate.LeftSource.Start, candidate.LeftSource.Length));
        Assert.Equal((8, 1), (candidate.HyphenSource.Start, candidate.HyphenSource.Length));
        Assert.Equal((9, 3), (candidate.RightSource.Start, candidate.RightSource.Length));
        Assert.Equal("OPS/chapter.xhtml", candidate.LeftSource.DocumentPath);
    }

    [Fact]
    public void Detect_FindsTextNodeBoundaryCandidate()
    {
        using var epub = CreateSingleDocumentEpub("<p><span>Viya-</span><span>na</span></p>");
        var stream = ReadStream(epub);

        var candidate = Assert.Single(new HyphenationDetector().Detect(stream));

        Assert.Equal("Viyana", candidate.UnhyphenatedText);
        Assert.Equal(HyphenationDetectionKind.TextNodeBoundary, candidate.DetectionKind);
        Assert.NotSame(candidate.LeftSource.SourceNode, candidate.RightSource.SourceNode);
    }

    [Fact]
    public void Detect_FindsParagraphBoundaryCandidateAndPreservesSources()
    {
        using var epub = CreateSingleDocumentEpub("<p>Viyana sosyete-</p><p>si cehennemine...</p>");
        var stream = ReadStream(epub);
        var originalText = stream.Text;
        var originalNodeTexts = stream.Segments.Select(segment => segment.Source.SourceNode.Data).ToArray();

        var candidate = Assert.Single(new HyphenationDetector().Detect(stream));

        Assert.Equal("sosyete", candidate.LeftPart);
        Assert.Equal("si", candidate.RightPart);
        Assert.Equal("sosyetesi", candidate.UnhyphenatedText);
        Assert.Equal(HyphenationDetectionKind.ParagraphBoundary, candidate.DetectionKind);
        Assert.Equal((7, 7), (candidate.LeftSource.Start, candidate.LeftSource.Length));
        Assert.Equal((14, 1), (candidate.HyphenSource.Start, candidate.HyphenSource.Length));
        Assert.Equal((0, 2), (candidate.RightSource.Start, candidate.RightSource.Length));
        Assert.Equal(candidate.LeftSource.DocumentPath, candidate.RightSource.DocumentPath);
        Assert.NotSame(candidate.LeftSource.SourceNode, candidate.RightSource.SourceNode);
        Assert.Equal(originalText, stream.Text);
        Assert.Equal(originalNodeTexts, stream.Segments.Select(segment => segment.Source.SourceNode.Data));
    }

    [Fact]
    public void Detect_FindsDocumentBoundaryCandidate()
    {
        using var epub = TemporaryEpub.Create(
            [
                new TestDocument("a", "a.xhtml", Xhtml("<p>İbsen gerçekten bir yazar-</p>")),
                new TestDocument("b", "b.xhtml", Xhtml("<p>mış, tıpkı Strindberg gibi...</p>"))
            ],
            [new TestSpineItem("a"), new TestSpineItem("b")]);
        var stream = ReadStream(epub);

        var candidate = Assert.Single(new HyphenationDetector().Detect(stream));

        Assert.Equal("yazar", candidate.LeftPart);
        Assert.Equal("mış", candidate.RightPart);
        Assert.Equal("yazarmış", candidate.UnhyphenatedText);
        Assert.Equal(HyphenationDetectionKind.DocumentBoundary, candidate.DetectionKind);
        Assert.Equal("OPS/a.xhtml", candidate.LeftSource.DocumentPath);
        Assert.Equal("OPS/b.xhtml", candidate.RightSource.DocumentPath);
    }

    [Fact]
    public void Detect_DoesNotTreatSpacedHyphenAsHyphenation()
    {
        using var epub = CreateSingleDocumentEpub("<p>geldi - sonra gitti</p>");
        var stream = ReadStream(epub);

        var candidates = new HyphenationDetector().Detect(stream);

        Assert.Empty(candidates);
    }

    private static TemporaryEpub CreateSingleDocumentEpub(string body)
    {
        return TemporaryEpub.Create(
            [new TestDocument("chapter", "chapter.xhtml", Xhtml(body))],
            [new TestSpineItem("chapter")]);
    }

    private static EpubFixer.Core.Epub.Models.LogicalTextStream ReadStream(TemporaryEpub epub)
    {
        return new EpubFixer.Core.Epub.EpubPackageReader().Read(epub.Path).LogicalText;
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
