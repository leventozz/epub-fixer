using EpubFixer.Core.Epub;
using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Mutation;
using EpubFixer.Core.Mutation.Models;
using EpubFixer.Core.Ocr;
using EpubFixer.Core.Ocr.Lattice;
using EpubFixer.Core.Ocr.Lattice.Models;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Tests.LossTaxonomy;

/// <summary>
/// A test-only <see cref="IOcrCorrectionPlanner"/> that performs the EXACT same steps as
/// <see cref="LatticeOcrCorrectionPlanner"/> (docs/phase-5-plan.md@adc202d forbids touching that class or
/// any other file under src/) but keeps every intermediate per-region result instead of
/// discarding them into a handful of summary strings. Driving <see cref="EpubFixService"/> with
/// this planner instead of the real one reproduces the exact production run (same hyphenation
/// pipeline output feeding the OCR stage - see D66/EpubFixService.Fix) while making that run's
/// internals inspectable for R5.0b's diagnosis.
/// </summary>
public sealed class DiagnosticLatticeOcrCorrectionPlanner : IOcrCorrectionPlanner
{
    private readonly ITurkishFrequencyList frequencyList;
    private readonly Func<BookVocabulary, ILexiconMatcher> matcherFactory;
    private readonly LatticeOptions options;

    public DiagnosticLatticeOcrCorrectionPlanner(ITurkishFrequencyList frequencyList, Func<BookVocabulary, ILexiconMatcher> matcherFactory, LatticeOptions options)
    {
        this.frequencyList = frequencyList ?? throw new ArgumentNullException(nameof(frequencyList));
        this.matcherFactory = matcherFactory ?? throw new ArgumentNullException(nameof(matcherFactory));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public LogicalTextStream? CapturedStream { get; private set; }

    public BookVocabulary? CapturedVocabulary { get; private set; }

    public ILexiconMatcher? CapturedMatcher { get; private set; }

    public IReadOnlyList<RegionDecision> CapturedDecisions { get; private set; } = Array.Empty<RegionDecision>();

    public OcrCorrectionPlanResult CreatePlan(LogicalTextStream stream, IMorphologyOracleBuilder oracleBuilder)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(oracleBuilder);

        CapturedStream = stream;
        var knowledge = new BookKnowledgeBuilder(frequencyList).Build(stream, oracleBuilder);
        CapturedVocabulary = knowledge.Vocabulary;
        var matcher = matcherFactory(knowledge.Vocabulary);
        CapturedMatcher = matcher;
        var builder = new WordLatticeBuilder(matcher, LogicalTextStreamBoundaries.HardOffsets(stream));
        var decoder = new LatticeDecoder(knowledge.LanguageModel, options);
        var gate = new CorrectionAcceptanceGate(knowledge.Vocabulary, options);

        var decisions = new List<RegionDecision>(knowledge.Regions.Count);
        var corrections = new List<RegionCorrection>();
        foreach (var region in knowledge.Regions)
        {
            var lattice = builder.Build(region, stream.Text, options);
            var paths = decoder.Decode(lattice, 3);
            var result = gate.Evaluate(region, lattice, paths);
            decisions.Add(new RegionDecision(region, lattice, paths, result));
            if (result.Verdict == AcceptanceVerdict.Apply)
            {
                corrections.Add(new RegionCorrection(result.LogicalStart, result.LogicalEndExclusive, result.Replacement!, result));
            }
        }

        CapturedDecisions = decisions;
        var planResult = new RegionMutationPlanner().Create(corrections, stream);
        return new OcrCorrectionPlanResult(planResult.Plan, OcrCorrectionEngine.Lattice, Array.Empty<string>());
    }

    public sealed record RegionDecision(
        CorruptedTextRegion Region,
        WordLattice Lattice,
        IReadOnlyList<DecodedPath> Paths,
        AcceptanceResult Result);
}
