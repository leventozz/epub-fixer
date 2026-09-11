using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Core.Decision.Models;

public sealed record OcrCorrectionDecision(
    OcrCorrectionOccurrence SourceOccurrence,
    OcrCorrectionDecisionKind DecisionKind,
    OcrCorrectionProposalSafetyEvidence? SelectedProposal,
    IReadOnlyList<OcrCorrectionDecisionReason> DecisionReasons,
    IReadOnlyList<OcrCorrectionProposalSafetyEvidence> CompetingProposals,
    OcrCasePattern SourceCase)
{
    /// <summary>
    /// The structural winner before a TitleCase proper-name safety guard
    /// downgraded the decision to Review. This is audit-only metadata.
    /// </summary>
    public OcrCorrectionProposalSafetyEvidence? ProvisionalSelectedProposal { get; init; }
}
