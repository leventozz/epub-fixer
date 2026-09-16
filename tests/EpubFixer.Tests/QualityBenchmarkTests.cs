using System.Reflection;
using EpubFixer.Core.Epub;
using EpubFixer.Core.Correction;
using EpubFixer.Core.Detection;
using EpubFixer.Core.Evidence;
using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Mutation.Models;
using EpubFixer.QualityBenchmarks.Models;
using EpubFixer.QualityBenchmarks;

namespace EpubFixer.Tests;

public sealed class QualityBenchmarkTests
{
    [Fact]
    public void Load_ParsesValidGroundTruthJson()
    {
        using var dataset = TemporaryQualityBenchmarkDataset.Create(ValidGroundTruthJson);

        var result = new QualityBenchmarkDatasetLoader().Load(dataset.Path);

        Assert.Equal(dataset.Name, result.Name);
        var occurrence = Assert.Single(result.GroundTruth.KnownErrors);
        Assert.Equal("error-1", occurrence.Id);
        Assert.Equal("main-3.xhtml", occurrence.DocumentPath);
        Assert.Equal("Auersber-ger", occurrence.Original);
        Assert.Equal("Auersberger", occurrence.Expected);
        var span = Assert.Single(occurrence.SourceSpans);
        Assert.Equal(70, span.TextNodeIndex);
        Assert.Equal(480, span.Start);
        Assert.Equal(12, span.Length);
        Assert.Empty(result.GroundTruth.ProtectedOccurrences);
    }

    [Fact]
    public void Runner_AppliesFreshInlineAndCrossParagraphPlansInMemory()
    {
        var datasetPath = LocateRepositoryPath("test-data", "odun-kesmek");
        var dataset = new QualityBenchmarkDatasetLoader().Load(datasetPath);

        var result = new QualityBenchmarkRunner().Run(dataset);

        // R5.0d (docs/phase-5-plan.md@adc202d section 7.4) grew the ground truth's OCR arm from 12 to 112
        // records (plus 28 new Hyphenation records), so these figures moved from the previous
        // 160/148/0/12/9 baseline - re-measured directly against the real dataset (kural 4.3).
        Assert.Equal(288, result.KnownErrors);
        Assert.True(result.Detected > 250);
        Assert.Equal(259, result.CorrectlyFixed);
        Assert.Equal(3, result.WronglyFixed);
        Assert.Equal(26, result.Deferred);
        Assert.Equal(9, result.ProtectedOccurrences);
        Assert.Equal(0, result.ProtectedChanged);
        Assert.Contains(result.ClassBreakdowns, item => item.ErrorClass == OcrErrorClass.GarbageInsertion && item.Detected > 0);
    }

    [Fact]
    public void Integrity_AllowsGroundTruthInlineMutation()
    {
        using var epub = TemporaryEpub.Create(
            [new TestDocument("chapter", "chapter.xhtml", Xhtml("<p>Auersber-ger</p>"))],
            [new TestSpineItem("chapter")]);
        var package = new EpubPackageReader().Read(epub.Path);
        var decisions = Analyze(package.LogicalText);
        var plans = new HyphenationCorrectionPlanner().Plan(decisions);
        var evaluator = new QualityBenchmarkIntegrityEvaluator();
        var before = evaluator.Capture(package.SpineDocuments);

        _ = new HyphenationCorrectionApplier().Apply(plans);

        var audit = evaluator.Audit(before, package.SpineDocuments, plans, "inline");

        Assert.Empty(audit.TextChanges);
        Assert.Empty(audit.NonTextChanges);
    }

    [Fact]
    public void Tracker_ResyncKeepsLaterSpanAlignedAfterMultiCharacterOcrMutation()
    {
        using var epub = TemporaryEpub.Create(
            [new TestDocument("chapter", "chapter.xhtml", Xhtml("<p>aaaaTARGET</p>"))],
            [new TestSpineItem("chapter")]);
        var package = new EpubPackageReader().Read(epub.Path);
        var segment = package.LogicalText.Segments.Single(item => item.Text.Contains("TARGET", StringComparison.Ordinal));
        var documentPath = segment.Source.DocumentPath;
        var targetStart = segment.Source.Start + segment.Text.IndexOf("TARGET", StringComparison.Ordinal);
        var tracker = Assert.Single(QualityBenchmarkOccurrenceTracker.CreateKnown(
            [
                new KnownErrorOccurrence(
                    "known-1",
                    documentPath,
                    "TARGET",
                    "TARGET",
                    [new GroundTruthSourceSpan(documentPath, segment.Source.TextNodeIndex, targetStart, 6)])
            ],
            package.LogicalText));
        var text = (AngleSharp.Dom.IText)package.SpineDocuments[0].Document.QuerySelector("p")!.FirstChild!;
        var beforeOcrText = "aaaaTARGET";
        text.TextContent = "bTARGET";
        var mutation = new OcrCorrectionMutation(
            documentPath,
            0,
            4,
            "aaaa",
            "b",
            null!,
            [new OcrMutationSourceSpan(documentPath, segment.Source.TextNodeIndex, segment.Source.Start, 4, "aaaa")]);

        var beforeOcrTextByNode = new Dictionary<AngleSharp.Dom.IText, string>(ReferenceEqualityComparer.Instance)
        {
            [text] = beforeOcrText
        };
        var mutationNodesByLocation = new Dictionary<(string DocumentPath, int TextNodeIndex), AngleSharp.Dom.IText>
        {
            [(documentPath, segment.Source.TextNodeIndex)] = text
        };
        tracker.Resync(beforeOcrTextByNode, mutationNodesByLocation, [mutation]);

        Assert.Equal("TARGET", tracker.ReadObservedText());
    }

    [Fact]
    public void Tracker_ResyncedOverCorrectionIsReadExactlyNotMaskedAsExpected()
    {
        using var epub = TemporaryEpub.Create(
            [new TestDocument("chapter", "chapter.xhtml", Xhtml("<p>XX</p>"))],
            [new TestSpineItem("chapter")]);
        var package = new EpubPackageReader().Read(epub.Path);
        var segment = package.LogicalText.Segments.Single(item => item.Text.Contains("XX", StringComparison.Ordinal));
        var documentPath = segment.Source.DocumentPath;
        var tracker = Assert.Single(QualityBenchmarkOccurrenceTracker.CreateKnown(
            [
                new KnownErrorOccurrence(
                    "known-1",
                    documentPath,
                    "XX",
                    "ABC",
                    [new GroundTruthSourceSpan(documentPath, segment.Source.TextNodeIndex, segment.Source.Start, 2)])
            ],
            package.LogicalText));
        var text = (AngleSharp.Dom.IText)package.SpineDocuments[0].Document.QuerySelector("p")!.FirstChild!;
        var beforeOcrText = "XX";
        // The engine over-corrects: it writes one character more than Expected.
        // "ABCD".StartsWith("ABC") would previously snap the read to "ABC" and hide
        // the extra character (R1) - the resynced span is now exact, so the guard
        // must not fire.
        text.TextContent = "ABCD";
        var mutation = new OcrCorrectionMutation(
            documentPath,
            0,
            2,
            "XX",
            "ABCD",
            null!,
            [new OcrMutationSourceSpan(documentPath, segment.Source.TextNodeIndex, segment.Source.Start, 2, "XX")]);

        var beforeOcrTextByNode = new Dictionary<AngleSharp.Dom.IText, string>(ReferenceEqualityComparer.Instance)
        {
            [text] = beforeOcrText
        };
        var mutationNodesByLocation = new Dictionary<(string DocumentPath, int TextNodeIndex), AngleSharp.Dom.IText>
        {
            [(documentPath, segment.Source.TextNodeIndex)] = text
        };
        tracker.Resync(beforeOcrTextByNode, mutationNodesByLocation, [mutation]);

        Assert.Equal("ABCD", tracker.ReadObservedText());
    }

    [Fact]
    public void Integrity_ReportsUnexpectedTextAndAttributeMutation()
    {
        using var epub = TemporaryEpub.Create(
            [new TestDocument("chapter", "chapter.xhtml", Xhtml("<p id=\"stable\">Auersber-ger</p>"))],
            [new TestSpineItem("chapter")]);
        var package = new EpubPackageReader().Read(epub.Path);
        var evaluator = new QualityBenchmarkIntegrityEvaluator();
        var before = evaluator.Capture(package.SpineDocuments);
        var paragraph = package.SpineDocuments[0].Document.QuerySelector("p")!;
        paragraph.SetAttribute("id", "changed");
        paragraph.TextContent = "unexpected";

        var audit = evaluator.Audit(
            before,
            package.SpineDocuments,
            Array.Empty<EpubFixer.Core.Correction.Models.HyphenationCorrectionPlan>(),
            "inline");

        Assert.NotEmpty(audit.TextChanges);
        Assert.NotEmpty(audit.NonTextChanges);
    }

    [Fact]
    public void Integrity_AllowsGroundTruthCrossParagraphMutation()
    {
        using var epub = TemporaryEpub.Create(
            [new TestDocument("chapter", "chapter.xhtml", Xhtml("<p>kol-</p>\n<p>tukta</p>"))],
            [new TestSpineItem("chapter")]);
        var package = new EpubPackageReader().Read(epub.Path);
        var plans = new HyphenationCorrectionPlanner().Plan(Analyze(package.LogicalText));
        var evaluator = new QualityBenchmarkIntegrityEvaluator();
        var before = evaluator.Capture(package.SpineDocuments);

        _ = new EpubFixer.Core.Correction.CrossParagraphHyphenationCorrectionApplier().Apply(plans);

        var audit = evaluator.Audit(before, package.SpineDocuments, plans, "cross-paragraph");

        Assert.Empty(audit.TextChanges);
        Assert.Empty(audit.NonTextChanges);
    }

    private static IReadOnlyList<EpubFixer.Core.Decision.Models.HyphenationDecision> Analyze(
        EpubFixer.Core.Epub.Models.LogicalTextStream stream)
    {
        var candidates = new HyphenationDetector().Detect(stream);
        var lexicon = new BookLexiconBuilder().Build(stream);
        var evidence = new HyphenationEvidenceEvaluator().Evaluate(candidates, lexicon, stream);
        return new EpubFixer.Core.Decision.HyphenationDecisionEvaluator().Evaluate(evidence);
    }

    [Fact]
    public void Load_PreservesSeparateOccurrencesWithTheSameOriginalText()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "knownErrors": [
                {
                  "id": "error-1",
                  "documentPath": "main-3.xhtml",
                  "original": "Auersber-ger",
                  "expected": "Auersberger",
                  "sourceSpans": [
                    { "documentPath": "main-3.xhtml", "textNodeIndex": 70, "start": 480, "length": 12 }
                  ]
                },
                {
                  "id": "error-2",
                  "documentPath": "main-3.xhtml",
                  "original": "Auersber-ger",
                  "expected": "Auersberger",
                  "sourceSpans": [
                    { "documentPath": "main-3.xhtml", "textNodeIndex": 78, "start": 2104, "length": 12 }
                  ]
                }
              ]
            }
            """;
        using var dataset = TemporaryQualityBenchmarkDataset.Create(json);

        var occurrences = new QualityBenchmarkDatasetLoader()
            .Load(dataset.Path)
            .GroundTruth.KnownErrors;

        Assert.Equal(2, occurrences.Count);
        Assert.Equal(["error-1", "error-2"], occurrences.Select(item => item.Id));
        Assert.Equal([480, 2104], occurrences.Select(item => item.SourceSpans[0].Start));
    }

    [Fact]
    public void Loader_ReadsSchemaVersionOne_DefaultsErrorClassToUnclassified()
    {
        using var dataset = TemporaryQualityBenchmarkDataset.Create(ValidGroundTruthJson);

        var result = new QualityBenchmarkDatasetLoader().Load(dataset.Path);

        Assert.Equal(OcrErrorClass.Unclassified, Assert.Single(result.GroundTruth.KnownErrors).ErrorClass);
    }

    [Fact]
    public void Loader_ReadsSchemaVersionTwo()
    {
        const string json = """
            {
              "schemaVersion": 2,
              "knownErrors": [
                {
                  "id": "error-1",
                  "documentPath": "main-3.xhtml",
                  "original": "ge-^:cn",
                  "expected": "geçen",
                  "errorClass": "GarbageInsertion",
                  "sourceSpans": [
                    { "documentPath": "main-3.xhtml", "textNodeIndex": 79, "start": 1889, "length": 7 }
                  ]
                }
              ],
              "protectedOccurrences": []
            }
            """;
        using var dataset = TemporaryQualityBenchmarkDataset.Create(json);

        var result = new QualityBenchmarkDatasetLoader().Load(dataset.Path);

        Assert.Equal(OcrErrorClass.GarbageInsertion, Assert.Single(result.GroundTruth.KnownErrors).ErrorClass);
    }

    [Fact]
    public void Loader_RejectsSchemaVersionTwoWithoutErrorClass()
    {
        const string json = """
            {
              "schemaVersion": 2,
              "knownErrors": [
                {
                  "id": "error-1",
                  "documentPath": "main-3.xhtml",
                  "original": "ge-^:cn",
                  "expected": "geçen",
                  "sourceSpans": [
                    { "documentPath": "main-3.xhtml", "textNodeIndex": 79, "start": 1889, "length": 7 }
                  ]
                }
              ],
              "protectedOccurrences": []
            }
            """;
        using var dataset = TemporaryQualityBenchmarkDataset.Create(json);

        var exception = Assert.Throws<InvalidDataException>(
            () => new QualityBenchmarkDatasetLoader().Load(dataset.Path));

        Assert.Contains("errorClass", exception.Message);
    }

    [Fact]
    public void Loader_StillReadsSchemaVersionTwo_AfterSchemaVersionThreeWasIntroduced()
    {
        // R5.0d (docs/phase-5-plan.md@adc202d section 7.4): schemaVersion moved to 3 to carry
        // verifiedBy/legacyProposal/source. The loader must keep reading a plain schemaVersion 2
        // document - one written before those fields existed - without requiring any of them.
        const string json = """
            {
              "schemaVersion": 2,
              "knownErrors": [
                {
                  "id": "error-1",
                  "documentPath": "main-3.xhtml",
                  "original": "ge-^:cn",
                  "expected": "geçen",
                  "errorClass": "GarbageInsertion",
                  "sourceSpans": [
                    { "documentPath": "main-3.xhtml", "textNodeIndex": 79, "start": 1889, "length": 7 }
                  ]
                }
              ],
              "protectedOccurrences": []
            }
            """;
        using var dataset = TemporaryQualityBenchmarkDataset.Create(json);

        var occurrence = Assert.Single(new QualityBenchmarkDatasetLoader().Load(dataset.Path).GroundTruth.KnownErrors);

        Assert.Equal(OcrErrorClass.GarbageInsertion, occurrence.ErrorClass);
        Assert.Null(occurrence.VerifiedBy);
        Assert.Null(occurrence.LegacyProposal);
        Assert.Null(occurrence.Source);
    }

    [Fact]
    public void Loader_ReadsSchemaVersionThree_WithVerificationMetadata()
    {
        const string json = """
            {
              "schemaVersion": 3,
              "knownErrors": [
                {
                  "id": "error-1",
                  "documentPath": "main-3.xhtml",
                  "original": "olın",
                  "expected": "John",
                  "errorClass": "GlyphConfusion",
                  "verifiedBy": "manual",
                  "legacyProposal": "olan",
                  "source": "engine-diff",
                  "sourceSpans": [
                    { "documentPath": "main-3.xhtml", "textNodeIndex": 180, "start": 580, "length": 4 }
                  ]
                },
                {
                  "id": "error-2",
                  "documentPath": "main-3.xhtml",
                  "original": "kü-^:ük",
                  "expected": "küçük",
                  "errorClass": "Mixed",
                  "verifiedBy": "manual",
                  "source": "engine-diff",
                  "sourceSpans": [
                    { "documentPath": "main-3.xhtml", "textNodeIndex": 96, "start": 1315, "length": 7 }
                  ]
                }
              ],
              "protectedOccurrences": []
            }
            """;
        using var dataset = TemporaryQualityBenchmarkDataset.Create(json);

        var occurrences = new QualityBenchmarkDatasetLoader().Load(dataset.Path).GroundTruth.KnownErrors;

        var withLegacyProposal = occurrences.Single(item => item.Id == "error-1");
        Assert.Equal("manual", withLegacyProposal.VerifiedBy);
        Assert.Equal("olan", withLegacyProposal.LegacyProposal);
        Assert.Equal("engine-diff", withLegacyProposal.Source);
        Assert.Equal(OcrErrorClass.GlyphConfusion, withLegacyProposal.ErrorClass);

        var withoutLegacyProposal = occurrences.Single(item => item.Id == "error-2");
        Assert.Equal("manual", withoutLegacyProposal.VerifiedBy);
        Assert.Null(withoutLegacyProposal.LegacyProposal);
        Assert.Equal("engine-diff", withoutLegacyProposal.Source);
        Assert.Equal(OcrErrorClass.Mixed, withoutLegacyProposal.ErrorClass);
    }

    [Fact]
    public void Load_ParsesSeparateProtectedOccurrencesWithTheSameOriginalText()
    {
        var json = CreateGroundTruthJsonWithProtected(
            [],
            CreateProtectedOccurrenceJson("protected-1", start: 27),
            CreateProtectedOccurrenceJson("protected-2", start: 81));
        using var dataset = TemporaryQualityBenchmarkDataset.Create(json);

        var occurrences = new QualityBenchmarkDatasetLoader()
            .Load(dataset.Path)
            .GroundTruth.ProtectedOccurrences;

        Assert.Equal(2, occurrences.Count);
        Assert.Equal(["protected-1", "protected-2"], occurrences.Select(item => item.Id));
        Assert.All(occurrences, item => Assert.Equal("e-posta", item.Original));
        Assert.Equal([27, 81], occurrences.Select(item => item.SourceSpans[0].Start));
    }

    [Fact]
    public void Load_RejectsExplicitlyNullProtectedOccurrences()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "knownErrors": [],
              "protectedOccurrences": null
            }
            """;
        using var dataset = TemporaryQualityBenchmarkDataset.Create(json);

        var exception = Assert.Throws<InvalidDataException>(
            () => new QualityBenchmarkDatasetLoader().Load(dataset.Path));

        Assert.Contains("protectedOccurrences must not be null", exception.Message);
    }

    [Fact]
    public void Load_RejectsDuplicateProtectedIds()
    {
        var json = CreateGroundTruthJsonWithProtected(
            [],
            CreateProtectedOccurrenceJson("duplicate-id", start: 27),
            CreateProtectedOccurrenceJson("duplicate-id", start: 81));
        using var dataset = TemporaryQualityBenchmarkDataset.Create(json);

        var exception = Assert.Throws<InvalidDataException>(
            () => new QualityBenchmarkDatasetLoader().Load(dataset.Path));

        Assert.Contains("id must be unique", exception.Message);
    }

    [Fact]
    public void Load_RejectsProtectedIdDuplicatedByKnownError()
    {
        var json = CreateGroundTruthJsonWithProtected(
            [CreateOccurrenceJson("duplicate-id", start: 10)],
            CreateProtectedOccurrenceJson("duplicate-id", start: 27));
        using var dataset = TemporaryQualityBenchmarkDataset.Create(json);

        var exception = Assert.Throws<InvalidDataException>(
            () => new QualityBenchmarkDatasetLoader().Load(dataset.Path));

        Assert.Contains("id must be unique", exception.Message);
    }

    [Fact]
    public void Load_RejectsDuplicateExactProtectedOccurrences()
    {
        var json = CreateGroundTruthJsonWithProtected(
            [],
            CreateProtectedOccurrenceJson("protected-1", start: 27),
            CreateProtectedOccurrenceJson("protected-2", start: 27));
        using var dataset = TemporaryQualityBenchmarkDataset.Create(json);

        var exception = Assert.Throws<InvalidDataException>(
            () => new QualityBenchmarkDatasetLoader().Load(dataset.Path));

        Assert.Contains("duplicates an exact source occurrence", exception.Message);
    }

    [Fact]
    public void Load_RejectsProtectedOccurrenceAtKnownErrorLocation()
    {
        var json = CreateGroundTruthJsonWithProtected(
            [CreateOccurrenceJson("error-1", start: 10)],
            CreateProtectedOccurrenceJson(
                "protected-1",
                start: 10,
                length: 12,
                original: "Auersber-ger"));
        using var dataset = TemporaryQualityBenchmarkDataset.Create(json);

        var exception = Assert.Throws<InvalidDataException>(
            () => new QualityBenchmarkDatasetLoader().Load(dataset.Path));

        Assert.Contains("duplicates an exact source occurrence", exception.Message);
    }

    [Fact]
    public void Load_RejectsStructurallyInvalidProtectedSourceSpan()
    {
        var json = CreateGroundTruthJsonWithProtected(
            [],
            CreateProtectedOccurrenceJson("protected-1", start: 27, length: 0));
        using var dataset = TemporaryQualityBenchmarkDataset.Create(json);

        var exception = Assert.Throws<InvalidDataException>(
            () => new QualityBenchmarkDatasetLoader().Load(dataset.Path));

        Assert.Contains("length > 0", exception.Message);
    }

    [Fact]
    public void Load_ReportsMissingInputEpub()
    {
        using var dataset = TemporaryQualityBenchmarkDataset.Create(
            ValidGroundTruthJson,
            createInputEpub: false);

        var exception = Assert.Throws<FileNotFoundException>(
            () => new QualityBenchmarkDatasetLoader().Load(dataset.Path));

        Assert.Contains("Input EPUB was not found", exception.Message);
        Assert.EndsWith("input.epub", exception.FileName, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_ReportsMissingGroundTruth()
    {
        using var dataset = TemporaryQualityBenchmarkDataset.Create(groundTruthJson: null);

        var exception = Assert.Throws<FileNotFoundException>(
            () => new QualityBenchmarkDatasetLoader().Load(dataset.Path));

        Assert.Contains("Ground truth file was not found", exception.Message);
        Assert.EndsWith("ground-truth.json", exception.FileName, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_ReportsEmptyGroundTruth()
    {
        using var dataset = TemporaryQualityBenchmarkDataset.Create(string.Empty);

        var exception = Assert.Throws<InvalidDataException>(
            () => new QualityBenchmarkDatasetLoader().Load(dataset.Path));

        Assert.Contains("Ground truth file is empty", exception.Message);
    }

    [Fact]
    public void Load_ReportsMalformedGroundTruth()
    {
        using var dataset = TemporaryQualityBenchmarkDataset.Create("{ not-json }");

        var exception = Assert.Throws<InvalidDataException>(
            () => new QualityBenchmarkDatasetLoader().Load(dataset.Path));

        Assert.Contains("Ground truth JSON is invalid", exception.Message);
        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public void Load_RejectsDuplicateIds()
    {
        var json = CreateGroundTruthJson(
            CreateOccurrenceJson("duplicate-id", start: 10),
            CreateOccurrenceJson("duplicate-id", start: 30));
        using var dataset = TemporaryQualityBenchmarkDataset.Create(json);

        var exception = Assert.Throws<InvalidDataException>(
            () => new QualityBenchmarkDatasetLoader().Load(dataset.Path));

        Assert.Contains("id must be unique", exception.Message);
    }

    [Fact]
    public void Load_RejectsDuplicateExactOccurrencesWithDifferentIds()
    {
        var json = CreateGroundTruthJson(
            CreateOccurrenceJson("error-1", start: 10),
            CreateOccurrenceJson("error-2", start: 10));
        using var dataset = TemporaryQualityBenchmarkDataset.Create(json);

        var exception = Assert.Throws<InvalidDataException>(
            () => new QualityBenchmarkDatasetLoader().Load(dataset.Path));

        Assert.Contains("duplicates an exact source occurrence", exception.Message);
    }

    [Fact]
    public void Load_RejectsStructurallyInvalidSourceSpan()
    {
        var json = CreateGroundTruthJson(
            CreateOccurrenceJson("error-1", start: 10, length: 0));
        using var dataset = TemporaryQualityBenchmarkDataset.Create(json);

        var exception = Assert.Throws<InvalidDataException>(
            () => new QualityBenchmarkDatasetLoader().Load(dataset.Path));

        Assert.Contains("length > 0", exception.Message);
    }

    [Fact]
    public void ProductionAssemblies_DoNotReferenceQualityBenchmarks()
    {
        var productionAssemblies = new[]
        {
            typeof(EpubPackageReader).Assembly,
            Assembly.Load("EpubFixer.Cli")
        };

        foreach (var assembly in productionAssemblies)
        {
            Assert.DoesNotContain(
                assembly.GetReferencedAssemblies(),
                reference => string.Equals(
                    reference.Name,
                    "EpubFixer.QualityBenchmarks",
                    StringComparison.Ordinal));
        }
    }

    private const string ValidGroundTruthJson = """
        {
          "schemaVersion": 1,
          "knownErrors": [
            {
              "id": "error-1",
              "documentPath": "main-3.xhtml",
              "original": "Auersber-ger",
              "expected": "Auersberger",
              "sourceSpans": [
                {
                  "documentPath": "main-3.xhtml",
                  "textNodeIndex": 70,
                  "start": 480,
                  "length": 12
                }
              ]
            }
          ]
        }
        """;

    private static string CreateGroundTruthJson(params string[] occurrences)
    {
        return $$"""
            {
              "schemaVersion": 1,
              "knownErrors": [
                {{string.Join(",", occurrences)}}
              ]
            }
            """;
    }

    private static string CreateOccurrenceJson(string id, int start, int length = 12)
    {
        return $$"""
            {
              "id": "{{id}}",
              "documentPath": "main-3.xhtml",
              "original": "Auersber-ger",
              "expected": "Auersberger",
              "sourceSpans": [
                {
                  "documentPath": "main-3.xhtml",
                  "textNodeIndex": 70,
                  "start": {{start}},
                  "length": {{length}}
                }
              ]
            }
            """;
    }

    private static string CreateGroundTruthJsonWithProtected(
        IReadOnlyList<string> knownErrors,
        params string[] protectedOccurrences)
    {
        return $$"""
            {
              "schemaVersion": 1,
              "knownErrors": [
                {{string.Join(",", knownErrors)}}
              ],
              "protectedOccurrences": [
                {{string.Join(",", protectedOccurrences)}}
              ]
            }
            """;
    }

    private static string CreateProtectedOccurrenceJson(
        string id,
        int start,
        int length = 7,
        string original = "e-posta")
    {
        return $$"""
            {
              "id": "{{id}}",
              "documentPath": "main-3.xhtml",
              "original": "{{original}}",
              "sourceSpans": [
                {
                  "documentPath": "main-3.xhtml",
                  "textNodeIndex": 70,
                  "start": {{start}},
                  "length": {{length}}
                }
              ]
            }
            """;
    }

    private static string LocateRepositoryPath(params string[] parts)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = directory.FullName;
            foreach (var part in parts)
            {
                candidate = Path.Combine(candidate, part);
            }
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException(string.Join(Path.DirectorySeparatorChar, parts));
    }

    private static string Xhtml(string body)
    {
        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <!DOCTYPE html>
            <html xmlns="http://www.w3.org/1999/xhtml">
              <head><title>Test</title></head>
              <body>{body}</body>
            </html>
            """;
    }
}

internal sealed class TemporaryQualityBenchmarkDataset : IDisposable
{
    private TemporaryQualityBenchmarkDataset(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public string Name => System.IO.Path.GetFileName(Path);

    public static TemporaryQualityBenchmarkDataset Create(
        string? groundTruthJson,
        bool createInputEpub = true)
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"epubfixer-quality-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);

        if (createInputEpub)
        {
            File.WriteAllBytes(System.IO.Path.Combine(path, "input.epub"), []);
        }

        if (groundTruthJson is not null)
        {
            File.WriteAllText(System.IO.Path.Combine(path, "ground-truth.json"), groundTruthJson);
        }

        return new TemporaryQualityBenchmarkDataset(path);
    }

    public void Dispose()
    {
        Directory.Delete(Path, recursive: true);
    }
}
