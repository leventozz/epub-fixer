using EpubFixer.Core.Lexicon.Models;

namespace EpubFixer.Core.Lexicon;

public sealed class BookVocabulary
{
    private readonly IReadOnlyDictionary<string, VocabularyEntry> entries;
    private readonly IReadOnlyDictionary<string, double> logProbabilities;
    private readonly BookVocabularyOptions options;
    private readonly IReadOnlyCollection<string> words;

    internal BookVocabulary(
        IReadOnlyDictionary<string, VocabularyEntry> entries,
        IReadOnlyDictionary<string, double> logProbabilities,
        BookVocabularyOptions options)
    {
        this.entries = entries;
        this.logProbabilities = logProbabilities;
        this.options = options;
        words = Array.AsReadOnly(entries.Keys.OrderBy(word => word, StringComparer.Ordinal).ToArray());
    }

    public int Count => entries.Count;

    public IReadOnlyCollection<string> Words => words;

    public bool Contains(string word) => entries.ContainsKey(TurkishWordNormalizer.Normalize(word));

    public double UnigramLogProbability(string word)
    {
        var normalized = TurkishWordNormalizer.Normalize(word);
        return logProbabilities.GetValueOrDefault(normalized, options.UnknownLogProbability);
    }

    public VocabularySource SourceOf(string word)
    {
        var normalized = TurkishWordNormalizer.Normalize(word);
        return entries.TryGetValue(normalized, out var entry)
            ? entry.Source
            : throw new VocabularyLookupException(word);
    }

    public VocabularyEntry? Find(string word)
    {
        var normalized = TurkishWordNormalizer.Normalize(word);
        return entries.GetValueOrDefault(normalized);
    }
}
