using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Ocr;
using EpubFixer.Core.Ocr.Lattice;
using EpubFixer.Cli.OcrReconstruction;

namespace EpubFixer.Cli.Ocr.Lattice;

public sealed class SymSpellLexiconMatcher : ILexiconMatcher
{
    public const int MaxMatchesPerSpan = 16;
    public const int MaxQueriesPerSpan = 24;

    private readonly SymSpellChecker checker;
    private readonly BookVocabulary vocabulary;
    private readonly IWeightedEditAligner aligner;
    private readonly LexiconQueryPlan queryPlan;
    private readonly Dictionary<CacheKey, IReadOnlyList<LexiconMatch>> cache = new();

    public SymSpellLexiconMatcher(
        BookVocabulary vocabulary,
        IWeightedEditAligner? aligner = null,
        LexiconQueryPlan? queryPlan = null)
    {
        ArgumentNullException.ThrowIfNull(vocabulary);
        this.vocabulary = vocabulary;
        this.aligner = aligner ?? new WeightedEditAligner();
        this.queryPlan = queryPlan ?? new LexiconQueryPlan();
        checker = BuildChecker(vocabulary);
    }

    public IReadOnlyList<LexiconMatch> Match(ReadOnlySpan<char> span, double budget)
    {
        var source = span.ToString();
        var normalized = TurkishWordNormalizer.Normalize(source);
        var key = new CacheKey(normalized, budget);
        if (cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var matches = MatchUncached(source, budget);
        cache[key] = matches;
        return matches;
    }

    private IReadOnlyList<LexiconMatch> MatchUncached(string source, double budget)
    {
        var candidates = new Dictionary<string, LexiconMatch>(StringComparer.Ordinal);
        Query(source, budget, queryPlan.CreateTierOne(source), candidates);
        if (candidates.Count == 0)
        {
            var remaining = Math.Max(0, MaxQueriesPerSpan - queryPlan.CreateTierOne(source).Count);
            Query(source, budget, queryPlan.CreateTierTwo(source, remaining), candidates);
        }

        return candidates.Values
            .OrderBy(match => match.Cost)
            .ThenBy(match => match.Word, StringComparer.Ordinal)
            .Take(MaxMatchesPerSpan)
            .ToArray();
    }

    private void Query(
        string source,
        double budget,
        IReadOnlyList<string> queries,
        Dictionary<string, LexiconMatch> candidates)
    {
        foreach (var query in queries.Take(MaxQueriesPerSpan))
        {
            foreach (var suggestion in checker.Lookup(query, SymSpell.Verbosity.All, 2))
            {
                var entry = vocabulary.Find(suggestion.term);
                if (entry is null)
                {
                    continue;
                }

                var surface = entry.PreferredSurface;
                if (!aligner.TryAlign(source, surface, budget, out var alignment))
                {
                    continue;
                }

                if (!candidates.TryGetValue(surface, out var existing) || alignment.Cost < existing.Cost)
                {
                    candidates[surface] = new LexiconMatch(surface, alignment.Cost);
                }
            }
        }
    }

    private static SymSpellChecker BuildChecker(BookVocabulary vocabulary)
    {
        var checker = new SymSpellChecker(vocabulary.Words.Count, 2);
        foreach (var word in vocabulary.Words)
        {
            var entry = vocabulary.Find(word);
            var count = Math.Max(1, (entry?.BookCount ?? 0) + 1);
            checker.CreateDictionaryEntry(word, count);
        }

        return checker;
    }

    private readonly record struct CacheKey(string NormalizedSpan, double Budget);
}
