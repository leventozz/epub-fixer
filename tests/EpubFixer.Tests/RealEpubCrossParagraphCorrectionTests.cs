using System.Security.Cryptography;
using EpubFixer.Core.Correction;
using EpubFixer.Core.Correction.Models;
using EpubFixer.Core.Decision;
using EpubFixer.Core.Decision.Models;
using EpubFixer.Core.Detection;
using EpubFixer.Core.Detection.Models;
using EpubFixer.Core.Epub;
using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Evidence;
using EpubFixer.Core.Lexicon;

namespace EpubFixer.Tests;

public sealed class RealEpubCrossParagraphCorrectionTests
{
    [Fact]
    public void ApplyParagraph_RebuildsExpectedPipelineWithoutChangingSourceEpub()
    {
        var epubPath = Path.Combine(
            AppContext.BaseDirectory,
            "test-data",
            "Odun Kesmek_recognized.epub");
        var sourceHashBefore = SHA256.HashData(File.ReadAllBytes(epubPath));
        var package = new EpubPackageReader().Read(epubPath);
        var original = Analyze(package.LogicalText);
        var originalAutoFixCandidates = original.Decisions
            .Where(decision => decision.DecisionKind == HyphenationDecisionKind.AutoFixCandidate)
            .Select(decision => decision.Evidence.Candidate)
            .ToArray();
        var inlinePlans = original.Plans
            .Where(plan => plan.CorrectionKind == HyphenationCorrectionKind.Inline)
            .ToArray();

        var inlineResult = new HyphenationCorrectionApplier().Apply(inlinePlans);
        var afterInlineStream = LogicalTextStreamBuilder.Build(package.SpineDocuments);
        var afterInline = Analyze(afterInlineStream);
        var crossParagraphPlans = afterInline.Plans
            .Where(plan => plan.CorrectionKind == HyphenationCorrectionKind.CrossParagraph)
            .ToArray();

        var crossParagraphResult =
            new CrossParagraphHyphenationCorrectionApplier().Apply(crossParagraphPlans);
        var finalStream = LogicalTextStreamBuilder.Build(package.SpineDocuments);
        var final = Analyze(finalStream);
        var remainingOriginalOccurrences = final.Candidates.Count(candidate =>
            originalAutoFixCandidates.Any(originalCandidate =>
                ReferenceEquals(
                    originalCandidate.HyphenSource.SourceNode,
                    candidate.HyphenSource.SourceNode)
                && string.Equals(
                    originalCandidate.LeftPart,
                    candidate.LeftPart,
                    StringComparison.Ordinal)
                && string.Equals(
                    originalCandidate.RightPart,
                    candidate.RightPart,
                    StringComparison.Ordinal)
                && string.Equals(
                    originalCandidate.UnhyphenatedText,
                    candidate.UnhyphenatedText,
                    StringComparison.Ordinal)));
        var sourceHashAfter = SHA256.HashData(File.ReadAllBytes(epubPath));

        Assert.Equal(448, original.Candidates.Count);
        Assert.Equal(148, originalAutoFixCandidates.Length);
        Assert.Equal(129, inlinePlans.Length);
        Assert.Equal(new HyphenationCorrectionApplyResult(129, 0), inlineResult);
        Assert.Equal(319, afterInline.Candidates.Count);
        Assert.Equal(19, crossParagraphPlans.Length);
        Assert.Equal(
            new HyphenationCorrectionApplyResult(19, 0),
            crossParagraphResult);
        Assert.Equal(300, final.Candidates.Count);
        Assert.Equal(
            258,
            final.Candidates.Count(candidate =>
                candidate.DetectionKind == HyphenationDetectionKind.Inline));
        Assert.Equal(
            41,
            final.Candidates.Count(candidate =>
                candidate.DetectionKind == HyphenationDetectionKind.ParagraphBoundary));
        Assert.Single(final.Candidates, candidate =>
            candidate.DetectionKind == HyphenationDetectionKind.DocumentBoundary);
        Assert.DoesNotContain(
            final.Decisions,
            decision => decision.DecisionKind == HyphenationDecisionKind.AutoFixCandidate);
        Assert.Empty(final.Plans);
        Assert.Equal(0, remainingOriginalOccurrences);
        Assert.Equal(sourceHashBefore, sourceHashAfter);
    }

    private static PipelineResult Analyze(LogicalTextStream stream)
    {
        var candidates = new HyphenationDetector().Detect(stream);
        var lexicon = new BookLexiconBuilder().Build(stream);
        var evidence = new HyphenationEvidenceEvaluator().Evaluate(
            candidates,
            lexicon,
            stream);
        var decisions = new HyphenationDecisionEvaluator().Evaluate(evidence);
        var plans = new HyphenationCorrectionPlanner().Plan(decisions);

        return new PipelineResult(candidates, decisions, plans);
    }

    private sealed record PipelineResult(
        IReadOnlyList<HyphenationCandidate> Candidates,
        IReadOnlyList<HyphenationDecision> Decisions,
        IReadOnlyList<HyphenationCorrectionPlan> Plans);
}
