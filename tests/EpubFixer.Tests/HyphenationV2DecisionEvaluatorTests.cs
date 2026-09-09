using AngleSharp.Html.Parser;
using EpubFixer.Core.Decision;
using EpubFixer.Core.Decision.Models;
using EpubFixer.Core.Detection.Models;
using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Evidence.Models;
using EpubFixer.Core.Morphology.Models;

namespace EpubFixer.Tests;

public sealed class HyphenationV2DecisionEvaluatorTests
{
    [Fact]
    public void Evaluate_AcceptsCleanValidLowLexiconWithTwoUnicodeLetters()
    {
        var evidence = CreateEvidence("sosyete", "si", 1);
        var morphology = CreateMorphology(evidence, isClean: true, valid: true);

        var decision = Assert.Single(new HyphenationV2DecisionEvaluator().Evaluate([evidence], [morphology]));

        Assert.Equal(HyphenationDecisionKind.AutoFixCandidate, decision.DecisionKind);
        Assert.Equal(HyphenationDecisionReason.TurkishMorphology, decision.Reason);
    }

    [Fact]
    public void Evaluate_UsesRuneLettersForSupplementaryCharacters()
    {
        var evidence = CreateEvidence("x", "𝒜x", 1);
        var morphology = CreateMorphology(evidence, isClean: true, valid: true);

        var decision = Assert.Single(new HyphenationV2DecisionEvaluator().Evaluate([evidence], [morphology]));

        Assert.Equal(2, morphology.RightFragmentLetterCount);
        Assert.Equal(HyphenationDecisionKind.AutoFixCandidate, decision.DecisionKind);
    }

    [Theory]
    [InlineData(false, true, "ab")]
    [InlineData(true, false, "ab")]
    [InlineData(true, true, "a")]
    public void Evaluate_DefersWhenFallbackPrerequisiteFails(bool isClean, bool valid, string right)
    {
        var evidence = CreateEvidence("left", right, 1, isClean);
        var morphology = CreateMorphology(evidence, isClean, valid);

        var decision = Assert.Single(new HyphenationV2DecisionEvaluator().Evaluate([evidence], [morphology]));

        Assert.Equal(HyphenationDecisionKind.Deferred, decision.DecisionKind);
        Assert.Null(decision.Reason);
    }

    [Fact]
    public void Evaluate_PreservesV1StrongLexiconRule()
    {
        var evidence = CreateEvidence("left", "right", 10);
        var morphology = CreateMorphology(evidence, isClean: true, valid: false);

        var decision = Assert.Single(new HyphenationV2DecisionEvaluator().Evaluate([evidence], [morphology]));

        Assert.Equal(HyphenationDecisionKind.AutoFixCandidate, decision.DecisionKind);
        Assert.Equal(HyphenationDecisionReason.StrongBookLexicon, decision.Reason);
    }

    private static HyphenationMorphologyEvidence CreateMorphology(
        HyphenationEvidence evidence,
        bool isClean,
        bool valid) => new(
            evidence.Candidate,
            evidence.UnhyphenatedOccurrenceCount,
            !isClean,
            false,
            isClean,
            valid);

    private static HyphenationEvidence CreateEvidence(string left, string right, int count, bool isClean = true)
    {
        var document = new HtmlParser().ParseDocument("<p>source</p>");
        var node = Assert.IsAssignableFrom<AngleSharp.Dom.IText>(document.QuerySelector("p")!.FirstChild);
        var source = new TextSourceLocation("chapter.xhtml", 0, node, 0, 1);
        var candidate = new HyphenationCandidate(
            left, right, left + right, HyphenationDetectionKind.Inline, source, source, source);
        return new HyphenationEvidence(
            candidate,
            count,
            count > 0,
            new HyphenationContextEvidence(isClean ? null : "-", null, !isClean, false));
    }
}
