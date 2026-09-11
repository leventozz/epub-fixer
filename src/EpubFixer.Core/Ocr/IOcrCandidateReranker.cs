using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Core.Ocr;

public interface IOcrCandidateReranker
{
    IReadOnlyList<ReconstructionCandidate> Rerank(
        CorruptedTextRegion region,
        IReadOnlyList<ReconstructionCandidate> candidates,
        OcrContext context);
}

public sealed record OcrContext(
    IReadOnlyList<string> PreviousWords,
    IReadOnlyList<string> NextWords,
    string SentenceSpan,
    string ParagraphSpan,
    IOcrBookContextLookup Book);

public interface IOcrBookContextLookup
{
    OcrBookContextMatch? FindBest(string candidate, IReadOnlyList<string> previous, IReadOnlyList<string> next);
}

public sealed record OcrBookContextMatch(
    bool PrefixOnly,
    int LeftMatches,
    int RightMatches,
    bool OrderedPair,
    bool BothSides,
    int OccurrenceCount);
