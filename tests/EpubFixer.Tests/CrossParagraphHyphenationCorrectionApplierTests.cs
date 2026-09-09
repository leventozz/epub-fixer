using AngleSharp.Dom;
using EpubFixer.Core.Correction;
using EpubFixer.Core.Correction.Models;
using EpubFixer.Core.Decision.Models;
using EpubFixer.Core.Detection;
using EpubFixer.Core.Epub;
using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Evidence.Models;

namespace EpubFixer.Tests;

public sealed class CrossParagraphHyphenationCorrectionApplierTests
{
    [Fact]
    public void Apply_MergesParagraphsAndRemovesInterveningWhitespace()
    {
        using var epub = CreateSingleDocumentEpub(
            "<p>Before</p><p class=\"body\">kol-</p>\n  "
            + "<p class=\"body\">tukta, devam...</p><p>After</p>");
        var package = new EpubPackageReader().Read(epub.Path);
        var plan = Assert.Single(CreatePlans(package.LogicalText));
        var paragraphs = package.SpineDocuments[0].Document.QuerySelectorAll("p");
        var before = paragraphs[0];
        var left = paragraphs[1];
        var whitespace = Assert.IsAssignableFrom<IText>(left.NextSibling);
        var right = paragraphs[2];
        var after = paragraphs[3];
        var beforeMarkup = before.OuterHtml;
        var afterMarkup = after.OuterHtml;

        var result = new CrossParagraphHyphenationCorrectionApplier().Apply([plan]);

        Assert.Equal(new HyphenationCorrectionApplyResult(1, 0), result);
        Assert.Equal("koltukta, devam...", left.TextContent);
        Assert.Null(right.Parent);
        Assert.Null(whitespace.Parent);
        Assert.Equal(beforeMarkup, before.OuterHtml);
        Assert.Equal(afterMarkup, after.OuterHtml);
        Assert.Same(after, left.NextElementSibling);
    }

    [Fact]
    public void Apply_MovesExistingInlineMarkupWithoutRecreatingNodes()
    {
        using var epub = CreateSingleDocumentEpub(
            "<p><strong>kol-</strong></p>\n<p><em>tukta</em><span>, devam</span></p>");
        var package = new EpubPackageReader().Read(epub.Path);
        var plan = Assert.Single(CreatePlans(package.LogicalText));
        var document = package.SpineDocuments[0].Document;
        var left = document.QuerySelector("p")!;
        var right = left.NextElementSibling!;
        var emphasis = right.QuerySelector("em")!;
        var continuation = right.QuerySelector("span")!;

        var result = new CrossParagraphHyphenationCorrectionApplier().Apply([plan]);

        Assert.Equal(new HyphenationCorrectionApplyResult(1, 0), result);
        Assert.Same(emphasis, left.QuerySelector("em"));
        Assert.Same(continuation, left.QuerySelector("span"));
        Assert.Equal("<strong>kol</strong><em>tukta</em><span>, devam</span>", left.InnerHtml);
        Assert.Null(right.Parent);
    }

    [Theory]
    [InlineData("<div>kol-</div>\n<div>tukta</div>")]
    [InlineData("<section><p>kol-</p></section>\n<section><p>tukta</p></section>")]
    [InlineData("<p>kol-</p><hr />\n<p>tukta</p>")]
    [InlineData("<p>kol-</p><!-- separator -->\n<p>tukta</p>")]
    [InlineData("<p class=\"left\">kol-</p>\n<p class=\"right\">tukta</p>")]
    public void Apply_SkipsWhenParagraphSafetyConditionIsBroken(string body)
    {
        using var epub = CreateSingleDocumentEpub(body);
        var package = new EpubPackageReader().Read(epub.Path);
        var plan = Assert.Single(CreatePlans(package.LogicalText));
        var before = package.SpineDocuments[0].Document.Body!.InnerHtml;

        var result = new CrossParagraphHyphenationCorrectionApplier().Apply([plan]);

        Assert.Equal(new HyphenationCorrectionApplyResult(0, 1), result);
        Assert.Equal(before, package.SpineDocuments[0].Document.Body!.InnerHtml);
    }

    [Fact]
    public void Apply_SkipsWhenLeftSourceIsNoLongerLastTextPosition()
    {
        using var epub = CreateSingleDocumentEpub("<p>kol-</p>\n<p>tukta</p>");
        var package = new EpubPackageReader().Read(epub.Path);
        var plan = Assert.Single(CreatePlans(package.LogicalText));
        var document = package.SpineDocuments[0].Document;
        var left = document.QuerySelector("p")!;
        left.AppendChild(document.CreateTextNode("stale"));
        var before = document.Body!.InnerHtml;

        var result = new CrossParagraphHyphenationCorrectionApplier().Apply([plan]);

        Assert.Equal(new HyphenationCorrectionApplyResult(0, 1), result);
        Assert.Equal(before, document.Body.InnerHtml);
    }

    [Fact]
    public void Apply_SkipsWhenRightSourceIsNoLongerFirstTextPosition()
    {
        using var epub = CreateSingleDocumentEpub("<p>kol-</p>\n<p>tukta</p>");
        var package = new EpubPackageReader().Read(epub.Path);
        var plan = Assert.Single(CreatePlans(package.LogicalText));
        var document = package.SpineDocuments[0].Document;
        var right = document.QuerySelectorAll("p")[1];
        right.InsertBefore(document.CreateTextNode("stale"), right.FirstChild);
        var before = document.Body!.InnerHtml;

        var result = new CrossParagraphHyphenationCorrectionApplier().Apply([plan]);

        Assert.Equal(new HyphenationCorrectionApplyResult(0, 1), result);
        Assert.Equal(before, document.Body.InnerHtml);
    }

    [Fact]
    public void Apply_SkipsStaleSourceRangeWithoutMutation()
    {
        using var epub = CreateSingleDocumentEpub("<p>kol-</p>\n<p>tukta</p>");
        var package = new EpubPackageReader().Read(epub.Path);
        var plan = Assert.Single(CreatePlans(package.LogicalText));
        var stalePlan = plan with
        {
            HyphenSource = plan.HyphenSource with { Start = 0 }
        };
        var before = package.SpineDocuments[0].Document.Body!.InnerHtml;

        var result = new CrossParagraphHyphenationCorrectionApplier().Apply([stalePlan]);

        Assert.Equal(new HyphenationCorrectionApplyResult(0, 1), result);
        Assert.Equal(before, package.SpineDocuments[0].Document.Body!.InnerHtml);
    }

    [Fact]
    public void Apply_SkipsTextBeforeRightSourceThatWouldPreventDirectJoining()
    {
        using var epub = CreateSingleDocumentEpub(
            "<p>kol-</p>\n<p><span> </span><em>tukta</em></p>");
        var package = new EpubPackageReader().Read(epub.Path);
        var plan = Assert.Single(CreatePlans(package.LogicalText));
        var before = package.SpineDocuments[0].Document.Body!.InnerHtml;

        var result = new CrossParagraphHyphenationCorrectionApplier().Apply([plan]);

        Assert.Equal(new HyphenationCorrectionApplyResult(0, 1), result);
        Assert.Equal(before, package.SpineDocuments[0].Document.Body!.InnerHtml);
    }

    [Fact]
    public void Apply_SkipsNonCrossParagraphPlan()
    {
        using var epub = CreateSingleDocumentEpub("<p>kol-tukta</p>");
        var package = new EpubPackageReader().Read(epub.Path);
        var plan = Assert.Single(CreatePlans(package.LogicalText));
        var before = plan.HyphenSource.SourceNode.Data;

        var result = new CrossParagraphHyphenationCorrectionApplier().Apply([plan]);

        Assert.Equal(HyphenationCorrectionKind.Inline, plan.CorrectionKind);
        Assert.Equal(new HyphenationCorrectionApplyResult(0, 1), result);
        Assert.Equal(before, plan.HyphenSource.SourceNode.Data);
    }

    [Fact]
    public void Apply_SupportsChainedMergesAgainstTheCurrentWorkingDom()
    {
        using var epub = CreateSingleDocumentEpub(
            "<p>kol-</p>\n<p>tuk-</p>\n<p>ta</p>");
        var package = new EpubPackageReader().Read(epub.Path);
        var plans = CreatePlans(package.LogicalText);

        var result = new CrossParagraphHyphenationCorrectionApplier().Apply(plans);

        Assert.Equal(2, plans.Count);
        Assert.Equal(new HyphenationCorrectionApplyResult(2, 0), result);
        var paragraph = Assert.Single(package.SpineDocuments[0].Document.QuerySelectorAll("p"));
        Assert.Equal("koltukta", paragraph.TextContent);
    }

    [Fact]
    public void Apply_LeavesOldStreamStaleAndRebuildReflectsMergedDom()
    {
        using var epub = CreateSingleDocumentEpub("<p>kol-</p>\n<p>tukta</p>");
        var package = new EpubPackageReader().Read(epub.Path);
        var oldStream = package.LogicalText;
        var plan = Assert.Single(CreatePlans(oldStream));

        _ = new CrossParagraphHyphenationCorrectionApplier().Apply([plan]);
        var rebuiltStream = LogicalTextStreamBuilder.Build(package.SpineDocuments);

        Assert.Equal("kol-tukta", oldStream.Text);
        Assert.Equal("koltukta", rebuiltStream.Text);
        Assert.Empty(new HyphenationDetector().Detect(rebuiltStream));
    }

    [Fact]
    public void Apply_RejectsNullInput()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CrossParagraphHyphenationCorrectionApplier().Apply(null!));
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
