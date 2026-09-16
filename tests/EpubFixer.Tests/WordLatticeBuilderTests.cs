using EpubFixer.Core.Ocr.Lattice;
using EpubFixer.Core.Ocr.Lattice.Models;
using EpubFixer.Core.Ocr.Models;
using EpubFixer.Adapters.Ocr.Lattice;
using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Morphology;

namespace EpubFixer.Tests;

public sealed class WordLatticeBuilderTests
{
    [Fact]
    public void Build_JoinArcSpansInnerSpace()
    {
        var builder = new WordLatticeBuilder(new FakeMatcher(("koli ukta", "koltukta", 1.30)));

        var lattice = builder.Build(Region("koli ukta", "koli ukta"), "koli ukta", new LatticeOptions());

        Assert.Contains(lattice.Arcs, arc => arc is { From: 0, To: 9, Word: "koltukta", Cost: 1.30, Kind: LatticeArcKind.Word });
    }

    [Fact]
    public void Build_SplitArcEndsMidToken()
    {
        var builder = new WordLatticeBuilder(new FakeMatcher(
            ("bugün", "bugün", 0),
            ("çok", "çok", 0)));

        var lattice = builder.Build(Region("bugünçok", "bugünçok"), "bugünçok", new LatticeOptions());

        Assert.Contains(lattice.Arcs, arc => arc is { From: 0, To: 5, Word: "bugün", Kind: LatticeArcKind.Word });
        Assert.Contains(lattice.Arcs, arc => arc is { From: 5, To: 8, Word: "çok", Kind: LatticeArcKind.Word });
    }

    [Fact]
    public void Build_IdentityArcAlwaysExists()
    {
        var builder = new WordLatticeBuilder(new FakeMatcher());

        var lattice = builder.Build(Region("temiz kelime", "temiz kelime"), "temiz kelime", new LatticeOptions());

        Assert.Contains(lattice.Arcs, arc => arc is { From: 0, To: 5, Word: "temiz", Cost: 0, Kind: LatticeArcKind.Identity });
        Assert.Contains(lattice.Arcs, arc => arc is { From: 6, To: 12, Word: "kelime", Cost: 0, Kind: LatticeArcKind.Identity });
    }

    [Fact]
    public void Build_LiteralArcsPreservePunctuation()
    {
        var builder = new WordLatticeBuilder(new FakeMatcher());

        var lattice = builder.Build(Region("koltukta,", "koltukta,"), "koltukta,", new LatticeOptions());

        Assert.Contains(lattice.Arcs, arc => arc is { From: 8, To: 9, Word: ",", Cost: 0, Kind: LatticeArcKind.Literal });
    }

    [Fact]
    public void Build_HardGarbageRunCostsToKeepAndIsCheaperToDrop()
    {
        var builder = new WordLatticeBuilder(new FakeMatcher(("ohbet", "sohbet", 1.0)));

        var lattice = builder.Build(Region("gece :,ohbet", "gece :,ohbet"), "gece :,ohbet", new LatticeOptions());

        // The run " :," spans [4,7) and carries two non-space characters. Keeping them is no longer
        // free (RetainedGarbage 0.25 each) and dropping them is cheaper (GarbageDeletion 0.20 each).
        // Both arcs count the SAME characters - otherwise keeping would still win (D91).
        Assert.Contains(lattice.Arcs, arc => arc is
        {
            From: 4, To: 7, Word: " :,", Cost: 0.50, Kind: LatticeArcKind.Literal
        });
        Assert.Contains(lattice.Arcs, arc => arc is
        {
            From: 4, To: 7, Word: " ", Cost: 0.40, Kind: LatticeArcKind.GarbageDeletion
        });
    }

    [Fact]
    public void Build_OrdinaryPunctuationStaysFreeAndUndeletable()
    {
        var builder = new WordLatticeBuilder(new FakeMatcher());

        var lattice = builder.Build(Region("yoktu. Gerçekten", "yoktu. Gerçekten"), "yoktu. Gerçekten", new LatticeOptions());

        // No hard garbage glyph in the run: charging it would tax ordinary prose, and deleting it
        // would weld two sentences together. D91.
        Assert.Contains(lattice.Arcs, arc => arc is { Word: ". ", Cost: 0, Kind: LatticeArcKind.Literal });
        Assert.DoesNotContain(lattice.Arcs, arc => arc.Kind == LatticeArcKind.GarbageDeletion);
    }

    [Fact]
    public void Build_TokenInternalGarbageRunIsUntouched()
    {
        var builder = new WordLatticeBuilder(new FakeMatcher(("kü-^:ük", "küçük", 0.75)));

        var lattice = builder.Build(Region("kü-^:ük", "kü-^:ük"), "kü-^:ük", new LatticeOptions());

        // The run "-^:" carries hard garbage but no whitespace: it sits inside a token and the word
        // arc already solves it. Charging or deleting it would only add a rival "küük" path.
        Assert.Contains(lattice.Arcs, arc => arc is { Word: "-^:", Cost: 0, Kind: LatticeArcKind.Literal });
        Assert.DoesNotContain(lattice.Arcs, arc => arc.Kind == LatticeArcKind.GarbageDeletion);
    }

    [Fact]
    public void Build_BudgetExceededIsReported()
    {
        var builder = new WordLatticeBuilder(new FakeMatcher(("koli", "koli", 0)));

        var lattice = builder.Build(Region("koli ukta", "koli ukta"), "koli ukta", new LatticeOptions(MaxRegionStates: 10));

        Assert.Equal(LatticeBuildOutcome.BudgetExceeded, lattice.Outcome);
        Assert.Empty(lattice.Arcs);
    }

    [Fact]
    public void Build_RestoresLeadingCapital()
    {
        var builder = new WordLatticeBuilder(new FakeMatcher(("Ce-lıimde", "cebimde", 1.50)));

        var lattice = builder.Build(Region("Ce-lıimde", "Ce-lıimde"), "Ce-lıimde", new LatticeOptions());

        Assert.Contains(lattice.Arcs, arc => arc is { From: 0, To: 9, Word: "Cebimde", Kind: LatticeArcKind.Word });
    }

    [Fact]
    public void Build_ArcCountStaysUnderCeiling()
    {
        var builder = LatticeFixture.CreateBuilder();
        var fixture = LatticeFixture.RegionText;
        var occurrence = LatticeFixture.Occurrences.OrderByDescending(item => item.Source.Length).First();

        var lattice = builder.Build(LatticeFixture.Region(occurrence.Source, fixture), fixture, new LatticeOptions(ContextTokens: 0));

        Assert.True(lattice.Arcs.Count <= 650, $"Arc count was {lattice.Arcs.Count}");
    }

    [Fact]
    public void Build_FixtureTargetArcCoverageWithFrequencyVocabularyIsPinned()
    {
        var builder = LatticeFixture.CreateBuilder();
        var fixture = LatticeFixture.RegionText;
        var targetsWithArc = 0;

        foreach (var occurrence in LatticeFixture.Occurrences)
        {
            var region = LatticeFixture.Region(occurrence.Source, fixture);
            var lattice = builder.Build(region, fixture, new LatticeOptions(ContextTokens: 0));
            var from = region.Start - lattice.WindowOffset;
            var to = region.EndExclusive - lattice.WindowOffset;

            Assert.Equal(LatticeBuildOutcome.Built, lattice.Outcome);
            if (lattice.Arcs.Any(arc => arc.Kind == LatticeArcKind.Word && arc.From == from && arc.To == to && arc.Word == occurrence.Target))
            {
                targetsWithArc++;
            }
        }

        Assert.Equal(9, targetsWithArc);
    }

    [Fact]
    public void Build_IsDeterministic()
    {
        var builder = new WordLatticeBuilder(new FakeMatcher(
            ("koli", "koli", 0),
            ("ukta", "ukta", 0),
            ("koli ukta", "koltukta", 1.30)));
        var region = Region("koli ukta", "koli ukta");
        var first = builder.Build(region, "koli ukta", new LatticeOptions()).Arcs;

        for (var i = 0; i < 50; i++)
        {
            Assert.Equal(first, builder.Build(region, "koli ukta", new LatticeOptions()).Arcs);
        }
    }

    private static CorruptedTextRegion Region(string raw, string fullText)
    {
        var start = fullText.IndexOf(raw, StringComparison.Ordinal);
        Assert.True(start >= 0);
        return new CorruptedTextRegion(raw, start, start + raw.Length, [raw], string.Empty, string.Empty, []);
    }

    private sealed class FakeMatcher(params (string Span, string Word, double Cost)[] matches) : ILexiconMatcher
    {
        public IReadOnlyList<LexiconMatch> Match(ReadOnlySpan<char> span, double budget)
        {
            var text = span.ToString();
            return matches
                .Where(match => match.Span == text && match.Cost <= budget)
                .Select(match => new LexiconMatch(match.Word, match.Cost))
                .ToArray();
        }
    }
}
