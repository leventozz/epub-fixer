using EpubFixer.Core.Ocr.Lattice;
using EpubFixer.Core.Ocr.Lattice.Models;
using EpubFixer.Core.Ocr.Models;

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
    public void Build_BudgetExceededIsReported()
    {
        var builder = new WordLatticeBuilder(new FakeMatcher(("koli", "koli", 0)));

        var lattice = builder.Build(Region("koli ukta", "koli ukta"), "koli ukta", new LatticeOptions(MaxRegionStates: 10));

        Assert.Equal(LatticeBuildOutcome.BudgetExceeded, lattice.Outcome);
        Assert.Empty(lattice.Arcs);
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
