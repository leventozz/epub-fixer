using EpubFixer.Core.Decision;
using EpubFixer.Core.Detection;
using EpubFixer.Core.Correction;
using EpubFixer.Core.Correction.Models;
using EpubFixer.Core.Epub;
using EpubFixer.Core.Evidence;
using EpubFixer.Core.Lexicon;
using EpubFixer.QualityBenchmarks.Models;

namespace EpubFixer.QualityBenchmarks;

public sealed class QualityBenchmarkRunner
{
    public QualityBenchmarkResult Run(QualityBenchmarkDataset dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        var package = new EpubPackageReader().Read(dataset.InputEpubPath);
        new QualityBenchmarkGroundTruthValidator().Validate(
            dataset.GroundTruth,
            package.LogicalText);
        var candidates = new HyphenationDetector().Detect(package.LogicalText);
        var lexicon = new BookLexiconBuilder().Build(package.LogicalText);
        var evidence = new HyphenationEvidenceEvaluator().Evaluate(
            candidates,
            lexicon,
            package.LogicalText);
        var decisions = new HyphenationDecisionEvaluator().Evaluate(evidence);

        var knownTrackers = QualityBenchmarkOccurrenceTracker.CreateKnown(
            dataset.GroundTruth.KnownErrors,
            package.LogicalText);
        var protectedTrackers = QualityBenchmarkOccurrenceTracker.CreateProtected(
            dataset.GroundTruth.ProtectedOccurrences,
            package.LogicalText);

        var detectionResult = new QualityBenchmarkMatcher().Match(
            dataset.GroundTruth.KnownErrors,
            candidates);
        var protectionResult = new QualityBenchmarkProtectionEvaluator().Evaluate(
            dataset.GroundTruth.ProtectedOccurrences,
            decisions);
        var knownDecisionResult = new QualityBenchmarkKnownDecisionEvaluator().Evaluate(
            dataset.GroundTruth.KnownErrors,
            decisions);

        var originalPlans = new HyphenationCorrectionPlanner().Plan(decisions);
        var inlinePlans = originalPlans
            .Where(plan => plan.CorrectionKind == HyphenationCorrectionKind.Inline)
            .ToArray();
        _ = new HyphenationCorrectionApplier().Apply(inlinePlans);

        var afterInlineStream = LogicalTextStreamBuilder.Build(package.SpineDocuments);
        var afterInlineCandidates = new HyphenationDetector().Detect(afterInlineStream);
        var afterInlineLexicon = new BookLexiconBuilder().Build(afterInlineStream);
        var afterInlineEvidence = new HyphenationEvidenceEvaluator().Evaluate(
            afterInlineCandidates,
            afterInlineLexicon,
            afterInlineStream);
        var afterInlineDecisions = new HyphenationDecisionEvaluator().Evaluate(afterInlineEvidence);
        var freshCrossParagraphPlans = new HyphenationCorrectionPlanner()
            .Plan(afterInlineDecisions)
            .Where(plan => plan.CorrectionKind == HyphenationCorrectionKind.CrossParagraph)
            .ToArray();
        _ = new CrossParagraphHyphenationCorrectionApplier().Apply(freshCrossParagraphPlans);

        var correctionResult = new QualityBenchmarkCorrectionEvaluator().Evaluate(
            dataset.GroundTruth.KnownErrors,
            knownTrackers);
        var protectedMutationResult = new QualityBenchmarkProtectedMutationEvaluator().Evaluate(
            dataset.GroundTruth.ProtectedOccurrences,
            protectedTrackers);

        foreach (var tracker in knownTrackers.Concat(protectedTrackers))
        {
            tracker.Detach();
        }

        return detectionResult with
        {
            ProtectedOccurrences = protectionResult.ProtectedOccurrences,
            ProtectedSafe = protectionResult.ProtectedSafe,
            ProtectedViolated = protectionResult.ProtectedViolated,
            ProtectionRate = protectionResult.ProtectionRate,
            ProtectedViolations = protectionResult.Violations,
            KnownAutoFixCandidates = knownDecisionResult.KnownAutoFixCandidates,
            KnownDeferred = knownDecisionResult.KnownDeferred,
            AutoFixCoverage = knownDecisionResult.AutoFixCoverage,
            KnownDeferredOccurrences = knownDecisionResult.DeferredOccurrences,
            CorrectlyFixed = correctionResult.CorrectlyFixed,
            WronglyFixed = correctionResult.WronglyFixed,
            Deferred = correctionResult.Deferred,
            CorrectionFailures = correctionResult.Failures,
            ProtectedChanged = protectedMutationResult.ProtectedChanged,
            ProtectedChanges = protectedMutationResult.Changes
        };
    }
}
