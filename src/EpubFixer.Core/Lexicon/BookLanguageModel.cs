namespace EpubFixer.Core.Lexicon;

public sealed class BookLanguageModel : ILanguageModel
{
    private readonly IReadOnlyDictionary<string, int> unigramCounts;
    private readonly IReadOnlyDictionary<(string Previous, string Word), int> bigramCounts;
    private readonly BookVocabulary vocabulary;
    private readonly double backoffAlpha;

    internal BookLanguageModel(
        IReadOnlyDictionary<string, int> unigramCounts,
        IReadOnlyDictionary<(string Previous, string Word), int> bigramCounts,
        BookVocabulary vocabulary,
        double backoffAlpha,
        LanguageModelStatistics statistics)
    {
        this.unigramCounts = unigramCounts;
        this.bigramCounts = bigramCounts;
        this.vocabulary = vocabulary;
        this.backoffAlpha = backoffAlpha;
        Statistics = statistics;
    }

    public LanguageModelStatistics Statistics { get; }

    public double LogProbability(string word, string? previousWord)
    {
        var normalized = TurkishWordNormalizer.Normalize(word);
        if (!vocabulary.Contains(normalized))
        {
            return vocabulary.UnigramLogProbability(normalized);
        }

        if (previousWord is null)
        {
            return vocabulary.UnigramLogProbability(normalized);
        }

        var previous = string.Equals(previousWord, "<s>", StringComparison.Ordinal)
            ? "<s>"
            : TurkishWordNormalizer.Normalize(previousWord);
        if (bigramCounts.TryGetValue((previous, normalized), out var bigram)
            && unigramCounts.TryGetValue(previous, out var previousCount)
            && previousCount > 0)
        {
            return Math.Log(bigram / (double)previousCount);
        }

        return Math.Log(backoffAlpha) + vocabulary.UnigramLogProbability(normalized);
    }
}
