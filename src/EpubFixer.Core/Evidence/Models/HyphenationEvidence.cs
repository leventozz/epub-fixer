using EpubFixer.Core.Detection.Models;

namespace EpubFixer.Core.Evidence.Models;

public sealed record HyphenationEvidence(
    HyphenationCandidate Candidate,
    int UnhyphenatedOccurrenceCount,
    bool ExistsInLexicon,
    HyphenationContextEvidence Context);
