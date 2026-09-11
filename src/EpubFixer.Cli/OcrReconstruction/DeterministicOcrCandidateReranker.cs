using System.Diagnostics;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Ocr;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Cli.OcrReconstruction;

internal sealed class DeterministicOcrCandidateReranker(
    ITurkishMorphologicalParser? parser = null) : IOcrCandidateReranker
{
    private readonly Dictionary<string, IReadOnlyList<TurkishMorphologicalAnalysis>> analysisCache = new(StringComparer.Ordinal);
    public IReadOnlyList<ReconstructionCandidate> Rerank(CorruptedTextRegion region, IReadOnlyList<ReconstructionCandidate> candidates, OcrContext context)
        => RerankDetailed(region, candidates, context).Select(x => x.Candidate).ToArray();

    internal IReadOnlyList<RerankDetail> RerankDetailed(CorruptedTextRegion region, IReadOnlyList<ReconstructionCandidate> candidates, OcrContext context)
    {
        var sw = Stopwatch.StartNew();
        var scored = candidates.Select((candidate, index) => Score(candidate, context, index)).ToArray();
        var leader = scored.OrderByDescending(x => x.BaseScore).ThenBy(x => x.OriginalRank).FirstOrDefault();
        var ranked = scored.OrderByDescending(x => x.FinalScore).ThenBy(x => x.OriginalRank).ThenBy(x => x.Candidate.Text, StringComparer.Ordinal).ToList();
        if (leader is not null && ranked.Count > 0 && ranked[0] != leader)
        {
            var challenger = ranked[0];
            var advantage = challenger.EvidenceDelta - leader.EvidenceDelta;
            var strong = challenger.StrongContext || challenger.StrongMorphology;
            if (!strong || advantage <= leader.BaseScore - challenger.BaseScore + .05)
            {
                ranked.Remove(leader); ranked.Insert(0, leader);
                leader = leader with { Guarded = true };
                ranked[0] = leader;
            }
        }
        return ranked.Select((x, i) => x with { Candidate = x.Candidate with { Score = x.FinalScore, Rank = i + 1 }, ElapsedMilliseconds = sw.Elapsed.TotalMilliseconds }).ToArray();
    }

    private RerankDetail Score(ReconstructionCandidate candidate, OcrContext context, int index)
    {
        var match = context.Book.FindBest(candidate.Text, context.PreviousWords, context.NextWords);
        var book = match is null ? 0 : (match.PrefixOnly ? .025 : .05) + .30 * Math.Min(2, match.LeftMatches + match.RightMatches) + (match.OrderedPair ? .25 : 0) + (match.BothSides ? .35 : 0) + (match.LeftMatches + match.RightMatches > 0 ? .35 : 0);
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
        return new(candidate with { Evidence = candidate.Evidence.Append($"rerank book={book:0.000}; morphology={morphology:0.000}; structural={structural:0.000}").ToArray() }, candidate.Rank, candidate.Score, book, morphology, structural, candidate.Score + delta, match is { BothSides: true } || match is { OrderedPair: true }, strongMorphology, false, 0);
    }

    private static bool HasAdverb(IReadOnlyList<TurkishMorphologicalAnalysis> analyses) => analyses.Any(a => a.Tags.Contains("Adv"));
    private IReadOnlyList<TurkishMorphologicalAnalysis> Analyze(string word)
    {
        if (parser is null) return Array.Empty<TurkishMorphologicalAnalysis>();
        if (!analysisCache.TryGetValue(word, out var value)) analysisCache[word] = value = parser.Analyze(word);
        return value;
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

    internal sealed record RerankDetail(ReconstructionCandidate Candidate, int OriginalRank, double BaseScore, double BookDelta, double MorphologyDelta, double StructuralDelta, double FinalScore, bool StrongContext, bool StrongMorphology, bool Guarded, double ElapsedMilliseconds)
    { public double EvidenceDelta => BookDelta + MorphologyDelta + StructuralDelta; }
}
