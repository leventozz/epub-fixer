using EpubFixer.Core.Ocr;
using EpubFixer.Core.Ocr.Models;
using EpubFixer.Core.Decision;
using EpubFixer.Core.Decision.Models;
using EpubFixer.TrMorph;
using System.Text.RegularExpressions;

namespace EpubFixer.Tests;

public sealed class OcrCorrectionV111RealRegressionTests
{
    [Fact]
    public void RealBook_PreservesStructuralCoverageAndRequiredTargets()
    {
        var epubPath = Path.Combine(AppContext.BaseDirectory, "test-data", "Odun Kesmek_recognized.epub");
        using var analyzer = new FomaTurkishMorphologyAnalyzer();

        var report = new OcrAnalysisService().AnalyzeCorrections(epubPath, analyzer);

        Assert.Equal(46_927, report.SourceAnalysis.TotalExamined);
        Assert.Equal(893, report.SourceAnalysis.Candidates.Count);
        Assert.Equal(137, report.SourceAnalysis.Candidates.Count(item => item.Confidence == OcrConfidence.High));
        Assert.Equal(80, report.SourceAnalysis.Candidates.Count(item => item.Confidence == OcrConfidence.Medium));
        Assert.Equal(676, report.SourceAnalysis.Candidates.Count(item => item.Confidence == OcrConfidence.EvidenceOnly));
        Assert.Equal(136, report.SourceAnalysis.Candidates.Count(item => item.BaseFormFrequency > 1));
        Assert.Equal(126, report.SourceAnalysis.Candidates.Count(IsPreviouslySuppressedByBaseFormFrequency));
        Assert.Empty(report.SourceAnalysis.RareInBookSuppressedOccurrences);
        Assert.Equal(642, report.Occurrences.Count(item => item.Proposals.Count > 0));
        Assert.Equal(251, report.Occurrences.Count(item => item.Proposals.Count == 0));
        var proposals = report.Occurrences.SelectMany(item => item.Proposals).ToArray();
        Assert.Equal(464, proposals.Count(item => item.StructuralTransformationCount > 1));
        Assert.Equal(296, proposals.Count(item => item.StructuralTransformationCount > 1 && item.TrMorphValid));
        Assert.Equal(2, proposals.Count(IsAdjacent));
        Assert.Equal(2, proposals.Count(item => IsAdjacent(item) && item.TrMorphValid));
        var caretProposals = report.Occurrences
            .Where(item => item.WorkingSource.Text.Contains('^'))
            .SelectMany(item => item.Proposals)
            .Where(item => item.GenerationReasons.Contains(OcrCorrectionGenerationReason.GlyphSubstitution)
                && item.ProposedText.Contains('ş'))
            .ToArray();
        Assert.Equal(80, caretProposals.Length);
        Assert.Equal(56, caretProposals.Count(item => item.TrMorphValid));
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
        AssertTarget(report, "Joana'mn", "Joana'nın", false);
        AssertTarget(report, "J3arış", "Barış", true);
        AssertTarget(report, "akşaın", "akşam", true);
        AssertTarget(report, "ınetre", "metre", true);
        AssertTarget(report, "Viya-ııa'da", "Viyana'da", true);
        AssertTarget(report, "Avııstıırya'nın", "Avusturya'nın", true);
        AssertTarget(report, "l<ilb'de", "Kilb'de", null);
        var joana = Assert.Single(report.Occurrences, item => item.Source.Candidate.Text == "Joana'mn");
        Assert.Equal(OcrConfidence.EvidenceOnly, joana.Source.Confidence);
        Assert.Equal("Joana", joana.Source.BaseForm);
        Assert.Equal(271, joana.Source.BaseFormFrequency);
        var eiles = Assert.Single(report.TargetRegressionExamples, item => item.Query == "Eiles");
        Assert.True(eiles.Found);
        Assert.Equal(1, eiles.ExactBookFrequency);
        Assert.Equal("Eiles", eiles.BaseForm);
        Assert.Equal(3, eiles.BaseFormFrequency);
        Assert.Contains(report.Occurrences, item => item.Source.Candidate.Text == "Eiles"
            && item.Source.Confidence == OcrConfidence.EvidenceOnly);
        var span = Assert.Single(report.TargetRegressionExamples, item => item.Query == "^iddetli");
        Assert.True(span.Found);
        Assert.Contains(span.Proposals, item => item.ProposedText == "şiddetli"
            && item.TrMorphValid
            && item.GenerationReasons.Contains(OcrCorrectionGenerationReason.GlyphSubstitution));
        AssertMissingProposal(report, "Lckrarlayan", "tekrarlayan");
        AssertMissingProposal(report, "ger-^ .ckten", "gerçekten");
        AssertMissingProposal(report, "ço-nıktu", "çocuktu");
    }

    [Fact]
    public void RealBook_DecisionV11MatchesSafetyRegressionSet()
    {
        var epubPath = Path.Combine(AppContext.BaseDirectory, "test-data", "Odun Kesmek_recognized.epub");
        using var analyzer = new FomaTurkishMorphologyAnalyzer();
        var candidates = new OcrAnalysisService().AnalyzeCorrections(epubPath, analyzer);
        var report = new OcrCorrectionDecisionEvaluator().Evaluate(candidates);

        Assert.Equal(893, report.Decisions.Count);
        Assert.Equal(123, report.Decisions.Count(item => item.DecisionKind == OcrCorrectionDecisionKind.AutoFixCandidate));
        Assert.Equal(381, report.Decisions.Count(item => item.DecisionKind == OcrCorrectionDecisionKind.Review));
        Assert.Equal(389, report.Decisions.Count(item => item.DecisionKind == OcrCorrectionDecisionKind.Defer));

        AssertDecision(report, "y1pranmışt1", OcrCorrectionDecisionKind.AutoFixCandidate, "yıpranmıştı");
        AssertDecision(report, "^iddetli", OcrCorrectionDecisionKind.AutoFixCandidate, "şiddetli");
        AssertDecision(report, "ilgi-1 iydi", OcrCorrectionDecisionKind.AutoFixCandidate, "ilgiliydi");
        AssertDecision(report, "liyatro", OcrCorrectionDecisionKind.AutoFixCandidate, "tiyatro");
        AssertDecision(report, "akşaın", OcrCorrectionDecisionKind.Review, null);
        AssertDecision(report, "ınetre", OcrCorrectionDecisionKind.Review, null);
        AssertDecision(report, "J3arış", OcrCorrectionDecisionKind.AutoFixCandidate, "Barış");
        AssertDecision(report, "Viya-ııa'da", OcrCorrectionDecisionKind.AutoFixCandidate, "Viyana'da");
        AssertDecision(report, "Avııstıırya'nın", OcrCorrectionDecisionKind.AutoFixCandidate, "Avusturya'nın");
        AssertDecision(report, "l<ilb'de", OcrCorrectionDecisionKind.AutoFixCandidate, "Kilb'de");
        AssertDecision(report, "Joana'mn", OcrCorrectionDecisionKind.Review, null);
        Assert.All(report.Decisions.Where(item => item.DecisionKind == OcrCorrectionDecisionKind.AutoFixCandidate), item =>
        {
            if (item.DecisionReasons.Contains(OcrCorrectionDecisionReason.CaseCompatible))
                Assert.True(item.SelectedProposal!.CaseCompatible);
        });
        var markdown = OcrDecisionReporting.SerializeMarkdown(report);
        Assert.Contains("## AutoFix Audit Risk Groups", markdown);
        Assert.Contains("## All AutoFix Candidates", markdown);
        Assert.Equal(123, markdown.Split('\n').Count(line => Regex.IsMatch(line, "^\\|\\s*\\d+\\s*\\|")));

        foreach (var query in new[] { "Eiles", "Metis", "Akzente", "Stallburg", "Eine", "Stefan" })
            Assert.DoesNotContain(report.Decisions, item => item.SourceOccurrence.Source.Candidate.Text == query
                && item.DecisionKind == OcrCorrectionDecisionKind.AutoFixCandidate);
        AssertDecision(report, "onlarm", OcrCorrectionDecisionKind.Review, null);
        AssertDecision(report, "Lckrarlayan", OcrCorrectionDecisionKind.Defer, null);
        AssertDecision(report, "ger-^ .ckten", OcrCorrectionDecisionKind.Defer, null);
        AssertDecision(report, "ço-nıktu", OcrCorrectionDecisionKind.Review, null);
    }

    private static void AssertDecision(
        OcrCorrectionDecisionAnalysisReport report,
        string query,
        OcrCorrectionDecisionKind kind,
        string? proposal)
    {
        var target = report.SourceAnalysis.TargetRegressionExamples.FirstOrDefault(item => item.Query == query);
        var decision = report.Decisions.FirstOrDefault(item =>
            item.SourceOccurrence.Source.Candidate.Text == query
            || item.SourceOccurrence.WorkingSource.Text == query
            || target is not null && target.Proposals.Any(candidate => item.SourceOccurrence.Proposals.Contains(candidate)));
        Assert.NotNull(decision);
        Assert.Equal(kind, decision!.DecisionKind);
        if (proposal is null)
            Assert.Null(decision.SelectedProposal);
        else
            Assert.Equal(proposal, decision.SelectedProposal!.Proposal.ProposedText);
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

    private static void AssertMissingProposal(
        EpubFixer.Core.Ocr.Models.OcrCorrectionAnalysisReport report,
        string query,
        string proposal)
    {
        var target = Assert.Single(report.TargetRegressionExamples, item => item.Query == query);
        Assert.True(target.Found);
        Assert.DoesNotContain(target.Proposals, item => item.ProposedText == proposal);
    }

    private static bool IsAdjacent(OcrCorrectionCandidate candidate) =>
        candidate.GenerationReasons.Contains(OcrCorrectionGenerationReason.AdjacentFragmentComposition);

    private static bool IsPreviouslySuppressedByBaseFormFrequency(OcrWordEvidence candidate) =>
        candidate.Confidence == OcrConfidence.EvidenceOnly
        && !candidate.TrMorphValid
        && candidate.BookFrequency == 1
        && candidate.BaseFormFrequency > 1;
}
