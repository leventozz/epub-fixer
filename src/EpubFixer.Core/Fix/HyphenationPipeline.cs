using EpubFixer.Core.Correction;
using EpubFixer.Core.Correction.Models;
using EpubFixer.Core.Decision;
using EpubFixer.Core.Decision.Models;
using EpubFixer.Core.Detection;
using EpubFixer.Core.Detection.Models;
using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Evidence;
using EpubFixer.Core.Lexicon;

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

        return new HyphenationPipelineState(candidates, decisions, plans);
    }
}

internal sealed record HyphenationPipelineState(
    IReadOnlyList<HyphenationCandidate> Candidates,
    IReadOnlyList<HyphenationDecision> Decisions,
    IReadOnlyList<HyphenationCorrectionPlan> Plans);
