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
        var integrityEvaluator = new QualityBenchmarkIntegrityEvaluator();
        var allowedOriginalPlans = originalPlans
            .Where(plan => dataset.GroundTruth.KnownErrors.Any(knownError =>
                QualityBenchmarkMatcher.IsMatch(
                    knownError,
                    plan.Decision.Evidence.Candidate)))
            .ToArray();
        var inlinePlans = originalPlans
            .Where(plan => plan.CorrectionKind == HyphenationCorrectionKind.Inline)
            .ToArray();

        var beforeInline = integrityEvaluator.Capture(package.SpineDocuments);
        _ = new HyphenationCorrectionApplier().Apply(inlinePlans);
        var inlineIntegrity = integrityEvaluator.Audit(
            beforeInline,
            package.SpineDocuments,
            allowedOriginalPlans.Where(plan =>
                plan.CorrectionKind == HyphenationCorrectionKind.Inline).ToArray(),
            "inline");

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

        var allowedFreshCrossParagraphPlans = freshCrossParagraphPlans
            .Where(fresh => allowedOriginalPlans.Any(original =>
                original.CorrectionKind == HyphenationCorrectionKind.CrossParagraph
                && string.Equals(original.UnhyphenatedText, fresh.UnhyphenatedText, StringComparison.Ordinal)
                && ReferenceEquals(original.LeftSource.SourceNode, fresh.LeftSource.SourceNode)
                && ReferenceEquals(original.HyphenSource.SourceNode, fresh.HyphenSource.SourceNode)
                && ReferenceEquals(original.RightSource.SourceNode, fresh.RightSource.SourceNode)))
            .ToArray();

        var beforeCrossParagraph = integrityEvaluator.Capture(package.SpineDocuments);
        _ = new CrossParagraphHyphenationCorrectionApplier().Apply(freshCrossParagraphPlans);
        var crossIntegrity = integrityEvaluator.Audit(
            beforeCrossParagraph,
            package.SpineDocuments,
            allowedFreshCrossParagraphPlans,
            "cross-paragraph");

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
            ProtectedChanges = protectedMutationResult.Changes,
            UnexpectedTextChanges = inlineIntegrity.TextChanges.Count + crossIntegrity.TextChanges.Count,
            NonTextChanges = inlineIntegrity.NonTextChanges.Count + crossIntegrity.NonTextChanges.Count,
            UnexpectedTextChangeDetails = inlineIntegrity.TextChanges
                .Concat(crossIntegrity.TextChanges)
                .ToArray(),
            NonTextChangeDetails = inlineIntegrity.NonTextChanges
                .Concat(crossIntegrity.NonTextChanges)
                .ToArray()
        };
    }
}
