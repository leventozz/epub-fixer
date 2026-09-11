namespace EpubFixer.Core.Decision.Models;

public enum OcrCorrectionDecisionReason
{
    DirectStructuralRepair,
    StructuralLexiconRepair,
    AdjacentCompositeRepair,
    EvidenceOnlyDominantLexicon,
    SameApostropheBase,
    UniqueStructuralWinner,
    CaseCompatible,
    AmbiguousCandidates,
    ProperNameRisk,
    InsufficientFrequencyDominance,
    PartialRepairOnly,
    NoValidProposal,
    NoProposal,
    CaseMismatch,
    EvidenceOnlyTooShort,
    EvidenceOnlyEditDistanceTooHigh,
    ConsumedByCompositeRepair,
    ApostropheSuffixChangeUnsafe,
    SameApostropheBaseReviewOnly,
    ProperNameStructuralAmbiguity
}
