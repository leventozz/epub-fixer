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

    OcrBookContextEvidence? FindEvidence(string candidate, IReadOnlyList<string> previous, IReadOnlyList<string> next) => null;
}

public sealed record OcrBookContextMatch(
    bool PrefixOnly,
    int LeftMatches,
    int RightMatches,
    bool OrderedPair,
    bool BothSides,
    int OccurrenceCount)
{
    public int Frequency => OccurrenceCount;
    public int BigramMatches { get; init; }
    public int TrigramMatches { get; init; }
    public int PhraseMatches { get; init; }
    public double ConsensusSupport { get; init; }
}

public sealed record OcrBookContextEvidence(
    int Frequency,
    int LeftMatches,
    int RightMatches,
    int BigramMatches,
    int TrigramMatches,
    int PhraseMatches,
    double ConsensusSupport);
