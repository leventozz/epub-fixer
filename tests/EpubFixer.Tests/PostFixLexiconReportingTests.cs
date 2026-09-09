using EpubFixer.Core.Correction;
using EpubFixer.Core.Correction.Models;
using EpubFixer.Core.Decision;
using EpubFixer.Core.Decision.Models;
using EpubFixer.Core.Detection;
using EpubFixer.Core.Epub;
using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Evidence;
using EpubFixer.Core.Lexicon;

namespace EpubFixer.Tests;

public sealed class PostFixLexiconReportingTests
{
    [Fact]
    public void RealEpub_ReportMatchesPostFixStatesAndAnswersEvidenceQuestion()
    {
        var epubPath = Path.Combine(
            AppContext.BaseDirectory,
            "test-data",
            "Odun Kesmek_recognized.epub");
        var package = new EpubPackageReader().Read(epubPath);
        var original = Analyze(package.LogicalText);
        var inlinePlans = original.Plans
            .Where(item => item.CorrectionKind == HyphenationCorrectionKind.Inline)
            .ToArray();
        var inlineResult = new HyphenationCorrectionApplier().Apply(inlinePlans);
        var afterInline = Analyze(LogicalTextStreamBuilder.Build(package.SpineDocuments));
        var crossPlans = afterInline.Plans
            .Where(item => item.CorrectionKind == HyphenationCorrectionKind.CrossParagraph)
            .ToArray();
        var crossResult = new CrossParagraphHyphenationCorrectionApplier().Apply(crossPlans);
        var final = Analyze(LogicalTextStreamBuilder.Build(package.SpineDocuments));
        var analysis = new PostFixLexiconAnalysisResult(
            original,
            inlinePlans,
            inlineResult,
            afterInline,
            crossPlans,
            crossResult,
            final);

        var markdown = PostFixLexiconReporting.SerializeMarkdown(analysis);

        Assert.Equal(148, inlinePlans.Length + crossPlans.Length);
        Assert.Equal(129, inlineResult.AppliedCount);
        Assert.Equal(19, crossResult.AppliedCount);
        Assert.Equal(300, final.Candidates.Count);
        Assert.Contains("| `Auersberger` | 202 | 223 | +21 |", markdown);
        Assert.Contains("| `Joana` | 75 | 78 | +3 |", markdown);
        Assert.Contains("| `Auersberger` | 202 | 243 | +41 |", markdown);
        Assert.Contains("| `Joana` | 75 | 87 | +12 |", markdown);
        Assert.Contains("| Lexicon count = 0 | 240 |", markdown);
        Assert.Contains("| Lexicon count = 1 | 16 |", markdown);
        Assert.Contains("| Lexicon count 2-4 | 23 |", markdown);
        Assert.Contains("| Lexicon count 5-9 | 20 |", markdown);
        Assert.Contains("| Lexicon count 10-49 | 1 |", markdown);
        Assert.Contains("| Lexicon count >= 50 | 0 |", markdown);
        Assert.Contains("İlk 148 düzeltme yeni güçlü exact-lexicon evidence oluşturmadı.", markdown);
        Assert.Contains("Final AutoFixCandidate: 0", markdown);
    }

    private static HyphenationPipelineResult Analyze(LogicalTextStream stream)
    {
        var candidates = new HyphenationDetector().Detect(stream);
        var lexicon = new BookLexiconBuilder().Build(stream);
        var evidence = new HyphenationEvidenceEvaluator().Evaluate(candidates, lexicon, stream);
        var decisions = new HyphenationDecisionEvaluator().Evaluate(evidence);
        var plans = new HyphenationCorrectionPlanner().Plan(decisions);
        return new HyphenationPipelineResult(stream, lexicon, evidence, candidates, decisions, plans);
    }
}
