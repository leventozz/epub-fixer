using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Ocr;
using EpubFixer.Core.Ocr.Lattice;
using EpubFixer.Core.Ocr.Lattice.Models;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Tests;

public sealed class LatticeRegionReconstructorTests
{
    [Fact]
    public void Reconstructor_ImplementsPortWithoutChangingIt()
    {
        IOcrRegionReconstructor reconstructor = Create("koli ukta", new FixedLanguageModel(("koltukta", "<s>", -0.01)));

        var candidates = reconstructor.Reconstruct(Region("koli ukta", 0));

        Assert.NotEmpty(candidates);
    }

    [Fact]
    public void Reconstructor_ReportsLatticeSource()
    {
        var reconstructor = Create("koli ukta", new FixedLanguageModel(("koltukta", "<s>", -0.01)));

        var candidates = reconstructor.Reconstruct(Region("koli ukta", 0));

        Assert.All(candidates, candidate => Assert.Equal(ReconstructionSource.Lattice, candidate.Source));
    }

    [Fact]
    public void Statistics_CountsSkippedAndExceeded()
    {
        var skipped = Create(new string('a', 60), new FixedLanguageModel(), new LatticeOptions(MaxWindowLength: 10));
        _ = skipped.Evaluate(Region(new string('a', 60), 0));
        Assert.Equal(1, skipped.Statistics.SkippedTooLong);

        var exceeded = Create("koli ukta", new FixedLanguageModel(), new LatticeOptions(MaxRegionStates: 1));
        _ = exceeded.Evaluate(Region("koli ukta", 0));
        Assert.Equal(1, exceeded.Statistics.BudgetExceeded);
    }

    private static LatticeRegionReconstructor Create(string fullText, ILanguageModel model, LatticeOptions? options = null)
    {
        options ??= new LatticeOptions();
        var matcher = new FakeMatcher(("koli ukta", "koltukta", 1.30));
        var builder = new WordLatticeBuilder(matcher);
        var vocabulary = BuildVocabulary([]);
        var gate = new CorrectionAcceptanceGate(vocabulary, options);
        return new LatticeRegionReconstructor(fullText, builder, new LatticeDecoder(model, options), gate, options);
    }

    private static BookVocabulary BuildVocabulary(IEnumerable<string> words)
    {
        var wordList = words.ToArray();
        var text = wordList.Length == 0 ? "dolgu dolgu" : string.Join(' ', wordList.SelectMany(word => new[] { word, word }));
        return new BookVocabularyBuilder(TurkishFrequencyList.FromLines([]), new BookVocabularyOptions(MinBookCount: 1))
            .Build(TestStreamFactory.FromSingleSegment(text), Array.Empty<CorruptedTextRegion>(), new FakeMorphologyOracle(text.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
    }

    private static CorruptedTextRegion Region(string raw, int start) =>
        new(raw, start, start + raw.Length, [raw], string.Empty, string.Empty, []);

    private sealed class FakeMatcher(params (string Span, string Word, double Cost)[] matches) : ILexiconMatcher
    {
        public IReadOnlyList<LexiconMatch> Match(ReadOnlySpan<char> span, double budget)
        {
            var text = span.ToString();
            return matches.Where(match => match.Span == text && match.Cost <= budget)
                .Select(match => new LexiconMatch(match.Word, match.Cost))
                .ToArray();
        }
    }

    private sealed class FixedLanguageModel(params (string Word, string? Previous, double LogProbability)[] values) : ILanguageModel
    {
        public double LogProbability(string word, string? previousWord)
        {
            foreach (var value in values)
            {
                if (value.Word == word && value.Previous == previousWord)
                {
                    return value.LogProbability;
                }
            }

            return -20;
        }
    }
}
