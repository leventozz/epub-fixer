using EpubFixer.Core.Correction;
using EpubFixer.Core.Correction.Models;
using EpubFixer.Core.Decision.Models;
using EpubFixer.Core.Detection;
using EpubFixer.Core.Epub;
using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Evidence.Models;

namespace EpubFixer.Tests;

public sealed class HyphenationCorrectionApplierTests
{
    [Fact]
    public void Apply_RemovesOnlyTheTargetInlineHyphen()
    {
        using var epub = CreateSingleDocumentEpub("<p>Auersber-ger</p>");
        var package = new EpubPackageReader().Read(epub.Path);
        var plan = Assert.Single(CreatePlans(package.LogicalText));

        var result = new HyphenationCorrectionApplier().Apply([plan]);

        Assert.Equal(new HyphenationCorrectionApplyResult(1, 0), result);
        Assert.Equal("Auersberger", plan.HyphenSource.SourceNode.Data);
    }

    [Fact]
    public void Apply_ProcessesMultipleEditsInOneNodeFromHighestOffsetToLowest()
    {
        using var epub = CreateSingleDocumentEpub("<p>Auersber-ger ... Jo-ana</p>");
        var package = new EpubPackageReader().Read(epub.Path);
        var plans = CreatePlans(package.LogicalText);

        Assert.Equal(2, plans.Count);
        Assert.Same(plans[0].HyphenSource.SourceNode, plans[1].HyphenSource.SourceNode);
        Assert.True(plans[0].HyphenSource.Start < plans[1].HyphenSource.Start);

        var result = new HyphenationCorrectionApplier().Apply(plans);

        Assert.Equal(new HyphenationCorrectionApplyResult(2, 0), result);
        Assert.Equal("Auersberger ... Joana", plans[0].HyphenSource.SourceNode.Data);
    }

    [Fact]
    public void Apply_DoesNotGloballyReplaceMatchingText()
    {
        using var epub = CreateSingleDocumentEpub(
            "<p>Auersber-ger ... Auersber-ger</p>");
        var package = new EpubPackageReader().Read(epub.Path);
        var plans = CreatePlans(package.LogicalText);

        var result = new HyphenationCorrectionApplier().Apply([plans[0]]);

        Assert.Equal(new HyphenationCorrectionApplyResult(1, 0), result);
        Assert.Equal(
            "Auersberger ... Auersber-ger",
            plans[0].HyphenSource.SourceNode.Data);
    }

    [Fact]
    public void Apply_SkipsCrossParagraphPlanWithoutMutatingEitherNode()
    {
        using var epub = CreateSingleDocumentEpub("<p>kol-</p><p>tukta</p>");
        var package = new EpubPackageReader().Read(epub.Path);
        var plan = Assert.Single(CreatePlans(package.LogicalText));
        var before = package.LogicalText.Segments
            .Select(segment => segment.Source.SourceNode.Data)
            .ToArray();

        var result = new HyphenationCorrectionApplier().Apply([plan]);

        Assert.Equal(HyphenationCorrectionKind.CrossParagraph, plan.CorrectionKind);
        Assert.Equal(new HyphenationCorrectionApplyResult(0, 1), result);
        Assert.Equal(
            before,
            package.LogicalText.Segments.Select(segment => segment.Source.SourceNode.Data));
    }

    [Fact]
    public void Apply_SkipsTextNodePlanWithoutMutatingEitherNode()
    {
        using var epub = CreateSingleDocumentEpub(
            "<p><span>Viya-</span><span>na</span></p>");
        var package = new EpubPackageReader().Read(epub.Path);
        var plan = Assert.Single(CreatePlans(package.LogicalText));
        var before = package.LogicalText.Segments
            .Select(segment => segment.Source.SourceNode.Data)
            .ToArray();

        var result = new HyphenationCorrectionApplier().Apply([plan]);

        Assert.Equal(HyphenationCorrectionKind.TextNode, plan.CorrectionKind);
        Assert.Equal(new HyphenationCorrectionApplyResult(0, 1), result);
        Assert.Equal(
            before,
            package.LogicalText.Segments.Select(segment => segment.Source.SourceNode.Data));
    }

    [Fact]
    public void Apply_DoesNotChangeContentOutsideTheSourceNode()
    {
        using var epub = CreateSingleDocumentEpub(
            "<p id=\"target\">Auersber-ger</p><p id=\"other\">Keep me intact</p>");
        var package = new EpubPackageReader().Read(epub.Path);
        var plan = Assert.Single(CreatePlans(package.LogicalText));
        var other = package.SpineDocuments[0].Document.QuerySelector("#other")!;
        var otherMarkup = other.OuterHtml;

        _ = new HyphenationCorrectionApplier().Apply([plan]);

        Assert.Equal("Auersberger", plan.HyphenSource.SourceNode.Data);
        Assert.Equal(otherMarkup, other.OuterHtml);
    }

    [Fact]
    public void Apply_LeavesOldStreamStaleAndRebuildReflectsTheWorkingDom()
    {
        using var epub = CreateSingleDocumentEpub("<p>Auersber-ger</p>");
        var package = new EpubPackageReader().Read(epub.Path);
        var oldStream = package.LogicalText;
        var plan = Assert.Single(CreatePlans(oldStream));

        _ = new HyphenationCorrectionApplier().Apply([plan]);
        var rebuiltStream = LogicalTextStreamBuilder.Build(package.SpineDocuments);

        Assert.Equal("Auersber-ger", oldStream.Text);
        Assert.Equal("Auersber-ger", Assert.Single(oldStream.Segments).Text);
        Assert.Equal("Auersberger", rebuiltStream.Text);
        Assert.Empty(new HyphenationDetector().Detect(rebuiltStream));
    }

    [Fact]
    public void Apply_SkipsStaleAndDuplicateSourceRanges()
    {
        using var epub = CreateSingleDocumentEpub(
            "<p>Auersber-ger ... Jo-ana</p>");
        var package = new EpubPackageReader().Read(epub.Path);
        var plans = CreatePlans(package.LogicalText);
        var stalePlan = plans[1] with
        {
            HyphenSource = plans[1].HyphenSource with { Start = 0 }
        };

        var result = new HyphenationCorrectionApplier().Apply(
            [plans[0], plans[0], stalePlan]);

        Assert.Equal(new HyphenationCorrectionApplyResult(1, 2), result);
        Assert.Equal("Auersberger ... Jo-ana", plans[0].HyphenSource.SourceNode.Data);
    }

    [Fact]
    public void Apply_RejectsNullInput()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new HyphenationCorrectionApplier().Apply(null!));
    }

    private static IReadOnlyList<HyphenationCorrectionPlan> CreatePlans(
        LogicalTextStream stream)
    {
        var candidates = new HyphenationDetector().Detect(stream);
        var decisions = candidates
            .Select(candidate => new HyphenationDecision(
                new HyphenationEvidence(
                    candidate,
                    10,
                    true,
                    new HyphenationContextEvidence(null, null, false, false)),
                HyphenationDecisionKind.AutoFixCandidate))
            .ToArray();

        return new HyphenationCorrectionPlanner().Plan(decisions);
    }

    private static TemporaryEpub CreateSingleDocumentEpub(string body)
    {
        return TemporaryEpub.Create(
            [new TestDocument("chapter", "chapter.xhtml", Xhtml(body))],
            [new TestSpineItem("chapter")]);
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
