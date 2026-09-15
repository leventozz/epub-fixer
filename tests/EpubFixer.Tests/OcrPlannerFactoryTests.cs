using EpubFixer.Adapters.Ocr;
using EpubFixer.Core.Ocr;

namespace EpubFixer.Tests;

public sealed class OcrPlannerFactoryTests
{
    [Fact]
    public void Resolve_Legacy_ReturnsNullSoCallerFallsBackToItsOwnDefault()
    {
        Assert.Null(OcrPlannerFactory.Resolve("legacy"));
        Assert.Null(OcrPlannerFactory.Resolve("LEGACY"));
    }

    [Fact]
    public void Resolve_Lattice_ReturnsLatticePlanner()
    {
        var planner = OcrPlannerFactory.Resolve("lattice");

        Assert.IsType<LatticeOcrCorrectionPlanner>(planner);
    }

    [Fact]
    public void Resolve_Hybrid_ReturnsCompositePlanner()
    {
        var planner = OcrPlannerFactory.Resolve("HYBRID");

        Assert.IsType<CompositeOcrCorrectionPlanner>(planner);
    }

    [Fact]
    public void Resolve_UnknownEngine_ThrowsRatherThanFallingBackToLegacy()
    {
        Assert.Throws<ArgumentException>(() => OcrPlannerFactory.Resolve("not-a-real-engine"));
    }

    [Theory]
    [InlineData("legacy", true)]
    [InlineData("LATTICE", true)]
    [InlineData("hybrid", true)]
    [InlineData("unknown", false)]
    public void IsKnownEngine_MatchesResolvableNames(string engine, bool expected)
    {
        Assert.Equal(expected, OcrPlannerFactory.IsKnownEngine(engine));
    }
}
