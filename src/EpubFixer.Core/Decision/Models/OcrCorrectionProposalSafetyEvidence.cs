using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Core.Decision.Models;

public sealed record OcrCorrectionProposalSafetyEvidence(
    OcrCorrectionCandidate Proposal,
    OcrCasePattern ProposalCase,
    bool CaseCompatible,
    bool SameApostropheBase);
