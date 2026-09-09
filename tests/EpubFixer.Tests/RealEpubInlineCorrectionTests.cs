using System.Security.Cryptography;
using EpubFixer.Core.Correction;
using EpubFixer.Core.Correction.Models;
using EpubFixer.Core.Decision;
using EpubFixer.Core.Detection;
using EpubFixer.Core.Detection.Models;
using EpubFixer.Core.Epub;
using EpubFixer.Core.Evidence;
using EpubFixer.Core.Lexicon;

namespace EpubFixer.Tests;

public sealed class RealEpubInlineCorrectionTests
{
    [Fact]
    public void ApplyInline_RebuildsExpectedCandidateCountsWithoutChangingSourceEpub()
    {
        var epubPath = Path.Combine(
            AppContext.BaseDirectory,
            "test-data",
            "Odun Kesmek_recognized.epub");
        var sourceHashBefore = SHA256.HashData(File.ReadAllBytes(epubPath));
        var package = new EpubPackageReader().Read(epubPath);
        var beforeCandidates = new HyphenationDetector().Detect(package.LogicalText);
        var lexicon = new BookLexiconBuilder().Build(package.LogicalText);
        var evidence = new HyphenationEvidenceEvaluator().Evaluate(
            beforeCandidates,
            lexicon,
            package.LogicalText);
        var decisions = new HyphenationDecisionEvaluator().Evaluate(evidence);
        var plans = new HyphenationCorrectionPlanner().Plan(decisions);
        var inlinePlans = plans
            .Where(plan => plan.CorrectionKind == HyphenationCorrectionKind.Inline)
            .ToArray();
        var plannedTransformations = inlinePlans
            .Select(plan => (
                plan.Decision.Evidence.Candidate.LeftPart,
                plan.Decision.Evidence.Candidate.RightPart,
                plan.UnhyphenatedText))
            .ToHashSet();

        var result = new HyphenationCorrectionApplier().Apply(plans);
        var rebuiltStream = LogicalTextStreamBuilder.Build(package.SpineDocuments);
        var afterCandidates = new HyphenationDetector().Detect(rebuiltStream);
        var remainingPlannedInlineCandidates = afterCandidates.Count(candidate =>
            candidate.DetectionKind == HyphenationDetectionKind.Inline
            && plannedTransformations.Contains((
                candidate.LeftPart,
                candidate.RightPart,
                candidate.UnhyphenatedText)));
        var sourceHashAfter = SHA256.HashData(File.ReadAllBytes(epubPath));

        Assert.Equal(448, beforeCandidates.Count);
        Assert.Equal(129, inlinePlans.Length);
        Assert.Equal(new HyphenationCorrectionApplyResult(129, 19), result);
        Assert.Equal(319, afterCandidates.Count);
        Assert.Equal(
            258,
            afterCandidates.Count(candidate =>
                candidate.DetectionKind == HyphenationDetectionKind.Inline));
        Assert.Equal(
            60,
            afterCandidates.Count(candidate =>
                candidate.DetectionKind == HyphenationDetectionKind.ParagraphBoundary));
        Assert.Single(afterCandidates, candidate =>
            candidate.DetectionKind == HyphenationDetectionKind.DocumentBoundary);
        Assert.Equal(0, remainingPlannedInlineCandidates);
        Assert.Equal(sourceHashBefore, sourceHashAfter);
    }
}
