using EpubFixer.Core.Evidence.Models;

namespace EpubFixer.Core.Decision.Models;

public sealed record HyphenationDecision(
    HyphenationEvidence Evidence,
    HyphenationDecisionKind DecisionKind);
