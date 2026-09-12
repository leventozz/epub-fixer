using System.Diagnostics;
using EpubFixer.Core.Morphology;

namespace EpubFixer.Tests;

internal sealed class CountingMorphologyAnalyzer : ITurkishMorphologyAnalyzer, IBatchTurkishMorphologicalParser, IDisposable
{
    private readonly IBatchTurkishMorphologicalParser parser;
    private readonly IDisposable? disposable;

    public CountingMorphologyAnalyzer(IBatchTurkishMorphologicalParser inner)
    {
        parser = inner ?? throw new ArgumentNullException(nameof(inner));
        disposable = inner as IDisposable;
    }

    public long IsValidWordCalls { get; private set; }
    public long AnalyzeCalls { get; private set; }
    public long AnalyzeBatchCalls { get; private set; }
    public long DistinctWords { get; private set; }
    public TimeSpan MorphologyElapsed { get; private set; }
    public TurkishMorphologyCacheStatistics CacheStatistics => parser.CacheStatistics;

    public bool IsValidWord(string word)
    {
        IsValidWordCalls++;
        var watch = Stopwatch.StartNew();
        try
        {
            return parser.Analyze(word).Count > 0;
        }
        finally
        {
            watch.Stop();
            MorphologyElapsed += watch.Elapsed;
        }
    }

    public IReadOnlyList<TurkishMorphologicalAnalysis> Analyze(string word)
    {
        AnalyzeCalls++;
        var watch = Stopwatch.StartNew();
        try
        {
            return parser.Analyze(word);
        }
        finally
        {
            watch.Stop();
            MorphologyElapsed += watch.Elapsed;
        }
    }

    public IReadOnlyDictionary<string, IReadOnlyList<TurkishMorphologicalAnalysis>> AnalyzeBatch(IEnumerable<string> words)
    {
        AnalyzeBatchCalls++;
        var materialized = words.ToArray();
        DistinctWords += materialized.Distinct(StringComparer.Ordinal).Count();
        var watch = Stopwatch.StartNew();
        try
        {
            return parser.AnalyzeBatch(materialized);
        }
        finally
        {
            watch.Stop();
            MorphologyElapsed += watch.Elapsed;
        }
    }

    public void Dispose() => disposable?.Dispose();
}
