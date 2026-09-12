using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Lexicon.Models;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Ocr.Models;
using EpubFixer.Core.Quality;

namespace EpubFixer.Core.Lexicon;

public sealed class BookVocabularyBuilder
{
    private readonly ITurkishFrequencyList frequencyList;
    private readonly BookVocabularyOptions options;

    public BookVocabularyBuilder(ITurkishFrequencyList frequencyList, BookVocabularyOptions? options = null)
    {
        this.frequencyList = frequencyList;
        this.options = options ?? new BookVocabularyOptions();
    }

    public IEnumerable<string> EnumerateMorphologyQueries(
        LogicalTextStream stream,
        IReadOnlyList<CorruptedTextRegion> regions)
    {
        return EnumerateMorphologyQueries(CleanTokenSequence.Build(stream, regions));
    }

    internal IEnumerable<string> EnumerateMorphologyQueries(IReadOnlyList<CleanToken> tokens)
    {
        return tokens
            .Where(token => IsBookCandidate(token.Surface, token.Normalized))
            .Where(token => !frequencyList.Contains(token.Normalized))
            .Select(token => token.Surface.Normalize())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(token => token, StringComparer.Ordinal);
    }

    public BookVocabulary Build(
        LogicalTextStream stream,
        IReadOnlyList<CorruptedTextRegion> regions,
        IMorphologyOracle oracle)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(regions);
        ArgumentNullException.ThrowIfNull(oracle);

        var tokens = CleanTokenSequence.Build(stream, regions);
        return Build(tokens, EnumerateMorphologyQueries(stream, regions), oracle);
    }

    internal BookVocabulary Build(
        IReadOnlyList<CleanToken> tokens,
        IEnumerable<string> morphologyQueries,
        IMorphologyOracle oracle)
    {
        var bookCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var surfaceCounts = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        foreach (var token in tokens)
        {
            if (!IsBookCandidate(token.Surface, token.Normalized))
            {
                continue;
            }

            bookCounts[token.Normalized] = bookCounts.GetValueOrDefault(token.Normalized) + 1;
            if (!surfaceCounts.TryGetValue(token.Normalized, out var bySurface))
            {
                bySurface = new Dictionary<string, int>(StringComparer.Ordinal);
                surfaceCounts[token.Normalized] = bySurface;
            }

            bySurface[token.Surface] = bySurface.GetValueOrDefault(token.Surface) + 1;
        }

        var entries = new Dictionary<string, VocabularyEntry>(StringComparer.Ordinal);
        var morphologyValid = morphologyQueries
            .GroupBy(TurkishWordNormalizer.Normalize, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Any(query => oracle.IsValid(query)),
                StringComparer.Ordinal);

        foreach (var (word, count) in bookCounts.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            var inFrequency = frequencyList.Contains(word);
            var validMorphology = morphologyValid.GetValueOrDefault(word);
            if (!inFrequency && count < options.MinBookCount && !validMorphology)
            {
                continue;
            }

            if (!inFrequency && !validMorphology && count < options.MinUnverifiedBookCount)
            {
                continue;
            }

            var source = count >= options.MinBookCount || inFrequency
                ? VocabularySource.Book
                : VocabularySource.Morphology;
            entries[word] = new VocabularyEntry(word, PreferredSurface(surfaceCounts[word]), count, source);
        }

        foreach (var word in frequencyList.Words.OrderBy(word => word, StringComparer.Ordinal))
        {
            entries.TryAdd(word, new VocabularyEntry(word, word, 0, VocabularySource.Frequency));
        }

        var probabilities = CreateLogProbabilities(entries);
        return new BookVocabulary(entries, probabilities, options);
    }

    private Dictionary<string, double> CreateLogProbabilities(IReadOnlyDictionary<string, VocabularyEntry> entries)
    {
        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        var bookTotal = entries.Values.Sum(entry => entry.BookCount);
        var vocabularySize = Math.Max(1, entries.Count);
        var denominator = bookTotal + options.AddK * vocabularySize;
        var rarestBookProbability = Math.Log(options.AddK / denominator);

        foreach (var entry in entries.Values)
        {
            result[entry.Normalized] = entry.BookCount > 0
                ? Math.Log((entry.BookCount + options.AddK) / denominator)
                : entry.Source switch
                {
                    VocabularySource.Morphology => options.MorphologyOnlyLogProbability,
                    _ => Math.Min(
                    rarestBookProbability - 0.001,
                    Math.Log(options.FrequencyListDiscount * (frequencyList.GetFrequency(entry.Normalized) + 1)
                        / Math.Max(1.0, frequencyList.TotalFrequency)))
                };
        }

        return result;
    }

    private static string PreferredSurface(IReadOnlyDictionary<string, int> surfaces) =>
        surfaces
            .OrderByDescending(item => item.Value)
            .ThenBy(item => item.Key, StringComparer.Ordinal)
            .First().Key;

    private bool IsBookCandidate(string surface, string normalized)
    {
        if (string.IsNullOrWhiteSpace(surface))
        {
            return false;
        }

        if (surface.EnumerateRunes().Count() == 1 && !frequencyList.Contains(normalized))
        {
            return false;
        }

        return !SuspiciousTokenRules.IsSuspicious(surface);
    }
}
