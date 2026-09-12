using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Ocr;
using EpubFixer.QualityBenchmarks.Models;

namespace EpubFixer.QualityBenchmarks;

public sealed record GroundTruthProposal(
    string Id,
    string DocumentPath,
    string Original,
    OcrErrorClass ProposedClass,
    IReadOnlyList<GroundTruthSourceSpan> SourceSpans,
    string ContextBefore,
    string ContextAfter);

public sealed class GroundTruthProposer
{
    public IReadOnlyList<GroundTruthProposal> Propose(LogicalTextStream stream, int perClassTarget = 40)
        => Propose(stream, new ConservativeMorphologyAnalyzer(), perClassTarget);

    public IReadOnlyList<GroundTruthProposal> Propose(
        LogicalTextStream stream,
        ITurkishMorphologyAnalyzer analyzer,
        int perClassTarget = 40)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(analyzer);
        if (perClassTarget <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(perClassTarget));
        }

        var counts = new Dictionary<OcrErrorClass, int>();
        var proposals = new List<GroundTruthProposal>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var candidate in EnumerateDetectorCandidates(stream, analyzer))
        {
            var original = stream.Text.Substring(candidate.Start, candidate.Length);
            var errorClass = Classify(original);
            if (errorClass == OcrErrorClass.Unclassified
                || counts.GetValueOrDefault(errorClass) >= perClassTarget)
            {
                continue;
            }

            var sourceSpans = CreateSourceSpans(stream, candidate.Start, candidate.Length);
            var key = CreateProposalKey(original, sourceSpans);
            if (!seen.Add(key))
            {
                continue;
            }

            counts[errorClass] = counts.GetValueOrDefault(errorClass) + 1;
            var id = $"proposal-{errorClass.ToString().ToLowerInvariant()}-{counts[errorClass]:0000}";
            proposals.Add(new GroundTruthProposal(
                id,
                stream.GetSourceLocationAt(candidate.Start).DocumentPath,
                original,
                errorClass,
                sourceSpans,
                stream.Text[Math.Max(0, candidate.Start - 40)..candidate.Start],
                stream.Text[(candidate.Start + candidate.Length)..Math.Min(stream.Text.Length, candidate.Start + candidate.Length + 40)]));
        }

        return proposals
            .OrderBy(item => item.DocumentPath, StringComparer.Ordinal)
            .ThenBy(item => item.SourceSpans[0].TextNodeIndex)
            .ThenBy(item => item.SourceSpans[0].Start)
            .ToArray();
    }

    private static IEnumerable<(int Start, int Length)> EnumerateDetectorCandidates(
        LogicalTextStream stream,
        ITurkishMorphologyAnalyzer analyzer)
    {
        var anomaly = new OcrAnomalyDetector().Analyze(stream, analyzer).Candidates
            .Select(item => (item.Candidate.LogicalStart, item.Candidate.Text.Length));
        var regions = new OcrRegionDetector().Detect(stream.Text, analyzer)
            .Select(item => (item.Start, item.EndExclusive - item.Start));
        return anomaly.Concat(regions)
            .Where(item => item.Item2 > 0)
            .OrderBy(item => item.Item1)
            .ThenByDescending(item => item.Item2);
    }

    private static OcrErrorClass Classify(string original)
    {
        var hasWhitespace = original.Any(char.IsWhiteSpace);
        var hasGarbage = original.Any(character => character is '<' or '>' or '^' or '~' or '|' or '\\' or '_' or '§' or ':' or ';');
        var hasDigit = original.Any(char.IsDigit);
        var hasHyphenGlyph = original.Contains("-ı", StringComparison.Ordinal)
            || original.Contains("-li", StringComparison.OrdinalIgnoreCase)
            || original.Contains("ıi", StringComparison.Ordinal)
            || original.Contains("lı", StringComparison.Ordinal);

        if (hasGarbage)
        {
            return OcrErrorClass.GarbageInsertion;
        }

        if (hasWhitespace && (hasDigit || original.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Any(part => part.Length == 1)))
        {
            return OcrErrorClass.Fragmentation;
        }

        if (hasWhitespace)
        {
            return OcrErrorClass.SpuriousSpace;
        }

        if (hasDigit || hasHyphenGlyph)
        {
            return OcrErrorClass.GlyphConfusion;
        }

        return OcrErrorClass.Unclassified;
    }

    private static IReadOnlyList<GroundTruthSourceSpan> CreateSourceSpans(LogicalTextStream stream, int start, int length)
    {
        var spans = new List<GroundTruthSourceSpan>();
        for (var index = start; index < start + length; index++)
        {
            var source = stream.GetSourceLocationAt(index);
            if (spans.Count > 0
                && string.Equals(spans[^1].DocumentPath, source.DocumentPath, StringComparison.Ordinal)
                && spans[^1].TextNodeIndex == source.TextNodeIndex
                && spans[^1].Start + spans[^1].Length == source.Start)
            {
                var previous = spans[^1];
                spans[^1] = previous with { Length = previous.Length + source.Length };
            }
            else
            {
                spans.Add(new GroundTruthSourceSpan(
                    source.DocumentPath,
                    source.TextNodeIndex,
                    source.Start,
                    source.Length));
            }
        }

        return spans;
    }

    private static string CreateProposalKey(string original, IReadOnlyList<GroundTruthSourceSpan> sourceSpans) =>
        original + "|" + string.Join(",", sourceSpans.Select(span =>
            $"{span.DocumentPath}:{span.TextNodeIndex}:{span.Start}:{span.Length}"));

    private sealed class ConservativeMorphologyAnalyzer : ITurkishMorphologyAnalyzer
    {
        public bool IsValidWord(string word) => false;
    }
}
