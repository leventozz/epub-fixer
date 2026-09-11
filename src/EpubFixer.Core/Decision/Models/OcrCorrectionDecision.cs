using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Core.Decision.Models;

public sealed record OcrCorrectionDecision(
    OcrCorrectionOccurrence SourceOccurrence,
    OcrCorrectionDecisionKind DecisionKind,
    OcrCorrectionProposalSafetyEvidence? SelectedProposal,
    IReadOnlyList<OcrCorrectionDecisionReason> DecisionReasons,
    IReadOnlyList<OcrCorrectionProposalSafetyEvidence> CompetingProposals,
    OcrCasePattern SourceCase);
