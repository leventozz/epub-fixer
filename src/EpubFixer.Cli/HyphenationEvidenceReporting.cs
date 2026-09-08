using System.Text.Json;
using EpubFixer.Core.Detection.Models;
using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Evidence.Models;

internal static class HyphenationEvidenceReporting
{
    private const int TopTransformationCount = 20;

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
        IReadOnlyList<HyphenationEvidence> evidence,
        HyphenationEvidenceSummary summary)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(summary);

        var report = new
        {
            candidateCount = evidence.Count,
            counts = new
            {
                inline = CountCandidates(evidence, HyphenationDetectionKind.Inline),
                textNodeBoundary = CountCandidates(evidence, HyphenationDetectionKind.TextNodeBoundary),
                paragraphBoundary = CountCandidates(evidence, HyphenationDetectionKind.ParagraphBoundary),
                documentBoundary = CountCandidates(evidence, HyphenationDetectionKind.DocumentBoundary)
            },
            evidence = new
            {
                existsInLexicon = summary.ExistsInLexicon,
                notFoundInLexicon = summary.NotFoundInLexicon,
                lexiconCountBuckets = new
                {
                    zero = summary.Buckets.Zero,
                    one = summary.Buckets.One,
                    twoToFour = summary.Buckets.TwoToFour,
                    fiveToNine = summary.Buckets.FiveToNine,
                    tenToFortyNine = summary.Buckets.TenToFortyNine,
                    fiftyOrMore = summary.Buckets.FiftyOrMore
                }
            },
            occurrences = evidence.Select(item => new
            {
                leftPart = item.Candidate.LeftPart,
                rightPart = item.Candidate.RightPart,
                unhyphenatedText = item.Candidate.UnhyphenatedText,
                detectionKind = FormatDetectionKind(item.Candidate.DetectionKind),
                unhyphenatedOccurrenceCount = item.UnhyphenatedOccurrenceCount,
                existsInLexicon = item.ExistsInLexicon,
                leftSource = CreatePortableSource(item.Candidate.LeftSource),
                hyphenSource = CreatePortableSource(item.Candidate.HyphenSource),
                rightSource = CreatePortableSource(item.Candidate.RightSource)
            }).ToArray()
        };

        return JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
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
