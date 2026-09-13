using EpubFixer.Adapters.Ocr.Lattice;
using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Mutation.Models;
using EpubFixer.Core.Ocr;

namespace EpubFixer.Tests;

public sealed class LatticeOcrCorrectionPlannerTests
{
    [Fact]
    public void LatticeOcrCorrectionPlanner_ProducesPlanForKnownRegion()
    {
        var stream = TestStreamFactory.FromSingleSegment(
            "Bahcede koli ukta oturuyordu. Baska koltukta kimse yoktu. Yine koltukta oturdu.");
        var frequency = TurkishFrequencyList.FromLines(
            ["koltukta 50", "bahcede 10", "oturuyordu 10", "baska 10", "kimse 10", "yoktu 10", "yine 10", "oturdu 10"]);
        var oracleBuilder = new FakeMorphologyOracleBuilder(
            ["koltukta", "bahcede", "oturuyordu", "baska", "kimse", "yoktu", "yine", "oturdu"]);
        var planner = new LatticeOcrCorrectionPlanner(frequency, vocabulary => new SymSpellLexiconMatcher(vocabulary));

        var result = planner.CreatePlan(stream, oracleBuilder);

        Assert.Equal(OcrCorrectionEngine.Lattice, result.Engine);
        Assert.True(result.Plan.IsValid);
        var mutation = Assert.Single(result.Plan.Mutations);
        Assert.Equal("koltukta", mutation.ReplacementText);
        Assert.Equal(OcrCorrectionEngine.Lattice, mutation.Provenance.Engine);
        Assert.Null(mutation.Confidence);
    }

    [Fact]
    public void LatticeOcrCorrectionPlanner_DoesNotIncludeReviewVerdicts()
    {
        // A region with two plausible, close corrections (an ambiguous seam) should
        // never reach the mutation plan even if the gate marks it Review instead of
        // Apply - Review decisions are audit-only (plan section 8.2).
        var stream = TestStreamFactory.FromSingleSegment(
            "Once ic dedi. Sonra uc dedi. Once uc dedi. Sonra ic dedi.");
        var frequency = TurkishFrequencyList.FromLines(["uc 10", "ic 10", "once 10", "sonra 10", "dedi 10"]);
        var oracleBuilder = new FakeMorphologyOracleBuilder(["uc", "ic", "once", "sonra", "dedi"]);
        var planner = new LatticeOcrCorrectionPlanner(frequency, vocabulary => new SymSpellLexiconMatcher(vocabulary));

        var result = planner.CreatePlan(stream, oracleBuilder);

        Assert.True(result.Plan.IsValid);
        Assert.Empty(result.Plan.Failures);
    }

    [Fact]
    public void LatticeOcrCorrectionPlanner_IsDeterministic()
    {
        var stream = TestStreamFactory.FromSingleSegment(
            "Bahcede koli ukta oturuyordu. Baska koltukta kimse yoktu. Yine koltukta oturdu.");
        var frequency = TurkishFrequencyList.FromLines(
            ["koltukta 50", "bahcede 10", "oturuyordu 10", "baska 10", "kimse 10", "yoktu 10", "yine 10", "oturdu 10"]);

        var first = new LatticeOcrCorrectionPlanner(frequency, vocabulary => new SymSpellLexiconMatcher(vocabulary))
            .CreatePlan(stream, new FakeMorphologyOracleBuilder(
                ["koltukta", "bahcede", "oturuyordu", "baska", "kimse", "yoktu", "yine", "oturdu"]));
        var second = new LatticeOcrCorrectionPlanner(frequency, vocabulary => new SymSpellLexiconMatcher(vocabulary))
            .CreatePlan(stream, new FakeMorphologyOracleBuilder(
                ["koltukta", "bahcede", "oturuyordu", "baska", "kimse", "yoktu", "yine", "oturdu"]));

        Assert.Equal(
            first.Plan.Mutations.Select(Signature),
            second.Plan.Mutations.Select(Signature));
    }

    private static (string DocumentPath, int LogicalStart, int LogicalLength, string ReplacementText) Signature(
        OcrCorrectionMutation mutation) =>
        (mutation.DocumentPath, mutation.LogicalStart, mutation.LogicalLength, mutation.ReplacementText);
}
