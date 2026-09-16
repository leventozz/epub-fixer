using System.Text;
using EpubFixer.Core.Morphology;

namespace EpubFixer.Tests;

public sealed class MorphologyOracleTests
{
    [Fact]
    public void IsValid_ReturnsTrueForPrefilledWordWithAnalyses()
    {
        var oracle = new MorphologyOracle(new Dictionary<string, IReadOnlyList<TurkishMorphologicalAnalysis>>(StringComparer.Ordinal)
        {
            ["koltukta"] = [new TurkishMorphologicalAnalysis("koltuk", new HashSet<string>(StringComparer.Ordinal))]
        });

        Assert.True(oracle.IsValid("koltukta"));
    }

    [Fact]
    public void IsValid_ReturnsFalseForPrefilledWordWithoutAnalyses()
    {
        var oracle = new MorphologyOracle(new Dictionary<string, IReadOnlyList<TurkishMorphologicalAnalysis>>(StringComparer.Ordinal)
        {
            ["zzzz"] = []
        });

        Assert.False(oracle.IsValid("zzzz"));
        Assert.Empty(oracle.Analyze("zzzz"));
        Assert.True(oracle.IsKnown("zzzz"));
    }

    [Fact]
    public void UnknownWordThrowsAndIsKnownReturnsFalse()
    {
        var oracle = new MorphologyOracle(new Dictionary<string, IReadOnlyList<TurkishMorphologicalAnalysis>>(StringComparer.Ordinal));

        var exception = Assert.Throws<MorphologyOracleException>(() => oracle.IsValid("koltukta"));
        Assert.Contains("koltukta", exception.Message);
        Assert.Contains("0 words", exception.Message);
        Assert.Throws<MorphologyOracleException>(() => oracle.Analyze("koltukta"));
        Assert.False(oracle.IsKnown("koltukta"));
    }

    [Fact]
    public void Lookup_UsesNfcNormalizedKey()
    {
        var decomposed = "s\u0327ubat";
        var oracle = new MorphologyOracle(new Dictionary<string, IReadOnlyList<TurkishMorphologicalAnalysis>>(StringComparer.Ordinal)
        {
            [decomposed] = [new TurkishMorphologicalAnalysis("subat", new HashSet<string>(StringComparer.Ordinal))]
        });

        Assert.True(oracle.IsKnown(decomposed.Normalize(NormalizationForm.FormC)));
    }

    [Fact]
    public void Lookup_IsCaseSensitive()
    {
        var oracle = new MorphologyOracle(new Dictionary<string, IReadOnlyList<TurkishMorphologicalAnalysis>>(StringComparer.Ordinal)
        {
            ["Auersberger"] = [new TurkishMorphologicalAnalysis("Auersberger", new HashSet<string>(StringComparer.Ordinal))]
        });

        Assert.True(oracle.IsKnown("Auersberger"));
        Assert.False(oracle.IsKnown("auersberger"));
    }

    [Fact]
    public void Constructor_CopiesVocabularySoLaterMutationIsNotVisible()
    {
        var source = new Dictionary<string, IReadOnlyList<TurkishMorphologicalAnalysis>>(StringComparer.Ordinal)
        {
            ["once"] = []
        };
        var oracle = new MorphologyOracle(source);

        source["sonra"] = [];

        Assert.True(oracle.IsKnown("once"));
        Assert.False(oracle.IsKnown("sonra"));
    }

    [Fact]
    public void IsValid_ThrowsArgumentExceptionForWhitespace()
    {
        var oracle = new MorphologyOracle(new Dictionary<string, IReadOnlyList<TurkishMorphologicalAnalysis>>(StringComparer.Ordinal));

        Assert.Throws<ArgumentException>(() => oracle.IsValid(" "));
        Assert.Throws<ArgumentException>(() => oracle.Analyze(""));
        Assert.Throws<ArgumentException>(() => oracle.IsKnown("\t"));
        Assert.Throws<ArgumentNullException>(() => oracle.IsKnown(null!));
    }
}
