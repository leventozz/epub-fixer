using AngleSharp.Html.Parser;
using EpubFixer.Core.Decision;
using EpubFixer.Core.Decision.Models;
using EpubFixer.Core.Detection.Models;
using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Evidence.Models;

namespace EpubFixer.Tests;

public sealed class HyphenationDecisionEvaluatorTests
{
    [Fact]
    public void Evaluate_ReturnsAutoFixCandidateForCleanHighCountEvidence()
    {
        var evidence = CreateEvidence("Auersber", "ger", 202);

        var decision = Assert.Single(new HyphenationDecisionEvaluator().Evaluate([evidence]));

        Assert.Same(evidence, decision.Evidence);
        Assert.Equal(HyphenationDecisionKind.AutoFixCandidate, decision.DecisionKind);
    }

    [Fact]
    public void Evaluate_DefersThresholdEvidenceWithAdjacentHyphen()
    {
        var evidence = CreateEvidence("ha", "lı", 10, hasAdjacentHyphen: true);

        var decision = Assert.Single(new HyphenationDecisionEvaluator().Evaluate([evidence]));

        Assert.Equal(HyphenationDecisionKind.Deferred, decision.DecisionKind);
    }

    [Fact]
    public void Evaluate_DefersCleanEvidenceBelowThreshold()
    {
        var evidence = CreateEvidence("Greg", "ers", 9);

        var decision = Assert.Single(new HyphenationDecisionEvaluator().Evaluate([evidence]));

        Assert.Equal(HyphenationDecisionKind.Deferred, decision.DecisionKind);
    }

    [Fact]
    public void Evaluate_DefersUnknownEvidence()
    {
        var evidence = CreateEvidence("Un", "known", 0);

        var decision = Assert.Single(new HyphenationDecisionEvaluator().Evaluate([evidence]));

        Assert.Equal(HyphenationDecisionKind.Deferred, decision.DecisionKind);
    }

    [Fact]
    public void Evaluate_DefersHighCountEvidenceWithSuspiciousCharacter()
    {
        var evidence = CreateEvidence(
            "Auersber",
            "ger",
            202,
            hasAdjacentSuspiciousCharacter: true);

        var decision = Assert.Single(new HyphenationDecisionEvaluator().Evaluate([evidence]));

        Assert.Equal(HyphenationDecisionKind.Deferred, decision.DecisionKind);
    }

    [Fact]
    public void Evaluate_PreservesEveryOccurrenceAndInputOrder()
    {
        var first = CreateEvidence("Auersber", "ger", 202);
        var second = CreateEvidence("Greg", "ers", 9);
        var third = CreateEvidence("Auersber", "ger", 202);

        var decisions = new HyphenationDecisionEvaluator().Evaluate([first, second, third]);

        Assert.Equal(3, decisions.Count);
        Assert.Same(first, decisions[0].Evidence);
        Assert.Same(second, decisions[1].Evidence);
        Assert.Same(third, decisions[2].Evidence);
        Assert.Equal(
            [
                HyphenationDecisionKind.AutoFixCandidate,
                HyphenationDecisionKind.Deferred,
                HyphenationDecisionKind.AutoFixCandidate
            ],
            decisions.Select(item => item.DecisionKind));
    }

    [Fact]
    public void Evaluate_DoesNotUseExistsInLexiconAsAnAdditionalRule()
    {
        var evidence = CreateEvidence("Auersber", "ger", 202, existsInLexicon: false);

        var decision = Assert.Single(new HyphenationDecisionEvaluator().Evaluate([evidence]));

        Assert.Equal(HyphenationDecisionKind.AutoFixCandidate, decision.DecisionKind);
    }

    [Fact]
    public void Evaluate_RejectsNullInput()
    {
        var evaluator = new HyphenationDecisionEvaluator();

        Assert.Throws<ArgumentNullException>(() => evaluator.Evaluate(null!));
    }

    private static HyphenationEvidence CreateEvidence(
        string leftPart,
        string rightPart,
        int lexiconCount,
        bool hasAdjacentHyphen = false,
        bool hasAdjacentSuspiciousCharacter = false,
        bool? existsInLexicon = null)
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

        return new HyphenationEvidence(
            candidate,
            lexiconCount,
            existsInLexicon ?? lexiconCount > 0,
            new HyphenationContextEvidence(
                hasAdjacentHyphen ? "-" : null,
                hasAdjacentSuspiciousCharacter ? "·" : null,
                hasAdjacentHyphen,
                hasAdjacentSuspiciousCharacter));
    }
}
