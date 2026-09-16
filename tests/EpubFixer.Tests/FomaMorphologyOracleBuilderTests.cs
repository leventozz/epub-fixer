using EpubFixer.Cli.Morphology;
using EpubFixer.Core.Morphology;
using EpubFixer.TrMorph;

namespace EpubFixer.Tests;

public sealed class FomaMorphologyOracleBuilderTests
{
    [Fact]
    public void Build_SecondCallReturnsOracleContainingBothVocabularies()
    {
        var parser = new RecordingBatchParser();
        using var builder = new FomaMorphologyOracleBuilder(() => parser);

        var first = builder.Build(["sabah"]);
        var second = builder.Build(["akşam"]);

        Assert.True(first.IsKnown("sabah"));
        Assert.True(second.IsKnown("sabah"));
        Assert.True(second.IsKnown("akşam"));
        Assert.Equal(2, parser.BatchInputs.Count);
    }

    [Fact]
    public void Build_DoesNotStartProcessWhenVocabularyIsEmpty()
    {
        var created = false;
        using var builder = new FomaMorphologyOracleBuilder(() =>
        {
            created = true;
            return new RecordingBatchParser();
        });

        var oracle = builder.Build([]);

        Assert.False(created);
        Assert.False(oracle.IsKnown("sabah"));
    }

    [Fact]
    public void Build_SendsEachDistinctWordOnceAndInDeterministicOrder()
    {
        var parser = new RecordingBatchParser();
        using var builder = new FomaMorphologyOracleBuilder(() => parser);

        builder.Build(["z", "a", "z"]);

        Assert.Equal([["a", "z"]], parser.BatchInputs);
    }

    [Fact]
    [Trait("Category", "Slow")]
    public void Build_ResolvesEveryRequestedWord()
    {
        using var builder = new FomaMorphologyOracleBuilder(() => new FomaTurkishMorphologyAnalyzer());

        var oracle = builder.Build(["sabah", "zzzzzzzzzz"]);

        Assert.True(oracle.IsKnown("sabah"));
        Assert.True(oracle.IsKnown("zzzzzzzzzz"));
        Assert.True(oracle.Analyze("sabah").Count > 0);
        Assert.False(oracle.IsValid("zzzzzzzzzz"));
    }

    private sealed class RecordingBatchParser : IBatchTurkishMorphologicalParser
    {
        public List<string[]> BatchInputs { get; } = [];
        public TurkishMorphologyCacheStatistics CacheStatistics => new(0, 0, BatchInputs.Sum(input => input.Length), BatchInputs.Count, BatchInputs.Sum(input => input.Length), 1);

        public IReadOnlyList<TurkishMorphologicalAnalysis> Analyze(string word) =>
            AnalyzeBatch([word])[word];

        public IReadOnlyDictionary<string, IReadOnlyList<TurkishMorphologicalAnalysis>> AnalyzeBatch(IEnumerable<string> words)
        {
            var input = words.ToArray();
            BatchInputs.Add(input);
            return input.ToDictionary(
                word => word,
                word => (IReadOnlyList<TurkishMorphologicalAnalysis>)[new TurkishMorphologicalAnalysis(word, new HashSet<string>(StringComparer.Ordinal))],
                StringComparer.Ordinal);
        }
    }
}
