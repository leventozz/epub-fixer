using EpubFixer.Core.Decision.Models;
using EpubFixer.Core.Detection;
using EpubFixer.Core.Detection.Models;
using EpubFixer.Core.Epub;
using EpubFixer.Core.Evidence.Models;
using EpubFixer.QualityBenchmarks;
using EpubFixer.QualityBenchmarks.Models;

namespace EpubFixer.Tests;

public sealed class QualityBenchmarkProtectionEvaluatorTests
{
    [Fact]
    public void Evaluate_ReportsExactAutoFixCandidateAsViolation()
    {
        using var epub = CreateSingleDocumentEpub("<p>Sankt-Pölten</p>");
        var candidate = Assert.Single(Detect(epub));
        var occurrence = CreateOccurrence("protected-1", candidate);
        var decision = CreateDecision(candidate, HyphenationDecisionKind.AutoFixCandidate);

        var result = new QualityBenchmarkProtectionEvaluator().Evaluate(
            [occurrence],
            [decision]);

        Assert.Equal(1, result.ProtectedOccurrences);
        Assert.Equal(0, result.ProtectedSafe);
        Assert.Equal(1, result.ProtectedViolated);
        Assert.Equal(0d, result.ProtectionRate);
        var violation = Assert.Single(result.Violations);
        Assert.Same(occurrence, violation.Occurrence);
        Assert.Equal(HyphenationDecisionKind.AutoFixCandidate, violation.DecisionKind);
    }

    [Fact]
    public void Evaluate_CountsDeferredDecisionAsSafe()
    {
        using var epub = CreateSingleDocumentEpub("<p>Sankt-Pölten</p>");
        var candidate = Assert.Single(Detect(epub));
        var occurrence = CreateOccurrence("protected-1", candidate);
        var decision = CreateDecision(candidate, HyphenationDecisionKind.Deferred);

        var result = new QualityBenchmarkProtectionEvaluator().Evaluate(
            [occurrence],
            [decision]);

        Assert.Equal(1, result.ProtectedSafe);
        Assert.Equal(0, result.ProtectedViolated);
        Assert.Equal(1d, result.ProtectionRate);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public void Evaluate_DoesNotMatchWrongSourceLocation()
    {
        using var epub = CreateSingleDocumentEpub("<p>Sankt-Pölten</p>");
        var candidate = Assert.Single(Detect(epub));
        var occurrence = CreateOccurrence("protected-1", candidate);
        var span = Assert.Single(occurrence.SourceSpans);
        occurrence = occurrence with
        {
            SourceSpans = [span with { Start = span.Start + 1 }]
        };

        var result = new QualityBenchmarkProtectionEvaluator().Evaluate(
            [occurrence],
            [CreateDecision(candidate, HyphenationDecisionKind.AutoFixCandidate)]);

        Assert.Equal(1, result.ProtectedSafe);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public void Evaluate_SeparatesOccurrencesWithTheSameOriginalText()
    {
        using var epub = CreateSingleDocumentEpub("<p>e-posta e-posta</p>");
        var candidates = Detect(epub);
        Assert.Equal(2, candidates.Count);
        var first = CreateOccurrence("protected-1", candidates[0]);
        var second = CreateOccurrence("protected-2", candidates[1]);

        var result = new QualityBenchmarkProtectionEvaluator().Evaluate(
            [first, second],
            [
                CreateDecision(candidates[0], HyphenationDecisionKind.AutoFixCandidate),
                CreateDecision(candidates[1], HyphenationDecisionKind.Deferred)
            ]);

        Assert.Equal(2, result.ProtectedOccurrences);
        Assert.Equal(1, result.ProtectedSafe);
        Assert.Equal(1, result.ProtectedViolated);
        Assert.Equal(0.5d, result.ProtectionRate);
        Assert.Same(first, Assert.Single(result.Violations).Occurrence);
    }

    [Fact]
    public void Evaluate_CountsOccurrenceWithoutMatchingDecisionAsSafe()
    {
        using var epub = CreateSingleDocumentEpub("<p>e-posta</p>");
        var occurrence = CreateOccurrence("protected-1", Assert.Single(Detect(epub)));

        var result = new QualityBenchmarkProtectionEvaluator().Evaluate([occurrence], []);

        Assert.Equal(1, result.ProtectedSafe);
        Assert.Equal(1d, result.ProtectionRate);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public void Evaluate_ConsumesEachDecisionAtMostOnce()
    {
        using var epub = CreateSingleDocumentEpub("<p>e-posta</p>");
        var candidate = Assert.Single(Detect(epub));
        var first = CreateOccurrence("protected-1", candidate);
        var second = first with { Id = "protected-2" };

        var result = new QualityBenchmarkProtectionEvaluator().Evaluate(
            [first, second],
            [CreateDecision(candidate, HyphenationDecisionKind.AutoFixCandidate)]);

        Assert.Equal(2, result.ProtectedOccurrences);
        Assert.Equal(1, result.ProtectedSafe);
        Assert.Equal(1, result.ProtectedViolated);
        Assert.Same(first, Assert.Single(result.Violations).Occurrence);
    }

    [Fact]
    public void Evaluate_ReturnsNullRateWhenThereAreNoProtectedOccurrences()
    {
        var result = new QualityBenchmarkProtectionEvaluator().Evaluate([], []);

        Assert.Equal(0, result.ProtectedOccurrences);
        Assert.Equal(0, result.ProtectedSafe);
        Assert.Equal(0, result.ProtectedViolated);
        Assert.Null(result.ProtectionRate);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public void Runner_UsesCoreEvidenceAndDecisionPipelineForProtectedOccurrences()
    {
        var lexiconText = string.Join(' ', Enumerable.Repeat("SanktPölten", 10));
        using var epub = CreateSingleDocumentEpub($"<p>Sankt-Pölten {lexiconText}</p>");
        var occurrence = CreateOccurrence("protected-1", Assert.Single(Detect(epub)));
        var dataset = new QualityBenchmarkDataset(
            "protected-test",
            Path.GetDirectoryName(epub.Path)!,
            epub.Path,
            Path.Combine(Path.GetDirectoryName(epub.Path)!, "ground-truth.json"),
            new GroundTruthDocument(1, [])
            {
                ProtectedOccurrences = [occurrence]
            });

        var result = new QualityBenchmarkRunner().Run(dataset);

        Assert.Equal(1, result.ProtectedOccurrences);
        Assert.Equal(0, result.ProtectedSafe);
        Assert.Equal(1, result.ProtectedViolated);
        Assert.Equal(0d, result.ProtectionRate);
        Assert.Equal(
            HyphenationDecisionKind.AutoFixCandidate,
            Assert.Single(result.ProtectedViolations).DecisionKind);
    }

    private static HyphenationDecision CreateDecision(
        HyphenationCandidate candidate,
        HyphenationDecisionKind decisionKind)
    {
        var evidence = new HyphenationEvidence(
            candidate,
            decisionKind == HyphenationDecisionKind.AutoFixCandidate ? 10 : 0,
            decisionKind == HyphenationDecisionKind.AutoFixCandidate,
            new HyphenationContextEvidence(null, null, false, false));

        return new HyphenationDecision(evidence, decisionKind);
    }

    private static ProtectedOccurrence CreateOccurrence(
        string id,
        HyphenationCandidate candidate)
    {
        var spans = new List<GroundTruthSourceSpan>();

        foreach (var source in new[]
                 {
                     candidate.LeftSource,
                     candidate.HyphenSource,
                     candidate.RightSource
                 })
        {
            if (spans.Count > 0
                && spans[^1].DocumentPath == source.DocumentPath
                && spans[^1].TextNodeIndex == source.TextNodeIndex
                && spans[^1].Start + spans[^1].Length == source.Start)
            {
                spans[^1] = spans[^1] with { Length = spans[^1].Length + source.Length };
                continue;
            }

            spans.Add(new GroundTruthSourceSpan(
                source.DocumentPath,
                source.TextNodeIndex,
                source.Start,
                source.Length));
        }

        return new ProtectedOccurrence(
            id,
            candidate.HyphenSource.DocumentPath,
            candidate.LeftPart + "-" + candidate.RightPart,
            spans.AsReadOnly());
    }

    private static IReadOnlyList<HyphenationCandidate> Detect(TemporaryEpub epub)
    {
        var logicalText = new EpubPackageReader().Read(epub.Path).LogicalText;
        return new HyphenationDetector().Detect(logicalText);
    }

    private static TemporaryEpub CreateSingleDocumentEpub(string body)
    {
        return TemporaryEpub.Create(
            [new TestDocument("chapter", "chapter.xhtml", Xhtml(body))],
            [new TestSpineItem("chapter")]);
    }

    private static string Xhtml(string body)
    {
        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <!DOCTYPE html>
            <html xmlns="http://www.w3.org/1999/xhtml">
              <head><title>Test</title></head>
              <body>{body}</body>
            </html>
            """;
    }
}
