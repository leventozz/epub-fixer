using AngleSharp.Html.Parser;
using EpubFixer.Core.Decision.Models;
using EpubFixer.Core.Detection.Models;
using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Evidence.Models;

namespace EpubFixer.Tests;

public sealed class HyphenationDecisionReportingTests
{
    [Fact]
    public void CreateSummary_CountsDecisionsAndAggregatesAutoFixCandidatesDeterministically()
    {
        var decisions = new[]
        {
            CreateDecision("A", "b", 10, HyphenationDecisionKind.AutoFixCandidate),
            CreateDecision("A", "b", 10, HyphenationDecisionKind.AutoFixCandidate),
            CreateDecision("A", "bb", 10, HyphenationDecisionKind.AutoFixCandidate),
            CreateDecision("B", "a", 20, HyphenationDecisionKind.AutoFixCandidate),
            CreateDecision("Deferred", "item", 202, HyphenationDecisionKind.Deferred)
        };

        var summary = HyphenationDecisionReporting.CreateSummary(decisions);

        Assert.Equal(5, summary.TotalDecisions);
        Assert.Equal(4, summary.AutoFixCandidate);
        Assert.Equal(1, summary.Deferred);
        Assert.Collection(
            summary.AutoFixCandidateTransformations,
            item => AssertAggregate(item, "B", "a", 20, 1),
            item => AssertAggregate(item, "A", "b", 10, 2),
            item => AssertAggregate(item, "A", "bb", 10, 1));
    }

    [Fact]
    public void CreateSummary_DoesNotLimitAutoFixCandidateTransformations()
    {
        var decisions = Enumerable.Range(0, 25)
            .Select(index => CreateDecision(
                $"Left{index:D2}",
                "Right",
                10,
                HyphenationDecisionKind.AutoFixCandidate))
            .ToArray();

        var summary = HyphenationDecisionReporting.CreateSummary(decisions);

        Assert.Equal(25, summary.AutoFixCandidateTransformations.Count);
    }

    [Fact]
    public void Print_IncludesDecisionCountsAndEveryAggregate()
    {
        var decisions = new[]
        {
            CreateDecision("Auersber", "ger", 202, HyphenationDecisionKind.AutoFixCandidate),
            CreateDecision("Greg", "ers", 9, HyphenationDecisionKind.Deferred)
        };
        var summary = HyphenationDecisionReporting.CreateSummary(decisions);
        using var writer = new StringWriter();

        HyphenationDecisionReporting.Print(writer, summary);

        var output = writer.ToString();
        Assert.Contains("Total decisions: 2", output);
        Assert.Contains("AutoFixCandidate: 1", output);
        Assert.Contains("Deferred: 1", output);
        Assert.Contains("AutoFixCandidate transformations (1 unique)", output);
        Assert.Contains("Auersber-ger -> Auersberger", output);
        Assert.Contains("Lexicon count: 202", output);
        Assert.Contains("Candidate occurrences: 1", output);
        Assert.DoesNotContain("Greg-ers", output);
    }

    [Fact]
    public void CreateSummary_RejectsNullInput()
    {
        Assert.Throws<ArgumentNullException>(() =>
            HyphenationDecisionReporting.CreateSummary(null!));
    }

    private static HyphenationDecision CreateDecision(
        string leftPart,
        string rightPart,
        int lexiconCount,
        HyphenationDecisionKind decisionKind)
    {
        var document = new HtmlParser().ParseDocument("<p>source</p>");
        var sourceNode = Assert.IsAssignableFrom<AngleSharp.Dom.IText>(
            document.QuerySelector("p")!.FirstChild);
        var source = new TextSourceLocation("chapter.xhtml", 0, sourceNode, 0, 1);
        var candidate = new HyphenationCandidate(
            leftPart,
            rightPart,
            leftPart + rightPart,
            HyphenationDetectionKind.Inline,
            source,
            source,
            source);
        var evidence = new HyphenationEvidence(
            candidate,
            lexiconCount,
            lexiconCount > 0,
            new HyphenationContextEvidence(null, null, false, false));

        return new HyphenationDecision(evidence, decisionKind);
    }

    private static void AssertAggregate(
        HyphenationDecisionAggregate item,
        string leftPart,
        string rightPart,
        int lexiconCount,
        int candidateOccurrences)
    {
        Assert.Equal(leftPart, item.Key.LeftPart);
        Assert.Equal(rightPart, item.Key.RightPart);
        Assert.Equal(leftPart + rightPart, item.Key.UnhyphenatedText);
        Assert.Equal(lexiconCount, item.LexiconCount);
        Assert.Equal(candidateOccurrences, item.CandidateOccurrences);
    }
}
