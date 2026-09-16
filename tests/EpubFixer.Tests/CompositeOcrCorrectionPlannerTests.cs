using EpubFixer.Core.Morphology;
using EpubFixer.Core.Mutation.Models;
using EpubFixer.Core.Ocr;
using EpubFixer.Core.Epub.Models;

namespace EpubFixer.Tests;

public sealed class CompositeOcrCorrectionPlannerTests
{
    private const string SnapshotText = "snapshot";

    [Fact]
    public void CreatePlan_NoOverlap_MergesBothPlansMutationCount()
    {
        var primary = StubPlanner(OcrCorrectionEngine.Legacy, Mutation("a.xhtml", 0, 3, "legacy"));
        var secondary = StubPlanner(OcrCorrectionEngine.Lattice, Mutation("a.xhtml", 10, 3, "lattice"));
        var composite = new CompositeOcrCorrectionPlanner(primary, secondary);

        var result = composite.CreatePlan(TestStreamFactory.FromSingleSegment(SnapshotText), new FakeMorphologyOracleBuilder());

        Assert.Equal(2, result.Plan.Mutations.Count);
        Assert.Equal(OcrCorrectionEngine.Hybrid, result.Engine);
    }

    [Fact]
    public void CreatePlan_FullOverlap_SkipsSecondaryMutationAndRecordsDiagnostic()
    {
        var primary = StubPlanner(OcrCorrectionEngine.Legacy, Mutation("a.xhtml", 5, 4, "legacy"));
        var secondary = StubPlanner(OcrCorrectionEngine.Lattice, Mutation("a.xhtml", 5, 4, "lattice"));
        var composite = new CompositeOcrCorrectionPlanner(primary, secondary);

        var result = composite.CreatePlan(TestStreamFactory.FromSingleSegment(SnapshotText), new FakeMorphologyOracleBuilder());

        var mutation = Assert.Single(result.Plan.Mutations);
        Assert.Equal("legacy", mutation.ReplacementText);
        Assert.Contains(result.Diagnostics, d => d.Contains("skipped secondary mutation"));
    }

    [Fact]
    public void CreatePlan_PartialOverlapFromEdge_SkipsSecondaryMutation()
    {
        // Primary covers [5, 9). Secondary covers [8, 12) - overlaps by one character at the tail.
        var primary = StubPlanner(OcrCorrectionEngine.Legacy, Mutation("a.xhtml", 5, 4, "legacy"));
        var secondary = StubPlanner(OcrCorrectionEngine.Lattice, Mutation("a.xhtml", 8, 4, "lattice"));
        var composite = new CompositeOcrCorrectionPlanner(primary, secondary);

        var result = composite.CreatePlan(TestStreamFactory.FromSingleSegment(SnapshotText), new FakeMorphologyOracleBuilder());

        var mutation = Assert.Single(result.Plan.Mutations);
        Assert.Equal("legacy", mutation.ReplacementText);
        Assert.Contains(result.Diagnostics, d => d.Contains("skipped secondary mutation"));
    }

    [Fact]
    public void CreatePlan_SameOffsetDifferentDocument_IsNotAConflict()
    {
        var primary = StubPlanner(OcrCorrectionEngine.Legacy, Mutation("a.xhtml", 5, 4, "legacy"));
        var secondary = StubPlanner(OcrCorrectionEngine.Lattice, Mutation("b.xhtml", 5, 4, "lattice"));
        var composite = new CompositeOcrCorrectionPlanner(primary, secondary);

        var result = composite.CreatePlan(TestStreamFactory.FromSingleSegment(SnapshotText), new FakeMorphologyOracleBuilder());

        Assert.Equal(2, result.Plan.Mutations.Count);
        Assert.DoesNotContain(result.Diagnostics, d => d.Contains("skipped secondary mutation"));
    }

    [Fact]
    public void CreatePlan_EmptySecondaryPlan_ReturnsPrimaryUnchanged()
    {
        var primaryMutation = Mutation("a.xhtml", 0, 3, "legacy");
        var primary = StubPlanner(OcrCorrectionEngine.Legacy, primaryMutation);
        var secondary = StubPlanner(OcrCorrectionEngine.Lattice);
        var composite = new CompositeOcrCorrectionPlanner(primary, secondary);

        var result = composite.CreatePlan(TestStreamFactory.FromSingleSegment(SnapshotText), new FakeMorphologyOracleBuilder());

        var mutation = Assert.Single(result.Plan.Mutations);
        Assert.Equal(primaryMutation, mutation);
    }

    [Fact]
    public void CreatePlan_OutputIsOrderedByDocumentThenLogicalStart()
    {
        var primary = StubPlanner(
            OcrCorrectionEngine.Legacy,
            Mutation("b.xhtml", 20, 1, "legacy-b"),
            Mutation("a.xhtml", 30, 1, "legacy-a-late"));
        var secondary = StubPlanner(
            OcrCorrectionEngine.Lattice,
            Mutation("a.xhtml", 10, 1, "lattice-a-early"));
        var composite = new CompositeOcrCorrectionPlanner(primary, secondary);

        var result = composite.CreatePlan(TestStreamFactory.FromSingleSegment(SnapshotText), new FakeMorphologyOracleBuilder());

        Assert.Equal(
            ["lattice-a-early", "legacy-a-late", "legacy-b"],
            result.Plan.Mutations.Select(m => m.ReplacementText));
    }

    [Fact]
    public void CreatePlan_SecondaryFailure_DoesNotInvalidatePlanAndIsRecordedInDiagnostics()
    {
        var primary = StubPlanner(OcrCorrectionEngine.Legacy, Mutation("a.xhtml", 0, 3, "legacy"));
        var secondary = StubPlanner(
            OcrCorrectionEngine.Lattice,
            mutations: [],
            failures: [new OcrMutationFailure(OcrMutationFailureReason.SourceTextMismatch, "lattice blew up")]);
        var composite = new CompositeOcrCorrectionPlanner(primary, secondary);

        var result = composite.CreatePlan(TestStreamFactory.FromSingleSegment(SnapshotText), new FakeMorphologyOracleBuilder());

        Assert.True(result.Plan.IsValid);
        Assert.Contains(result.Diagnostics, d => d.Contains("lattice blew up"));
    }

    private static OcrCorrectionMutation Mutation(string documentPath, int logicalStart, int logicalLength, string replacementText)
    {
        return new OcrCorrectionMutation(
            documentPath,
            logicalStart,
            logicalLength,
            OriginalSourceText: new string('x', logicalLength),
            ReplacementText: replacementText,
            Provenance: new OcrMutationProvenance(OcrCorrectionEngine.Legacy, "test-rule", Confidence: null, ConsumesMultipleSources: false),
            SourceSpans: []);
    }

    private static StubOcrCorrectionPlanner StubPlanner(
        OcrCorrectionEngine engine,
        params OcrCorrectionMutation[] mutations)
    {
        return new StubOcrCorrectionPlanner(engine, mutations, failures: []);
    }

    private static StubOcrCorrectionPlanner StubPlanner(
        OcrCorrectionEngine engine,
        IReadOnlyList<OcrCorrectionMutation> mutations,
        IReadOnlyList<OcrMutationFailure> failures)
    {
        return new StubOcrCorrectionPlanner(engine, mutations, failures);
    }

    private sealed class StubOcrCorrectionPlanner(
        OcrCorrectionEngine engine,
        IReadOnlyList<OcrCorrectionMutation> mutations,
        IReadOnlyList<OcrMutationFailure> failures) : IOcrCorrectionPlanner
    {
        public OcrCorrectionPlanResult CreatePlan(LogicalTextStream stream, IMorphologyOracleBuilder oracleBuilder)
        {
            return new OcrCorrectionPlanResult(
                new OcrCorrectionMutationPlan(stream.Text, mutations, failures),
                engine,
                Diagnostics: []);
        }
    }
}
