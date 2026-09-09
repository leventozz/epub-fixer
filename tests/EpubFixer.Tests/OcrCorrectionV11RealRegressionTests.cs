using EpubFixer.Core.Ocr;
using EpubFixer.Core.Ocr.Models;
using EpubFixer.TrMorph;

namespace EpubFixer.Tests;

public sealed class OcrCorrectionV11RealRegressionTests
{
    [Fact]
    public void RealBook_PreservesStructuralCoverageAndRequiredTargets()
    {
        var epubPath = Path.Combine(AppContext.BaseDirectory, "test-data", "Odun Kesmek_recognized.epub");
        using var analyzer = new FomaTurkishMorphologyAnalyzer();

        var report = new OcrAnalysisService().AnalyzeCorrections(epubPath, analyzer);

        Assert.Equal(46_927, report.SourceAnalysis.TotalExamined);
        Assert.Equal(137, report.SourceAnalysis.Candidates.Count(item => item.Confidence == OcrConfidence.High));
        Assert.Equal(80, report.SourceAnalysis.Candidates.Count(item => item.Confidence == OcrConfidence.Medium));
        var highWithoutProposal = report.Occurrences
            .Where(item => item.Source.Confidence == OcrConfidence.High && item.Proposals.Count == 0)
            .Select(item => item.Source.Candidate.Text)
            .ToArray();
        Assert.True(highWithoutProposal.Length == 0,
            $"HIGH occurrences without proposals: {string.Join(", ", highWithoutProposal)}");
        Assert.All(report.Occurrences.Where(item => item.Source.Confidence == OcrConfidence.Medium),
            item => Assert.NotEmpty(item.Proposals));

        AssertTarget(report, "y1pranmışt1", "yıpranmıştı", true);
        AssertTarget(report, "ilgi-1 iydi", "ilgiliydi", true);
        AssertTarget(report, "liyatro", "tiyatro", true);
        AssertTarget(report, "Joana'mn", "Joana'nın", null);
        AssertTarget(report, "J3arış", "Barış", true);
        AssertTarget(report, "akşaın", "akşam", true);
        var eiles = Assert.Single(report.TargetRegressionExamples, item => item.Query == "Eiles");
        Assert.True(eiles.Found);
        Assert.Equal("Eiles", eiles.BaseForm);
        Assert.True(eiles.BaseFormFrequency > eiles.ExactBookFrequency);
        var span = Assert.Single(report.TargetRegressionExamples, item => item.Query == "^iddetli");
        Assert.True(span.Found);
        Assert.NotEmpty(span.Proposals);
    }

    private static void AssertTarget(
        EpubFixer.Core.Ocr.Models.OcrCorrectionAnalysisReport report,
        string query,
        string proposal,
        bool? trMorphValid)
    {
        var target = Assert.Single(report.TargetRegressionExamples, item => item.Query == query);
        Assert.True(target.Found);
        Assert.Contains(target.Proposals, item => item.ProposedText == proposal
            && (trMorphValid is null || item.TrMorphValid == trMorphValid));
    }
}
