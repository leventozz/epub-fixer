using System.Text;
using EpubFixer.Core.Decision.Models;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Core.Decision;

public sealed class OcrCorrectionDecisionEvaluator
{
    public OcrCorrectionDecisionAnalysisReport Evaluate(OcrCorrectionAnalysisReport analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        var decisions = analysis.Occurrences
            .Select(EvaluateOccurrence)
            .ToArray();
        return new OcrCorrectionDecisionAnalysisReport(analysis, decisions);
    }

    private static OcrCorrectionDecision EvaluateOccurrence(OcrCorrectionOccurrence occurrence)
    {
        var sourceCase = GetCasePattern(occurrence.WorkingSource.Text);
        var evidence = occurrence.Proposals
            .Select(proposal => new OcrCorrectionProposalSafetyEvidence(
                proposal,
                GetCasePattern(proposal.ProposedText),
                IsCaseCompatible(sourceCase, GetCasePattern(proposal.ProposedText)),
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
            && (item.CaseCompatible || IsStructuralApostropheException(occurrence, item))
            && (item.Proposal.TrMorphValid || IsStructuralApostropheException(occurrence, item))).ToArray();
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
        return Create(occurrence, sourceCase, OcrCorrectionDecisionKind.AutoFixCandidate, structuralWinner,
            structural.Where(item => !ReferenceEquals(item, structuralWinner)).ToArray(), reasons);
    }

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
        var winner = candidates.OrderByDescending(item => item.Proposal.BookFrequency).First();
        var runner = candidates.Where(item => !ReferenceEquals(item, winner)).MaxBy(item => item.Proposal.BookFrequency);
        if (runner is not null && winner.Proposal.BookFrequency < runner.Proposal.BookFrequency * 3)
        {
            reasons.Add(OcrCorrectionDecisionReason.InsufficientFrequencyDominance);
            return Create(occurrence, sourceCase, OcrCorrectionDecisionKind.Review, null, candidates, reasons);
        }
        reasons.Add(OcrCorrectionDecisionReason.SameApostropheBase);
        reasons.Add(OcrCorrectionDecisionReason.CaseCompatible);
        return Create(occurrence, sourceCase, OcrCorrectionDecisionKind.AutoFixCandidate, winner,
            candidates.Where(item => !ReferenceEquals(item, winner)).ToArray(), reasons);
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
            !item.Proposal.IsPartialStructuralRepair && item.CaseCompatible && item.Proposal.TrMorphValid).ToArray();
        if (candidates.Length == 0) return null;
        var winner = candidates.OrderByDescending(item => item.Proposal.BookFrequency)
            .ThenBy(item => item.Proposal.EditDistance)
            .ThenBy(item => item.Proposal.ProposedText, StringComparer.Ordinal).First();
        var runner = candidates.Where(item => !ReferenceEquals(item, winner)).MaxBy(item => item.Proposal.BookFrequency);
        if (winner.Proposal.EditDistance > 2 || winner.Proposal.BookFrequency < 3
            || runner is not null && winner.Proposal.BookFrequency < runner.Proposal.BookFrequency * 3)
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
        && item.Proposal.GenerationReasons.Contains(OcrCorrectionGenerationReason.BookLexiconNeighbor)
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

    public static OcrCasePattern GetCasePattern(string text)
    {
        var letters = text.EnumerateRunes().Where(Rune.IsLetter).ToArray();
        if (letters.Length == 0) return OcrCasePattern.NoLetters;
        if (letters.All(Rune.IsLower)) return OcrCasePattern.Lowercase;
        if (letters.All(Rune.IsUpper)) return OcrCasePattern.Uppercase;
        if (Rune.IsUpper(letters[0]) && letters.Skip(1).All(Rune.IsLower)) return OcrCasePattern.TitleCase;
        return OcrCasePattern.Mixed;
    }

    private static bool IsCaseCompatible(OcrCasePattern source, OcrCasePattern proposal) => source == proposal;

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
