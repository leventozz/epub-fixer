using System.Diagnostics;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Ocr;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Cli.OcrReconstruction;

internal sealed class DeterministicOcrCandidateReranker(
    ITurkishMorphologicalParser? parser = null) : IOcrCandidateReranker
{
    public IReadOnlyList<ReconstructionCandidate> Rerank(CorruptedTextRegion region, IReadOnlyList<ReconstructionCandidate> candidates, OcrContext context)
        => RerankDetailed(region, candidates, context).Select(x => x.Candidate).ToArray();

    internal IReadOnlyList<RerankDetail> RerankDetailed(CorruptedTextRegion region, IReadOnlyList<ReconstructionCandidate> candidates, OcrContext context)
    {
        var sw = Stopwatch.StartNew();
        var scored = candidates.Select((candidate, index) => Score(candidate, context, index)).ToArray();
        var ranked = scored.OrderByDescending(x => x.FinalScore).ThenBy(x => x.OriginalRank).ThenBy(x => x.Candidate.Text, StringComparer.Ordinal).ToList();
        return ranked.Select((x, i) => x with { Candidate = x.Candidate with { Score = x.FinalScore, Rank = i + 1 }, ElapsedMilliseconds = sw.Elapsed.TotalMilliseconds }).ToArray();
    }

    private RerankDetail Score(ReconstructionCandidate candidate, OcrContext context, int index)
    {
        var match = context.Book.FindBest(candidate.Text, context.PreviousWords, context.NextWords);
        var evidence = context.Book.FindEvidence(candidate.Text, context.PreviousWords, context.NextWords);
        evidence ??= match is null ? null : new OcrBookContextEvidence(match.OccurrenceCount, match.LeftMatches, match.RightMatches,
            match.OrderedPair ? 1 : 0, match.BothSides ? 1 : 0, match.BothSides ? 1 : 0, match.ConsensusSupport);
        // Keep each evidence family independently traceable and bounded.  The
        // lookup object may expose richer exact n-gram/phrase evidence; the
        // legacy fields remain the fallback for older lookup implementations.
        var frequency = evidence?.Frequency > 0 ? Math.Min(1d, Math.Log(1 + evidence.Frequency) / 3d) : 0d;
        var left = match is null ? 0d : Math.Min(1d, match.LeftMatches / 2d);
        var right = match is null ? 0d : Math.Min(1d, match.RightMatches / 2d);
        var bigram = evidence is null ? 0d : Math.Min(1d, evidence.BigramMatches / 2d);
        var trigram = evidence is null ? 0d : Math.Min(1d, evidence.TrigramMatches / 2d);
        var consensus = evidence?.ConsensusSupport ?? 0d;
        var book = .06 * frequency + .80 * left + .80 * right + .35 * bigram + .35 * trigram + .14 * consensus;
        if (match is { BothSides: true }) book += 2.0;
        if (match?.PrefixOnly == true) book *= .75;
        var morphology = 0d; var strongMorphology = false;
        var analyses = Analyze(candidate.Text);
        if (HasAdverb(analyses) && context.NextWords.FirstOrDefault() is "de" or "da") morphology += .20;
        var nearby = context.NextWords.Concat(context.PreviousWords.Reverse()).Take(8);
        var nearestFinite = nearby.Select(word => Analyze(word)
                .Where(a => a.Tags.Contains("V") && a.Tags.Any(t => t is "past" or "pres" or "fut"))
                .SelectMany(a => a.Tags.Where(IsPerson)).ToArray())
            .FirstOrDefault(tags => tags.Length > 0) ?? Array.Empty<string>();
        var verbPersons = nearestFinite.ToHashSet(StringComparer.Ordinal);
        var candidatePersons = analyses.Where(a => a.Tags.Contains("Prn:refl"))
            .SelectMany(a => a.Tags.Where(IsPerson)).ToHashSet(StringComparer.Ordinal);
        if (candidatePersons.Overlaps(verbPersons)) { morphology += .45; strongMorphology = true; }
        var structural = Structural(candidate.Text);
        var delta = book + morphology + structural;
        return new(candidate with { Evidence = candidate.Evidence.Append($"rerank frequency={frequency:0.000}; left={left:0.000}; right={right:0.000}; bigram={bigram:0.000}; trigram={trigram:0.000}; consensus={consensus:0.000}; book={book:0.000}; morphology={morphology:0.000}; structural={structural:0.000}").ToArray() }, candidate.Rank, candidate.Score, book, morphology, structural, candidate.Score + delta, match is { BothSides: true } || match is { OrderedPair: true }, strongMorphology, false, 0, frequency, left, right, bigram, trigram, consensus);
    }

    private static bool HasAdverb(IReadOnlyList<TurkishMorphologicalAnalysis> analyses) => analyses.Any(a => a.Tags.Contains("Adv"));
    private IReadOnlyList<TurkishMorphologicalAnalysis> Analyze(string word)
    {
        return parser?.Analyze(word) ?? Array.Empty<TurkishMorphologicalAnalysis>();
    }
    private static bool IsPerson(string tag) => tag is "1s" or "2s" or "3s" or "p1s" or "p2s" or "p3s";
    private static double Structural(string text)
    {
        var score = .10;
        if (text.Any(char.IsWhiteSpace)) score -= .25;
        if (text.Any(c => char.IsControl(c) || char.IsDigit(c) || c is '^' or ';' or ':' or '<' or '>' or ',')) score -= .25;
        if (text.Any(char.IsPunctuation)) score -= .15;
        return score;
    }

    internal sealed record RerankDetail(ReconstructionCandidate Candidate, int OriginalRank, double BaseScore, double BookDelta, double MorphologyDelta, double StructuralDelta, double FinalScore, bool StrongContext, bool StrongMorphology, bool Guarded, double ElapsedMilliseconds, double FrequencyEvidence = 0, double LeftEvidence = 0, double RightEvidence = 0, double BigramEvidence = 0, double TrigramEvidence = 0, double ConsensusEvidence = 0)
    { public double EvidenceDelta => BookDelta + MorphologyDelta + StructuralDelta; }
}
