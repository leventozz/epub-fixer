using AngleSharp.Dom;
using EpubFixer.Core.Detection;
using EpubFixer.Core.Epub;
using EpubFixer.Core.Evidence;
using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Ocr;

namespace EpubFixer.Tests;

public sealed class MorphologyPrefillEnumerationTests
{
    [Fact]
    public void HyphenationMorphologyAnalyzer_EnumeratesExactlyTheWordsItQueries()
    {
        using var epub = CreateSingleDocumentEpub("<p>sa-bah yan-abc</p>");
        var stream = new EpubPackageReader().Read(epub.Path).LogicalText;
        var candidates = new HyphenationDetector().Detect(stream);
        var lexicon = new BookLexiconBuilder().Build(stream);
        var evidence = new HyphenationEvidenceEvaluator().Evaluate(candidates, lexicon, stream);
        var analyzer = new HyphenationMorphologyAnalyzer();
        var oracle = new AllKnownRecordingOracle([]);

        _ = analyzer.Analyze(evidence, oracle);

        Assert.Equal(analyzer.EnumerateMorphologyQueries(evidence), oracle.Queried);
    }

    [Fact]
    public void OcrAnomalyDetector_EnumeratesExactlyTheWordsItQueries()
    {
        using var epub = CreateSingleDocumentEpub("<p>ger-^ .ckten iıç temiz</p>");
        var stream = new EpubPackageReader().Read(epub.Path).LogicalText;
        var detector = new OcrAnomalyDetector();
        var oracle = new AllKnownRecordingOracle([]);

        _ = detector.Analyze(stream, oracle);

        Assert.Equal(detector.EnumerateMorphologyQueries(stream), oracle.Queried);
    }

    [Fact]
    public void OcrRegionDetector_EnumerationCoversEveryQueriedForm()
    {
        var text = "1 ı iç ve :,ohbet";
        var detector = new OcrRegionDetector();
        var oracle = new AllKnownRecordingOracle(["1", "ı"]);

        _ = detector.Detect(text, oracle);

        var enumerated = detector.EnumerateMorphologyQueries(text).ToHashSet(StringComparer.Ordinal);
        Assert.True(oracle.Queried.ToHashSet(StringComparer.Ordinal).IsSubsetOf(enumerated),
            "Uncovered queries: " + string.Join(", ", oracle.Queried.Where(query => !enumerated.Contains(query))));
    }

    [Fact]
    public void OcrCorrectionCandidateGenerator_EnumerationCoversEveryQueriedForm()
    {
        using var epub = CreateSingleDocumentEpub("<p>ilgi-1 iydi ilgili</p>");
        var stream = new EpubPackageReader().Read(epub.Path).LogicalText;
        var anomalyOracle = new AllKnownRecordingOracle([]);
        var analysis = new OcrAnomalyDetector().Analyze(stream, anomalyOracle);
        var lexicon = new BookLexiconBuilder().Build(stream);
        var generator = new OcrCorrectionCandidateGenerator();
        var generationOracle = new AllKnownRecordingOracle(["ilgili", "ilgiliydi"]);

        _ = generator.Generate(analysis, stream, lexicon, generationOracle);

        var enumerated = generator.EnumerateMorphologyQueries(analysis, stream, lexicon).ToHashSet(StringComparer.Ordinal);
        Assert.True(generationOracle.Queried.ToHashSet(StringComparer.Ordinal).IsSubsetOf(enumerated),
            "Uncovered queries: " + string.Join(", ", generationOracle.Queried.Where(query => !enumerated.Contains(query))));
    }

    [Fact]
    public void Analyze_ThrowsWhenOracleWasNotPrefilled()
    {
        using var epub = CreateSingleDocumentEpub("<p>zzzz</p>");
        var stream = new EpubPackageReader().Read(epub.Path).LogicalText;

        Assert.Throws<MorphologyOracleException>(() =>
            new OcrAnomalyDetector().Analyze(
                stream,
                new MorphologyOracle(new Dictionary<string, IReadOnlyList<TurkishMorphologicalAnalysis>>(StringComparer.Ordinal))));
    }

    private static TemporaryEpub CreateSingleDocumentEpub(string body) =>
        TemporaryEpub.Create(
            [new TestDocument("chapter", "chapter.xhtml", Xhtml(body))],
            [new TestSpineItem("chapter")]);

    private static string Xhtml(string body) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <html xmlns="http://www.w3.org/1999/xhtml"><body>{body}</body></html>
        """;

    private sealed class AllKnownRecordingOracle(IEnumerable<string> invalid) : IMorphologyOracle
    {
        private readonly HashSet<string> invalid = invalid.ToHashSet(StringComparer.Ordinal);
        public List<string> Queried { get; } = [];

        public bool IsValid(string word)
        {
            Queried.Add(word);
            return !invalid.Contains(word);
        }

        public IReadOnlyList<TurkishMorphologicalAnalysis> Analyze(string word) =>
            IsValid(word) ? [new TurkishMorphologicalAnalysis(word, new HashSet<string>(StringComparer.Ordinal))] : [];

        public bool IsKnown(string word) => true;
    }
}
