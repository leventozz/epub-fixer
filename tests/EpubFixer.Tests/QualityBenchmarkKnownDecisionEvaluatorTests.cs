using EpubFixer.Core.Decision.Models;
using EpubFixer.Core.Detection;
using EpubFixer.Core.Detection.Models;
using EpubFixer.Core.Epub;
using EpubFixer.Core.Evidence.Models;
using EpubFixer.QualityBenchmarks;
using EpubFixer.QualityBenchmarks.Models;

namespace EpubFixer.Tests;

public sealed class QualityBenchmarkKnownDecisionEvaluatorTests
{
    [Fact]
    public void Evaluate_CountsExactAutoFixDecision()
    {
        using var epub = CreateEpub("Auersber-ger");
        var candidate = Assert.Single(Detect(epub));
        var result = new QualityBenchmarkKnownDecisionEvaluator().Evaluate(
            [Occurrence("one", candidate)],
            [Decision(candidate, HyphenationDecisionKind.AutoFixCandidate)]);

        Assert.Equal(1, result.KnownAutoFixCandidates);
        Assert.Equal(0, result.KnownDeferred);
        Assert.Equal(1d, result.AutoFixCoverage);
        Assert.Empty(result.DeferredOccurrences);
    }

    [Fact]
    public void Evaluate_CountsExactDeferredDecisionAndPreservesOccurrence()
    {
        using var epub = CreateEpub("Auersber-ger");
        var candidate = Assert.Single(Detect(epub));
        var occurrence = Occurrence("one", candidate);
        var result = new QualityBenchmarkKnownDecisionEvaluator().Evaluate(
            [occurrence],
            [Decision(candidate, HyphenationDecisionKind.Deferred)]);

        Assert.Equal(0, result.KnownAutoFixCandidates);
        Assert.Equal(1, result.KnownDeferred);
        Assert.Equal(0d, result.AutoFixCoverage);
        Assert.Same(occurrence, Assert.Single(result.DeferredOccurrences));
    }

    [Fact]
    public void Evaluate_DoesNotCountMissedOccurrenceAsDeferredOrAutoFix()
    {
        using var epub = CreateEpub("Auersber-ger");
        var candidate = Assert.Single(Detect(epub));
        var occurrence = Occurrence("one", candidate) with { SourceSpans = [new("chapter.xhtml", 0, 99, 1)] };
        var result = new QualityBenchmarkKnownDecisionEvaluator().Evaluate(
            [occurrence],
            [Decision(candidate, HyphenationDecisionKind.Deferred)]);

        Assert.Equal(0, result.KnownAutoFixCandidates);
        Assert.Equal(0, result.KnownDeferred);
        Assert.Empty(result.DeferredOccurrences);
        Assert.Equal(0d, result.AutoFixCoverage);
    }

    [Fact]
    public void Evaluate_ConsumesDecisionOnlyOnceForDuplicateOccurrences()
    {
        using var epub = CreateEpub("Auersber-ger");
        var candidate = Assert.Single(Detect(epub));
        var first = Occurrence("one", candidate);
        var second = first with { Id = "two" };
        var result = new QualityBenchmarkKnownDecisionEvaluator().Evaluate(
            [first, second],
            [Decision(candidate, HyphenationDecisionKind.AutoFixCandidate)]);

        Assert.Equal(1, result.KnownAutoFixCandidates);
        Assert.Equal(0, result.KnownDeferred);
    }

    [Fact]
    public void Evaluate_CalculatesPartialAutoFixCoverage()
    {
        using var epub = CreateEpub("Auersber-ger Jo-ana");
        var candidates = Detect(epub);
        var result = new QualityBenchmarkKnownDecisionEvaluator().Evaluate(
            [Occurrence("one", candidates[0]), Occurrence("two", candidates[1])],
            [
                Decision(candidates[0], HyphenationDecisionKind.AutoFixCandidate),
                Decision(candidates[1], HyphenationDecisionKind.Deferred)
            ]);

        Assert.Equal(1, result.KnownAutoFixCandidates);
        Assert.Equal(1, result.KnownDeferred);
        Assert.Equal(0.5d, result.AutoFixCoverage);
    }

    [Fact]
    public void Evaluate_ReturnsNullCoverageWhenThereAreNoKnownErrors()
    {
        var result = new QualityBenchmarkKnownDecisionEvaluator().Evaluate([], []);

        Assert.Null(result.AutoFixCoverage);
    }

    [Fact]
    public void ReportWriter_PrintsNAForNullAutoFixCoverage()
    {
        var result = new QualityBenchmarkResult(0, 0, 0, null, [])
        {
            AutoFixCoverage = null
        };
        using var writer = new StringWriter();

        QualityBenchmarkReportWriter.Write(writer, "empty", result);

        Assert.Contains("Auto-fix coverage: N/A", writer.ToString());
    }

    private static HyphenationDecision Decision(
        HyphenationCandidate candidate,
        HyphenationDecisionKind kind) => new(
        new HyphenationEvidence(
            candidate,
            kind == HyphenationDecisionKind.AutoFixCandidate ? 10 : 0,
            kind == HyphenationDecisionKind.AutoFixCandidate,
            new HyphenationContextEvidence(null, null, false, false)),
        kind);

    private static KnownErrorOccurrence Occurrence(string id, HyphenationCandidate candidate) => new(
        id,
        candidate.HyphenSource.DocumentPath,
        candidate.LeftPart + "-" + candidate.RightPart,
        candidate.UnhyphenatedText,
        [new GroundTruthSourceSpan(
            candidate.LeftSource.DocumentPath,
            candidate.LeftSource.TextNodeIndex,
            candidate.LeftSource.Start,
            candidate.LeftSource.Length + candidate.HyphenSource.Length + candidate.RightSource.Length)]);

    private static IReadOnlyList<HyphenationCandidate> Detect(TemporaryEpub epub) =>
        new HyphenationDetector().Detect(new EpubPackageReader().Read(epub.Path).LogicalText);

    private static TemporaryEpub CreateEpub(string text) => TemporaryEpub.Create(
        [new TestDocument("chapter", "chapter.xhtml", $"<html xmlns=\"http://www.w3.org/1999/xhtml\"><body><p>{text}</p></body></html>")],
        [new TestSpineItem("chapter")]);
}
