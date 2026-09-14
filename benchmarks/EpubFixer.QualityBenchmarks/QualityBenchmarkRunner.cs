using EpubFixer.Core.Decision;
using EpubFixer.Core.Detection;
using EpubFixer.Core.Correction;
using EpubFixer.Core.Correction.Models;
using EpubFixer.Core.Epub;
using EpubFixer.Core.Evidence;
using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Mutation;
using EpubFixer.Core.Ocr;
using EpubFixer.QualityBenchmarks.Models;
using EpubFixer.TrMorph;

namespace EpubFixer.QualityBenchmarks;

public sealed class QualityBenchmarkRunner
{
    private readonly IOcrCorrectionPlanner ocrCorrectionPlanner;

    public QualityBenchmarkRunner(IOcrCorrectionPlanner? ocrCorrectionPlanner = null)
    {
        this.ocrCorrectionPlanner = ocrCorrectionPlanner ?? new LegacyOcrCorrectionPlanner();
    }

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
            candidates,
            CreateOcrDetections(package.LogicalText));
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

        var beforeOcr = integrityEvaluator.Capture(package.SpineDocuments);
        var ocrStream = LogicalTextStreamBuilder.Build(package.SpineDocuments);
        using var ocrMorphologyAnalyzer = new FomaTurkishMorphologyAnalyzer();
        var ocrOracleBuilder = new BatchMorphologyOracleBuilder(ocrMorphologyAnalyzer);
        var ocrPlanResult = ocrCorrectionPlanner.CreatePlan(ocrStream, ocrOracleBuilder);
        var ocrResult = new OcrCorrectionMutationApplier().Apply(package, ocrPlanResult.Plan) with { Engine = ocrPlanResult.Engine };
        if (!ocrResult.Succeeded)
        {
            throw new InvalidOperationException(
                "OCR mutation stage failed during quality benchmark: "
                + string.Join(", ", ocrResult.Failures.Select(item => item.Reason)));
        }
        var ocrIntegrity = integrityEvaluator.Audit(
            beforeOcr,
            package.SpineDocuments,
            ocrResult.AppliedMutations,
            "ocr");

        var beforeOcrText = new Dictionary<AngleSharp.Dom.IText, string>(ReferenceEqualityComparer.Instance);
        foreach (var pair in beforeOcr.Nodes)
        {
            if (pair.Key is AngleSharp.Dom.IText text)
            {
                beforeOcrText[text] = pair.Value.Value;
            }
        }

        // OcrMutationSourceSpan.TextNodeIndex is positional in ocrStream (built after
        // hyphenation ran, which can remove/merge text nodes) - it is NOT comparable
        // to a tracker's TextNodeIndex, which is positional in the pristine,
        // pre-hyphenation stream. Resolve each mutation location to its actual node
        // object through ocrStream's own segments so trackers can match by reference.
        var mutationNodesByLocation = new Dictionary<(string DocumentPath, int TextNodeIndex), AngleSharp.Dom.IText>();
        foreach (var segment in ocrStream.Segments)
        {
            mutationNodesByLocation[(segment.Source.DocumentPath, segment.Source.TextNodeIndex)] = segment.Source.SourceNode;
        }

        foreach (var tracker in knownTrackers.Concat(protectedTrackers))
        {
            tracker.Resync(beforeOcrText, mutationNodesByLocation, ocrResult.AppliedMutations);
        }

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
            UnexpectedTextChanges = inlineIntegrity.TextChanges.Count
                + crossIntegrity.TextChanges.Count
                + ocrIntegrity.TextChanges.Count,
            NonTextChanges = inlineIntegrity.NonTextChanges.Count
                + crossIntegrity.NonTextChanges.Count
                + ocrIntegrity.NonTextChanges.Count,
            UnexpectedTextChangeDetails = inlineIntegrity.TextChanges
                .Concat(crossIntegrity.TextChanges)
                .Concat(ocrIntegrity.TextChanges)
                .ToArray(),
            NonTextChangeDetails = inlineIntegrity.NonTextChanges
                .Concat(crossIntegrity.NonTextChanges)
                .Concat(ocrIntegrity.NonTextChanges)
                .ToArray(),
            OcrEngine = ocrResult.Engine.ToString().ToLowerInvariant(),
            OcrMutation = ocrResult,
            ClassBreakdowns = CreateClassBreakdowns(
                dataset.GroundTruth.KnownErrors,
                detectionResult.MissedOccurrences,
                correctionResult.Failures)
        };
    }

    private static IReadOnlyList<OcrDetectionSource> CreateOcrDetections(EpubFixer.Core.Epub.Models.LogicalTextStream stream)
    {
        using var analyzer = new FomaTurkishMorphologyAnalyzer();
        var builder = new BatchMorphologyOracleBuilder(analyzer);
        var anomalyDetector = new OcrAnomalyDetector();
        var anomaly = anomalyDetector.Analyze(stream, builder.Build(anomalyDetector.EnumerateMorphologyQueries(stream))).Candidates
            .SelectMany(item => item.Candidate.Sources.Select(span =>
                new OcrDetectionSource(span.DocumentPath, span.TextNodeIndex, span.Start, span.Start + span.Length)));
        var regionDetector = new OcrRegionDetector();
        var regions = regionDetector.Detect(stream.Text, builder.Build(regionDetector.EnumerateMorphologyQueries(stream.Text)))
            .SelectMany(item => CreateSourceDetections(stream, item.Start, item.EndExclusive));
        return anomaly.Concat(regions)
            .OrderBy(item => item.Start)
            .ThenBy(item => item.EndExclusive)
            .ToArray();
    }

    private static IReadOnlyList<OcrDetectionSource> CreateSourceDetections(
        EpubFixer.Core.Epub.Models.LogicalTextStream stream,
        int start,
        int endExclusive)
    {
        var detections = new List<OcrDetectionSource>();
        for (var index = start; index < endExclusive; index++)
        {
            var source = stream.GetSourceLocationAt(index);
            if (detections.Count > 0
                && string.Equals(detections[^1].DocumentPath, source.DocumentPath, StringComparison.Ordinal)
                && detections[^1].TextNodeIndex == source.TextNodeIndex
                && detections[^1].EndExclusive == source.Start)
            {
                var previous = detections[^1];
                detections[^1] = previous with { EndExclusive = source.Start + source.Length };
            }
            else
            {
                detections.Add(new OcrDetectionSource(
                    source.DocumentPath,
                    source.TextNodeIndex,
                    source.Start,
                    source.Start + source.Length));
            }
        }

        return detections;
    }

    internal static IReadOnlyList<QualityBenchmarkClassBreakdown> CreateClassBreakdowns(
        IReadOnlyList<KnownErrorOccurrence> knownErrors,
        IReadOnlyList<KnownErrorOccurrence> missed,
        IReadOnlyList<KnownErrorCorrectionFailure> failures)
    {
        var missedIds = missed.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var failureIds = failures.Select(item => item.Occurrence.Id).ToHashSet(StringComparer.Ordinal);
        return knownErrors
            .GroupBy(item => item.ErrorClass)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var known = group.Count();
                var detected = group.Count(item => !missedIds.Contains(item.Id));
                var wrong = group.Count(item => failures.Any(failure =>
                    string.Equals(failure.Occurrence.Id, item.Id, StringComparison.Ordinal)
                    && string.Equals(failure.Classification, "WronglyFixed", StringComparison.Ordinal)));
                var correct = group.Count(item => !failureIds.Contains(item.Id));
                return new QualityBenchmarkClassBreakdown(
                    group.Key,
                    known,
                    detected,
                    correct,
                    wrong,
                    correct + wrong == 0 ? null : (double)correct / (correct + wrong),
                    known == 0 ? null : (double)correct / known);
            })
            .ToArray();
    }

}
