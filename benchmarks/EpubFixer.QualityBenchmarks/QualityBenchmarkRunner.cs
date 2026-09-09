using EpubFixer.Core.Decision;
using EpubFixer.Core.Detection;
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

        var detectionResult = new QualityBenchmarkMatcher().Match(
            dataset.GroundTruth.KnownErrors,
            candidates);
        var protectionResult = new QualityBenchmarkProtectionEvaluator().Evaluate(
            dataset.GroundTruth.ProtectedOccurrences,
            decisions);
        var knownDecisionResult = new QualityBenchmarkKnownDecisionEvaluator().Evaluate(
            dataset.GroundTruth.KnownErrors,
            decisions);

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
            KnownDeferredOccurrences = knownDecisionResult.DeferredOccurrences
        };
    }
}
