using System.Text.Json;
using AngleSharp.Html.Parser;
using EpubFixer.Core.Detection.Models;
using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Evidence.Models;

namespace EpubFixer.Tests;

public sealed class HyphenationEvidenceReportingTests
{
    [Fact]
    public void CreateSummary_CountsEveryOccurrenceInExactlyOneBucket()
    {
        var evidence = new[] { 0, 1, 2, 4, 5, 9, 10, 49, 50 }
            .Select((count, index) => CreateEvidence($"Left{index}", $"Right{index}", count))
            .ToArray();

        var summary = HyphenationEvidenceReporting.CreateSummary(evidence);

        Assert.Equal(9, summary.TotalCandidates);
        Assert.Equal(8, summary.ExistsInLexicon);
        Assert.Equal(1, summary.NotFoundInLexicon);
        Assert.Equal(
            new LexiconCountBuckets(1, 1, 2, 2, 2, 1),
            summary.Buckets);
    }

    [Fact]
    public void CreateSummary_GroupsExactTransformationsAndSortsDeterministically()
    {
        var evidence = new[]
        {
            CreateEvidence("A", "b", 10),
            CreateEvidence("A", "b", 10),
            CreateEvidence("A", "bb", 10),
            CreateEvidence("B", "a", 20),
            CreateEvidence("Missing", "word", 0)
        };

        var summary = HyphenationEvidenceReporting.CreateSummary(evidence);

        Assert.Collection(
            summary.TopTransformations,
            item => AssertAggregate(item, "B", "a", 20, 1),
            item => AssertAggregate(item, "A", "b", 10, 2),
            item => AssertAggregate(item, "A", "bb", 10, 1));
    }

    [Fact]
    public void CreateSummary_LimitsTopTransformationsToTwenty()
    {
        var evidence = Enumerable.Range(1, 21)
            .Select(index => CreateEvidence($"Left{index:D2}", "Right", index))
            .ToArray();

        var summary = HyphenationEvidenceReporting.CreateSummary(evidence);

        Assert.Equal(20, summary.TopTransformations.Count);
        Assert.Equal(21, summary.TopTransformations[0].UnhyphenatedOccurrenceCount);
        Assert.Equal(2, summary.TopTransformations[^1].UnhyphenatedOccurrenceCount);
    }

    [Fact]
    public void Print_IncludesSummaryBucketsAndTransformationDetails()
    {
        var evidence = new[]
        {
            CreateEvidence("Auersber", "ger", 202),
            CreateEvidence("Auersber", "ger", 202),
            CreateEvidence("Mayıs", "Haziran", 0)
        };
        var summary = HyphenationEvidenceReporting.CreateSummary(evidence);
        using var writer = new StringWriter();

        HyphenationEvidenceReporting.Print(writer, summary);

        var output = writer.ToString();
        Assert.Contains("Hyphenation evidence", output);
        Assert.Contains("Total candidates: 3", output);
        Assert.Contains("Exists in lexicon: 2", output);
        Assert.Contains("Not found in lexicon: 1", output);
        Assert.Contains("Lexicon count = 0: 1", output);
        Assert.Contains("Lexicon count >= 50: 2", output);
        Assert.Contains("Auersber-ger -> Auersberger", output);
        Assert.Contains("Occurrences in book: 202", output);
        Assert.Contains("Candidate occurrences: 2", output);
    }

    [Fact]
    public void SerializeJson_PreservesExistingShapeAndAddsPortableEvidence()
    {
        var evidence = new[] { CreateEvidence("Auersber", "ger", 202) };
        var summary = HyphenationEvidenceReporting.CreateSummary(evidence);

        var json = HyphenationEvidenceReporting.SerializeJson(evidence, summary);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var occurrence = root.GetProperty("occurrences")[0];

        Assert.Equal(1, root.GetProperty("candidateCount").GetInt32());
        Assert.Equal(1, root.GetProperty("counts").GetProperty("inline").GetInt32());
        Assert.Equal(1, root.GetProperty("evidence").GetProperty("existsInLexicon").GetInt32());
        Assert.Equal(
            1,
            root.GetProperty("evidence")
                .GetProperty("lexiconCountBuckets")
                .GetProperty("fiftyOrMore")
                .GetInt32());
        Assert.Equal("Auersber", occurrence.GetProperty("leftPart").GetString());
        Assert.Equal("ger", occurrence.GetProperty("rightPart").GetString());
        Assert.Equal("Auersberger", occurrence.GetProperty("unhyphenatedText").GetString());
        Assert.Equal("Inline", occurrence.GetProperty("detectionKind").GetString());
        Assert.Equal(202, occurrence.GetProperty("unhyphenatedOccurrenceCount").GetInt32());
        Assert.True(occurrence.GetProperty("existsInLexicon").GetBoolean());
        Assert.Equal("chapter.xhtml", occurrence.GetProperty("leftSource").GetProperty("documentPath").GetString());
        Assert.DoesNotContain("SourceNode", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AngleSharp", json, StringComparison.OrdinalIgnoreCase);
    }

    private static HyphenationEvidence CreateEvidence(
        string leftPart,
        string rightPart,
        int unhyphenatedOccurrenceCount)
    {
        var document = new HtmlParser().ParseDocument("<p>source</p>");
        var sourceNode = Assert.IsAssignableFrom<AngleSharp.Dom.IText>(
            document.QuerySelector("p")!.FirstChild);
        var source = new TextSourceLocation("chapter.xhtml", 0, sourceNode, 0, 1);
        var candidate = new HyphenationCandidate(
            leftPart,
            rightPart,
            leftPart + rightPart,
            HyphenationDetectionKind.Inline,
            source,
            source,
            source);

        return new HyphenationEvidence(
            candidate,
            unhyphenatedOccurrenceCount,
            unhyphenatedOccurrenceCount > 0);
    }

    private static void AssertAggregate(
        HyphenationEvidenceAggregate item,
        string leftPart,
        string rightPart,
        int occurrenceCount,
        int candidateOccurrences)
    {
        Assert.Equal(leftPart, item.Key.LeftPart);
        Assert.Equal(rightPart, item.Key.RightPart);
        Assert.Equal(occurrenceCount, item.UnhyphenatedOccurrenceCount);
        Assert.Equal(candidateOccurrences, item.CandidateOccurrences);
    }
}
