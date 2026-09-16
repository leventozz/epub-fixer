using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Ocr;

namespace EpubFixer.Core.Lexicon;

public sealed class BookKnowledgeBuilder
{
    private readonly ITurkishFrequencyList frequencyList;
    private readonly BookVocabularyOptions? options;

    public BookKnowledgeBuilder(ITurkishFrequencyList frequencyList, BookVocabularyOptions? options = null)
    {
        this.frequencyList = frequencyList;
        this.options = options;
    }

    public BookKnowledge Build(LogicalTextStream stream, IMorphologyOracleBuilder oracleBuilder, IEnumerable<string>? targets = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(oracleBuilder);

        var detector = new OcrRegionDetector();
        var detectorOracle = oracleBuilder.Build(detector.EnumerateMorphologyQueries(stream.Text));
        var regions = detector.Detect(stream.Text, detectorOracle);
        var vocabularyBuilder = new BookVocabularyBuilder(frequencyList, options);
        var allTokens = CleanTokenSequence.Build(stream, regions);
        var vocabularyQueries = vocabularyBuilder.EnumerateMorphologyQueries(allTokens).ToArray();
        var vocabularyOracle = oracleBuilder.Build(vocabularyQueries);
        var vocabulary = vocabularyBuilder.Build(allTokens, vocabularyQueries, vocabularyOracle);
        var heldOutStarts = HeldOutTokenSplit.SelectHeldOutLogicalStarts(allTokens);
        var trainingTokens = allTokens.Where(token => !heldOutStarts.Contains(token.LogicalStart)).ToArray();
        var trainingQueries = trainingTokens
            .Where(token => !frequencyList.Contains(token.Normalized))
            .Select(token => token.Surface.Normalize())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(token => token, StringComparer.Ordinal)
            .ToArray();
        var heldOutVocabulary = vocabularyBuilder.Build(trainingTokens, trainingQueries, vocabularyOracle);
        var languageModel = new BookLanguageModelBuilder(vocabulary).Build(allTokens);
        var coverage = new VocabularyCoverageMeter().Measure(vocabulary, heldOutVocabulary, allTokens, targets);
        return new BookKnowledge(vocabulary, languageModel, regions, coverage);
    }
}
