using EpubFixer.Core.Correction;
using EpubFixer.Core.Correction.Models;
using EpubFixer.Core.Decision;
using EpubFixer.Core.Decision.Models;
using EpubFixer.Core.Detection;
using EpubFixer.Core.Evidence;
using EpubFixer.Core.Epub;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Ocr.Models;
using EpubFixer.Core.Lexicon;

namespace EpubFixer.Core.Ocr;

public sealed class OcrAnalysisService
{
    public OcrAnalysisReport Analyze(string epubPath, ITurkishMorphologyAnalyzer analyzer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(epubPath);
        ArgumentNullException.ThrowIfNull(analyzer);
        var package = new EpubPackageReader().Read(epubPath);

        ApplyExistingHyphenation(package, analyzer);
        var stream = LogicalTextStreamBuilder.Build(package.SpineDocuments);
        return new OcrAnomalyDetector().Analyze(stream, analyzer);
    }

    public OcrCorrectionAnalysisReport AnalyzeCorrections(string epubPath, ITurkishMorphologyAnalyzer analyzer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(epubPath);
        ArgumentNullException.ThrowIfNull(analyzer);
        var package = new EpubPackageReader().Read(epubPath);
        ApplyExistingHyphenation(package, analyzer);
        var stream = LogicalTextStreamBuilder.Build(package.SpineDocuments);
        var analysis = new OcrAnomalyDetector().Analyze(stream, analyzer);
        var lexicon = new BookLexiconBuilder().Build(stream);
        return new OcrCorrectionCandidateGenerator().Generate(analysis, lexicon, analyzer);
    }

    private static void ApplyExistingHyphenation(EpubFixer.Core.Epub.Models.EpubPackage package, ITurkishMorphologyAnalyzer analyzer)
    {
        var original = AnalyzeHyphenation(package.LogicalText);
        Apply(new HyphenationCorrectionApplier(), original.Plans.Where(plan => plan.CorrectionKind == HyphenationCorrectionKind.Inline));
        var afterInline = AnalyzeHyphenation(LogicalTextStreamBuilder.Build(package.SpineDocuments));
        Apply(new CrossParagraphHyphenationCorrectionApplier(), afterInline.Plans.Where(plan => plan.CorrectionKind == HyphenationCorrectionKind.CrossParagraph));
        var afterV1 = LogicalTextStreamBuilder.Build(package.SpineDocuments);
        var v2Candidates = AnalyzeHyphenationV2(afterV1, analyzer);
        Apply(new HyphenationCorrectionApplier(), v2Candidates.Plans.Where(plan => plan.CorrectionKind == HyphenationCorrectionKind.Inline));
        var afterV2Inline = LogicalTextStreamBuilder.Build(package.SpineDocuments);
        var refreshed = AnalyzeHyphenationV2(afterV2Inline, analyzer);
        Apply(new CrossParagraphHyphenationCorrectionApplier(), refreshed.Plans.Where(plan => plan.CorrectionKind == HyphenationCorrectionKind.CrossParagraph));
    }

    private static void Apply<T>(T applier, IEnumerable<HyphenationCorrectionPlan> plans) where T : class
    {
        var materialized = plans.ToArray();
        if (applier is HyphenationCorrectionApplier inline) inline.Apply(materialized);
        else if (applier is CrossParagraphHyphenationCorrectionApplier cross) cross.Apply(materialized);
    }

    private static PipelineState AnalyzeHyphenation(EpubFixer.Core.Epub.Models.LogicalTextStream stream)
    {
        var candidates = new HyphenationDetector().Detect(stream);
        var lexicon = new EpubFixer.Core.Lexicon.BookLexiconBuilder().Build(stream);
        var evidence = new HyphenationEvidenceEvaluator().Evaluate(candidates, lexicon, stream);
        var decisions = new HyphenationDecisionEvaluator().Evaluate(evidence);
        return new PipelineState(new HyphenationCorrectionPlanner().Plan(decisions));
    }

    private static PipelineState AnalyzeHyphenationV2(EpubFixer.Core.Epub.Models.LogicalTextStream stream, ITurkishMorphologyAnalyzer analyzer)
    {
        var baseState = AnalyzeHyphenation(stream);
        var candidates = new HyphenationDetector().Detect(stream);
        var lexicon = new EpubFixer.Core.Lexicon.BookLexiconBuilder().Build(stream);
        var evidence = new HyphenationEvidenceEvaluator().Evaluate(candidates, lexicon, stream);
        var morphology = new EpubFixer.Core.Morphology.HyphenationMorphologyAnalyzer().Analyze(evidence, analyzer);
        var decisions = new EpubFixer.Core.Decision.HyphenationV2DecisionEvaluator().Evaluate(evidence, morphology);
        return new PipelineState(new HyphenationCorrectionPlanner().Plan(decisions));
    }

    private sealed record PipelineState(IReadOnlyList<HyphenationCorrectionPlan> Plans);
}
