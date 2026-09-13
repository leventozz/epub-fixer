using EpubFixer.Core.Epub;
using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Mutation;
using EpubFixer.Core.Mutation.Models;
using EpubFixer.Core.Ocr.Lattice;
using EpubFixer.Core.Ocr.Lattice.Models;

namespace EpubFixer.Core.Ocr;

/// <summary>
/// Runs the lattice reconstruction engine over a book's regions and turns every
/// <see cref="AcceptanceVerdict.Apply"/> verdict into a region correction, exactly the
/// setup <c>debug-lattice</c> exercises. The lexicon matcher is a detail (SymSpell today)
/// injected as a factory so this class never depends on it directly (D58) - Core stays
/// free of that dependency.
/// </summary>
public sealed class LatticeOcrCorrectionPlanner : IOcrCorrectionPlanner
{
    private readonly ITurkishFrequencyList frequencyList;
    private readonly Func<BookVocabulary, ILexiconMatcher> matcherFactory;
    private readonly LatticeOptions options;

    public LatticeOcrCorrectionPlanner(
        ITurkishFrequencyList frequencyList,
        Func<BookVocabulary, ILexiconMatcher> matcherFactory,
        LatticeOptions? options = null)
    {
        this.frequencyList = frequencyList ?? throw new ArgumentNullException(nameof(frequencyList));
        this.matcherFactory = matcherFactory ?? throw new ArgumentNullException(nameof(matcherFactory));
        this.options = options ?? new LatticeOptions();
    }

    public OcrCorrectionPlanResult CreatePlan(LogicalTextStream stream, IMorphologyOracleBuilder oracleBuilder)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(oracleBuilder);

        var knowledge = new BookKnowledgeBuilder(frequencyList).Build(stream, oracleBuilder);
        var matcher = matcherFactory(knowledge.Vocabulary);
        var reconstructor = new LatticeRegionReconstructor(
            stream.Text,
            new WordLatticeBuilder(matcher, LogicalTextStreamBoundaries.HardOffsets(stream)),
            new LatticeDecoder(knowledge.LanguageModel, options),
            new CorrectionAcceptanceGate(knowledge.Vocabulary, options),
            options);

        var corrections = new List<RegionCorrection>();
        var reviewCount = 0;
        foreach (var region in knowledge.Regions)
        {
            var result = reconstructor.Evaluate(region);
            if (result.Verdict == AcceptanceVerdict.Apply)
            {
                corrections.Add(new RegionCorrection(result.LogicalStart, result.LogicalEndExclusive, result.Replacement!, result));
            }
            else if (result.Verdict == AcceptanceVerdict.Review)
            {
                // Review decisions are audit-only: they never reach the mutation plan.
                reviewCount++;
            }
        }

        var planResult = new RegionMutationPlanner().Create(corrections, stream);
        var statistics = reconstructor.Statistics;
        var diagnostics = new List<string>
        {
            $"regions={statistics.Regions}; built={statistics.Built}; skippedTooLong={statistics.SkippedTooLong}; "
                + $"budgetExceeded={statistics.BudgetExceeded}; applied={statistics.Applied}; reviewed={statistics.Reviewed}; left={statistics.Left}",
            $"review decisions not applied: {reviewCount}"
        };
        diagnostics.AddRange(planResult.Skipped
            .GroupBy(item => item.Reason)
            .OrderBy(group => group.Key)
            .Select(group => $"skipped ({group.Key}): {group.Count()}"));

        return new OcrCorrectionPlanResult(planResult.Plan, OcrCorrectionEngine.Lattice, diagnostics);
    }
}
