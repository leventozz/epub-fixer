using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using EpubFixer.Core.Correction;
using EpubFixer.Core.Correction.Models;
using EpubFixer.Core.Decision.Models;
using EpubFixer.Core.Detection.Models;
using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Evidence.Models;

namespace EpubFixer.Tests;

public sealed class HyphenationCorrectionPlannerTests
{
    [Fact]
    public void Plan_CreatesInlinePlanWithReplacementAndOriginalSourceLocations()
    {
        var fixture = CreateDecision(
            "Auersber",
            "ger",
            HyphenationDetectionKind.Inline,
            HyphenationDecisionKind.AutoFixCandidate);

        var plan = Assert.Single(new HyphenationCorrectionPlanner().Plan([fixture.Decision]));
        var candidate = fixture.Decision.Evidence.Candidate;

        Assert.Same(fixture.Decision, plan.Decision);
        Assert.Equal(HyphenationCorrectionKind.Inline, plan.CorrectionKind);
        Assert.Equal("Auersberger", plan.UnhyphenatedText);
        Assert.Same(candidate.LeftSource, plan.LeftSource);
        Assert.Same(candidate.HyphenSource, plan.HyphenSource);
        Assert.Same(candidate.RightSource, plan.RightSource);
        Assert.Equal((0, 8), (plan.LeftSource.Start, plan.LeftSource.Length));
        Assert.Equal((8, 1), (plan.HyphenSource.Start, plan.HyphenSource.Length));
        Assert.Equal((9, 3), (plan.RightSource.Start, plan.RightSource.Length));
    }

    [Fact]
    public void Plan_CreatesCrossParagraphPlan()
    {
        var fixture = CreateDecision(
            "kol",
            "tukta",
            HyphenationDetectionKind.ParagraphBoundary,
            HyphenationDecisionKind.AutoFixCandidate);

        var plan = Assert.Single(new HyphenationCorrectionPlanner().Plan([fixture.Decision]));

        Assert.Equal(HyphenationCorrectionKind.CrossParagraph, plan.CorrectionKind);
        Assert.Equal("koltukta", plan.UnhyphenatedText);
        Assert.Same(fixture.LeftNode, plan.HyphenSource.SourceNode);
        Assert.Same(fixture.RightNode, plan.RightSource.SourceNode);
        Assert.NotSame(plan.HyphenSource.SourceNode, plan.RightSource.SourceNode);
    }

    [Fact]
    public void Plan_PreservesTextNodeBoundaryAsSeparateCorrectionKind()
    {
        var fixture = CreateDecision(
            "Viya",
            "na",
            HyphenationDetectionKind.TextNodeBoundary,
            HyphenationDecisionKind.AutoFixCandidate);

        var plan = Assert.Single(new HyphenationCorrectionPlanner().Plan([fixture.Decision]));

        Assert.Equal(HyphenationCorrectionKind.TextNode, plan.CorrectionKind);
        Assert.Same(fixture.LeftNode, plan.HyphenSource.SourceNode);
        Assert.Same(fixture.RightNode, plan.RightSource.SourceNode);
    }

    [Fact]
    public void Plan_DoesNotCreatePlansForDeferredOrDocumentBoundaryDecisions()
    {
        var deferred = CreateDecision(
            "Greg",
            "ers",
            HyphenationDetectionKind.Inline,
            HyphenationDecisionKind.Deferred);
        var documentBoundary = CreateDecision(
            "yazar",
            "mış",
            HyphenationDetectionKind.DocumentBoundary,
            HyphenationDecisionKind.AutoFixCandidate);

        var plans = new HyphenationCorrectionPlanner().Plan(
            [deferred.Decision, documentBoundary.Decision]);

        Assert.Empty(plans);
        Assert.Equal(HyphenationDecisionKind.Deferred, deferred.Decision.DecisionKind);
        Assert.Equal(
            HyphenationDecisionKind.AutoFixCandidate,
            documentBoundary.Decision.DecisionKind);
    }

    [Fact]
    public void Plan_PreservesOccurrenceOrderAndCreatesOnePlanPerOccurrence()
    {
        var first = CreateDecision(
            "Auersber",
            "ger",
            HyphenationDetectionKind.Inline,
            HyphenationDecisionKind.AutoFixCandidate);
        var deferred = CreateDecision(
            "Greg",
            "ers",
            HyphenationDetectionKind.Inline,
            HyphenationDecisionKind.Deferred);
        var second = CreateDecision(
            "Auersber",
            "ger",
            HyphenationDetectionKind.Inline,
            HyphenationDecisionKind.AutoFixCandidate);

        var plans = new HyphenationCorrectionPlanner().Plan(
            [first.Decision, deferred.Decision, second.Decision]);

        Assert.Collection(
            plans,
            plan => Assert.Same(first.Decision, plan.Decision),
            plan => Assert.Same(second.Decision, plan.Decision));
    }

    [Fact]
    public void Plan_DoesNotMutateAnySourceTextNode()
    {
        var fixture = CreateDecision(
            "kol",
            "tukta",
            HyphenationDetectionKind.ParagraphBoundary,
            HyphenationDecisionKind.AutoFixCandidate);
        var originalTexts = new[] { fixture.LeftNode.Data, fixture.RightNode.Data };

        _ = new HyphenationCorrectionPlanner().Plan([fixture.Decision]);

        Assert.Equal(
            originalTexts,
            new[] { fixture.LeftNode.Data, fixture.RightNode.Data });
    }

    [Fact]
    public void Plan_RejectsNullInput()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new HyphenationCorrectionPlanner().Plan(null!));
    }

    private static DecisionFixture CreateDecision(
        string leftPart,
        string rightPart,
        HyphenationDetectionKind detectionKind,
        HyphenationDecisionKind decisionKind)
    {
        var parser = new HtmlParser();
        IText leftNode;
        IText rightNode;
        string leftDocumentPath;
        string rightDocumentPath;

        if (detectionKind == HyphenationDetectionKind.Inline)
        {
            var document = parser.ParseDocument($"<p>{leftPart}-{rightPart}</p>");
            leftNode = GetTextNode(document.QuerySelector("p")!);
            rightNode = leftNode;
            leftDocumentPath = "chapter.xhtml";
            rightDocumentPath = leftDocumentPath;
        }
        else if (detectionKind == HyphenationDetectionKind.TextNodeBoundary)
        {
            var document = parser.ParseDocument(
                $"<p><span>{leftPart}-</span><span>{rightPart}</span></p>");
            var spans = document.QuerySelectorAll("span");
            leftNode = GetTextNode(spans[0]);
            rightNode = GetTextNode(spans[1]);
            leftDocumentPath = "chapter.xhtml";
            rightDocumentPath = leftDocumentPath;
        }
        else if (detectionKind == HyphenationDetectionKind.ParagraphBoundary)
        {
            var document = parser.ParseDocument($"<p>{leftPart}-</p><p>{rightPart}</p>");
            var paragraphs = document.QuerySelectorAll("p");
            leftNode = GetTextNode(paragraphs[0]);
            rightNode = GetTextNode(paragraphs[1]);
            leftDocumentPath = "chapter.xhtml";
            rightDocumentPath = leftDocumentPath;
        }
        else
        {
            var leftDocument = parser.ParseDocument($"<p>{leftPart}-</p>");
            var rightDocument = parser.ParseDocument($"<p>{rightPart}</p>");
            leftNode = GetTextNode(leftDocument.QuerySelector("p")!);
            rightNode = GetTextNode(rightDocument.QuerySelector("p")!);
            leftDocumentPath = "left.xhtml";
            rightDocumentPath = "right.xhtml";
        }

        var leftSource = new TextSourceLocation(
            leftDocumentPath,
            0,
            leftNode,
            0,
            leftPart.Length);
        var hyphenSource = new TextSourceLocation(
            leftDocumentPath,
            0,
            leftNode,
            leftPart.Length,
            1);
        var rightSource = new TextSourceLocation(
            rightDocumentPath,
            detectionKind == HyphenationDetectionKind.Inline ? 0 : 1,
            rightNode,
            detectionKind == HyphenationDetectionKind.Inline ? leftPart.Length + 1 : 0,
            rightPart.Length);
        var candidate = new HyphenationCandidate(
            leftPart,
            rightPart,
            leftPart + rightPart,
            detectionKind,
            leftSource,
            hyphenSource,
            rightSource);
        var evidence = new HyphenationEvidence(
            candidate,
            10,
            true,
            new HyphenationContextEvidence(null, null, false, false));

        return new DecisionFixture(
            new HyphenationDecision(evidence, decisionKind),
            leftNode,
            rightNode);
    }

    private static IText GetTextNode(IElement element)
    {
        return Assert.IsAssignableFrom<IText>(element.FirstChild);
    }

    private sealed record DecisionFixture(
        HyphenationDecision Decision,
        IText LeftNode,
        IText RightNode);
}
