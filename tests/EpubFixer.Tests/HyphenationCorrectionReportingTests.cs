using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using EpubFixer.Core.Correction;
using EpubFixer.Core.Decision.Models;
using EpubFixer.Core.Detection.Models;
using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Evidence.Models;

namespace EpubFixer.Tests;

public sealed class HyphenationCorrectionReportingTests
{
    [Fact]
    public void CreateSummaryAndPrint_ReportsPlanDistributionAndDocumentExclusion()
    {
        var decisions = new[]
        {
            CreateDecision(HyphenationDetectionKind.Inline),
            CreateDecision(HyphenationDetectionKind.Inline),
            CreateDecision(HyphenationDetectionKind.ParagraphBoundary),
            CreateDecision(HyphenationDetectionKind.TextNodeBoundary),
            CreateDecision(HyphenationDetectionKind.DocumentBoundary)
        };
        var plans = new HyphenationCorrectionPlanner().Plan(decisions);
        var summary = HyphenationCorrectionReporting.CreateSummary(decisions, plans);
        using var writer = new StringWriter();

        HyphenationCorrectionReporting.Print(writer, summary);

        Assert.Equal(5, summary.AutoFixCandidate);
        Assert.Equal(4, summary.CorrectionPlans);
        Assert.Equal(2, summary.Inline);
        Assert.Equal(1, summary.CrossParagraph);
        Assert.Equal(1, summary.TextNode);
        Assert.Equal(1, summary.Document);

        var output = writer.ToString();
        Assert.Contains("AutoFixCandidate: 5", output);
        Assert.Contains("Correction plans: 4", output);
        Assert.Contains("Inline: 2", output);
        Assert.Contains("CrossParagraph: 1", output);
        Assert.Contains("TextNode: 1", output);
        Assert.Contains("Document: 1", output);
        Assert.Contains(
            "Unplanned AutoFixCandidate: 1 (DocumentBoundary: 1)",
            output);
    }

    [Fact]
    public void CreateSummary_RejectsAnUnexplainedPlanCountDifference()
    {
        var decisions = new[] { CreateDecision(HyphenationDetectionKind.Inline) };

        Assert.Throws<InvalidOperationException>(() =>
            HyphenationCorrectionReporting.CreateSummary(decisions, []));
    }

    [Fact]
    public void CreateSummary_RejectsNullInputs()
    {
        Assert.Throws<ArgumentNullException>(() =>
            HyphenationCorrectionReporting.CreateSummary(null!, []));
        Assert.Throws<ArgumentNullException>(() =>
            HyphenationCorrectionReporting.CreateSummary([], null!));
    }

    private static HyphenationDecision CreateDecision(HyphenationDetectionKind detectionKind)
    {
        var document = new HtmlParser().ParseDocument("<p>left-right</p>");
        var sourceNode = Assert.IsAssignableFrom<IText>(
            document.QuerySelector("p")!.FirstChild);
        var source = new TextSourceLocation("chapter.xhtml", 0, sourceNode, 0, 1);
        var candidate = new HyphenationCandidate(
            "left",
            "right",
            "leftright",
            detectionKind,
            source,
            source,
            source);
        var evidence = new HyphenationEvidence(
            candidate,
            10,
            true,
            new HyphenationContextEvidence(null, null, false, false));

        return new HyphenationDecision(
            evidence,
            HyphenationDecisionKind.AutoFixCandidate);
    }
}
