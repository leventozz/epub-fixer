using System.Text;
using EpubFixer.Core.Decision.Models;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Core.Decision;

public sealed class OcrCorrectionDecisionEvaluator
{
    public OcrCorrectionDecisionAnalysisReport Evaluate(OcrCorrectionAnalysisReport analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        var provisional = analysis.Occurrences
            .Select(EvaluateOccurrence)
            .ToArray();
        var consumed = provisional
            .Where(item => item.DecisionKind == OcrCorrectionDecisionKind.AutoFixCandidate
                && item.DecisionReasons.Contains(OcrCorrectionDecisionReason.AdjacentCompositeRepair))
            .SelectMany(item => item.SelectedProposal!.Proposal.ConsumedSources.Skip(1))
            .ToArray();
        var decisions = provisional.Select(item =>
        {
            if (item.DecisionReasons.Contains(OcrCorrectionDecisionReason.AdjacentCompositeRepair)
                || !consumed.Any(source => SameGeometry(source, item.SourceOccurrence.Source.Candidate)))
                return item;

            var reasons = item.DecisionReasons.Append(OcrCorrectionDecisionReason.ConsumedByCompositeRepair);
            var competing = item.SelectedProposal is null
                ? item.CompetingProposals
                : item.CompetingProposals.Append(item.SelectedProposal).ToArray();
            return item with
            {
                DecisionKind = OcrCorrectionDecisionKind.Review,
                SelectedProposal = null,
                DecisionReasons = reasons.Distinct().OrderBy(reason => reason).ToArray(),
                CompetingProposals = competing
            };
        }).ToArray();
        return new OcrCorrectionDecisionAnalysisReport(analysis, decisions);
    }

    private static OcrCorrectionDecision EvaluateOccurrence(OcrCorrectionOccurrence occurrence)
    {
        var sourceCase = GetCasePattern(occurrence.WorkingSource.Text);
        var evidence = occurrence.Proposals
            .Select(proposal => new OcrCorrectionProposalSafetyEvidence(
                proposal,
                GetCasePattern(proposal.ProposedText),
                IsCaseCompatible(occurrence, sourceCase, proposal),
                SameApostropheBase(occurrence.Source.Candidate.Text, proposal.ProposedText)))
            .ToArray();
        var reasons = new HashSet<OcrCorrectionDecisionReason>();

        if (evidence.Length == 0)
            return Create(occurrence, sourceCase, OcrCorrectionDecisionKind.Defer, null, [], [OcrCorrectionDecisionReason.NoProposal]);

        var structural = EvaluateStructural(occurrence, sourceCase, evidence, reasons);
        if (structural is not null)
            return structural;

        var apostrophe = EvaluateApostrophe(occurrence, sourceCase, evidence, reasons);
        if (apostrophe is not null)
            return apostrophe;

        var lexical = EvaluateEvidenceOnly(occurrence, sourceCase, evidence, reasons);
        if (lexical is not null)
            return lexical;

        var usable = evidence.Where(IsUsable).ToArray();
        if (usable.Length == 0)
        {
            if (evidence.All(item => item.Proposal.IsPartialStructuralRepair))
                reasons.Add(OcrCorrectionDecisionReason.PartialRepairOnly);
            if (evidence.All(item => !item.CaseCompatible))
                reasons.Add(OcrCorrectionDecisionReason.CaseMismatch);
            reasons.Add(OcrCorrectionDecisionReason.NoValidProposal);
            if (IsProperNameRisk(sourceCase)) reasons.Add(OcrCorrectionDecisionReason.ProperNameRisk);
            return Create(occurrence, sourceCase, OcrCorrectionDecisionKind.Defer, null, evidence, reasons);
        }

        if (IsProperNameRisk(sourceCase)) reasons.Add(OcrCorrectionDecisionReason.ProperNameRisk);
        if (usable.Length > 1) reasons.Add(OcrCorrectionDecisionReason.AmbiguousCandidates);
        if (occurrence.Source.Confidence == OcrConfidence.EvidenceOnly)
            reasons.Add(OcrCorrectionDecisionReason.InsufficientFrequencyDominance);
        return Create(occurrence, sourceCase, OcrCorrectionDecisionKind.Review, null, usable, reasons);
    }

    private static OcrCorrectionDecision? EvaluateStructural(
        OcrCorrectionOccurrence occurrence,
        OcrCasePattern sourceCase,
        IReadOnlyList<OcrCorrectionProposalSafetyEvidence> evidence,
        ISet<OcrCorrectionDecisionReason> reasons)
    {
        if (!HasStructuralSourceEvidence(occurrence)) return null;

        var composite = evidence.Where(item =>
            item.Proposal.ConsumesMultipleOccurrences
            && !item.Proposal.IsPartialStructuralRepair
            && item.Proposal.TrMorphValid
            && item.CaseCompatible
            && item.Proposal.GenerationReasons.Contains(OcrCorrectionGenerationReason.AdjacentFragmentComposition)).ToArray();
        if (composite.Length > 0)
        {
            var winner = Best(composite);
            var ties = composite.Where(item => SameSafety(item, winner)).ToArray();
            if (ties.Length == 1)
            {
                reasons.Add(OcrCorrectionDecisionReason.AdjacentCompositeRepair);
                reasons.Add(OcrCorrectionDecisionReason.UniqueStructuralWinner);
                reasons.Add(OcrCorrectionDecisionReason.CaseCompatible);
                return Create(occurrence, sourceCase, OcrCorrectionDecisionKind.AutoFixCandidate, winner,
                    composite.Where(item => !ReferenceEquals(item, winner)).ToArray(), reasons);
            }
            reasons.Add(OcrCorrectionDecisionReason.AmbiguousCandidates);
            return Create(occurrence, sourceCase, OcrCorrectionDecisionKind.Review, null, ties, reasons);
        }

        var structural = evidence.Where(item =>
            !item.Proposal.IsPartialStructuralRepair
            && HasStructuralGeneration(item.Proposal)
            && item.CaseCompatible
            && (item.Proposal.TrMorphValid || IsStructuralApostropheException(occurrence, item))).ToArray();
        if (ContainsAnyApostrophe(occurrence.Source.Candidate.Text)
            && structural.Length == 0
            && evidence.Any(item => !item.Proposal.TrMorphValid && HasStructuralGeneration(item.Proposal)
                && item.Proposal.GenerationReasons.Contains(OcrCorrectionGenerationReason.BookLexiconNeighbor)
                && item.CaseCompatible && !SameApostropheSuffix(occurrence.Source.Candidate.Text, item.Proposal.ProposedText)))
            reasons.Add(OcrCorrectionDecisionReason.ApostropheSuffixChangeUnsafe);
        if (structural.Length == 0) return null;

        var structuralWinner = Best(structural);
        var structuralTies = structural.Where(item => SameSafety(item, structuralWinner)).ToArray();
        if (structuralTies.Length != 1)
        {
            reasons.Add(OcrCorrectionDecisionReason.AmbiguousCandidates);
            return Create(occurrence, sourceCase, OcrCorrectionDecisionKind.Review, null, structuralTies, reasons);
        }

        reasons.Add(OcrCorrectionDecisionReason.UniqueStructuralWinner);
        reasons.Add(OcrCorrectionDecisionReason.CaseCompatible);
        reasons.Add(structuralWinner.Proposal.GenerationReasons.Contains(OcrCorrectionGenerationReason.BookLexiconNeighbor)
            ? OcrCorrectionDecisionReason.StructuralLexiconRepair
            : OcrCorrectionDecisionReason.DirectStructuralRepair);

        if (sourceCase == OcrCasePattern.TitleCase)
        {
            var properNameCompetitors = evidence
                .Where(item => !ReferenceEquals(item, structuralWinner)
                    && IsTitleCaseSafetyCompetitor(structuralWinner, item))
                .ToArray();
            if (properNameCompetitors.Length > 0)
            {
                reasons.Add(OcrCorrectionDecisionReason.ProperNameStructuralAmbiguity);
                reasons.Add(OcrCorrectionDecisionReason.AmbiguousCandidates);
                var review = Create(occurrence, sourceCase, OcrCorrectionDecisionKind.Review, null,
                    [structuralWinner, .. properNameCompetitors], reasons);
                return review with { ProvisionalSelectedProposal = structuralWinner };
            }
        }

        return Create(occurrence, sourceCase, OcrCorrectionDecisionKind.AutoFixCandidate, structuralWinner,
            structural.Where(item => !ReferenceEquals(item, structuralWinner)).ToArray(), reasons);
    }

    private static bool IsTitleCaseSafetyCompetitor(
        OcrCorrectionProposalSafetyEvidence selected,
        OcrCorrectionProposalSafetyEvidence competitor) =>
        competitor.CaseCompatible
        && !competitor.Proposal.IsPartialStructuralRepair
        && competitor.Proposal.BookFrequency > 0
        && competitor.Proposal.EditDistance <= selected.Proposal.EditDistance
        && competitor.Proposal.GenerationCost <= selected.Proposal.GenerationCost
        && SameSourceSpans(selected.Proposal, competitor.Proposal);

    private static bool SameSourceSpans(OcrCorrectionCandidate left, OcrCorrectionCandidate right) =>
        left.ConsumedSources.Count == right.ConsumedSources.Count
        && left.ConsumedSources.Zip(right.ConsumedSources)
            .All(pair => SameGeometry(pair.First, pair.Second));

    private static OcrCorrectionDecision? EvaluateApostrophe(
        OcrCorrectionOccurrence occurrence,
        OcrCasePattern sourceCase,
        IReadOnlyList<OcrCorrectionProposalSafetyEvidence> evidence,
        ISet<OcrCorrectionDecisionReason> reasons)
    {
        if (!occurrence.Source.Candidate.Text.Contains('\'') && !occurrence.Source.Candidate.Text.Contains('’')) return null;
        if (occurrence.Source.BookFrequency > 1 || occurrence.Source.BaseFormFrequency <= 1) return null;
        var candidates = evidence.Where(item =>
            item.SameApostropheBase && item.CaseCompatible && !item.Proposal.IsPartialStructuralRepair
            && item.Proposal.EditDistance <= 2 && item.Proposal.BookFrequency >= 5).ToArray();
        if (candidates.Length == 0) return null;
        reasons.Add(OcrCorrectionDecisionReason.SameApostropheBaseReviewOnly);
        reasons.Add(OcrCorrectionDecisionReason.CaseCompatible);
        return Create(occurrence, sourceCase, OcrCorrectionDecisionKind.Review, null, candidates, reasons);
    }

    private static OcrCorrectionDecision? EvaluateEvidenceOnly(
        OcrCorrectionOccurrence occurrence,
        OcrCasePattern sourceCase,
        IReadOnlyList<OcrCorrectionProposalSafetyEvidence> evidence,
        ISet<OcrCorrectionDecisionReason> reasons)
    {
        if (occurrence.Source.Confidence != OcrConfidence.EvidenceOnly || HasStructuralSourceEvidence(occurrence)) return null;
        if (IsProperNameRisk(sourceCase))
        {
            reasons.Add(OcrCorrectionDecisionReason.ProperNameRisk);
            return null;
        }
        var candidates = evidence.Where(item =>
            !item.Proposal.IsPartialStructuralRepair && item.CaseCompatible && item.Proposal.TrMorphValid
            && item.Proposal.EditDistance == 1 && item.Proposal.BookFrequency >= 5
            && item.Proposal.SourceSpanCount == 1).ToArray();
        if (occurrence.Source.Candidate.Text.EnumerateRunes().Count(Rune.IsLetter) < 4)
            reasons.Add(OcrCorrectionDecisionReason.EvidenceOnlyTooShort);
        if (evidence.Any(item => item.Proposal.EditDistance > 1))
            reasons.Add(OcrCorrectionDecisionReason.EvidenceOnlyEditDistanceTooHigh);
        if (occurrence.Source.Candidate.Text.EnumerateRunes().Any(IsUnicodeDash))
            reasons.Add(OcrCorrectionDecisionReason.InsufficientFrequencyDominance);
        if (ContainsAnyApostrophe(occurrence.Source.Candidate.Text))
            reasons.Add(OcrCorrectionDecisionReason.SameApostropheBaseReviewOnly);
        if (candidates.Length == 0) return null;
        var winner = candidates.OrderByDescending(item => item.Proposal.BookFrequency)
            .ThenBy(item => item.Proposal.EditDistance)
            .ThenBy(item => item.Proposal.ProposedText, StringComparer.Ordinal).First();
        var sameSafety = candidates.Where(item => SameSafety(item, winner)).ToArray();
        var runner = sameSafety.Where(item => !ReferenceEquals(item, winner)).MaxBy(item => item.Proposal.BookFrequency);
        if (occurrence.Source.Candidate.Text.EnumerateRunes().Count(Rune.IsLetter) < 4
            || ContainsAnyApostrophe(occurrence.Source.Candidate.Text)
            || occurrence.Source.Candidate.Text.EnumerateRunes().Any(IsUnicodeDash)
            || evidence.Any(item => item.Proposal.EditDistance > 1)
            || runner is not null && winner.Proposal.BookFrequency < runner.Proposal.BookFrequency * 5)
        {
            reasons.Add(OcrCorrectionDecisionReason.InsufficientFrequencyDominance);
            if (runner is not null) reasons.Add(OcrCorrectionDecisionReason.AmbiguousCandidates);
            return null;
        }
        reasons.Add(OcrCorrectionDecisionReason.EvidenceOnlyDominantLexicon);
        reasons.Add(OcrCorrectionDecisionReason.CaseCompatible);
        return Create(occurrence, sourceCase, OcrCorrectionDecisionKind.AutoFixCandidate, winner,
            candidates.Where(item => !ReferenceEquals(item, winner)).ToArray(), reasons);
    }

    private static OcrCorrectionDecision Create(
        OcrCorrectionOccurrence occurrence,
        OcrCasePattern sourceCase,
        OcrCorrectionDecisionKind kind,
        OcrCorrectionProposalSafetyEvidence? selected,
        IReadOnlyList<OcrCorrectionProposalSafetyEvidence> competing,
        IEnumerable<OcrCorrectionDecisionReason> reasons) =>
        new(occurrence, kind, selected, reasons.Distinct().OrderBy(item => item).ToArray(), competing, sourceCase);

    private static bool IsUsable(OcrCorrectionProposalSafetyEvidence item) =>
        !item.Proposal.IsPartialStructuralRepair && item.CaseCompatible
        && (item.Proposal.TrMorphValid || item.SameApostropheBase);

    private static bool HasStructuralSourceEvidence(OcrCorrectionOccurrence occurrence) =>
        occurrence.Source.Confidence is OcrConfidence.High or OcrConfidence.Medium
            && occurrence.Source.DetectionReasons.Any(IsStructuralReason)
        || !string.Equals(occurrence.WorkingSource.Text, occurrence.Source.Candidate.Text, StringComparison.Ordinal)
        || occurrence.Source.Candidate.ContextBefore.EndsWith("-", StringComparison.Ordinal);

    private static bool HasStructuralGeneration(OcrCorrectionCandidate proposal) =>
        proposal.GenerationReasons.Any(reason => reason is
            OcrCorrectionGenerationReason.StructuralNormalization
            or OcrCorrectionGenerationReason.GarbageRemoval
            or OcrCorrectionGenerationReason.GlyphSubstitution
            or OcrCorrectionGenerationReason.FragmentJoin
            or OcrCorrectionGenerationReason.HyphenRemoval);

    private static bool IsStructuralReason(OcrDetectionReason reason) =>
        reason is not OcrDetectionReason.MorphologyInvalid and not OcrDetectionReason.RareInBook;

    private static bool IsStructuralApostropheException(OcrCorrectionOccurrence occurrence, OcrCorrectionProposalSafetyEvidence item) =>
        (occurrence.Source.Candidate.Text.Contains('\'') || occurrence.Source.Candidate.Text.Contains('’'))
        && item.CaseCompatible
        && !item.Proposal.IsPartialStructuralRepair
        && item.Proposal.GenerationReasons.Contains(OcrCorrectionGenerationReason.BookLexiconNeighbor)
        && HasStructuralGeneration(item.Proposal)
        && SameApostropheSuffix(occurrence.Source.Candidate.Text, item.Proposal.ProposedText)
        && item.Proposal.BookFrequency >= 5;

    private static OcrCorrectionProposalSafetyEvidence Best(IEnumerable<OcrCorrectionProposalSafetyEvidence> items) =>
        items.OrderBy(item => item.Proposal.GenerationCost)
            .ThenBy(item => item.Proposal.StructuralTransformationCount)
            .ThenBy(item => item.Proposal.EditDistance)
            .ThenBy(item => item.Proposal.ProposedText, StringComparer.Ordinal)
            .First();

    private static bool SameSafety(OcrCorrectionProposalSafetyEvidence left, OcrCorrectionProposalSafetyEvidence right) =>
        left.Proposal.GenerationCost == right.Proposal.GenerationCost
        && left.Proposal.StructuralTransformationCount == right.Proposal.StructuralTransformationCount
        && left.Proposal.EditDistance == right.Proposal.EditDistance;

    private static bool IsProperNameRisk(OcrCasePattern pattern) => pattern is OcrCasePattern.TitleCase or OcrCasePattern.Uppercase;

    private static bool SameGeometry(OcrWordCandidate left, OcrWordCandidate right) =>
        string.Equals(left.Document, right.Document, StringComparison.Ordinal)
        && left.LogicalStart == right.LogicalStart
        && left.Text.Length == right.Text.Length
        && string.Equals(left.Text, right.Text, StringComparison.Ordinal);

    private static bool IsUnicodeDash(Rune rune) =>
        Rune.GetUnicodeCategory(rune) == System.Globalization.UnicodeCategory.DashPunctuation;

    private static bool ContainsAnyApostrophe(string value) => value.Contains('\'') || value.Contains('’');

    public static OcrCasePattern GetCasePattern(string text)
    {
        var letters = text.EnumerateRunes().Where(Rune.IsLetter).ToArray();
        if (letters.Length == 0) return OcrCasePattern.NoLetters;
        if (letters.All(Rune.IsLower)) return OcrCasePattern.Lowercase;
        if (letters.All(Rune.IsUpper)) return OcrCasePattern.Uppercase;
        if (Rune.IsUpper(letters[0]) && letters.Skip(1).All(Rune.IsLower)) return OcrCasePattern.TitleCase;
        return OcrCasePattern.Mixed;
    }

    private static bool IsCaseCompatible(
        OcrCorrectionOccurrence occurrence,
        OcrCasePattern source,
        OcrCorrectionCandidate proposal)
    {
        var proposalCase = GetCasePattern(proposal.ProposedText);
        if (source == proposalCase) return true;

        // A structural glyph can disappear before the lexicon neighbor is chosen.
        // Compare only alphabetic letters that survive a minimal edit alignment;
        // this narrow guard is used only for apostrophe structural repairs.
        if (!HasStructuralSourceEvidence(occurrence)
            || !HasStructuralGeneration(proposal)
            || !proposal.GenerationReasons.Contains(OcrCorrectionGenerationReason.BookLexiconNeighbor)
            || !SameApostropheSuffix(occurrence.Source.Candidate.Text, proposal.ProposedText))
            return false;

        var sourceBase = RawApostropheBase(occurrence.Source.Candidate.Text);
        var proposalBase = RawApostropheBase(proposal.ProposedText);
        if (sourceBase is null || proposalBase is null) return false;
        return SurvivingLettersPreserveCase(sourceBase, proposalBase);
    }

    private static bool SameApostropheSuffix(string source, string proposal)
    {
        var sourceIndex = source.IndexOfAny(['\'', '’']);
        var proposalIndex = proposal.IndexOfAny(['\'', '’']);
        return sourceIndex > 0 && proposalIndex > 0
            && string.Equals(source[(sourceIndex + 1)..], proposal[(proposalIndex + 1)..], StringComparison.Ordinal);
    }

    private static string? RawApostropheBase(string value)
    {
        var index = value.IndexOfAny(['\'', '’']);
        return index > 0 && index < value.Length - 1 ? value[..index] : null;
    }

    private static bool SurvivingLettersPreserveCase(string source, string proposal)
    {
        var left = source.EnumerateRunes().ToArray();
        var right = proposal.EnumerateRunes().ToArray();
        var costs = new int[left.Length + 1, right.Length + 1];
        var moves = new byte[left.Length + 1, right.Length + 1];
        for (var j = 0; j <= right.Length; j++) costs[0, j] = j;
        for (var i = 0; i <= left.Length; i++) costs[i, 0] = i;
        for (var i = 1; i <= left.Length; i++)
        {
            for (var j = 1; j <= right.Length; j++)
            {
                var substitution = costs[i - 1, j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1);
                var deletion = costs[i - 1, j] + 1;
                var insertion = costs[i, j - 1] + 1;
                costs[i, j] = Math.Min(substitution, Math.Min(deletion, insertion));
                moves[i, j] = substitution <= deletion && substitution <= insertion ? (byte)1
                    : deletion <= insertion ? (byte)2 : (byte)3;
            }
        }

        var iIndex = left.Length;
        var jIndex = right.Length;
        var surviving = 0;
        while (iIndex > 0 || jIndex > 0)
        {
            if (iIndex == 0) { jIndex--; continue; }
            if (jIndex == 0) { iIndex--; continue; }
            if (iIndex > 0 && jIndex > 0 && moves[iIndex, jIndex] == 1)
            {
                if (Rune.IsLetter(left[iIndex - 1]) && Rune.IsLetter(right[jIndex - 1])
                    && Rune.ToLowerInvariant(left[iIndex - 1]) == Rune.ToLowerInvariant(right[jIndex - 1]))
                {
                    surviving++;
                    if (Rune.IsUpper(left[iIndex - 1]) != Rune.IsUpper(right[jIndex - 1])) return false;
                }
                iIndex--; jIndex--; continue;
            }
            if (moves[iIndex, jIndex] == 2) iIndex--; else jIndex--;
        }
        return surviving > 0;
    }

    private static bool SameApostropheBase(string source, string proposal)
    {
        var sourceBase = ApostropheBase(source);
        var proposalBase = ApostropheBase(proposal);
        return sourceBase is not null && string.Equals(sourceBase, proposalBase, StringComparison.Ordinal);
    }

    private static string? ApostropheBase(string value)
    {
        var index = value.IndexOfAny(['\'', '’']);
        if (index <= 0 || index >= value.Length - 1) return null;
        var left = value[..index];
        var right = value[(index + 1)..];
        return left.EnumerateRunes().All(Rune.IsLetter) && right.EnumerateRunes().All(Rune.IsLetter) ? left : null;
    }
}
