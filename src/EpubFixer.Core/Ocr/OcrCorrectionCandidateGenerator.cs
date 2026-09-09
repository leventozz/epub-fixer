using System.Text;
using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Lexicon.Models;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Core.Ocr;

public sealed class OcrCorrectionCandidateGenerator
{
    private const int MaxStructuralDepth = 3;
    private const int MaxStructuralStates = 128;
    private const int MaxProposals = 10;

    private static readonly string[] TargetQueries =
    [
        "y1pranmışt1", "ilgi-1 iydi", "Eiles", "liyatro", "Joana'mn", "J3arış", "akşaın",
        "^iddetli", "ınetre", "Lckrarlayan", "()yuncu", "Viya-ııa'da", "Avııstıırya'nın",
        "l<ilb'de", "ger-^ .ckten", "ço-nıktu"
    ];

    public OcrCorrectionAnalysisReport Generate(
        OcrAnalysisReport sourceAnalysis,
        LogicalTextStream stream,
        BookLexicon lexicon,
        ITurkishMorphologyAnalyzer analyzer)
    {
        ArgumentNullException.ThrowIfNull(sourceAnalysis);
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(lexicon);
        ArgumentNullException.ThrowIfNull(analyzer);

        var sources = sourceAnalysis.Candidates.OrderBy(item => item.Candidate.LogicalStart).ToArray();
        var occurrences = new OcrCorrectionOccurrence[sources.Length];
        for (var index = 0; index < sources.Length; index++)
        {
            var next = index + 1 < sources.Length ? sources[index + 1] : null;
            occurrences[index] = GenerateOccurrence(sources[index], next, stream, lexicon, analyzer);
        }

        var targets = CreateTargetExamples(sourceAnalysis, occurrences, stream, lexicon, analyzer);
        return new OcrCorrectionAnalysisReport(sourceAnalysis, occurrences, targets);
    }

    private static OcrCorrectionOccurrence GenerateOccurrence(
        OcrWordEvidence source,
        OcrWordEvidence? next,
        LogicalTextStream stream,
        BookLexicon lexicon,
        ITurkishMorphologyAnalyzer analyzer)
    {
        var workingSource = ExpandWorkingSource(source.Candidate, stream);
        var parts = SplitLexicalParts(workingSource.Text);
        var spanExpanded = workingSource.LogicalStart != source.Candidate.LogicalStart
            || workingSource.Text.Length != source.Candidate.Text.Length;
        var structural = spanExpanded || HasStructuralEvidence(source);
        var seeds = StructuralSeeds(parts.Core, source, structural).ToArray();
        var generated = new Dictionary<DraftKey, Draft>();

        foreach (var seed in seeds)
        {
            if (seed.Depth == 0) continue;
            Add(generated, seed.Text, seed.Reasons, seed.Cost, seed.Depth, [source.Candidate]);
        }

        foreach (var entry in lexicon.Entries)
        {
            foreach (var seed in seeds)
            {
                var limit = DistanceLimit(seed.Text);
                if (RuneLength(entry.Key) < RuneLength(seed.Text) - limit
                    || RuneLength(entry.Key) > RuneLength(seed.Text) + limit
                    || string.Equals(entry.Key, parts.Core, StringComparison.Ordinal))
                    continue;

                var distance = BoundedDistance(seed.Text, entry.Key, limit);
                if (distance <= limit)
                    Add(generated, entry.Key,
                        seed.Reasons.Append(OcrCorrectionGenerationReason.BookLexiconNeighbor),
                        seed.Cost + distance, seed.Depth, [source.Candidate]);
            }
        }

        if (next is not null && CanComposeAdjacent(source, next, workingSource, stream, structural))
        {
            var nextParts = SplitLexicalParts(next.Candidate.Text);
            foreach (var seed in seeds.Where(item => item.Depth > 0 && !analyzer.IsValidWord(item.Text)))
            {
                var composite = seed.Text + nextParts.Core;
                if (!analyzer.IsValidWord(composite)) continue;
                Add(generated, composite,
                    seed.Reasons.Append(OcrCorrectionGenerationReason.AdjacentFragmentComposition),
                    seed.Cost + 1, seed.Depth, [source.Candidate, next.Candidate]);
            }
        }

        var hasComposite = generated.Keys.Any(key => key.SourceSpanCount > 1);
        var enriched = generated
            .Where(pair => pair.Key.Text.EnumerateRunes().Any(Rune.IsLetter))
            .Select(pair => Enrich(pair.Key.Text, pair.Value, source, parts.Core, lexicon, analyzer,
                hasComposite && pair.Key.SourceSpanCount == 1))
            .ToArray();

        var ordered = Order(enriched).ToArray();
        var selected = new List<OcrCorrectionCandidate>(MaxProposals);
        Reserve(selected, ordered.FirstOrDefault(item => item.TrMorphValid
            && item.StructuralTransformationCount > 1 && !item.ConsumesMultipleOccurrences));
        Reserve(selected, ordered.FirstOrDefault(item => item.TrMorphValid
            && item.GenerationReasons.Contains(OcrCorrectionGenerationReason.AdjacentFragmentComposition)));
        foreach (var candidate in ordered)
        {
            if (selected.Count == MaxProposals) break;
            if (!selected.Contains(candidate)) selected.Add(candidate);
        }

        var proposals = Order(selected)
            .Select((item, index) => item with { ProposalRank = index + 1 })
            .ToArray();

        return new OcrCorrectionOccurrence(source, workingSource, parts.Prefix, parts.Core, parts.Suffix, proposals);
    }

    private static IEnumerable<OcrCorrectionCandidate> Order(IEnumerable<OcrCorrectionCandidate> candidates) =>
        candidates
            .OrderByDescending(item => item.BookFrequency)
            .ThenByDescending(item => item.TrMorphValid)
            .ThenBy(item => item.EditDistance)
            .ThenBy(item => item.GenerationCost)
            .ThenBy(item => item.ProposedText, StringComparer.Ordinal)
            .ThenBy(item => item.SourceSpanCount);

    private static void Reserve(ICollection<OcrCorrectionCandidate> selected, OcrCorrectionCandidate? candidate)
    {
        if (candidate is not null && !selected.Contains(candidate)) selected.Add(candidate);
    }

    private static OcrCorrectionCandidate Enrich(
        string text,
        Draft draft,
        OcrWordEvidence source,
        string sourceCore,
        BookLexicon lexicon,
        ITurkishMorphologyAnalyzer analyzer,
        bool partial)
    {
        var comparisonSource = string.Concat(draft.ConsumedSources.Select((item, index) =>
            index == 0 ? sourceCore : SplitLexicalParts(item.Text).Core));
        return new OcrCorrectionCandidate(
            source.Candidate,
            source.Confidence,
            text,
            draft.Reasons.OrderBy(reason => reason).ToArray(),
            BoundedDistance(comparisonSource, text, int.MaxValue),
            draft.Cost,
            analyzer.IsValidWord(text),
            lexicon.GetCount(text),
            0,
            draft.ConsumedSources,
            draft.StructuralDepth,
            partial);
    }

    private static void Add(
        IDictionary<DraftKey, Draft> drafts,
        string text,
        IEnumerable<OcrCorrectionGenerationReason> reasons,
        int cost,
        int structuralDepth,
        IReadOnlyList<OcrWordCandidate> consumedSources)
    {
        var key = new DraftKey(text, consumedSources.Count);
        if (!drafts.TryGetValue(key, out var draft))
            drafts[key] = draft = new Draft(cost, structuralDepth, consumedSources);
        else if (cost < draft.Cost || cost == draft.Cost && structuralDepth < draft.StructuralDepth)
        {
            draft.Cost = cost;
            draft.StructuralDepth = structuralDepth;
            draft.ConsumedSources = consumedSources;
        }
        draft.Reasons.UnionWith(reasons);
    }

    private static IEnumerable<Seed> StructuralSeeds(string sourceCore, OcrWordEvidence source, bool structural)
    {
        var initial = new Seed(sourceCore, [], 0, 0);
        var queue = new Queue<Seed>([initial]);
        var seen = new HashSet<string>(StringComparer.Ordinal) { sourceCore };
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            yield return current;
            if (current.Depth >= MaxStructuralDepth) continue;

            foreach (var next in Expand(current.Text, source, structural))
            {
                if (seen.Count >= MaxStructuralStates) yield break;
                if (!seen.Add(next.Text)) continue;
                queue.Enqueue(next with
                {
                    Cost = current.Cost + next.Cost,
                    Depth = current.Depth + 1,
                    Reasons = current.Reasons.Concat(next.Reasons).Distinct().ToArray()
                });
            }
        }
    }

    private static IEnumerable<Seed> Expand(string text, OcrWordEvidence source, bool structural)
    {
        if (!structural) yield break;
        foreach (var replacement in ReplaceGarbage(text))
            yield return new Seed(replacement,
                [OcrCorrectionGenerationReason.GarbageRemoval, OcrCorrectionGenerationReason.StructuralNormalization], 1, 1);
        if (source.DetectionReasons.Contains(OcrDetectionReason.IsolatedLetterFragmentation) && text.Contains(' '))
            yield return new Seed(text.Replace(" ", string.Empty, StringComparison.Ordinal),
                [OcrCorrectionGenerationReason.FragmentJoin, OcrCorrectionGenerationReason.StructuralNormalization], 1, 1);
        if (text.Contains('-'))
            yield return new Seed(text.Replace("-", string.Empty, StringComparison.Ordinal),
                [OcrCorrectionGenerationReason.HyphenRemoval, OcrCorrectionGenerationReason.StructuralNormalization], 1, 1);
        foreach (var pair in new[] { ("()", new[] { "o", "O" }), ("ıı", new[] { "n", "u" }) })
            foreach (var value in ReplaceAll(text, pair.Item1, pair.Item2))
                yield return new Seed(value,
                    [OcrCorrectionGenerationReason.GlyphSubstitution, OcrCorrectionGenerationReason.StructuralNormalization], 1, 1);
        foreach (var pair in new[] { ('1', new[] { 'l', 'i', 'ı', 'I' }), ('0', new[] { 'o', 'O', 'ö', 'Ö' }), ('3', new[] { 'e', 'E' }) })
            foreach (var value in ReplaceChar(text, pair.Item1, pair.Item2))
                yield return new Seed(value,
                    [OcrCorrectionGenerationReason.GlyphSubstitution, OcrCorrectionGenerationReason.StructuralNormalization], 1, 1);
    }

    private static bool CanComposeAdjacent(
        OcrWordEvidence current,
        OcrWordEvidence next,
        OcrWordCandidate workingSource,
        LogicalTextStream stream,
        bool structural)
    {
        if (!structural || next.TrMorphValid && !HasStructuralEvidence(next)) return false;
        var currentSuffix = SplitLexicalParts(workingSource.Text).Suffix;
        var nextPrefix = SplitLexicalParts(next.Candidate.Text).Prefix;
        if (currentSuffix.IndexOfAny(['.', '?', '!', ';', ':']) >= 0
            || nextPrefix.IndexOfAny(['.', '?', '!', ';', ':']) >= 0)
            return false;
        var currentEnd = Math.Max(current.Candidate.LogicalStart + current.Candidate.Text.Length,
            workingSource.LogicalStart + workingSource.Text.Length);
        if (next.Candidate.LogicalStart < currentEnd) return false;
        if (HasHardBoundaryBetween(stream, currentEnd, next.Candidate.LogicalStart)) return false;
        var gap = stream.Text[currentEnd..next.Candidate.LogicalStart];
        return gap.All(character => char.IsWhiteSpace(character) || character is '-' or '\u00ad' or '^' or '<' or '>');
    }

    private static bool HasHardBoundaryBetween(LogicalTextStream stream, int start, int endInclusive) =>
        stream.Boundaries.Any(boundary =>
        {
            if (boundary.Kind == TextBoundaryKind.TextNode) return false;
            var split = stream.Segments[boundary.BeforeSegmentIndex].LogicalStart
                + stream.Segments[boundary.BeforeSegmentIndex].Length;
            return split >= start && split <= endInclusive;
        });

    private static bool HasStructuralEvidence(OcrWordEvidence source) => source.DetectionReasons.Any(reason =>
        reason is not OcrDetectionReason.MorphologyInvalid and not OcrDetectionReason.RareInBook);

    private static OcrWordCandidate ExpandWorkingSource(OcrWordCandidate source, LogicalTextStream stream)
    {
        var start = source.LogicalStart;
        var end = start + source.Text.Length;
        while (start > 0 && IsExpandableGarbage(stream.Text[start - 1])
            && !CrossesHardBoundary(stream, start - 1, start))
            start--;
        while (end < stream.Text.Length && IsExpandableGarbage(stream.Text[end])
            && !CrossesHardBoundary(stream, end, end + 1))
            end++;
        return start == source.LogicalStart && end == source.LogicalStart + source.Text.Length
            ? source
            : CreateCandidate(stream, start, end - start);
    }

    private static bool IsExpandableGarbage(char value) => value is '<' or '>' or '^' or ';';

    private static LexicalParts SplitLexicalParts(string text)
    {
        var start = 0;
        var end = text.Length;
        var protectEmptyParentheses = text.StartsWith("()", StringComparison.Ordinal);
        while (start < end && IsBoundaryPunctuation(text[start], protectEmptyParentheses && start < 2)) start++;
        while (end > start && IsBoundaryPunctuation(text[end - 1], false)) end--;
        return new LexicalParts(text[..start], text[start..end], text[end..]);
    }

    private static bool IsBoundaryPunctuation(char value, bool protectedStructural) =>
        !protectedStructural && value is '.' or ',' or ':' or '!' or '?' or '…' or '"' or '“' or '”' or '«' or '»'
            or '[' or ']' or '{' or '}' or '(' or ')';

    private static bool CrossesHardBoundary(LogicalTextStream stream, int start, int endExclusive)
    {
        if (endExclusive <= start) return false;
        return stream.Boundaries.Any(boundary =>
        {
            if (boundary.Kind == TextBoundaryKind.TextNode) return false;
            var split = stream.Segments[boundary.BeforeSegmentIndex].LogicalStart
                + stream.Segments[boundary.BeforeSegmentIndex].Length;
            return split > start && split <= endExclusive;
        });
    }

    private static OcrWordCandidate CreateCandidate(LogicalTextStream stream, int start, int length)
    {
        var sources = new List<TextSourceLocation>();
        for (var index = start; index < start + length; index++)
        {
            var location = stream.GetSourceLocationAt(index);
            if (sources.Count > 0 && ReferenceEquals(sources[^1].SourceNode, location.SourceNode)
                && sources[^1].Start + sources[^1].Length == location.Start)
                sources[^1] = sources[^1] with { Length = sources[^1].Length + 1 };
            else sources.Add(location);
        }
        var left = Math.Max(0, start - 160);
        var right = Math.Min(stream.Text.Length, start + length + 160);
        return new OcrWordCandidate(stream.Text.Substring(start, length), start, sources, sources[0].DocumentPath,
            TakeRunes(stream.Text[left..start], 80, true),
            TakeRunes(stream.Text[(start + length)..right], 80, false));
    }

    private static IReadOnlyList<OcrTargetRegressionExample> CreateTargetExamples(
        OcrAnalysisReport sourceAnalysis,
        IReadOnlyList<OcrCorrectionOccurrence> occurrences,
        LogicalTextStream stream,
        BookLexicon lexicon,
        ITurkishMorphologyAnalyzer analyzer)
    {
        var allEvidence = sourceAnalysis.Candidates.Concat(sourceAnalysis.RareInBookSuppressedOccurrences)
            .OrderBy(item => item.Candidate.LogicalStart).ToArray();
        var results = new List<OcrTargetRegressionExample>(TargetQueries.Length);
        foreach (var query in TargetQueries)
        {
            var exactEvidence = allEvidence.FirstOrDefault(item =>
                string.Equals(item.Candidate.Text, query, StringComparison.Ordinal));
            var start = exactEvidence?.Candidate.LogicalStart
                ?? stream.Text.IndexOf(query, StringComparison.Ordinal);
            if (start < 0 || CrossesHardBoundary(stream, start, start + query.Length))
            {
                results.Add(new OcrTargetRegressionExample(query, false, string.Empty, string.Empty, [], 0,
                    string.Empty, 0, []));
                continue;
            }

            var end = start + query.Length;
            var evidence = allEvidence.Where(item => Overlaps(item.Candidate, start, end)).ToArray();
            var occurrence = occurrences
                .Where(item => Overlaps(item.WorkingSource, start, end))
                .OrderByDescending(item => OverlapLength(item.WorkingSource, start, end))
                .ThenBy(item => item.WorkingSource.LogicalStart)
                .FirstOrDefault();
            if (occurrence is null && evidence.Length > 0)
                occurrence = GenerateOccurrence(evidence[0], null, stream, lexicon, analyzer);
            var contextSource = CreateCandidate(stream, start, query.Length);
            results.Add(new OcrTargetRegressionExample(
                query,
                true,
                query,
                $"{contextSource.ContextBefore}⟦{query}⟧{contextSource.ContextAfter}",
                evidence.SelectMany(item => item.DetectionReasons).Distinct().OrderBy(item => item).ToArray(),
                lexicon.GetCount(query),
                lexicon.GetBaseForm(query),
                lexicon.GetBaseFormCount(query),
                occurrence?.Proposals ?? []));
        }
        return results;
    }

    private static bool Overlaps(OcrWordCandidate candidate, int start, int end) =>
        candidate.LogicalStart < end && candidate.LogicalStart + candidate.Text.Length > start;

    private static int OverlapLength(OcrWordCandidate candidate, int start, int end) =>
        Math.Max(0, Math.Min(candidate.LogicalStart + candidate.Text.Length, end)
            - Math.Max(candidate.LogicalStart, start));

    private static string TakeRunes(string value, int count, bool fromEnd)
    {
        var runes = value.EnumerateRunes().ToArray();
        return fromEnd ? string.Concat(runes.TakeLast(count)) : string.Concat(runes.Take(count));
    }

    private static IEnumerable<string> ReplaceGarbage(string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            if (!IsExpandableGarbage(text[index])) continue;
            var copy = new StringBuilder(text);
            copy.Remove(index, 1);
            yield return copy.ToString();
        }

        for (var start = 0; start < text.Length; start++)
        {
            if (!IsExpandableGarbage(text[start]) || start > 0 && IsExpandableGarbage(text[start - 1])) continue;
            var end = start + 1;
            while (end < text.Length && IsExpandableGarbage(text[end])) end++;
            if (end - start <= 1) continue;
            yield return text.Remove(start, end - start);
        }
    }

    private static IEnumerable<string> ReplaceAll(string text, string target, IEnumerable<string> replacements)
    {
        for (var index = text.IndexOf(target, StringComparison.Ordinal); index >= 0;
             index = text.IndexOf(target, index + 1, StringComparison.Ordinal))
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
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1));
                rowMin = Math.Min(rowMin, current[j]);
            }
            if (limit != int.MaxValue && rowMin > limit) return limit + 1;
            previous = current;
        }
        return previous[^1];
    }

    private sealed class Draft(int cost, int structuralDepth, IReadOnlyList<OcrWordCandidate> consumedSources)
    {
        public int Cost { get; set; } = cost;
        public int StructuralDepth { get; set; } = structuralDepth;
        public IReadOnlyList<OcrWordCandidate> ConsumedSources { get; set; } = consumedSources;
        public HashSet<OcrCorrectionGenerationReason> Reasons { get; } = new();
    }

    private sealed record DraftKey(string Text, int SourceSpanCount);
    private sealed record Seed(string Text, IReadOnlyList<OcrCorrectionGenerationReason> Reasons, int Cost, int Depth);
    private sealed record LexicalParts(string Prefix, string Core, string Suffix);
}
