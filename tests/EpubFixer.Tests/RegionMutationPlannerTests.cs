using EpubFixer.Core.Decision;
using EpubFixer.Core.Decision.Models;
using EpubFixer.Core.Epub;
using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Mutation;
using EpubFixer.Core.Mutation.Models;
using EpubFixer.Core.Ocr.Lattice.Models;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Tests;

public sealed class RegionMutationPlannerTests
{
    [Fact]
    public void Create_SingleNodeRange_ProducesOneSpan()
    {
        var stream = CreateStream("<p>hello world</p>");
        var start = stream.Text.IndexOf("world", StringComparison.Ordinal);
        var correction = new RegionCorrection(start, start + 5, "earth", Accept("single-node"));

        var result = new RegionMutationPlanner().Create([correction], stream);

        Assert.Empty(result.Skipped);
        var mutation = Assert.Single(result.Plan.Mutations);
        var span = Assert.Single(mutation.SourceSpans);
        Assert.Equal("world", span.ExpectedText);
        Assert.Equal("earth", mutation.ReplacementText);
        Assert.Equal("world", mutation.OriginalSourceText);
    }

    [Fact]
    public void Create_RangeAcrossTwoTextNodes_ProducesTwoSpansWithExpectedText()
    {
        // "foo" + "bar" + "baz" are three sibling text nodes (split by the <b>).
        var stream = CreateStream("<p>foo<b>bar</b>baz</p>");
        Assert.Equal("foobarbaz", stream.Text);
        var correction = new RegionCorrection(1, 8, "XYZ123", Accept("multi-node"));

        var result = new RegionMutationPlanner().Create([correction], stream);

        Assert.Empty(result.Skipped);
        var mutation = Assert.Single(result.Plan.Mutations);
        Assert.Equal(3, mutation.SourceSpans.Count);
        Assert.Equal("oo", mutation.SourceSpans[0].ExpectedText);
        Assert.Equal("bar", mutation.SourceSpans[1].ExpectedText);
        Assert.Equal("ba", mutation.SourceSpans[2].ExpectedText);
    }

    [Fact]
    public void Create_ReplacementGoesToFirstSpan_RestAreEmptied()
    {
        var package = CreatePackage("<p>foo<b>bar</b>baz</p>", out var stream);
        var correction = new RegionCorrection(1, 8, "XYZ", Accept("multi-node"));
        var plan = new RegionMutationPlanner().Create([correction], stream).Plan;

        var applyResult = new OcrCorrectionMutationApplier().Apply(package, plan);

        Assert.True(applyResult.Succeeded);
        var textNodes = QuerySelectorAllTextNodes(package).ToArray();
        Assert.Equal("fXYZ", textNodes[0].Data);
        Assert.Equal("", textNodes[1].Data);
        Assert.Equal("z", textNodes[2].Data);
    }

    [Fact]
    public void Create_JoinCorrection_RemovesInteriorSpace()
    {
        var stream = CreateStream("<p>koli ukta</p>");

        var correction = new RegionCorrection(0, stream.Text.Length, "koltukta", Accept("join"));
        var result = new RegionMutationPlanner().Create([correction], stream);

        Assert.Empty(result.Skipped);
        var mutation = Assert.Single(result.Plan.Mutations);
        Assert.Equal("koli ukta", mutation.OriginalSourceText);
        Assert.Equal("koltukta", mutation.ReplacementText);
    }

    [Fact]
    public void Create_SplitCorrection_AddsSpace()
    {
        var stream = CreateStream("<p>birsey</p>");

        var correction = new RegionCorrection(0, stream.Text.Length, "bir sey", Accept("split"));
        var result = new RegionMutationPlanner().Create([correction], stream);

        Assert.Empty(result.Skipped);
        var mutation = Assert.Single(result.Plan.Mutations);
        Assert.Equal("birsey", mutation.OriginalSourceText);
        Assert.Equal("bir sey", mutation.ReplacementText);
    }

    [Fact]
    public void Create_OverlappingCorrections_KeepsFirstAndSkipsSecond()
    {
        var stream = CreateStream("<p>hello world</p>");
        var first = new RegionCorrection(0, 5, "HELLO", Accept("first"));
        var second = new RegionCorrection(3, 8, "XX", Accept("second"));

        var result = new RegionMutationPlanner().Create([first, second], stream);

        var mutation = Assert.Single(result.Plan.Mutations);
        Assert.Equal("HELLO", mutation.ReplacementText);
        var skipped = Assert.Single(result.Skipped);
        Assert.Equal(RegionCorrectionSkipReason.Overlapping, skipped.Reason);
        Assert.Same(second, skipped.Correction);
    }

    [Fact]
    public void Create_NoChangeCorrection_IsSkipped()
    {
        var stream = CreateStream("<p>hello</p>");
        var correction = new RegionCorrection(0, 5, "hello", Accept("no-change"));

        var result = new RegionMutationPlanner().Create([correction], stream);

        Assert.Empty(result.Plan.Mutations);
        var skipped = Assert.Single(result.Skipped);
        Assert.Equal(RegionCorrectionSkipReason.NoChange, skipped.Reason);
    }

    [Theory]
    [InlineData(-1, 4)]
    [InlineData(0, 0)]
    [InlineData(0, 1000)]
    public void Create_OutOfRangeCorrection_IsSkipped(int start, int endExclusive)
    {
        var stream = CreateStream("<p>hello</p>");
        var correction = new RegionCorrection(start, endExclusive, "x", Accept("out-of-range"));

        var result = new RegionMutationPlanner().Create([correction], stream);

        Assert.Empty(result.Plan.Mutations);
        var skipped = Assert.Single(result.Skipped);
        Assert.Equal(RegionCorrectionSkipReason.OutOfRange, skipped.Reason);
    }

    [Fact]
    public void Create_PlanIsAlwaysValid()
    {
        var stream = CreateStream("<p>hello world</p>");
        var corrections = new[]
        {
            new RegionCorrection(0, 5, "HELLO", Accept("ok")),
            new RegionCorrection(0, 5, "HELLO", Accept("no-change-after-dup")) with { Replacement = "hello" },
            new RegionCorrection(-5, -1, "bad", Accept("out-of-range")),
            new RegionCorrection(3, 8, "conflict", Accept("overlap")),
        };

        var result = new RegionMutationPlanner().Create(corrections, stream);

        Assert.True(result.Plan.IsValid);
        Assert.Empty(result.Plan.Failures);
        Assert.NotEmpty(result.Skipped);
    }

    [Fact]
    public void Create_IsDeterministic()
    {
        var stream = CreateStream("<p>hello world</p>");
        var corrections = new[]
        {
            new RegionCorrection(6, 11, "earth", Accept("a")),
            new RegionCorrection(0, 5, "HELLO", Accept("b")),
        };

        var first = new RegionMutationPlanner().Create(corrections, stream);
        var second = new RegionMutationPlanner().Create(corrections, stream);

        Assert.Equal(first.Plan.Mutations.Select(Signature), second.Plan.Mutations.Select(Signature));
        Assert.Equal(first.Skipped.Select(item => item.Reason), second.Skipped.Select(item => item.Reason));
    }

    [Fact]
    public void Create_ProvenanceCarriesAcceptanceReasons()
    {
        var stream = CreateStream("<p>koli ukta</p>");
        var correction = new RegionCorrection(
            0,
            stream.Text.Length,
            "koltukta",
            new AcceptanceResult(AcceptanceVerdict.Apply, "koltukta", 0, stream.Text.Length, ["FrequencyDominant", "OriginalTokenIsValid"]));

        var result = new RegionMutationPlanner().Create([correction], stream);

        var mutation = Assert.Single(result.Plan.Mutations);
        Assert.Equal("FrequencyDominant; OriginalTokenIsValid", mutation.DecisionRule);
        Assert.Equal(OcrCorrectionEngine.Lattice, mutation.Provenance.Engine);
        Assert.Null(mutation.Confidence);
        Assert.True(mutation.IsMultiSource);
    }

    [Fact]
    public void Apply_RoundTripsThroughExistingApplier()
    {
        var package = CreatePackage("<p>koli ukta</p>", out var stream);
        var correction = new RegionCorrection(0, stream.Text.Length, "koltukta", Accept("join"));
        var plan = new RegionMutationPlanner().Create([correction], stream).Plan;

        var applyResult = new OcrCorrectionMutationApplier().Apply(package, plan);

        Assert.True(applyResult.Succeeded);
        var rebuilt = LogicalTextStreamBuilder.Build(package.SpineDocuments);
        Assert.Equal("koltukta", rebuilt.Text);
    }

    [Fact]
    public void LegacyPlanner_ProvenanceMatchesPreviousDecisionFields()
    {
        var stream = LogicalTextStream.FromPlainText("liyatro", "test.xhtml");
        var candidate = new OcrWordCandidate("liyatro", 0, [], "test.xhtml", "", "");
        var evidence = new OcrWordEvidence(candidate, 1, "liyatro", 2, false,
            [OcrDetectionReason.MorphologyInvalid, OcrDetectionReason.RareInBook], OcrConfidence.EvidenceOnly);
        var proposal = new OcrCorrectionCandidate(
            new OcrWordCandidate("source", 0, [], "test.xhtml", "", ""),
            OcrConfidence.EvidenceOnly,
            "tiyatro",
            [OcrCorrectionGenerationReason.BookLexiconNeighbor],
            1,
            1,
            true,
            37,
            1,
            [new OcrWordCandidate("source", 0, [], "test.xhtml", "", "")],
            0,
            false);
        var occurrence = new OcrCorrectionOccurrence(evidence, candidate, "", "liyatro", "", [proposal]);
        var analysis = new OcrCorrectionAnalysisReport(
            new OcrAnalysisReport(1, 1, 0, [occurrence.Source], [], 0),
            [occurrence],
            []);
        var decision = Assert.Single(new OcrCorrectionDecisionEvaluator().Evaluate(analysis).Decisions);
        Assert.Equal(OcrCorrectionDecisionKind.AutoFixCandidate, decision.DecisionKind);

        var plan = new OcrCorrectionMutationPlanner().Create([decision], stream);

        var mutation = Assert.Single(plan.Mutations);
        Assert.Equal(decision.DecisionReasons.FirstOrDefault().ToString(), mutation.DecisionRule);
        Assert.Equal(decision.SourceOccurrence.Source.Confidence, mutation.Confidence);
        Assert.Equal(decision.SelectedProposal?.Proposal.ConsumesMultipleOccurrences == true, mutation.IsMultiSource);
        Assert.Equal(OcrCorrectionEngine.Legacy, mutation.Provenance.Engine);
    }

    private static (string DocumentPath, int LogicalStart, int LogicalLength, string OriginalSourceText, string ReplacementText, string Spans) Signature(
        OcrCorrectionMutation mutation) =>
        (
            mutation.DocumentPath,
            mutation.LogicalStart,
            mutation.LogicalLength,
            mutation.OriginalSourceText,
            mutation.ReplacementText,
            string.Join("|", mutation.SourceSpans.Select(span => $"{span.DocumentPath}:{span.TextNodeIndex}:{span.Start}:{span.Length}:{span.ExpectedText}")));

    private static AcceptanceResult Accept(string reason) =>
        new(AcceptanceVerdict.Apply, null, 0, 0, [reason]);

    private static LogicalTextStream CreateStream(string body)
    {
        using var epub = TemporaryEpub.Create(
            [new TestDocument("chapter", "chapter.xhtml", Xhtml(body))],
            [new TestSpineItem("chapter")]);
        return new EpubPackageReader().Read(epub.Path).LogicalText;
    }

    private static EpubPackage CreatePackage(string body, out LogicalTextStream stream)
    {
        using var epub = TemporaryEpub.Create(
            [new TestDocument("chapter", "chapter.xhtml", Xhtml(body))],
            [new TestSpineItem("chapter")]);
        var package = new EpubPackageReader().Read(epub.Path);
        stream = package.LogicalText;
        return package;
    }

    private static IEnumerable<AngleSharp.Dom.IText> QuerySelectorAllTextNodes(EpubPackage package)
    {
        return EnumerateTextNodes(package.SpineDocuments[0].Document.Body!);
    }

    private static IEnumerable<AngleSharp.Dom.IText> EnumerateTextNodes(AngleSharp.Dom.INode node)
    {
        foreach (var child in node.ChildNodes)
        {
            if (child is AngleSharp.Dom.IText text)
            {
                yield return text;
            }
            foreach (var descendant in EnumerateTextNodes(child))
            {
                yield return descendant;
            }
        }
    }

    private static string Xhtml(string body)
    {
        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <!DOCTYPE html>
            <html xmlns="http://www.w3.org/1999/xhtml">
              <head><title>Test</title></head>
              <body>{body}</body>
            </html>
            """;
    }
}
