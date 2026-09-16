using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Morphology;

namespace EpubFixer.Tests;

public sealed class BookLanguageModelTests
{
    [Fact]
    public void LogProbability_UsesBigramWhenSeen()
    {
        var (vocabulary, model) = Build("berjer koltukta berjer koltukta başka söz");

        Assert.True(model.LogProbability("koltukta", "berjer") > model.LogProbability("koltukta", "başka"));
    }

    [Fact]
    public void LogProbability_TreatsNullPreviousWordAsUnigram()
    {
        var (vocabulary, model) = Build("berjer koltukta berjer");

        Assert.Equal(vocabulary.UnigramLogProbability("berjer"), model.LogProbability("berjer", null));
    }

    [Fact]
    public void Build_IsDeterministic()
    {
        var (_, left) = Build("berjer koltukta berjer koltukta");
        var (_, right) = Build("berjer koltukta berjer koltukta");

        Assert.Equal(left.Statistics, right.Statistics);
        Assert.Equal(left.LogProbability("koltukta", "berjer"), right.LogProbability("koltukta", "berjer"));
    }

    [Fact]
    public void Statistics_ReportTypeAndTokenCounts()
    {
        var (_, model) = Build("berjer koltukta berjer");

        Assert.Equal(2, model.Statistics.UnigramTypes);
        Assert.Equal(3, model.Statistics.UnigramTokens);
        Assert.Equal(3, model.Statistics.BigramTokens);
    }

    [Fact]
    public void Build_DoesNotRemoveHeldOutBigramsFromProductionModel()
    {
        var (_, model) = Build("aa bb cc dd ee ff gg hh ii jj kk");

        Assert.Equal(11, model.Statistics.UnigramTokens);
        Assert.Equal(11, model.Statistics.BigramTokens);
    }

    private static (BookVocabulary Vocabulary, BookLanguageModel Model) Build(string text)
    {
        var stream = TestStreamFactory.FromSingleSegment(text);
        var list = TurkishFrequencyList.FromLines([]);
        var vocabulary = new BookVocabularyBuilder(list, new BookVocabularyOptions(MinBookCount: 1))
            .Build(stream, [], new FakeMorphologyOracle(text.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
        var model = new BookLanguageModelBuilder(vocabulary).Build(stream, []);
        return (vocabulary, model);
    }
}
