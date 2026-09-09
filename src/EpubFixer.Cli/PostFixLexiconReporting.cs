using System.Text;
using System.Text.RegularExpressions;
using EpubFixer.Core.Correction.Models;
using EpubFixer.Core.Decision.Models;
using EpubFixer.Core.Detection.Models;
using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Evidence.Models;

internal static class PostFixLexiconReporting
{
    public static string SerializeMarkdown(PostFixLexiconAnalysisResult analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        var original = analysis.Original;
        var final = analysis.Final;
        var positiveDeltas = final.Lexicon.Entries
            .Select(item => new LexiconDelta(
                item.Key,
                original.Lexicon.GetCount(item.Key),
                item.Value))
            .Where(item => item.Delta > 0)
            .OrderByDescending(item => item.Delta)
            .ThenBy(item => item.Word, StringComparer.Ordinal)
            .ToArray();
        var targetAggregates = analysis.InlinePlans
            .Concat(analysis.CrossParagraphPlans)
            .GroupBy(plan => plan.UnhyphenatedText, StringComparer.Ordinal)
            .Select(group => new TargetAggregate(
                group.Key,
                original.Lexicon.GetCount(group.Key),
                group.Count()))
            .OrderByDescending(item => item.Delta)
            .ThenBy(item => item.Word, StringComparer.Ordinal)
            .ToArray();
        var finalEvidence = final.Evidence
            .GroupBy(item => new TransformationKey(
                item.Candidate.LeftPart,
                item.Candidate.RightPart,
                item.Candidate.UnhyphenatedText))
            .ToDictionary(group => group.Key, group => group.ToArray());
        var originalDeferred = original.Decisions
            .Where(item => item.DecisionKind == HyphenationDecisionKind.Deferred)
            .Select(item => new TransformationKey(
                item.Evidence.Candidate.LeftPart,
                item.Evidence.Candidate.RightPart,
                item.Evidence.Candidate.UnhyphenatedText))
            .ToHashSet();
        var newlyStrong = finalEvidence
            .Where(item => originalDeferred.Contains(item.Key)
                && original.Lexicon.GetCount(item.Key.UnhyphenatedText) < 10
                && final.Lexicon.GetCount(item.Key.UnhyphenatedText) >= 10)
            .Select(item => CreateNewlyStrong(item.Key, item.Value, original, final))
            .OrderByDescending(item => item.AfterLexiconCount)
            .ThenBy(item => item.Key.UnhyphenatedText, StringComparer.Ordinal)
            .ToArray();

        var buckets = new[]
        {
            ("= 0", final.Evidence.Count(item => item.UnhyphenatedOccurrenceCount == 0)),
            ("= 1", final.Evidence.Count(item => item.UnhyphenatedOccurrenceCount == 1)),
            ("2-4", final.Evidence.Count(item => item.UnhyphenatedOccurrenceCount is >= 2 and <= 4)),
            ("5-9", final.Evidence.Count(item => item.UnhyphenatedOccurrenceCount is >= 5 and <= 9)),
            ("10-49", final.Evidence.Count(item => item.UnhyphenatedOccurrenceCount is >= 10 and <= 49)),
            (">= 50", final.Evidence.Count(item => item.UnhyphenatedOccurrenceCount >= 50))
        };

        var writer = new StringBuilder();
        writer.AppendLine("# Post-fix BookLexicon Analysis");
        writer.AppendLine();
        writer.AppendLine("## Pipeline");
        writer.AppendLine();
        writer.AppendLine($"- Original candidates: {original.Candidates.Count}");
        writer.AppendLine($"- Original AutoFix decisions: {original.Decisions.Count(item => item.DecisionKind == HyphenationDecisionKind.AutoFixCandidate)}");
        writer.AppendLine($"- Inline applied: {analysis.InlineApplyResult.AppliedCount}");
        writer.AppendLine($"- CrossParagraph applied: {analysis.CrossParagraphApplyResult.AppliedCount}");
        writer.AppendLine($"- Remaining candidates: {final.Candidates.Count}");
        writer.AppendLine();
        writer.AppendLine("## Positive exact BookLexicon deltas");
        writer.AppendLine();
        writer.AppendLine("Apostrophe-attached tokens are separate exact BookLexicon entries.");
        writer.AppendLine();
        writer.AppendLine("| Word | BeforeCount | AfterCount | Delta |");
        writer.AppendLine("| --- | ---: | ---: | ---: |");
        foreach (var item in positiveDeltas)
        {
            writer.AppendLine($"| `{item.Word}` | {item.BeforeCount} | {item.AfterCount} | +{item.Delta} |");
        }

        writer.AppendLine();
        writer.AppendLine("## Corrected-target aggregates");
        writer.AppendLine();
        writer.AppendLine("These are derived from applied correction occurrences, not exact post-fix lexicon entries.");
        writer.AppendLine();
        writer.AppendLine("| Word | BeforeCount | AfterCount | Delta |");
        writer.AppendLine("| --- | ---: | ---: | ---: |");
        foreach (var item in targetAggregates)
        {
            writer.AppendLine($"| `{item.Word}` | {item.BeforeCount} | {item.AggregateAfterCount} | +{item.Delta} |");
        }

        writer.AppendLine();
        writer.AppendLine("## Remaining candidate evidence buckets");
        writer.AppendLine();
        writer.AppendLine($"Remaining candidates: {final.Candidates.Count}");
        writer.AppendLine();
        writer.AppendLine("| Lexicon bucket | Candidate occurrences |");
        writer.AppendLine("| --- | ---: |");
        foreach (var bucket in buckets)
        {
            writer.AppendLine($"| Lexicon count {bucket.Item1} | {bucket.Item2} |");
        }

        writer.AppendLine();
        writer.AppendLine("## Newly strong deferred transformations");
        writer.AppendLine();
        if (newlyStrong.Length == 0)
        {
            writer.AppendLine("İlk 148 düzeltme yeni güçlü exact-lexicon evidence oluşturmadı.");
        }
        else
        {
            foreach (var item in newlyStrong)
            {
                writer.AppendLine($"### `{item.Key.LeftPart}-{item.Key.RightPart}` → `{item.Key.UnhyphenatedText}`");
                writer.AppendLine();
                writer.AppendLine($"- BeforeLexiconCount: {item.BeforeLexiconCount}");
                writer.AppendLine($"- AfterLexiconCount: {item.AfterLexiconCount}");
                writer.AppendLine($"- CandidateOccurrences: {item.CandidateOccurrences}");
                writer.AppendLine($"- DetectionKinds: {string.Join(", ", item.DetectionKinds)}");
                writer.AppendLine($"- ProductionDecision: {item.ProductionDecision}");
                writer.AppendLine($"- Neighborhood evidence: {item.NeighborhoodEvidence}");
                writer.AppendLine();
            }
        }

        writer.AppendLine();
        writer.AppendLine("## Production decision re-evaluation");
        writer.AppendLine();
        writer.AppendLine($"- Final AutoFixCandidate: {final.Decisions.Count(item => item.DecisionKind == HyphenationDecisionKind.AutoFixCandidate)}");
        writer.AppendLine($"- Final Deferred: {final.Decisions.Count(item => item.DecisionKind == HyphenationDecisionKind.Deferred)}");
        return writer.ToString();
    }

    private static NewlyStrong CreateNewlyStrong(
        TransformationKey key,
        IReadOnlyList<HyphenationEvidence> evidence,
        HyphenationPipelineResult original,
        HyphenationPipelineResult final)
    {
        var first = evidence[0];
        var decisions = final.Decisions
            .Where(item => SameKey(item.Evidence.Candidate, key))
            .Select(item => item.DecisionKind)
            .Distinct()
            .ToArray();
        return new NewlyStrong(
            key,
            original.Lexicon.GetCount(key.UnhyphenatedText),
            final.Lexicon.GetCount(key.UnhyphenatedText),
            evidence.Count,
            evidence.Select(item => item.Candidate.DetectionKind.ToString()).Distinct().ToArray(),
            string.Join(", ", decisions.Select(item => item.ToString())),
            CreateNeighborhood(final.LogicalText, first));
    }

    private static string CreateNeighborhood(LogicalTextStream stream, HyphenationEvidence evidence)
    {
        var start = GetLogicalIndex(stream, evidence.Candidate.LeftSource);
        var end = GetLogicalIndex(stream, evidence.Candidate.RightSource) + evidence.Candidate.RightSource.Length;
        var before = Regex.Matches(stream.Text[..start], @"\S+").Cast<Match>().TakeLast(6).Select(item => item.Value);
        var after = Regex.Matches(stream.Text[end..], @"\S+").Cast<Match>().Take(6).Select(item => item.Value);
        var candidate = evidence.Candidate.DetectionKind == HyphenationDetectionKind.Inline
            ? $"{evidence.Candidate.LeftPart}-{evidence.Candidate.RightPart}"
            : $"{evidence.Candidate.LeftPart}- ⟨{evidence.Candidate.DetectionKind}⟩ {evidence.Candidate.RightPart}";
        return $"{string.Join(' ', before)} [{candidate}] {string.Join(' ', after)}; "
            + $"PreviousRune={evidence.Context.PreviousRune ?? "∅"}, NextRune={evidence.Context.NextRune ?? "∅"}, "
            + $"HasAdjacentHyphen={evidence.Context.HasAdjacentHyphen}, HasAdjacentSuspiciousCharacter={evidence.Context.HasAdjacentSuspiciousCharacter}";
    }

    private static int GetLogicalIndex(LogicalTextStream stream, TextSourceLocation source)
    {
        var segment = stream.Segments.First(item => ReferenceEquals(item.Source.SourceNode, source.SourceNode));
        return segment.LogicalStart + source.Start - segment.Source.Start;
    }

    private static bool SameKey(HyphenationCandidate candidate, TransformationKey key) =>
        candidate.LeftPart == key.LeftPart
        && candidate.RightPart == key.RightPart
        && candidate.UnhyphenatedText == key.UnhyphenatedText;

    private sealed record LexiconDelta(string Word, int BeforeCount, int AfterCount)
    {
        public int Delta => AfterCount - BeforeCount;
    }

    private sealed record TargetAggregate(string Word, int BeforeCount, int Delta)
    {
        public int AggregateAfterCount => BeforeCount + Delta;
    }

    private sealed record TransformationKey(string LeftPart, string RightPart, string UnhyphenatedText);

    private sealed record NewlyStrong(
        TransformationKey Key,
        int BeforeLexiconCount,
        int AfterLexiconCount,
        int CandidateOccurrences,
        IReadOnlyList<string> DetectionKinds,
        string ProductionDecision,
        string NeighborhoodEvidence);
}
