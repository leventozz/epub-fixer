using EpubFixer.Core.Ocr.Lattice;
using EpubFixer.Core.Ocr.Lattice.Models;
using EpubFixer.Core.Ocr.Models;
using EpubFixer.Cli.Ocr.Lattice;
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
        var builder = CreateFixtureBuilder();
        var fixture = File.ReadAllText(FindRepositoryFile(Path.Combine("tests", "Fixtures", "OcrRegion", "odun-kesmek-region-01.txt")));
        var occurrence = FixtureOccurrences().OrderByDescending(item => item.Source.Length).First();

        var lattice = builder.Build(Region(occurrence.Source, fixture), fixture, new LatticeOptions(ContextTokens: 0));

        Assert.True(lattice.Arcs.Count <= 240, $"Arc count was {lattice.Arcs.Count}");
    }

    [Fact]
    public void Build_AllTenTargetsHaveAnArc()
    {
        var builder = CreateFixtureBuilder();
        var fixture = File.ReadAllText(FindRepositoryFile(Path.Combine("tests", "Fixtures", "OcrRegion", "odun-kesmek-region-01.txt")));

        foreach (var occurrence in FixtureOccurrences())
        {
            var region = Region(occurrence.Source, fixture);
            var lattice = builder.Build(region, fixture, new LatticeOptions(ContextTokens: 0));
            var from = region.Start - lattice.WindowOffset;
            var to = region.EndExclusive - lattice.WindowOffset;

            Assert.Equal(LatticeBuildOutcome.Built, lattice.Outcome);
            Assert.Contains(lattice.Arcs, arc => arc.Kind == LatticeArcKind.Word && arc.From == from && arc.To == to && arc.Word == occurrence.Target);
        }
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

    private static WordLatticeBuilder CreateFixtureBuilder() =>
        new(new SymSpellLexiconMatcher(BuildVocabulary(FixtureOccurrences().Select(item => item.Target))));

    private static BookVocabulary BuildVocabulary(IEnumerable<string> words)
    {
        var wordList = words.Distinct(StringComparer.Ordinal).ToArray();
        var text = string.Join(' ', wordList.SelectMany(word => new[] { word, word }));
        return new BookVocabularyBuilder(
                TurkishFrequencyList.FromLines([]),
                new BookVocabularyOptions(MinBookCount: 1))
            .Build(TestStreamFactory.FromSingleSegment(text), Array.Empty<CorruptedTextRegion>(), new FakeMorphologyOracle(wordList));
    }

    private static IReadOnlyList<(string Source, string Target)> FixtureOccurrences() =>
    [
        ("ı ıç", "üç"),
        ("kendi-ıni", "kendimi"),
        ("1 ı iç", "hiç"),
        (":,ohbet", "sohbet"),
        ("koli ukta", "koltukta"),
        ("ı ızellikle", "özellikle"),
        ("ı ılduğu", "olduğu"),
        ("Ce-lıimde", "Cebimde"),
        ("ge-^:cn", "geçen"),
        ("yü-ıiimeye", "yürümeye")
    ];

    private static string FindRepositoryFile(string relativePath)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(relativePath);
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
