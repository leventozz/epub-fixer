using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Lexicon.Models;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Tests;

public sealed class BookVocabularyBuilderTests
{
    [Fact]
    public void Build_LearnsRepeatedBookNamesAndPreferredSurface()
    {
        var vocabulary = BuildVocabulary("Berjer koltukta Berjer", [], new FakeMorphologyOracle(["Berjer", "koltukta"]));

        var entry = vocabulary.Find("berjer");
        Assert.NotNull(entry);
        Assert.Equal(VocabularySource.Book, entry.Source);
        Assert.Equal("Berjer", entry.PreferredSurface);
    }

    [Fact]
    public void Build_DoesNotAskMorphologyForFrequencyWords()
    {
        var oracle = new FakeMorphologyOracle();
        _ = BuildVocabulary("temiz özel", ["temiz 10"], oracle);

        Assert.DoesNotContain("temiz", oracle.Queried);
    }

    [Fact]
    public void Build_CountsSingleRuneFrequencyWordsFromTheBook()
    {
        var vocabulary = BuildVocabulary("o o", ["o 10"], new FakeMorphologyOracle());

        var entry = vocabulary.Find("o");
        Assert.NotNull(entry);
        Assert.Equal(2, entry.BookCount);
        Assert.Equal(VocabularySource.Book, entry.Source);
    }

    [Fact]
    public void Build_FiltersSuspiciousUnverifiedTokens()
    {
        var vocabulary = BuildVocabulary("a1 a1 a1 temiz temiz", [], new FakeMorphologyOracle(["temiz"]));

        Assert.False(vocabulary.Contains("a1"));
    }

    [Fact]
    public void UnigramLogProbability_FrequencyOnlyWordNeverOutranksRarestBookWord()
    {
        var vocabulary = BuildVocabulary("kitap kitap", ["frekans 100000"], new FakeMorphologyOracle(["kitap"]));

        Assert.True(vocabulary.UnigramLogProbability("kitap") > vocabulary.UnigramLogProbability("frekans"));
    }

    private static BookVocabulary BuildVocabulary(string text, IEnumerable<string> frequencyLines, IMorphologyOracle oracle)
    {
        var stream = TestStreamFactory.FromSingleSegment(text);
        var list = TurkishFrequencyList.FromLines(frequencyLines);
        return new BookVocabularyBuilder(list).Build(stream, [], oracle);
    }
}
