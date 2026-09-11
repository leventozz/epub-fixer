using EpubFixer.Core.Decision;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Tests;

public sealed class OcrDecisionReportingTests
{
    [Fact]
    public void SerializeMarkdownIncludesDecisionSummaryAndRegressionSection()
    {
        var candidate = new OcrWordCandidate("liyatro", 0, [], "test.xhtml", "", "");
        var evidence = new OcrWordEvidence(candidate, 1, "liyatro", 1, false,
            [OcrDetectionReason.MorphologyInvalid, OcrDetectionReason.RareInBook], OcrConfidence.EvidenceOnly);
        var occurrence = new OcrCorrectionOccurrence(evidence, candidate, "", "liyatro", "", []);
        var analysis = new OcrCorrectionAnalysisReport(
            new OcrAnalysisReport(1, 1, 0, [evidence], [], 0), [occurrence], []);
        var decisions = new OcrCorrectionDecisionEvaluator().Evaluate(analysis);

        var markdown = OcrDecisionReporting.SerializeMarkdown(decisions);

        Assert.Contains("AutoFixCandidate: 0", markdown);
        Assert.Contains("Review: 0", markdown);
        Assert.Contains("Defer: 1", markdown);
        Assert.Contains("## Decision Regression Examples", markdown);
        Assert.Contains("## AutoFix Audit Risk Groups", markdown);
        Assert.Contains("## All AutoFix Candidates", markdown);
        Assert.Contains("| # | Source | SelectedProposal |", markdown);
        Assert.Contains("`onlarm`", markdown);
    }
}
