using EpubFixer.Core.Epub;
using EpubFixer.Core.Quality;

namespace EpubFixer.Tests;

public sealed class BookHealthMeterTests
{
    [Fact]
    public void Measure_CountsEveryWordToken()
    {
        var health = Measure("Bir iki Ankara'da.", ["bir", "iki", "ankara"]);
        Assert.Equal(3, health.TotalTokens);
    }

    [Fact]
    public void Measure_UnresolvableCountsOnlyUnrecognizedTokens()
    {
        var health = Measure("Bir bozukk.", ["bir"]);
        Assert.Equal(1, health.UnresolvableTokens);
        Assert.Equal(["bozukk"], health.WorstExamples);
    }

    [Fact]
    public void Measure_ApostropheSuffixFallsBackToStem()
    {
        var health = Measure("Ankara'da", ["ankara"]);
        Assert.Equal(0, health.UnresolvableTokens);
    }

    [Fact]
    public void Measure_RateIsPerThousandTokens()
    {
        var health = Measure("bir iki uc dort", ["bir", "iki"]);
        Assert.Equal(500d, health.UnresolvableRate);
    }

    [Fact]
    public void Measure_EmptyStreamHasZeroRateAndNoExamples()
    {
        var health = Measure("", []);
        Assert.Equal(0, health.TotalTokens);
        Assert.Equal(0, health.UnresolvableRate);
        Assert.Empty(health.WorstExamples);
    }

    [Fact]
    public void Measure_WorstExamplesAreOrderedByFrequencyThenOrdinal()
    {
        var health = Measure("z y z a y z", [], worstExampleCount: 3);
        Assert.Equal(["z", "y", "a"], health.WorstExamples);
    }

    [Fact]
    public void Measure_IsDeterministicAcrossRuns()
    {
        var first = Measure("Bir bozukk ge1en", ["bir"]);
        var second = Measure("Bir bozukk ge1en", ["bir"]);
        Assert.Equal(first, second);
    }

    private static BookHealth Measure(string text, IReadOnlyCollection<string> recognized, int worstExampleCount = 20)
    {
        using var epub = TemporaryEpub.Create(
            [new TestDocument("main", "main.xhtml", $"<html xmlns=\"http://www.w3.org/1999/xhtml\"><body><p>{System.Security.SecurityElement.Escape(text)}</p></body></html>")],
            [new TestSpineItem("main")]);
        var stream = new EpubPackageReader().Read(epub.Path).LogicalText;
        return new BookHealthMeter(new FakeRecognizer(recognized), worstExampleCount).Measure(stream);
    }

    private sealed class FakeRecognizer(IReadOnlyCollection<string> words) : IWordRecognizer
    {
        private readonly HashSet<string> words = new(words, StringComparer.Ordinal);

        public bool IsRecognized(string normalizedWord) => words.Contains(normalizedWord);
    }
}
