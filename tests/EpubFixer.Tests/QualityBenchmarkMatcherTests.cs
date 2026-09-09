using EpubFixer.Core.Detection;
using EpubFixer.Core.Detection.Models;
using EpubFixer.Core.Epub;
using EpubFixer.QualityBenchmarks;
using EpubFixer.QualityBenchmarks.Models;

namespace EpubFixer.Tests;

public sealed class QualityBenchmarkMatcherTests
{
    [Fact]
    public void Match_DetectsKnownErrorAtExactInlineSourceLocation()
    {
        using var epub = CreateSingleDocumentEpub("<p>Auersber-ger</p>");
        var candidate = Assert.Single(Detect(epub));
        var knownError = CreateInlineOccurrence("error-1", candidate);

        var result = new QualityBenchmarkMatcher().Match([knownError], [candidate]);

        Assert.Equal(1, result.KnownErrors);
        Assert.Equal(1, result.Detected);
        Assert.Equal(0, result.Missed);
        Assert.Equal(1d, result.DetectionRecall);
        Assert.Empty(result.MissedOccurrences);
    }

    [Fact]
    public void Match_SeparatesOccurrencesWithTheSameOriginalText()
    {
        using var epub = CreateSingleDocumentEpub("<p>Auersber-ger Auersber-ger</p>");
        var candidates = Detect(epub);
        Assert.Equal(2, candidates.Count);
        var knownErrors = new[]
        {
            CreateInlineOccurrence("error-1", candidates[0]),
            CreateInlineOccurrence("error-2", candidates[1])
        };

        var result = new QualityBenchmarkMatcher().Match(knownErrors, candidates);

        Assert.Equal(2, result.Detected);
        Assert.Equal(0, result.Missed);
    }

    [Fact]
    public void Match_DoesNotMatchWrongSourceLocation()
    {
        using var epub = CreateSingleDocumentEpub("<p>Auersber-ger</p>");
        var candidate = Assert.Single(Detect(epub));
        var knownError = CreateInlineOccurrence("error-1", candidate);
        var sourceSpan = Assert.Single(knownError.SourceSpans);
        knownError = knownError with
        {
            SourceSpans = [sourceSpan with { Start = sourceSpan.Start + 1 }]
        };

        var result = new QualityBenchmarkMatcher().Match([knownError], [candidate]);

        Assert.Equal(0, result.Detected);
        Assert.Equal(1, result.Missed);
        Assert.Same(knownError, Assert.Single(result.MissedOccurrences));
    }

    [Fact]
    public void Match_CalculatesMissedOccurrencesAndDetectionRecall()
    {
        using var epub = CreateSingleDocumentEpub("<p>Auersber-ger Jo-ana</p>");
        var candidates = Detect(epub);
        Assert.Equal(2, candidates.Count);
        var detected = CreateInlineOccurrence("detected", candidates[0]);
        var missed = CreateInlineOccurrence("missed", candidates[1]);

        var result = new QualityBenchmarkMatcher().Match([detected, missed], [candidates[0]]);

        Assert.Equal(2, result.KnownErrors);
        Assert.Equal(1, result.Detected);
        Assert.Equal(1, result.Missed);
        Assert.Equal(0.5d, result.DetectionRecall);
        Assert.Same(missed, Assert.Single(result.MissedOccurrences));
    }

    [Fact]
    public void Match_ReturnsNullRecallWhenThereAreNoKnownErrors()
    {
        var result = new QualityBenchmarkMatcher().Match([], []);

        Assert.Equal(0, result.KnownErrors);
        Assert.Equal(0, result.Detected);
        Assert.Equal(0, result.Missed);
        Assert.Null(result.DetectionRecall);
        Assert.Empty(result.MissedOccurrences);
    }

    [Fact]
    public void Match_ConsumesEachCandidateAtMostOnce()
    {
        using var epub = CreateSingleDocumentEpub("<p>Auersber-ger</p>");
        var candidate = Assert.Single(Detect(epub));
        var first = CreateInlineOccurrence("error-1", candidate);
        var second = CreateInlineOccurrence("error-2", candidate);

        var result = new QualityBenchmarkMatcher().Match([first, second], [candidate]);

        Assert.Equal(1, result.Detected);
        Assert.Equal(1, result.Missed);
        Assert.Same(second, Assert.Single(result.MissedOccurrences));
    }

    [Fact]
    public void Match_MergesAdjacentSourcesButPreservesParagraphBoundarySpan()
    {
        using var epub = CreateSingleDocumentEpub("<p>kol-</p><p>tukta</p>");
        var candidate = Assert.Single(Detect(epub));
        var knownError = new KnownErrorOccurrence(
            "boundary-error",
            candidate.HyphenSource.DocumentPath,
            "kol-tukta",
            "koltukta",
            [
                new GroundTruthSourceSpan(
                    candidate.LeftSource.DocumentPath,
                    candidate.LeftSource.TextNodeIndex,
                    candidate.LeftSource.Start,
                    candidate.LeftSource.Length + candidate.HyphenSource.Length),
                new GroundTruthSourceSpan(
                    candidate.RightSource.DocumentPath,
                    candidate.RightSource.TextNodeIndex,
                    candidate.RightSource.Start,
                    candidate.RightSource.Length)
            ]);

        var result = new QualityBenchmarkMatcher().Match([knownError], [candidate]);

        Assert.Equal(1, result.Detected);
        Assert.Equal(1d, result.DetectionRecall);
    }

    private static KnownErrorOccurrence CreateInlineOccurrence(
        string id,
        HyphenationCandidate candidate)
    {
        return new KnownErrorOccurrence(
            id,
            candidate.HyphenSource.DocumentPath,
            candidate.LeftPart + "-" + candidate.RightPart,
            candidate.UnhyphenatedText,
            [
                new GroundTruthSourceSpan(
                    candidate.LeftSource.DocumentPath,
                    candidate.LeftSource.TextNodeIndex,
                    candidate.LeftSource.Start,
                    candidate.LeftSource.Length
                        + candidate.HyphenSource.Length
                        + candidate.RightSource.Length)
            ]);
    }

    private static IReadOnlyList<HyphenationCandidate> Detect(TemporaryEpub epub)
    {
        var logicalText = new EpubPackageReader().Read(epub.Path).LogicalText;
        return new HyphenationDetector().Detect(logicalText);
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
