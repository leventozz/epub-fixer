using EpubFixer.Core.Morphology;
using EpubFixer.TrMorph;

namespace EpubFixer.Cli.Morphology;

public sealed class FomaMorphologyOracleBuilder : IMorphologyOracleBuilder, IDisposable
{
    private readonly Func<IBatchTurkishMorphologicalParser> analyzerFactory;
    private readonly Dictionary<string, IReadOnlyList<TurkishMorphologicalAnalysis>> analyses = new(StringComparer.Ordinal);
    private IBatchTurkishMorphologicalParser? analyzer;
    private bool disposed;

    public FomaMorphologyOracleBuilder()
        : this(() => new FomaTurkishMorphologyAnalyzer())
    {
    }

    public FomaMorphologyOracleBuilder(Func<IBatchTurkishMorphologicalParser> analyzerFactory)
    {
        this.analyzerFactory = analyzerFactory ?? throw new ArgumentNullException(nameof(analyzerFactory));
    }

    public IMorphologyOracle Build(IEnumerable<string> vocabulary)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var requested = MorphologyPrefill.NormalizeDistinctOrder(vocabulary);
        var missing = requested.Where(word => !analyses.ContainsKey(word)).ToArray();

        if (missing.Length > 0)
        {
            analyzer ??= analyzerFactory();
            foreach (var pair in analyzer.AnalyzeBatch(missing))
            {
                analyses[pair.Key] = pair.Value;
            }
        }

        return new MorphologyOracle(analyses);
    }

    public TurkishMorphologyCacheStatistics CacheStatistics =>
        analyzer?.CacheStatistics ?? new TurkishMorphologyCacheStatistics(0, 0, analyses.Count, 0, 0, 0);

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (analyzer is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
