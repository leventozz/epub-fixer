using AngleSharp.Dom;
using EpubFixer.Core.Detection;
using EpubFixer.Core.Evidence;
using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Epub;

namespace EpubFixer.Tests;

public sealed class HyphenationMorphologyAnalyzerTests
{
    [Fact]
    public void Analyze_UsesJoinedFormAndCarriesContextWithoutChangingEvidence()
    {
        using var epub = CreateSingleDocumentEpub("<p>sa-bah yan-abc</p>");
        var stream = ReadStream(epub);
        var candidates = new HyphenationDetector().Detect(stream);
        var lexicon = new BookLexiconBuilder().Build(stream);
        var evidence = new HyphenationEvidenceEvaluator().Evaluate(candidates, lexicon, stream);
        var analyzer = new FakeAnalyzer("sabah");

        var result = new HyphenationMorphologyAnalyzer().Analyze(evidence, analyzer);

        Assert.Equal(2, result.Count);
        Assert.Equal("sa-bah", result[0].Original);
        Assert.Equal("sabah", result[0].JoinedForm);
        Assert.True(result[0].TRmorphValid);
        Assert.Contains("sabah", analyzer.Queried);
        Assert.Contains("yanabc", analyzer.Queried);
        Assert.False(result[1].TRmorphValid);
        Assert.Equal(evidence[0].Context.HasAdjacentHyphen, result[0].HasAdjacentHyphen);
    }

    private sealed class FakeAnalyzer(params string[] validWords) : ITurkishMorphologyAnalyzer
    {
        private readonly HashSet<string> valid = validWords.ToHashSet(StringComparer.Ordinal);
        public List<string> Queried { get; } = [];
        public bool IsValidWord(string word)
        {
            Queried.Add(word);
            return valid.Contains(word);
        }
    }

    private static TemporaryEpub CreateSingleDocumentEpub(string body) =>
        TemporaryEpub.Create(
            [new TestDocument("chapter", "chapter.xhtml", Xhtml(body))],
            [new TestSpineItem("chapter")]);

    private static EpubFixer.Core.Epub.Models.LogicalTextStream ReadStream(TemporaryEpub epub) =>
        new EpubPackageReader().Read(epub.Path).LogicalText;

    private static string Xhtml(string body) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <html xmlns="http://www.w3.org/1999/xhtml"><body>{body}</body></html>
        """;
}
