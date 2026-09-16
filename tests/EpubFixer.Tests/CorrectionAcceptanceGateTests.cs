using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Lexicon.Models;
using EpubFixer.Core.Ocr.Lattice;
using EpubFixer.Core.Ocr.Lattice.Models;
using EpubFixer.Core.Ocr.Models;
using EpubFixer.Adapters.Ocr.Lattice;

namespace EpubFixer.Tests;

public sealed class CorrectionAcceptanceGateTests
{
    [Fact]
    public void Gate_AppliesConfidentCorrection()
    {
        var gate = Gate();
        var lattice = Built("koli ukta", 100, new LatticeArc(0, 9, "koltukta", 1.30, LatticeArcKind.Word));
        var paths = new[]
        {
            new DecodedPath("koltukta", 1.30, [lattice.Arcs[0]]),
            new DecodedPath("koli ukta", 3.0, [])
        };

        var result = gate.Evaluate(Region("koli ukta", 100), lattice, paths);

        Assert.Equal(AcceptanceVerdict.Apply, result.Verdict);
        Assert.Equal("koltukta", result.Replacement);
        Assert.Equal(100, result.LogicalStart);
        Assert.Equal(109, result.LogicalEndExclusive);
    }

    [Fact]
    public void Gate_LeavesCleanToken()
    {
        var lattice = Built("temiz", 0);

        var result = Gate(["temiz"]).Evaluate(Region("temiz", 0), lattice, [new DecodedPath("temiz", 0, [])]);

        Assert.Equal(AcceptanceVerdict.Leave, result.Verdict);
        Assert.Contains("NoChange", result.Reasons);
    }

    [Fact]
    public void Gate_NeverTouchesValidOriginal()
    {
        var lattice = Built("Auersberger", 0, new LatticeArc(0, 11, "Auerzberger", 1, LatticeArcKind.Word));
        var path = new DecodedPath("Auerzberger", 1, [lattice.Arcs[0]]);

        var result = Gate(["Auersberger"]).Evaluate(Region("Auersberger", 0), lattice, [path, new DecodedPath("Auersberger", 3, [])]);

        Assert.Equal(AcceptanceVerdict.Leave, result.Verdict);
        Assert.Contains("OriginalTokenIsValid", result.Reasons);
    }

    [Fact]
    public void Gate_ReviewsOnSmallMargin()
    {
        var lattice = Built("koli ukta", 0, new LatticeArc(0, 9, "koltukta", 1.30, LatticeArcKind.Word));

        var result = Gate().Evaluate(Region("koli ukta", 0), lattice,
        [
            new DecodedPath("koltukta", 1.30, [lattice.Arcs[0]]),
            new DecodedPath("koli ukta", 1.40, [])
        ]);

        Assert.Equal(AcceptanceVerdict.Review, result.Verdict);
        Assert.Contains("MarginTooSmall", result.Reasons);
    }

    [Fact]
    public void Gate_RejectsAboveCostThreshold()
    {
        var lattice = Built("koli ukta", 0, new LatticeArc(0, 9, "koltukta", 3.0, LatticeArcKind.Word));

        var result = Gate().Evaluate(Region("koli ukta", 0), lattice,
        [
            new DecodedPath("koltukta", 3.0, [lattice.Arcs[0]]),
            new DecodedPath("koli ukta", 5.0, [])
        ]);

        Assert.Equal(AcceptanceVerdict.Leave, result.Verdict);
        Assert.Contains("CostAboveThreshold", result.Reasons);
    }

    [Fact]
    public void Gate_RejectsTwoOrdinarySubstitutions()
    {
        var lattice = Built("zz", 0, new LatticeArc(0, 2, "aa", 2.0, LatticeArcKind.Word));

        var result = Gate().Evaluate(Region("zz", 0), lattice,
        [
            new DecodedPath("aa", 2.0, [lattice.Arcs[0]]),
            new DecodedPath("zz", 4.0, [])
        ]);

        Assert.Equal(AcceptanceVerdict.Leave, result.Verdict);
        Assert.Contains("TooManyOrdinaryEdits", result.Reasons);
    }

    [Fact]
    public void Gate_RejectsSingleCharacterExpansion()
    {
        var lattice = Built("1", 0, new LatticeArc(0, 1, "göster", 1.0, LatticeArcKind.Word));

        var result = Gate().Evaluate(Region("1", 0), lattice,
        [
            new DecodedPath("göster", 1.0, [lattice.Arcs[0]]),
            new DecodedPath("1", 3.0, [])
        ]);

        Assert.Equal(AcceptanceVerdict.Leave, result.Verdict);
        Assert.Contains("TooManyOrdinaryEdits", result.Reasons);
    }

    [Fact]
    public void Gate_RejectsOrdinaryEditInProperName()
    {
        var lattice = Built("Ope-ra'daki", 0, new LatticeArc(0, 11, "Pera'daki", 1.0, LatticeArcKind.Word));

        var result = Gate().Evaluate(Region("Ope-ra'daki", 0), lattice,
        [
            new DecodedPath("Pera'daki", 1.0, [lattice.Arcs[0]]),
            new DecodedPath("Ope-ra'daki", 3.0, [])
        ]);

        Assert.Equal(AcceptanceVerdict.Leave, result.Verdict);
        Assert.Contains("ProperNameRisk", result.Reasons);
    }

    [Fact]
    public void Gate_AllowsProperNameHyphenDeletion()
    {
        var lattice = Built("Strind-berg", 0, new LatticeArc(0, 11, "Strindberg", 0.1, LatticeArcKind.Word));

        var result = Gate().Evaluate(Region("Strind-berg", 0), lattice,
        [
            new DecodedPath("Strindberg", 0.1, [lattice.Arcs[0]]),
            new DecodedPath("Strind-berg", 3.0, [])
        ]);

        Assert.Equal(AcceptanceVerdict.Apply, result.Verdict);
    }

    [Fact]
    public void Gate_RejectsApostropheStemRewrite()
    {
        var lattice = Built(":lcdal'ı", 0, new LatticeArc(0, 8, "iledal'ı", 1.0, LatticeArcKind.Word));

        var result = Gate().Evaluate(Region(":lcdal'ı", 0), lattice,
        [
            new DecodedPath("iledal'ı", 1.0, [lattice.Arcs[0]]),
            new DecodedPath(":lcdal'ı", 3.0, [])
        ]);

        Assert.Equal(AcceptanceVerdict.Leave, result.Verdict);
        Assert.Contains("ProperNameRisk", result.Reasons);
    }

    [Fact]
    public void Gate_RejectsSuspiciousReplacementShape()
    {
        var lattice = Built("oldukla-rırn", 0, new LatticeArc(0, 12, "olduklarıen", 1.0, LatticeArcKind.Word));

        var result = Gate().Evaluate(Region("oldukla-rırn", 0), lattice,
        [
            new DecodedPath("olduklarıen", 1.0, [lattice.Arcs[0]]),
            new DecodedPath("oldukla-rırn", 3.0, [])
        ]);

        Assert.Equal(AcceptanceVerdict.Leave, result.Verdict);
        Assert.Contains("SuspiciousReplacement", result.Reasons);
    }

    [Fact]
    public void Gate_SpanCoversOnlyChangedText()
    {
        var lattice = Built("berjer koli ukta", 50, new LatticeArc(7, 16, "koltukta", 1.30, LatticeArcKind.Word));

        var result = Gate().Evaluate(Region("koli ukta", 57), lattice,
        [
            new DecodedPath("berjer koltukta", 1.30, [lattice.Arcs[0]]),
            new DecodedPath("berjer koli ukta", 3.0, [])
        ]);

        Assert.Equal(57, result.LogicalStart);
        Assert.Equal(66, result.LogicalEndExclusive);
        Assert.Equal("koltukta", result.Replacement);
    }

    [Fact]
    public void Gate_ReasonsAreAlwaysPopulated()
    {
        var result = Gate().Evaluate(Region("x", 0), new WordLattice("x", 0, [], LatticeBuildOutcome.SkippedTooLong, 0), []);

        Assert.NotEmpty(result.Reasons);
    }

    [Fact]
    public void Gate_DoesNotCallMorphology()
    {
        var lattice = Built("koli ukta", 0, new LatticeArc(0, 9, "koltukta", 1.30, LatticeArcKind.Word));

        var result = Gate().Evaluate(Region("koli ukta", 0), lattice,
        [
            new DecodedPath("koltukta", 1.30, [lattice.Arcs[0]]),
            new DecodedPath("koli ukta", 3.0, [])
        ]);

        Assert.Equal(AcceptanceVerdict.Apply, result.Verdict);
    }

    [Fact]
    public void Fixture_VerdictDistributionIsPinned()
    {
        var fixture = LatticeFixture.RegionText;
        var builder = LatticeFixture.CreateBuilder();
        var gate = Gate();
        var counts = new Dictionary<AcceptanceVerdict, int>
        {
            [AcceptanceVerdict.Apply] = 0,
            [AcceptanceVerdict.Review] = 0,
            [AcceptanceVerdict.Leave] = 0
        };

        foreach (var occurrence in LatticeFixture.Occurrences)
        {
            var lattice = builder.Build(RegionInText(occurrence.Source, fixture), fixture, new LatticeOptions(ContextTokens: 0));
            var decoder = new LatticeDecoder(LatticeFixture.LanguageModel, new LatticeOptions(Lambda: 0.5));
            var result = gate.Evaluate(Region(occurrence.Source, fixture.IndexOf(occurrence.Source, StringComparison.Ordinal)), lattice, decoder.Decode(lattice, 3));
            counts[result.Verdict]++;
        }

        Assert.Equal(6, counts[AcceptanceVerdict.Apply]);
        Assert.Equal(1, counts[AcceptanceVerdict.Review]);
        Assert.Equal(3, counts[AcceptanceVerdict.Leave]);
    }

    private static CorrectionAcceptanceGate Gate(IEnumerable<string>? validWords = null) =>
        new(BuildVocabulary(validWords ?? []), new LatticeOptions());

    private static BookVocabulary BuildVocabulary(IEnumerable<string> words)
    {
        var wordList = words.ToArray();
        var text = wordList.Length == 0
            ? "dolgu dolgu"
            : string.Join(' ', wordList.SelectMany(word => new[] { word, word }));
        return new BookVocabularyBuilder(
                TurkishFrequencyList.FromLines([]),
                new BookVocabularyOptions(MinBookCount: 1))
            .Build(TestStreamFactory.FromSingleSegment(text), Array.Empty<CorruptedTextRegion>(), new FakeMorphologyOracle(text.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
    }

    private static WordLattice Built(string window, int offset, params LatticeArc[] arcs) =>
        new(window, offset, arcs, LatticeBuildOutcome.Built, arcs.Length);

    private static CorruptedTextRegion Region(string raw, int start) =>
        new(raw, start, start + raw.Length, [raw], string.Empty, string.Empty, []);

    private static CorruptedTextRegion RegionInText(string raw, string fullText)
    {
        var start = fullText.IndexOf(raw, StringComparison.Ordinal);
        Assert.True(start >= 0);
        return Region(raw, start);
    }

}
