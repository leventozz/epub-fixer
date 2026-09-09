using System.Text;
using EpubFixer.Core.Lexicon.Models;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Core.Ocr;

public sealed class OcrCorrectionCandidateGenerator
{
    private const int MaxSeeds = 32;
    private const int MaxProposals = 10;

    public OcrCorrectionAnalysisReport Generate(
        OcrAnalysisReport sourceAnalysis,
        BookLexicon lexicon,
        ITurkishMorphologyAnalyzer analyzer)
    {
        ArgumentNullException.ThrowIfNull(sourceAnalysis);
        ArgumentNullException.ThrowIfNull(lexicon);
        ArgumentNullException.ThrowIfNull(analyzer);

        var occurrences = sourceAnalysis.Candidates
            .Select(source => GenerateOccurrence(source, lexicon, analyzer))
            .ToArray();
        return new OcrCorrectionAnalysisReport(sourceAnalysis, occurrences);
    }

    private static OcrCorrectionOccurrence GenerateOccurrence(
        OcrWordEvidence source,
        BookLexicon lexicon,
        ITurkishMorphologyAnalyzer analyzer)
    {
        var generated = new Dictionary<string, Draft>(StringComparer.Ordinal);
        var seeds = StructuralSeeds(source, MaxSeeds).ToArray();
        foreach (var seed in seeds)
        {
            if (string.Equals(seed.Text, source.Candidate.Text, StringComparison.Ordinal)) continue;
            Add(generated, seed.Text, seed.Reasons, seed.Cost);
        }

        foreach (var entry in lexicon.Entries)
        {
            foreach (var seed in seeds)
            {
                var limit = DistanceLimit(seed.Text);
                if (RuneLength(entry.Key) < RuneLength(seed.Text) - limit
                    || RuneLength(entry.Key) > RuneLength(seed.Text) + limit
                    || string.Equals(entry.Key, source.Candidate.Text, StringComparison.Ordinal)) continue;
                var distance = BoundedDistance(seed.Text, entry.Key, limit);
                if (distance <= limit)
                    Add(generated, entry.Key,
                        seed.Reasons.Append(OcrCorrectionGenerationReason.BookLexiconNeighbor),
                        seed.Cost + distance);
            }
        }

        var proposals = generated
            .Where(pair => pair.Key.EnumerateRunes().Any(Rune.IsLetter))
            .Select(pair => Enrich(pair.Key, pair.Value, source, lexicon, analyzer))
            .OrderByDescending(item => item.BookFrequency)
            .ThenByDescending(item => item.TrMorphValid)
            .ThenBy(item => item.EditDistance)
            .ThenBy(item => item.GenerationCost)
            .ThenBy(item => item.ProposedText, StringComparer.Ordinal)
            .Take(MaxProposals)
            .Select((item, index) => item with { ProposalRank = index + 1 })
            .ToArray();

        return new OcrCorrectionOccurrence(source, proposals);
    }

    private static OcrCorrectionCandidate Enrich(
        string text,
        Draft draft,
        OcrWordEvidence source,
        BookLexicon lexicon,
        ITurkishMorphologyAnalyzer analyzer)
    {
        return new OcrCorrectionCandidate(
            source.Candidate,
            source.Confidence,
            text,
            draft.Reasons.OrderBy(reason => reason).ToArray(),
            BoundedDistance(source.Candidate.Text, text, int.MaxValue),
            draft.Cost,
            analyzer.IsValidWord(text),
            lexicon.GetCount(text),
            0);
    }

    private static void Add(
        IDictionary<string, Draft> drafts,
        string text,
        IEnumerable<OcrCorrectionGenerationReason> reasons,
        int cost)
    {
        if (!drafts.TryGetValue(text, out var draft))
            drafts[text] = draft = new Draft(cost);
        draft.Cost = Math.Min(draft.Cost, cost);
        draft.Reasons.UnionWith(reasons);
    }

    private static IEnumerable<Seed> StructuralSeeds(OcrWordEvidence source, int limit)
    {
        var structural = source.DetectionReasons.Any(reason =>
            reason is not OcrDetectionReason.MorphologyInvalid and not OcrDetectionReason.RareInBook);
        var queue = new Queue<Seed>([new Seed(source.Candidate.Text, [], 0)]);
        var seen = new HashSet<string>(StringComparer.Ordinal) { source.Candidate.Text };
        while (queue.Count > 0 && seen.Count <= limit)
        {
            var current = queue.Dequeue();
            yield return current;
            foreach (var next in Expand(current.Text, source, structural))
            {
                if (seen.Add(next.Text) && seen.Count <= limit)
                {
                    queue.Enqueue(next with
                    {
                        Cost = current.Cost + next.Cost,
                        Reasons = current.Reasons.Concat(next.Reasons).Distinct().ToArray()
                    });
                }
            }
        }
    }

    private static IEnumerable<Seed> Expand(string text, OcrWordEvidence source, bool structural)
    {
        if (!structural) yield break;
        if (source.DetectionReasons.Contains(OcrDetectionReason.EmbeddedDigit)
            && text.Length > 1
            && text[^1] is '.' or ',' or ';' or ':' or '!' or '?')
            yield return new Seed(text[..^1], [OcrCorrectionGenerationReason.StructuralNormalization], 1);
        foreach (var replacement in ReplaceGarbage(text))
            yield return new Seed(replacement, [OcrCorrectionGenerationReason.GarbageRemoval, OcrCorrectionGenerationReason.StructuralNormalization], 1);
        if (source.DetectionReasons.Contains(OcrDetectionReason.IsolatedLetterFragmentation))
            yield return new Seed(text.Replace(" ", string.Empty, StringComparison.Ordinal), [OcrCorrectionGenerationReason.FragmentJoin, OcrCorrectionGenerationReason.StructuralNormalization], 1);
        if (source.DetectionReasons.Contains(OcrDetectionReason.SuspiciousCharacterSequence))
            yield return new Seed(text.Replace("-", string.Empty, StringComparison.Ordinal), [OcrCorrectionGenerationReason.HyphenRemoval, OcrCorrectionGenerationReason.StructuralNormalization], 1);
        foreach (var pair in new[] { ("()", new[] { "o", "O" }), ("ıı", new[] { "n", "u" }) })
            foreach (var value in ReplaceAll(text, pair.Item1, pair.Item2))
                yield return new Seed(value, [OcrCorrectionGenerationReason.GlyphSubstitution, OcrCorrectionGenerationReason.StructuralNormalization], 1);
        foreach (var pair in new[] { ('1', new[] { 'ı', 'i', 'l', 'I' }), ('0', new[] { 'o', 'O', 'ö', 'Ö' }), ('3', new[] { 'e', 'E' }) })
            foreach (var value in ReplaceChar(text, pair.Item1, pair.Item2))
                yield return new Seed(value, [OcrCorrectionGenerationReason.GlyphSubstitution, OcrCorrectionGenerationReason.StructuralNormalization], 1);
    }

    private static IEnumerable<string> ReplaceGarbage(string text)
    {
        var chars = text.ToCharArray();
        for (var index = 0; index < chars.Length; index++)
        {
            if (chars[index] is '<' or '>' or '^' or ';')
            {
                var copy = new StringBuilder(text);
                copy.Remove(index, 1);
                yield return copy.ToString();
            }
        }
    }

    private static IEnumerable<string> ReplaceAll(string text, string target, IEnumerable<string> replacements)
    {
        var index = text.IndexOf(target, StringComparison.Ordinal);
        if (index < 0) yield break;
        foreach (var replacement in replacements)
            yield return text[..index] + replacement + text[(index + target.Length)..];
    }

    private static IEnumerable<string> ReplaceChar(string text, char target, IEnumerable<char> replacements)
    {
        for (var index = 0; index < text.Length; index++)
            if (text[index] == target)
                foreach (var replacement in replacements)
                    yield return text[..index] + replacement + text[(index + 1)..];
    }

    private static int DistanceLimit(string text) => RuneLength(text) <= 4 ? 1 : 2;
    private static int RuneLength(string text) => text.EnumerateRunes().Count();

    private static int BoundedDistance(string first, string second, int limit)
    {
        var left = first.EnumerateRunes().ToArray();
        var right = second.EnumerateRunes().ToArray();
        if (limit != int.MaxValue && Math.Abs(left.Length - right.Length) > limit) return limit + 1;
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        for (var i = 1; i <= left.Length; i++)
        {
            var current = new int[right.Length + 1];
            current[0] = i;
            var rowMin = current[0];
            for (var j = 1; j <= right.Length; j++)
            {
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1));
                rowMin = Math.Min(rowMin, current[j]);
            }
            if (limit != int.MaxValue && rowMin > limit) return limit + 1;
            previous = current;
        }
        return previous[^1];
    }

    private sealed class Draft(int cost)
    {
        public int Cost { get; set; } = cost;
        public HashSet<OcrCorrectionGenerationReason> Reasons { get; } = new();
    }

    private sealed record Seed(string Text, IReadOnlyList<OcrCorrectionGenerationReason> Reasons, int Cost);
}
