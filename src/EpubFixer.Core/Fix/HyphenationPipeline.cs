using EpubFixer.Core.Correction;
using EpubFixer.Core.Correction.Models;
using EpubFixer.Core.Decision;
using EpubFixer.Core.Decision.Models;
using EpubFixer.Core.Detection;
using EpubFixer.Core.Detection.Models;
using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Evidence;
using EpubFixer.Core.Evidence.Models;
using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Morphology.Models;

namespace EpubFixer.Core.Fix;

internal static class HyphenationPipeline
{
    public static HyphenationPipelineState Analyze(LogicalTextStream logicalText)
    {
        var candidates = new HyphenationDetector().Detect(logicalText);
        var lexicon = new BookLexiconBuilder().Build(logicalText);
        var evidence = new HyphenationEvidenceEvaluator().Evaluate(
            candidates,
            lexicon,
            logicalText);
        var decisions = new HyphenationDecisionEvaluator().Evaluate(evidence);
        var plans = new HyphenationCorrectionPlanner().Plan(decisions);

        return new HyphenationPipelineState(candidates, evidence, decisions, plans);
    }

    public static HyphenationPipelineState AnalyzeV2(
        LogicalTextStream logicalText,
        ITurkishMorphologyAnalyzer analyzer)
    {
        var state = Analyze(logicalText);
        var morphology = new HyphenationMorphologyAnalyzer().Analyze(state.Evidence, analyzer);
        var decisions = new HyphenationV2DecisionEvaluator().Evaluate(state.Evidence, morphology);
        var plans = new HyphenationCorrectionPlanner().Plan(decisions);
        return state with { Decisions = decisions, Plans = plans };
    }
}

internal sealed record HyphenationPipelineState(
    IReadOnlyList<HyphenationCandidate> Candidates,
    IReadOnlyList<HyphenationEvidence> Evidence,
    IReadOnlyList<HyphenationDecision> Decisions,
    IReadOnlyList<HyphenationCorrectionPlan> Plans);
