using EpubFixer.Core.Morphology;

namespace EpubFixer.Cli.Morphology;

public sealed class CachingMorphologyOracleBuilder : IMorphologyOracleBuilder, IDisposable
{
    private readonly IMorphologyOracleBuilder inner;
    private readonly MorphologyOracleCache cache;
    private readonly Dictionary<string, IReadOnlyList<TurkishMorphologicalAnalysis>> analyses;
    private bool changed;

    public CachingMorphologyOracleBuilder(IMorphologyOracleBuilder inner, MorphologyOracleCache cache)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
        analyses = cache.Read().ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }

    public IMorphologyOracle Build(IEnumerable<string> vocabulary)
    {
        var requested = MorphologyPrefill.NormalizeDistinctOrder(vocabulary);
        var missing = requested.Where(word => !analyses.ContainsKey(word)).ToArray();
        if (missing.Length > 0)
        {
            var resolved = inner.Build(missing);
            foreach (var word in missing)
            {
                analyses[word] = resolved.Analyze(word);
            }
            changed = true;
        }

        return new MorphologyOracle(analyses);
    }

    public void Flush()
    {
        if (!changed)
        {
            return;
        }

        cache.Write(analyses);
        changed = false;
    }

    public void Dispose()
    {
        Flush();
        if (inner is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
