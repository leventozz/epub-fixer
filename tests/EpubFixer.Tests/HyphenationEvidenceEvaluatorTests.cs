using EpubFixer.Core.Detection;
using EpubFixer.Core.Evidence;
using EpubFixer.Core.Epub;
using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Lexicon;

namespace EpubFixer.Tests;

public sealed class HyphenationEvidenceEvaluatorTests
{
    [Fact]
    public void Evaluate_ReturnsExactLexiconCountAndExistence()
    {
        var occurrences = string.Join(' ', Enumerable.Repeat("Auersberger", 10));
        using var epub = CreateSingleDocumentEpub($"<p>Auersber-ger {occurrences}</p>");
        var stream = ReadStream(epub);
        var candidate = Assert.Single(new HyphenationDetector().Detect(stream));
        var lexicon = new BookLexiconBuilder().Build(stream);

        var evidence = Assert.Single(
            new HyphenationEvidenceEvaluator().Evaluate([candidate], lexicon));

        Assert.Same(candidate, evidence.Candidate);
        Assert.Equal(10, evidence.UnhyphenatedOccurrenceCount);
        Assert.True(evidence.ExistsInLexicon);
    }

    [Fact]
    public void Evaluate_ReturnsZeroAndFalseWhenCandidateIsMissingFromLexicon()
    {
        using var epub = CreateSingleDocumentEpub("<p>Mayıs-Haziran</p>");
        var stream = ReadStream(epub);
        var candidate = Assert.Single(new HyphenationDetector().Detect(stream));
        var lexicon = new BookLexiconBuilder().Build(stream);

        var evidence = Assert.Single(
            new HyphenationEvidenceEvaluator().Evaluate([candidate], lexicon));

        Assert.Equal("MayısHaziran", evidence.Candidate.UnhyphenatedText);
        Assert.Equal(0, evidence.UnhyphenatedOccurrenceCount);
        Assert.False(evidence.ExistsInLexicon);
    }

    [Fact]
    public void Evaluate_UsesCaseSensitiveLexiconLookup()
    {
        using var epub = CreateSingleDocumentEpub("<p>Auersber-ger auersberger</p>");
        var stream = ReadStream(epub);
        var candidate = Assert.Single(new HyphenationDetector().Detect(stream));
        var lexicon = new BookLexiconBuilder().Build(stream);

        var evidence = Assert.Single(
            new HyphenationEvidenceEvaluator().Evaluate([candidate], lexicon));

        Assert.Equal(0, evidence.UnhyphenatedOccurrenceCount);
        Assert.False(evidence.ExistsInLexicon);
    }

    [Fact]
    public void Evaluate_PreservesEachCandidateOccurrenceAndInputOrder()
    {
        using var epub = CreateSingleDocumentEpub(
            "<p>Auersber-ger Auersber-ger Auersberger</p>");
        var stream = ReadStream(epub);
        var candidates = new HyphenationDetector().Detect(stream);
        var lexicon = new BookLexiconBuilder().Build(stream);

        var evidence = new HyphenationEvidenceEvaluator().Evaluate(candidates, lexicon);

        Assert.Equal(2, candidates.Count);
        Assert.Equal(2, evidence.Count);

        for (var index = 0; index < candidates.Count; index++)
        {
            Assert.Same(candidates[index], evidence[index].Candidate);
            Assert.Equal(1, evidence[index].UnhyphenatedOccurrenceCount);
            Assert.True(evidence[index].ExistsInLexicon);
        }
    }

    [Fact]
    public void Evaluate_RejectsNullInputs()
    {
        using var epub = CreateSingleDocumentEpub("<p>Auersber-ger</p>");
        var stream = ReadStream(epub);
        var candidate = Assert.Single(new HyphenationDetector().Detect(stream));
        var lexicon = new BookLexiconBuilder().Build(stream);
        var evaluator = new HyphenationEvidenceEvaluator();

        Assert.Throws<ArgumentNullException>(() => evaluator.Evaluate(null!, lexicon));
        Assert.Throws<ArgumentNullException>(() => evaluator.Evaluate([candidate], null!));
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
