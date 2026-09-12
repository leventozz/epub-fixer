using System.Text;

namespace EpubFixer.Core.Morphology;

public sealed class MorphologyOracle : IMorphologyOracle
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<TurkishMorphologicalAnalysis>> analyses;

    public MorphologyOracle(IReadOnlyDictionary<string, IReadOnlyList<TurkishMorphologicalAnalysis>> analyses)
    {
        ArgumentNullException.ThrowIfNull(analyses);
        this.analyses = analyses.ToDictionary(
            pair => NormalizeKey(pair.Key),
            pair => (IReadOnlyList<TurkishMorphologicalAnalysis>)pair.Value.ToArray(),
            StringComparer.Ordinal);
    }

    public bool IsValid(string word) => Analyze(word).Count > 0;

    public IReadOnlyList<TurkishMorphologicalAnalysis> Analyze(string word)
    {
        var key = NormalizeInput(word);
        if (analyses.TryGetValue(key, out var result))
        {
            return result;
        }

        throw new MorphologyOracleException(
            $"Morphology oracle was not pre-filled for '{word}' (oracle holds {analyses.Count} words).");
    }

    public bool IsKnown(string word) => analyses.ContainsKey(NormalizeInput(word));

    private static string NormalizeInput(string word)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(word);
        return NormalizeKey(word);
    }

    private static string NormalizeKey(string word) => word.Normalize(NormalizationForm.FormC);
}
