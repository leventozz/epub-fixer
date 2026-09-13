using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Ocr.Lattice;
using EpubFixer.Core.Ocr.Lattice.Models;
using EpubFixer.Core.Ocr.Models;
using EpubFixer.Cli.Ocr.Lattice;

namespace EpubFixer.Tests;

public sealed class LatticeDecoderTests
{
    [Fact]
    public void Decode_IdentityPathReproducesWindowExactly()
    {
        var lattice = Built("temiz", new LatticeArc(0, 5, "temiz", 0, LatticeArcKind.Identity));
        var decoder = new LatticeDecoder(new FixedLanguageModel(), new LatticeOptions(Lambda: 0));

        var path = Assert.Single(decoder.Decode(lattice, 1));

        Assert.Equal("temiz", path.Text);
        Assert.Equal(0, path.Cost);
    }

    [Fact]
    public void Decode_PrefersCheaperEditPath()
    {
        var lattice = Built("koli ukta",
            new LatticeArc(0, 4, "koli", 0, LatticeArcKind.Identity),
            new LatticeArc(4, 5, " ", 0, LatticeArcKind.Literal),
            new LatticeArc(5, 9, "ukta", 0, LatticeArcKind.Identity),
            new LatticeArc(0, 9, "koltukta", 1.30, LatticeArcKind.Word));
        var model = new FixedLanguageModel(("koltukta", null, -0.1), ("koli", null, -10), ("ukta", "koli", -10));
        var decoder = new LatticeDecoder(model, new LatticeOptions(Lambda: 0.5));

        Assert.Equal("koltukta", decoder.Decode(lattice, 1)[0].Text);
    }

    [Fact]
    public void Decode_ContextChangesWinner()
    {
        var lattice = Built("x",
            new LatticeArc(0, 1, "koltukta", 1, LatticeArcKind.Word),
            new LatticeArc(0, 1, "koltuk", 1, LatticeArcKind.Word));
        var model = new FixedLanguageModel(
            ("koltukta", "berjer", -0.01),
            ("koltuk", "berjer", -5),
            ("koltukta", "<s>", -5),
            ("koltuk", "<s>", -0.01));

        Assert.Equal("koltukta", new LatticeDecoder(model, new LatticeOptions(), "berjer").Decode(lattice, 1)[0].Text);
        Assert.Equal("koltuk", new LatticeDecoder(model, new LatticeOptions()).Decode(lattice, 1)[0].Text);
    }

    [Fact]
    public void Decode_LambdaZeroIsPureEditCost()
    {
        var lattice = Built("x",
            new LatticeArc(0, 1, "pahalıDil", 0.5, LatticeArcKind.Word),
            new LatticeArc(0, 1, "ucuzDil", 1.0, LatticeArcKind.Word));
        var model = new FixedLanguageModel(("ucuzDil", "<s>", -0.01), ("pahalıDil", "<s>", -10));

        var result = new LatticeDecoder(model, new LatticeOptions(Lambda: 0)).Decode(lattice, 1);

        Assert.Equal("pahalıDil", result[0].Text);
    }

    [Fact]
    public void Decode_KBestReturnsDistinctTexts()
    {
        var lattice = Built("x",
            new LatticeArc(0, 1, "a", 0, LatticeArcKind.Word),
            new LatticeArc(0, 1, "b", 0.1, LatticeArcKind.Word),
            new LatticeArc(0, 1, "c", 0.2, LatticeArcKind.Word));
        var result = new LatticeDecoder(new FixedLanguageModel(), new LatticeOptions(Lambda: 0)).Decode(lattice, 3);

        Assert.Equal(["a", "b", "c"], result.Select(path => path.Text).ToArray());
    }

    [Fact]
    public void Decode_ReturnsEmptyForSkippedLattice()
    {
        var lattice = new WordLattice(string.Empty, 0, [], LatticeBuildOutcome.SkippedTooLong, 0);

        Assert.Empty(new LatticeDecoder(new FixedLanguageModel(), new LatticeOptions()).Decode(lattice));
    }

    [Fact]
    public void Decode_IsExactNotBeam()
    {
        var lattice = Built("abcd",
            new LatticeArc(0, 1, "a", 0, LatticeArcKind.Word),
            new LatticeArc(1, 4, "zzz", 10, LatticeArcKind.Word),
            new LatticeArc(0, 2, "ab", 2, LatticeArcKind.Word),
            new LatticeArc(2, 4, "cd", 0, LatticeArcKind.Word));

        var result = new LatticeDecoder(new FixedLanguageModel(), new LatticeOptions(Lambda: 0)).Decode(lattice, 1);

        Assert.Equal("abcd", result[0].Text);
        Assert.Equal(2, result[0].Cost);
    }

    [Fact]
    public void Decode_IsNotLimitedToTopEightPrefixes()
    {
        var arcs = new List<LatticeArc>();
        for (var i = 0; i < 12; i++)
        {
            arcs.Add(new LatticeArc(0, 1, $"p{i:00}", i * 0.01, LatticeArcKind.Word));
        }
        arcs.Add(new LatticeArc(1, 2, "tail", 0, LatticeArcKind.Word));
        var lmValues = Enumerable.Range(0, 12)
            .Select(i => ("tail", (string?)$"p{i:00}", i == 11 ? 0.0 : -100.0))
            .ToArray();

        var result = new LatticeDecoder(new FixedLanguageModel(lmValues), new LatticeOptions(Lambda: 1)).Decode(Built("xx", arcs.ToArray()), 1);

        Assert.Equal("p11tail", result[0].Text);
    }

    [Fact]
    public void Decode_FixtureTopOneWithFixtureLanguageModelIsPinned()
    {
        var fixture = LatticeFixture.RegionText;
        var builder = LatticeFixture.CreateBuilder();
        var topOne = 0;

        foreach (var occurrence in LatticeFixture.Occurrences)
        {
            var decoder = new LatticeDecoder(LatticeFixture.LanguageModel, new LatticeOptions(Lambda: 0.5));
            var lattice = builder.Build(LatticeFixture.Region(occurrence.Source, fixture), fixture, new LatticeOptions(ContextTokens: 0));
            var decoded = decoder.Decode(lattice, 1);

            Assert.NotEmpty(decoded);
            if (string.Equals(occurrence.Target, decoded[0].Text, StringComparison.Ordinal))
            {
                topOne++;
            }
        }

        Assert.Equal(8, topOne);
    }

    [Fact]
    public void Decode_IsDeterministic()
    {
        var lattice = Built("x",
            new LatticeArc(0, 1, "b", 0, LatticeArcKind.Word),
            new LatticeArc(0, 1, "a", 0, LatticeArcKind.Word));
        var decoder = new LatticeDecoder(new FixedLanguageModel(), new LatticeOptions(Lambda: 0));
        var first = decoder.Decode(lattice, 2);

        for (var i = 0; i < 50; i++)
        {
            var next = decoder.Decode(lattice, 2);
            Assert.Equal(first.Select(path => path.Text), next.Select(path => path.Text));
            Assert.Equal(first.Select(path => path.Cost), next.Select(path => path.Cost));
            Assert.Equal(first.SelectMany(path => path.Arcs), next.SelectMany(path => path.Arcs));
        }
    }

    private static WordLattice Built(string window, params LatticeArc[] arcs) =>
        new(window, 0, arcs, LatticeBuildOutcome.Built, arcs.Length);

    private sealed class FixedLanguageModel(params (string Word, string? Previous, double LogProbability)[] values) : ILanguageModel
    {
        public double LogProbability(string word, string? previousWord)
        {
            foreach (var value in values)
            {
                if (string.Equals(value.Word, word, StringComparison.Ordinal)
                    && string.Equals(value.Previous, previousWord, StringComparison.Ordinal))
                {
                    return value.LogProbability;
                }
            }

            return 0;
        }
    }

}
