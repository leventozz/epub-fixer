namespace EpubFixer.Core.Morphology;

/// <summary>Optional batched morphology operations and diagnostics.</summary>
public interface IBatchTurkishMorphologicalParser : ITurkishMorphologicalParser
{
    IReadOnlyDictionary<string, IReadOnlyList<TurkishMorphologicalAnalysis>> AnalyzeBatch(IEnumerable<string> words);
    TurkishMorphologyCacheStatistics CacheStatistics { get; }
}

public sealed record TurkishMorphologyCacheStatistics(
    long Hits,
    long Misses,
    long UniqueAnalyzedWords,
    long BatchRequests,
    long BatchedWords);
