using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Core.Lexicon;

public sealed class BookLanguageModelBuilder
{
    private readonly BookVocabulary vocabulary;
    private readonly double backoffAlpha;

    public BookLanguageModelBuilder(BookVocabulary vocabulary, double backoffAlpha = 0.4)
    {
        this.vocabulary = vocabulary;
        this.backoffAlpha = backoffAlpha;
    }

    public BookLanguageModel Build(LogicalTextStream stream, IReadOnlyList<CorruptedTextRegion> regions)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(regions);

        return Build(CleanTokenSequence.Build(stream, regions));
    }

    internal BookLanguageModel Build(IReadOnlyList<CleanToken> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        var unigrams = new Dictionary<string, int>(StringComparer.Ordinal);
        var bigrams = CountBigrams(tokens, _ => true);
        var heldOutStarts = HeldOutTokenSplit.SelectHeldOutLogicalStarts(tokens);
        var trainingBigrams = CountBigrams(tokens, token => !heldOutStarts.Contains(token.LogicalStart));
        var heldOutBigrams = CountBigrams(tokens, token => heldOutStarts.Contains(token.LogicalStart));
        foreach (var token in tokens)
        {
            unigrams[token.Normalized] = unigrams.GetValueOrDefault(token.Normalized) + 1;
        }

        unigrams["<s>"] = bigrams.Where(item => item.Key.Previous == "<s>").Sum(item => item.Value);
        var heldOutBigramTotal = heldOutBigrams.Values.Sum();
        var heldOutBigramHits = heldOutBigrams
            .Where(item => trainingBigrams.ContainsKey(item.Key))
            .Sum(item => item.Value);
        var stats = new LanguageModelStatistics(
            unigrams.Count(key => key.Key != "<s>"),
            unigrams.Where(item => item.Key != "<s>").Sum(item => item.Value),
            bigrams.Count,
            bigrams.Values.Sum(),
            heldOutBigramTotal == 0 ? 1.0 : heldOutBigramHits / (double)heldOutBigramTotal);

        return new BookLanguageModel(unigrams, bigrams, vocabulary, backoffAlpha, stats);
    }

    private Dictionary<(string Previous, string Word), int> CountBigrams(
        IReadOnlyList<CleanToken> tokens,
        Func<CleanToken, bool> include)
    {
        var bigrams = new Dictionary<(string Previous, string Word), int>();
        string? previous = null;
        foreach (var token in tokens)
        {
            if (!vocabulary.Contains(token.Normalized) || !include(token))
            {
                previous = null;
                continue;
            }

            var effectivePrevious = token.StartsSegment ? "<s>" : previous;
            if (effectivePrevious is not null)
            {
                var key = (effectivePrevious, token.Normalized);
                bigrams[key] = bigrams.GetValueOrDefault(key) + 1;
            }

            previous = token.Normalized;
        }

        return bigrams;
    }

}
