namespace EpubFixer.Core.Morphology;

public sealed class BatchMorphologyOracleBuilder : IMorphologyOracleBuilder
{
    private readonly IBatchTurkishMorphologicalParser parser;
    private readonly Dictionary<string, IReadOnlyList<TurkishMorphologicalAnalysis>> analyses = new(StringComparer.Ordinal);

    public BatchMorphologyOracleBuilder(IBatchTurkishMorphologicalParser parser)
    {
        this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    public IMorphologyOracle Build(IEnumerable<string> vocabulary)
    {
        var requested = MorphologyPrefill.NormalizeDistinctOrder(vocabulary);
        var missing = requested.Where(word => !analyses.ContainsKey(word)).ToArray();
        if (missing.Length > 0)
        {
            foreach (var pair in parser.AnalyzeBatch(missing))
            {
                analyses[pair.Key] = pair.Value;
            }
        }

        return new MorphologyOracle(analyses);
    }
}
