using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EpubFixer.Core.Decision.Models;
using EpubFixer.Core.Detection.Models;
using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Evidence.Models;

internal static class HyphenationEvidenceReporting
{
    private const int TopTransformationCount = 20;
    private const int ContextWordCount = 6;
    private const int LowerBucketReportLimit = 30;

    public static HyphenationEvidenceSummary CreateSummary(
        IReadOnlyList<HyphenationEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        var existsInLexicon = evidence.Count(item => item.ExistsInLexicon);
        var buckets = new LexiconCountBuckets(
            evidence.Count(item => item.UnhyphenatedOccurrenceCount == 0),
            evidence.Count(item => item.UnhyphenatedOccurrenceCount == 1),
            evidence.Count(item => item.UnhyphenatedOccurrenceCount is >= 2 and <= 4),
            evidence.Count(item => item.UnhyphenatedOccurrenceCount is >= 5 and <= 9),
            evidence.Count(item => item.UnhyphenatedOccurrenceCount is >= 10 and <= 49),
            evidence.Count(item => item.UnhyphenatedOccurrenceCount >= 50));

        var topTransformations = evidence
            .Where(item => item.ExistsInLexicon)
            .GroupBy(item => new EvidenceCandidateKey(
                item.Candidate.LeftPart,
                item.Candidate.RightPart,
                item.Candidate.UnhyphenatedText))
            .Select(group => new HyphenationEvidenceAggregate(
                group.Key,
                group.First().UnhyphenatedOccurrenceCount,
                group.Count()))
            .OrderByDescending(item => item.UnhyphenatedOccurrenceCount)
            .ThenByDescending(item => item.CandidateOccurrences)
            .ThenBy(item => item.Key.LeftPart, StringComparer.Ordinal)
            .ThenBy(item => item.Key.RightPart, StringComparer.Ordinal)
            .ThenBy(item => item.Key.UnhyphenatedText, StringComparer.Ordinal)
            .Take(TopTransformationCount)
            .ToArray();

        return new HyphenationEvidenceSummary(
            evidence.Count,
            existsInLexicon,
            evidence.Count - existsInLexicon,
            buckets,
            Array.AsReadOnly(topTransformations));
    }

    public static HyphenationEvidenceAnalysis CreateAnalysis(
        IReadOnlyList<HyphenationEvidence> evidence,
        LogicalTextStream stream)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(stream);

        var aggregates = evidence
            .GroupBy(item => new EvidenceCandidateKey(
                item.Candidate.LeftPart,
                item.Candidate.RightPart,
                item.Candidate.UnhyphenatedText))
            .Select(group => CreateAnalysisAggregate(group, stream))
            .OrderByDescending(item => item.LexiconCount)
            .ThenByDescending(item => item.CandidateOccurrenceCount)
            .ThenBy(item => item.Key.LeftPart, StringComparer.Ordinal)
            .ThenBy(item => item.Key.RightPart, StringComparer.Ordinal)
            .ThenBy(item => item.Key.UnhyphenatedText, StringComparer.Ordinal)
            .ToArray();

        var buckets = new[]
        {
            CreateAnalysisBucket("Lexicon count >= 10", aggregates, count => count >= 10, null),
            CreateAnalysisBucket("Lexicon count 5-9", aggregates, count => count is >= 5 and <= 9, null),
            CreateAnalysisBucket("Lexicon count 2-4", aggregates, count => count is >= 2 and <= 4, LowerBucketReportLimit),
            CreateAnalysisBucket("Lexicon count = 1", aggregates, count => count == 1, LowerBucketReportLimit),
            CreateAnalysisBucket("Lexicon count = 0", aggregates, count => count == 0, LowerBucketReportLimit)
        };

        return new HyphenationEvidenceAnalysis(
            evidence.Count,
            aggregates.Length,
            Array.AsReadOnly(buckets));
    }

    public static void Print(TextWriter writer, HyphenationEvidenceSummary summary)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(summary);

        writer.WriteLine();
        writer.WriteLine("Hyphenation evidence");
        writer.WriteLine();
        writer.WriteLine($"Total candidates: {summary.TotalCandidates}");
        writer.WriteLine($"Exists in lexicon: {summary.ExistsInLexicon}");
        writer.WriteLine($"Not found in lexicon: {summary.NotFoundInLexicon}");
        writer.WriteLine();
        writer.WriteLine("Lexicon count distribution");
        writer.WriteLine();
        writer.WriteLine($"Lexicon count = 0: {summary.Buckets.Zero}");
        writer.WriteLine($"Lexicon count = 1: {summary.Buckets.One}");
        writer.WriteLine($"Lexicon count = 2-4: {summary.Buckets.TwoToFour}");
        writer.WriteLine($"Lexicon count = 5-9: {summary.Buckets.FiveToNine}");
        writer.WriteLine($"Lexicon count = 10-49: {summary.Buckets.TenToFortyNine}");
        writer.WriteLine($"Lexicon count >= 50: {summary.Buckets.FiftyOrMore}");

        if (summary.TopTransformations.Count == 0)
        {
            return;
        }

        writer.WriteLine();
        writer.WriteLine("Top 20 transformations by occurrences in book");

        foreach (var item in summary.TopTransformations)
        {
            writer.WriteLine();
            writer.WriteLine($"{item.Key.LeftPart}-{item.Key.RightPart} -> {item.Key.UnhyphenatedText}");
            writer.WriteLine($"Occurrences in book: {item.UnhyphenatedOccurrenceCount}");
            writer.WriteLine($"Candidate occurrences: {item.CandidateOccurrences}");
        }
    }

    public static string SerializeJson(
        IReadOnlyList<HyphenationDecision> decisions,
        HyphenationEvidenceSummary evidenceSummary,
        HyphenationDecisionSummary decisionSummary)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(evidenceSummary);
        ArgumentNullException.ThrowIfNull(decisionSummary);

        var evidence = decisions.Select(item => item.Evidence).ToArray();

        var report = new
        {
            candidateCount = evidence.Length,
            counts = new
            {
                inline = CountCandidates(evidence, HyphenationDetectionKind.Inline),
                textNodeBoundary = CountCandidates(evidence, HyphenationDetectionKind.TextNodeBoundary),
                paragraphBoundary = CountCandidates(evidence, HyphenationDetectionKind.ParagraphBoundary),
                documentBoundary = CountCandidates(evidence, HyphenationDetectionKind.DocumentBoundary)
            },
            evidence = new
            {
                existsInLexicon = evidenceSummary.ExistsInLexicon,
                notFoundInLexicon = evidenceSummary.NotFoundInLexicon,
                lexiconCountBuckets = new
                {
                    zero = evidenceSummary.Buckets.Zero,
                    one = evidenceSummary.Buckets.One,
                    twoToFour = evidenceSummary.Buckets.TwoToFour,
                    fiveToNine = evidenceSummary.Buckets.FiveToNine,
                    tenToFortyNine = evidenceSummary.Buckets.TenToFortyNine,
                    fiftyOrMore = evidenceSummary.Buckets.FiftyOrMore
                }
            },
            decisions = new
            {
                total = decisionSummary.TotalDecisions,
                autoFixCandidate = decisionSummary.AutoFixCandidate,
                deferred = decisionSummary.Deferred,
                autoFixCandidateTransformations = decisionSummary.AutoFixCandidateTransformations
                    .Select(item => new
                    {
                        leftPart = item.Key.LeftPart,
                        rightPart = item.Key.RightPart,
                        unhyphenatedText = item.Key.UnhyphenatedText,
                        lexiconCount = item.LexiconCount,
                        candidateOccurrences = item.CandidateOccurrences
                    })
                    .ToArray()
            },
            occurrences = decisions.Select(decision => new
            {
                leftPart = decision.Evidence.Candidate.LeftPart,
                rightPart = decision.Evidence.Candidate.RightPart,
                unhyphenatedText = decision.Evidence.Candidate.UnhyphenatedText,
                detectionKind = FormatDetectionKind(decision.Evidence.Candidate.DetectionKind),
                unhyphenatedOccurrenceCount = decision.Evidence.UnhyphenatedOccurrenceCount,
                existsInLexicon = decision.Evidence.ExistsInLexicon,
                decisionKind = FormatDecisionKind(decision.DecisionKind),
                context = new
                {
                    previousRune = decision.Evidence.Context.PreviousRune,
                    nextRune = decision.Evidence.Context.NextRune,
                    hasAdjacentHyphen = decision.Evidence.Context.HasAdjacentHyphen,
                    hasAdjacentSuspiciousCharacter = decision.Evidence.Context.HasAdjacentSuspiciousCharacter
                },
                leftSource = CreatePortableSource(decision.Evidence.Candidate.LeftSource),
                hyphenSource = CreatePortableSource(decision.Evidence.Candidate.HyphenSource),
                rightSource = CreatePortableSource(decision.Evidence.Candidate.RightSource)
            }).ToArray()
        };

        return JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
    }

    public static string SerializeMarkdown(HyphenationEvidenceAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        var writer = new StringBuilder();
        writer.AppendLine("# Hyphenation Lexicon Evidence Analysis");
        writer.AppendLine();
        writer.AppendLine($"- Candidate occurrences: {analysis.CandidateOccurrenceCount}");
        writer.AppendLine($"- Unique transformations: {analysis.UniqueTransformationCount}");
        writer.AppendLine();
        writer.AppendLine("## Bucket summary");
        writer.AppendLine();
        writer.AppendLine("| Group | Unique transformations | Candidate occurrences | Reported |");
        writer.AppendLine("| --- | ---: | ---: | ---: |");

        foreach (var bucket in analysis.Buckets)
        {
            writer.AppendLine(
                $"| {bucket.Label} | {bucket.TotalUniqueTransformations} | "
                + $"{bucket.TotalCandidateOccurrences} | {bucket.Transformations.Count} |");
        }

        foreach (var bucket in analysis.Buckets)
        {
            writer.AppendLine();
            writer.AppendLine($"## {bucket.Label}");
            writer.AppendLine();

            if (bucket.Transformations.Count < bucket.TotalUniqueTransformations)
            {
                writer.AppendLine(
                    $"Showing the first {bucket.Transformations.Count} of "
                    + $"{bucket.TotalUniqueTransformations} unique transformations.");
                writer.AppendLine();
            }

            foreach (var transformation in bucket.Transformations)
            {
                writer.AppendLine(
                    $"### `{transformation.Key.LeftPart}-{transformation.Key.RightPart}` → "
                    + $"`{transformation.Key.UnhyphenatedText}`");
                writer.AppendLine();
                writer.AppendLine($"- LexiconCount: {transformation.LexiconCount}");
                writer.AppendLine($"- CandidateOccurrences: {transformation.CandidateOccurrenceCount}");
                writer.AppendLine("- Kinds:");

                foreach (var kind in transformation.DetectionKinds)
                {
                    writer.AppendLine($"  - {FormatDetectionKind(kind.Kind)}: {kind.Count}");
                }

                writer.AppendLine(
                    "- SourceDocuments: "
                    + string.Join(", ", transformation.SourceDocumentPaths.Select(path => $"`{path}`")));
                writer.AppendLine("- First occurrence context:");
                writer.AppendLine();
                writer.AppendLine($"> {EscapeMarkdownBlockquote(transformation.FirstOccurrenceContext)}");
                writer.AppendLine();
            }
        }

        return writer.ToString();
    }

    private static HyphenationEvidenceAnalysisAggregate CreateAnalysisAggregate(
        IGrouping<EvidenceCandidateKey, HyphenationEvidence> group,
        LogicalTextStream stream)
    {
        var first = group.First();
        var detectionKinds = Enum.GetValues<HyphenationDetectionKind>()
            .Select(kind => new HyphenationDetectionKindCount(
                kind,
                group.Count(item => item.Candidate.DetectionKind == kind)))
            .Where(item => item.Count > 0)
            .ToArray();
        var sourceDocumentPaths = group
            .SelectMany(item => new[]
            {
                item.Candidate.LeftSource.DocumentPath,
                item.Candidate.RightSource.DocumentPath
            })
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        return new HyphenationEvidenceAnalysisAggregate(
            group.Key,
            first.UnhyphenatedOccurrenceCount,
            group.Count(),
            Array.AsReadOnly(detectionKinds),
            Array.AsReadOnly(sourceDocumentPaths),
            CreateFirstOccurrenceContext(stream, first.Candidate));
    }

    private static HyphenationEvidenceAnalysisBucket CreateAnalysisBucket(
        string label,
        IReadOnlyList<HyphenationEvidenceAnalysisAggregate> aggregates,
        Func<int, bool> belongsToBucket,
        int? reportLimit)
    {
        var matching = aggregates
            .Where(item => belongsToBucket(item.LexiconCount))
            .ToArray();
        var reported = reportLimit is null
            ? matching
            : matching.Take(reportLimit.Value).ToArray();

        return new HyphenationEvidenceAnalysisBucket(
            label,
            matching.Length,
            matching.Sum(item => item.CandidateOccurrenceCount),
            Array.AsReadOnly(reported));
    }

    private static string CreateFirstOccurrenceContext(
        LogicalTextStream stream,
        HyphenationCandidate candidate)
    {
        var candidateStart = GetLogicalIndex(stream, candidate.LeftSource);
        var candidateEnd = GetLogicalIndex(stream, candidate.RightSource)
            + candidate.RightSource.Length;
        var beforeMatches = Regex.Matches(stream.Text[..candidateStart], @"\S+");
        var afterMatches = Regex.Matches(stream.Text[candidateEnd..], @"\S+");
        var before = beforeMatches
            .Cast<Match>()
            .TakeLast(ContextWordCount)
            .Select(match => match.Value);
        var after = afterMatches
            .Cast<Match>()
            .Take(ContextWordCount)
            .Select(match => match.Value);
        var context = new List<string>();

        if (beforeMatches.Count > 0)
        {
            context.Add((beforeMatches.Count > ContextWordCount ? "… " : string.Empty) + string.Join(' ', before));
        }

        context.Add($"[{FormatCandidateForContext(candidate)}]");

        if (afterMatches.Count > 0)
        {
            context.Add(string.Join(' ', after) + (afterMatches.Count > ContextWordCount ? " …" : string.Empty));
        }

        return Regex.Replace(string.Join(' ', context), @"\s+", " ").Trim();
    }

    private static int GetLogicalIndex(LogicalTextStream stream, TextSourceLocation source)
    {
        var segment = stream.Segments.FirstOrDefault(item =>
            ReferenceEquals(item.Source.SourceNode, source.SourceNode));

        if (segment is null)
        {
            throw new InvalidOperationException("Candidate source was not found in the logical text stream.");
        }

        return segment.LogicalStart + source.Start - segment.Source.Start;
    }

    private static string FormatCandidateForContext(HyphenationCandidate candidate)
    {
        if (candidate.DetectionKind == HyphenationDetectionKind.Inline)
        {
            return $"{candidate.LeftPart}-{candidate.RightPart}";
        }

        return $"{candidate.LeftPart}- ⟨{FormatDetectionKind(candidate.DetectionKind)}⟩ {candidate.RightPart}";
    }

    private static string EscapeMarkdownBlockquote(string text)
    {
        return text
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    private static object CreatePortableSource(TextSourceLocation source)
    {
        return new
        {
            documentPath = source.DocumentPath,
            textNodeIndex = source.TextNodeIndex,
            start = source.Start,
            length = source.Length
        };
    }

    private static int CountCandidates(
        IReadOnlyList<HyphenationEvidence> evidence,
        HyphenationDetectionKind kind)
    {
        return evidence.Count(item => item.Candidate.DetectionKind == kind);
    }

    private static string FormatDetectionKind(HyphenationDetectionKind kind)
    {
        return kind switch
        {
            HyphenationDetectionKind.Inline => "Inline",
            HyphenationDetectionKind.TextNodeBoundary => "TextNodeBoundary",
            HyphenationDetectionKind.ParagraphBoundary => "ParagraphBoundary",
            HyphenationDetectionKind.DocumentBoundary => "DocumentBoundary",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    private static string FormatDecisionKind(HyphenationDecisionKind kind)
    {
        return kind switch
        {
            HyphenationDecisionKind.AutoFixCandidate => "AutoFixCandidate",
            HyphenationDecisionKind.Deferred => "Deferred",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }
}

internal sealed record HyphenationEvidenceSummary(
    int TotalCandidates,
    int ExistsInLexicon,
    int NotFoundInLexicon,
    LexiconCountBuckets Buckets,
    IReadOnlyList<HyphenationEvidenceAggregate> TopTransformations);

internal sealed record LexiconCountBuckets(
    int Zero,
    int One,
    int TwoToFour,
    int FiveToNine,
    int TenToFortyNine,
    int FiftyOrMore);

internal sealed record EvidenceCandidateKey(
    string LeftPart,
    string RightPart,
    string UnhyphenatedText);

internal sealed record HyphenationEvidenceAggregate(
    EvidenceCandidateKey Key,
    int UnhyphenatedOccurrenceCount,
    int CandidateOccurrences);

internal sealed record HyphenationEvidenceAnalysis(
    int CandidateOccurrenceCount,
    int UniqueTransformationCount,
    IReadOnlyList<HyphenationEvidenceAnalysisBucket> Buckets);

internal sealed record HyphenationEvidenceAnalysisBucket(
    string Label,
    int TotalUniqueTransformations,
    int TotalCandidateOccurrences,
    IReadOnlyList<HyphenationEvidenceAnalysisAggregate> Transformations);

internal sealed record HyphenationEvidenceAnalysisAggregate(
    EvidenceCandidateKey Key,
    int LexiconCount,
    int CandidateOccurrenceCount,
    IReadOnlyList<HyphenationDetectionKindCount> DetectionKinds,
    IReadOnlyList<string> SourceDocumentPaths,
    string FirstOccurrenceContext);

internal sealed record HyphenationDetectionKindCount(
    HyphenationDetectionKind Kind,
    int Count);
