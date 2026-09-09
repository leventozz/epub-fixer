using EpubFixer.Core.Detection;
using EpubFixer.Core.Detection.Models;
using EpubFixer.Core.Epub;
using EpubFixer.Core.Epub.Models;
using EpubFixer.QualityBenchmarks;
using EpubFixer.QualityBenchmarks.Models;

namespace EpubFixer.Tests;

public sealed class QualityBenchmarkGroundTruthValidatorTests
{
    [Fact]
    public void Validate_AcceptsInlineAndMultiSpanOccurrences()
    {
        using var epub = CreateSingleDocumentEpub(
            "<p>Auersber-ger</p><p>kol-</p><p>tukta</p>");
        var stream = ReadStream(epub);
        var candidates = new HyphenationDetector().Detect(stream);
        var occurrences = candidates
            .Select((candidate, index) => CreateOccurrence($"error-{index + 1}", candidate))
            .ToArray();

        new QualityBenchmarkGroundTruthValidator().Validate(
            new GroundTruthDocument(1, occurrences),
            stream);

        Assert.Equal(2, occurrences.Length);
        Assert.Single(occurrences[0].SourceSpans);
        Assert.Equal(2, occurrences[1].SourceSpans.Count);
    }

    [Fact]
    public void Validate_RejectsUnknownTextNode()
    {
        using var epub = CreateSingleDocumentEpub("<p>Auersber-ger</p>");
        var (stream, occurrence) = CreateSingleOccurrence(epub);
        var span = occurrence.SourceSpans[0] with { TextNodeIndex = 999 };

        var exception = Assert.Throws<InvalidDataException>(() => Validate(
            occurrence with { SourceSpans = [span] },
            stream));

        Assert.Contains("does not resolve to a logical text source", exception.Message);
    }

    [Fact]
    public void Validate_RejectsSpanOutsideTextNode()
    {
        using var epub = CreateSingleDocumentEpub("<p>Auersber-ger</p>");
        var (stream, occurrence) = CreateSingleOccurrence(epub);
        var span = occurrence.SourceSpans[0] with { Length = 1000 };

        var exception = Assert.Throws<InvalidDataException>(() => Validate(
            occurrence with { SourceSpans = [span] },
            stream));

        Assert.Contains("outside the referenced text node", exception.Message);
    }

    [Fact]
    public void Validate_RejectsNonCanonicalOrOverlappingSpans()
    {
        using var epub = CreateSingleDocumentEpub("<p>Auersber-ger</p>");
        var (stream, occurrence) = CreateSingleOccurrence(epub);
        var span = occurrence.SourceSpans[0];

        var exception = Assert.Throws<InvalidDataException>(() => Validate(
            occurrence with
            {
                SourceSpans =
                [
                    span with { Length = 8 },
                    span with { Start = span.Start + 7, Length = span.Length - 7 }
                ]
            },
            stream));

        Assert.Contains("canonical, non-overlapping", exception.Message);
    }

    [Fact]
    public void Validate_RejectsSourceTextThatDoesNotEqualOriginal()
    {
        using var epub = CreateSingleDocumentEpub("<p>Auersber-ger</p>");
        var (stream, occurrence) = CreateSingleOccurrence(epub);

        var exception = Assert.Throws<InvalidDataException>(() => Validate(
            occurrence with { Original = "Jo-ana" },
            stream));

        Assert.Contains("source text does not equal original", exception.Message);
    }

    private static void Validate(
        KnownErrorOccurrence occurrence,
        LogicalTextStream stream)
    {
        new QualityBenchmarkGroundTruthValidator().Validate(
            new GroundTruthDocument(1, [occurrence]),
            stream);
    }

    private static (LogicalTextStream Stream, KnownErrorOccurrence Occurrence)
        CreateSingleOccurrence(TemporaryEpub epub)
    {
        var stream = ReadStream(epub);
        var candidate = Assert.Single(new HyphenationDetector().Detect(stream));
        return (stream, CreateOccurrence("error-1", candidate));
    }

    private static KnownErrorOccurrence CreateOccurrence(
        string id,
        HyphenationCandidate candidate)
    {
        var spans = new List<GroundTruthSourceSpan>();

        foreach (var source in new[]
                 {
                     candidate.LeftSource,
                     candidate.HyphenSource,
                     candidate.RightSource
                 })
        {
            if (spans.Count > 0
                && spans[^1].DocumentPath == source.DocumentPath
                && spans[^1].TextNodeIndex == source.TextNodeIndex
                && spans[^1].Start + spans[^1].Length == source.Start)
            {
                spans[^1] = spans[^1] with { Length = spans[^1].Length + source.Length };
                continue;
            }

            spans.Add(new GroundTruthSourceSpan(
                source.DocumentPath,
                source.TextNodeIndex,
                source.Start,
                source.Length));
        }

        return new KnownErrorOccurrence(
            id,
            candidate.HyphenSource.DocumentPath,
            candidate.LeftPart + "-" + candidate.RightPart,
            candidate.UnhyphenatedText,
            spans.AsReadOnly());
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
