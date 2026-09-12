using EpubFixer.Core.Morphology;

namespace EpubFixer.Tests;

internal sealed class FakeMorphologyOracle : IMorphologyOracle
{
    private readonly HashSet<string> valid;

    public FakeMorphologyOracle(IEnumerable<string>? validWords = null)
    {
        valid = (validWords ?? []).ToHashSet(StringComparer.Ordinal);
    }

    public List<string> Queried { get; } = [];

    public bool IsValid(string word)
    {
        Queried.Add(word);
        return valid.Contains(word);
    }

    public IReadOnlyList<TurkishMorphologicalAnalysis> Analyze(string word) =>
        IsValid(word) ? [new TurkishMorphologicalAnalysis(word, new HashSet<string>(StringComparer.Ordinal))] : [];

    public bool IsKnown(string word) => true;

    public void Clear() => Queried.Clear();
}
