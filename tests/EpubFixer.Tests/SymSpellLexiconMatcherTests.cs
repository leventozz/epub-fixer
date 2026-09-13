using EpubFixer.Adapters.Ocr.Lattice;
using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Ocr.Lattice;

namespace EpubFixer.Tests;

public sealed class SymSpellLexiconMatcherTests
{
    [Fact]
    public void Match_FindsFixtureTargets()
    {
        var matcher = new SymSpellLexiconMatcher(LatticeFixture.Vocabulary);

        foreach (var (source, target) in LatticeFixture.Occurrences)
        {
            var matches = matcher.Match(source, 3.0);
            Assert.Contains(matches, match => match.Word == target);
        }
    }

    [Fact]
    public void Match_RespectsBudget()
    {
        var matcher = new SymSpellLexiconMatcher(BuildVocabulary(["koltukta"]));

        var matches = matcher.Match("koli ukta", 0.5);

        Assert.DoesNotContain(matches, match => match.Word == "koltukta");
    }

    [Fact]
    public void Match_OrdersByWeightedCost()
    {
        var matcher = new SymSpellLexiconMatcher(BuildVocabulary(["üç", "uç"]));

        var matches = matcher.Match("ıç", 3.0);

        Assert.Equal("üç", matches[0].Word);
    }

    [Fact]
    public void Match_ReturnsPreferredSurface()
    {
        var matcher = new SymSpellLexiconMatcher(BuildVocabulary(["Auersberger"]));

        var matches = matcher.Match("auersberger", 1.0);

        Assert.Contains(matches, match => match.Word == "Auersberger");
    }

    [Fact]
    public void Match_IsCached()
    {
        var aligner = new CountingAligner(new EpubFixer.Core.Ocr.WeightedEditAligner());
        var matcher = new SymSpellLexiconMatcher(BuildVocabulary(["koltukta"]), aligner);

        _ = matcher.Match("koli ukta", 3.0);
        var first = aligner.Calls;
        _ = matcher.Match("koli ukta", 3.0);

        Assert.Equal(first, aligner.Calls);
    }

    [Fact]
    public void Match_DoesNotCallMorphology()
    {
        var matcher = new SymSpellLexiconMatcher(BuildFrequencyVocabulary(["koltukta"], new ThrowingMorphologyOracle()));

        var matches = matcher.Match("koli ukta", 3.0);

        Assert.Contains(matches, match => match.Word == "koltukta");
    }

    private static BookVocabulary BuildVocabulary(IEnumerable<string> words, IMorphologyOracle? oracle = null)
    {
        var text = string.Join(' ', words.SelectMany(word => new[] { word, word }));
        var stream = TestStreamFactory.FromSingleSegment(text);
        return new BookVocabularyBuilder(
                TurkishFrequencyList.FromLines([]),
                new BookVocabularyOptions(MinBookCount: 1))
            .Build(stream, Array.Empty<EpubFixer.Core.Ocr.Models.CorruptedTextRegion>(), oracle ?? new FakeMorphologyOracle(words));
    }

    private static BookVocabulary BuildFrequencyVocabulary(IEnumerable<string> words, IMorphologyOracle oracle)
    {
        var wordList = words.ToArray();
        var text = string.Join(' ', wordList.SelectMany(word => new[] { word, word }));
        var stream = TestStreamFactory.FromSingleSegment(text);
        var frequency = TurkishFrequencyList.FromLines(wordList.Select(word => $"{word} 10"));
        return new BookVocabularyBuilder(
                frequency,
                new BookVocabularyOptions(MinBookCount: 1))
            .Build(stream, Array.Empty<EpubFixer.Core.Ocr.Models.CorruptedTextRegion>(), oracle);
    }

    private sealed class CountingAligner(EpubFixer.Core.Ocr.IWeightedEditAligner inner) : EpubFixer.Core.Ocr.IWeightedEditAligner
    {
        public int Calls { get; private set; }

        public bool TryAlign(ReadOnlySpan<char> source, ReadOnlySpan<char> target, double budget, out EpubFixer.Core.Ocr.Models.EditAlignment alignment)
        {
            Calls++;
            return inner.TryAlign(source, target, budget, out alignment);
        }
    }

    private sealed class ThrowingMorphologyOracle : IMorphologyOracle
    {
        public bool IsValid(string word) => throw new InvalidOperationException("Morphology must not be called.");

        public IReadOnlyList<TurkishMorphologicalAnalysis> Analyze(string word) => throw new InvalidOperationException("Morphology must not be called.");

        public bool IsKnown(string word) => throw new InvalidOperationException("Morphology must not be called.");
    }
}
