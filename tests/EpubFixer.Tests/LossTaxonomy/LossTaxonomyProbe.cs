using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Lexicon.Models;
using EpubFixer.Core.Ocr;
using EpubFixer.Core.Ocr.Lattice;
using EpubFixer.Core.Ocr.Lattice.Models;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Tests.LossTaxonomy;

/// <summary>
/// The R5.0b measurement instrument (docs/phase-5-plan.md section 7.2). Given the real pipeline's
/// intermediate results for the region a loss case falls into - or the absence of a region, or of
/// a built window - establishes the <see cref="LossProbeSignals"/> and hands them to
/// <see cref="LossTaxonomyClassifier"/>.
///
/// This class touches production types (WordLattice, DecodedPath, AcceptanceResult, BookVocabulary,
/// ILexiconMatcher) but performs no I/O of its own and modifies nothing in src/ - the caller
/// (LossTaxonomyBaselineTests) is responsible for actually running the lattice pipeline over the
/// real book and handing this class the results.
///
/// Two small pieces of CorrectionAcceptanceGate's PRIVATE logic are reproduced here for
/// diagnostic purposes only (documented at each site): the changed-token tokenizer, and the
/// per-span matcher budget formula. Both are copied verbatim from
/// src/EpubFixer.Core/Ocr/Lattice/CorrectionAcceptanceGate.cs and
/// src/EpubFixer.Core/Ocr/Lattice/WordLatticeBuilder.cs respectively; if those change, this copy
/// must be re-synced (there is no shared production seam for a diagnostic tool to hook into
/// without adding a new dependency to src/, which R5.0b's contract forbids).
/// </summary>
public static class LossTaxonomyProbe
{
    /// <summary>The case's region was never produced by <c>OcrRegionDetector</c>.</summary>
    public static LossCaseDiagnosis DiagnoseRegionNotDetected(LossCase lossCase)
    {
        ArgumentNullException.ThrowIfNull(lossCase);
        var signals = new LossProbeSignals(false, false, false, false, null, false, false, null);
        return Build(lossCase, LossTaxonomyClassifier.Classify(signals), gateReason: null, validToken: null,
            targetInLattice: false, arcLength: null, matcherFound: null, targetInVocabulary: null,
            decoderTopReplacement: null, notes: "No detected region contains [logicalStart, logicalStart+original.Length).");
    }

    /// <summary>The region was detected but its window was not built (skipped-too-long or budget-exceeded).</summary>
    public static LossCaseDiagnosis DiagnoseWindowNotBuilt(LossCase lossCase, LatticeBuildOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(lossCase);
        if (outcome == LatticeBuildOutcome.Built)
        {
            throw new ArgumentException("Outcome must not be Built.", nameof(outcome));
        }

        var budgetExceeded = outcome == LatticeBuildOutcome.BudgetExceeded;
        var signals = new LossProbeSignals(true, false, budgetExceeded, false, null, false, false, null);
        return Build(lossCase, LossTaxonomyClassifier.Classify(signals), gateReason: null, validToken: null,
            targetInLattice: false, arcLength: null, matcherFound: null, targetInVocabulary: null,
            decoderTopReplacement: null, notes: $"WordLatticeBuilder outcome was {outcome}.");
    }

    /// <summary>
    /// The full diagnosis for a region whose window WAS built. <paramref name="lattice"/>,
    /// <paramref name="paths"/> and <paramref name="gateResult"/> must be the real, production
    /// values for this exact region (same matcher, same vocabulary, same options the production
    /// lattice engine used) - this method only reads them, it never re-decides anything the real
    /// pipeline already decided.
    /// </summary>
    public static LossCaseDiagnosis DiagnoseBuiltWindow(
        LossCase lossCase,
        BookVocabulary vocabulary,
        ILexiconMatcher matcher,
        LatticeOptions options,
        WordLattice lattice,
        IReadOnlyList<DecodedPath> paths,
        AcceptanceResult gateResult)
    {
        ArgumentNullException.ThrowIfNull(lossCase);
        ArgumentNullException.ThrowIfNull(vocabulary);
        ArgumentNullException.ThrowIfNull(matcher);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(lattice);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(gateResult);
        if (lattice.Outcome != LatticeBuildOutcome.Built)
        {
            throw new ArgumentException("Lattice must be Built.", nameof(lattice));
        }

        var localStart = lossCase.LogicalStart - lattice.WindowOffset;
        var localEnd = localStart + lossCase.Original.Length;
        if (localStart < 0 || localEnd > lattice.Window.Length
            || !string.Equals(lattice.Window.Substring(localStart, lossCase.Original.Length), lossCase.Original, StringComparison.Ordinal))
        {
            // The matched region's window does not actually cover the corrupted span verbatim.
            // This should not happen for a correctly matched region (the window always covers
            // the region it was built from) - if it does, it is a finding, not something to
            // paper over by guessing at coordinates.
            var anomalySignals = new LossProbeSignals(true, true, false, false, LatticeUnreachableCause.PositionUnreachable, false, false, null);
            return Build(lossCase, LossTaxonomyClassifier.Classify(anomalySignals), gateReason: null, validToken: null,
                targetInLattice: false, arcLength: null, matcherFound: null, targetInVocabulary: null,
                decoderTopReplacement: null,
                notes: "ANOMALY: window does not cover the corrupted span at the expected offset; coordinates could not be trusted.");
        }

        var expectedWindowText = lattice.Window[..localStart] + lossCase.Replacement + lattice.Window[localEnd..];
        var arcLength = localEnd - localStart;
        var reachable = IsReachable(lattice.Arcs, lattice.Window.Length, expectedWindowText);

        if (!reachable)
        {
            var (cause, matcherFound, targetInVocabulary) = DiagnoseUnreachable(
                vocabulary, matcher, options, lattice.Arcs, lattice.Window, localStart, arcLength, lossCase.Replacement);
            var signals = new LossProbeSignals(true, true, false, false, cause, false, false, null);
            return Build(lossCase, LossTaxonomyClassifier.Classify(signals), gateReason: null, validToken: null,
                targetInLattice: false, arcLength, matcherFound, targetInVocabulary, decoderTopReplacement: null,
                notes: $"Unreachable cause: {cause}.");
        }

        var targetRankIndex = -1;
        for (var i = 0; i < paths.Count; i++)
        {
            if (string.Equals(paths[i].Text, expectedWindowText, StringComparison.Ordinal))
            {
                targetRankIndex = i;
                break;
            }
        }

        var isTopRank = targetRankIndex == 0;

        if (!isTopRank)
        {
            var rankSignals = new LossProbeSignals(true, true, false, true, null, false, false, null);
            var note = targetRankIndex < 0
                ? $"Target reachable in the full lattice but not within the top-{paths.Count} decoded paths."
                : $"Target ranked #{targetRankIndex + 1} of {paths.Count} decoded paths.";
            return Build(lossCase, LossTaxonomyClassifier.Classify(rankSignals), gateReason: null, validToken: null,
                targetInLattice: true, arcLength, matcherFound: null, targetInVocabulary: null,
                decoderTopReplacement: paths.Count > 0 ? paths[0].Text : null, notes: note);
        }

        var appliedUnexpectedly = gateResult.Verdict == AcceptanceVerdict.Apply;
        var gateReason = gateResult.Reasons.Count > 0 ? gateResult.Reasons[0] : null;
        var finalSignals = new LossProbeSignals(true, true, false, true, null, true, appliedUnexpectedly, gateReason);
        var reason = LossTaxonomyClassifier.Classify(finalSignals);

        string? validToken = null;
        if (!appliedUnexpectedly && string.Equals(gateReason, "OriginalTokenIsValid", StringComparison.Ordinal))
        {
            var changedStart = gateResult.LogicalStart - lattice.WindowOffset;
            var changedEnd = gateResult.LogicalEndExclusive - lattice.WindowOffset;
            validToken = ChangedTokens(lattice.Window, changedStart, changedEnd)
                .FirstOrDefault(token => IsValidOriginalToken(vocabulary, token));
        }

        return Build(lossCase, reason, gateReason, validToken, targetInLattice: true, arcLength,
            matcherFound: null, targetInVocabulary: null, decoderTopReplacement: paths[0].Text,
            notes: appliedUnexpectedly
                ? "ANOMALY: gate applied the target for a case listed as a loss; check region matching."
                : null);
    }

    /// <summary>
    /// Reproduces <c>CorrectionAcceptanceGate.IsValidOriginalToken</c> (private) using only
    /// <see cref="BookVocabulary"/>'s public surface, since the gate itself is not the thing
    /// under diagnosis here.
    /// </summary>
    public static bool IsValidOriginalToken(BookVocabulary vocabulary, string token)
    {
        ArgumentNullException.ThrowIfNull(vocabulary);
        ArgumentNullException.ThrowIfNull(token);
        var entry = vocabulary.Find(token);
        return entry is not null
            && (entry.BookCount >= 2 || entry.Source is VocabularySource.Frequency or VocabularySource.Morphology);
    }

    /// <summary>Reproduces <c>CorrectionAcceptanceGate.ChangedTokens</c> (private, verbatim).</summary>
    public static IEnumerable<string> ChangedTokens(string text, int start, int end)
    {
        ArgumentNullException.ThrowIfNull(text);
        var tokenStart = -1;
        for (var i = start; i < end; i++)
        {
            if (char.IsLetterOrDigit(text[i]) || text[i] is '\'' or '’')
            {
                tokenStart = tokenStart < 0 ? i : tokenStart;
                continue;
            }

            if (tokenStart >= 0)
            {
                yield return text[tokenStart..i];
                tokenStart = -1;
            }
        }

        if (tokenStart >= 0)
        {
            yield return text[tokenStart..end];
        }
    }

    /// <summary>
    /// True if the OCR-corrupted <paramref name="original"/> text, once garbage glyphs
    /// (^ ; : &lt; &gt; ,) are stripped, equals or contains the token the gate found valid. This
    /// is the operational test of the 6.4 hypothesis: that <c>ChangedTokens</c> is finding the
    /// clean word left behind after a garbage glyph is peeled off a corrupted token.
    /// </summary>
    public static bool MatchesGarbageStripHypothesis(string original, string validToken)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(validToken);
        var stripped = new string(original.Where(c => !OcrConfusionSet.Default.IsGarbageGlyph(c)).ToArray());
        return stripped.Contains(validToken, StringComparison.OrdinalIgnoreCase);
    }

    private static (LatticeUnreachableCause Cause, bool? MatcherFound, bool? TargetInVocabulary) DiagnoseUnreachable(
        BookVocabulary vocabulary, ILexiconMatcher matcher, LatticeOptions options, IReadOnlyList<LatticeArc> arcs,
        string window, int localStart, int arcLength, string replacement)
    {
        // Graph reachability (ignores arc.Word content) rather than the separator heuristic
        // WordLatticeBuilder uses to seed its OWN traversal: a position can be a valid word-arc
        // query start node and STILL be unreachable in the decode graph, if the identity/literal
        // run-merging skips straight over it (see LatticeUnreachableCause.PositionUnreachable).
        if (!ComputeReachablePositions(arcs).Contains(localStart))
        {
            return (LatticeUnreachableCause.PositionUnreachable, null, null);
        }

        if (arcLength > options.MaxArcLength)
        {
            return (LatticeUnreachableCause.ExceedsMaxArcLength, null, null);
        }

        var spanSource = window.Substring(localStart, arcLength);
        if (!IsMatchableSpan(spanSource))
        {
            return (LatticeUnreachableCause.SpanNotMatchable, null, null);
        }

        var budget = BudgetFor(spanSource, options);
        var aligner = new WeightedEditAligner();
        if (!aligner.TryAlign(spanSource, replacement, budget, out _))
        {
            return (LatticeUnreachableCause.ExceedsBudget, null, null);
        }

        var matches = matcher.Match(spanSource, budget);
        var matcherFound = matches.Any(match => string.Equals(match.Word, replacement, StringComparison.Ordinal));
        var targetInVocabulary = vocabulary.Find(replacement) is not null;
        // matcherFound=true here (single-arc span matches, in budget, yet still unreachable
        // overall) means the correction genuinely needs more than one arc - a composite
        // reconstruction this single-span heuristic does not model. ExceedsBudget is the closest
        // named cause; targetInVocabulary/matcherFound are reported alongside it so this
        // approximation is visible rather than silently folded away.
        return (matcherFound ? LatticeUnreachableCause.ExceedsBudget : LatticeUnreachableCause.MatcherMissedTarget, matcherFound, targetInVocabulary);
    }

    /// <summary>
    /// Every window position reachable from 0 by following one or more arcs (of any kind), i.e.
    /// exactly the set of positions <c>LatticeDecoder</c> would ever populate a DP state for.
    /// </summary>
    private static HashSet<int> ComputeReachablePositions(IReadOnlyList<LatticeArc> arcs)
    {
        var byFrom = arcs
            .Where(arc => arc.From < arc.To)
            .GroupBy(arc => arc.From)
            .ToDictionary(group => group.Key, group => group.Select(arc => arc.To).Distinct().ToArray());
        var reachable = new HashSet<int> { 0 };
        var queue = new Queue<int>();
        queue.Enqueue(0);
        while (queue.Count > 0)
        {
            var position = queue.Dequeue();
            if (!byFrom.TryGetValue(position, out var destinations))
            {
                continue;
            }

            foreach (var destination in destinations)
            {
                if (reachable.Add(destination))
                {
                    queue.Enqueue(destination);
                }
            }
        }

        return reachable;
    }

    /// <summary>Reproduces <c>WordLatticeBuilder.IsMatchableSpan</c> (private, verbatim).</summary>
    private static bool IsMatchableSpan(string span) =>
        span.Length > 0
        && !char.IsWhiteSpace(span[0])
        && !char.IsWhiteSpace(span[^1])
        && span.Count(char.IsLetterOrDigit) >= 2;

    /// <summary>Reproduces <c>WordLatticeBuilder.BudgetFor</c> (private, verbatim).</summary>
    private static double BudgetFor(string span, LatticeOptions options) =>
        Math.Min(options.BudgetCap, options.BudgetBase + options.BudgetPerFourChars * (span.Length / 4));

    /// <summary>
    /// Whether some sequence of arcs from position 0 to the end of the window concatenates
    /// (via <see cref="LatticeArc.Word"/>) to exactly <paramref name="target"/>. This is the same
    /// search space <c>LatticeDecoder</c> explores, but asks a yes/no reachability question for
    /// one specific text instead of ranking every path by cost.
    /// </summary>
    private static bool IsReachable(IReadOnlyList<LatticeArc> arcs, int windowLength, string target)
    {
        var arcsByFrom = arcs
            .Where(arc => arc.From < arc.To)
            .GroupBy(arc => arc.From)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var memo = new Dictionary<(int Position, int TextPosition), bool>();
        return CanReach(arcsByFrom, windowLength, target, 0, 0, memo);
    }

    private static bool CanReach(
        IReadOnlyDictionary<int, LatticeArc[]> arcsByFrom,
        int windowLength,
        string target,
        int position,
        int textPosition,
        Dictionary<(int, int), bool> memo)
    {
        if (position == windowLength)
        {
            return textPosition == target.Length;
        }

        var key = (position, textPosition);
        if (memo.TryGetValue(key, out var cached))
        {
            return cached;
        }

        memo[key] = false;
        var result = false;
        if (arcsByFrom.TryGetValue(position, out var arcs))
        {
            foreach (var arc in arcs)
            {
                var word = arc.Word;
                if (textPosition + word.Length <= target.Length
                    && string.CompareOrdinal(target, textPosition, word, 0, word.Length) == 0
                    && CanReach(arcsByFrom, windowLength, target, arc.To, textPosition + word.Length, memo))
                {
                    result = true;
                    break;
                }
            }
        }

        memo[key] = result;
        return result;
    }

    private static LossCaseDiagnosis Build(
        LossCase lossCase,
        string lossReason,
        string? gateReason,
        string? validToken,
        bool targetInLattice,
        int? arcLength,
        bool? matcherFound,
        bool? targetInVocabulary,
        string? decoderTopReplacement,
        string? notes) =>
        new(
            lossCase.DocumentPath,
            lossCase.LogicalStart,
            lossCase.Original,
            lossCase.Replacement,
            lossCase.DecisionRule,
            lossReason,
            gateReason,
            validToken,
            targetInLattice,
            arcLength,
            matcherFound,
            targetInVocabulary,
            decoderTopReplacement,
            notes);
}
