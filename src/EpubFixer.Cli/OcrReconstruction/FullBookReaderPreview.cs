using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using AngleSharp.Dom;
using EpubFixer.Core.Correction;
using EpubFixer.Core.Correction.Models;
using EpubFixer.Core.Decision.Models;
using EpubFixer.Core.Detection;
using EpubFixer.Core.Epub;
using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Evidence;
using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Lexicon.Models;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Mutation;
using EpubFixer.Core.Mutation.Models;
using EpubFixer.Core.Ocr;
using EpubFixer.Core.Ocr.Models;
using EpubFixer.TrMorph;

namespace EpubFixer.Cli.OcrReconstruction;

internal static class FullBookReaderPreview
{
    private static readonly string[] SanityTargets =
    [
        "ı ıç", "kendi-ıni", "1 ı iç", ":,ohbet", "koli ukta",
        "ı ızellikle", "ı ılduğu", "Ce-lıimde", "ge-^:cn", "yü-ıiimeye"
    ];

    public static int Run(string[] arguments)
    {
        if (arguments.Length is < 2 or > 4)
        {
            Console.Error.WriteLine("Usage: reader-preview <original.epub> [output.epub] [audit.md]");
            return 1;
        }

        var inputPath = Path.GetFullPath(arguments[1]);
        var outputPath = Path.GetFullPath(arguments.Length >= 3
            ? arguments[2]
            : "artifacts/reader-preview/Odun Kesmek_reader-preview-v1.epub");
        var auditPath = Path.GetFullPath(arguments.Length >= 4
            ? arguments[3]
            : "artifacts/debug/full-book-reader-preview.md");

        try
        {
            if (!File.Exists(inputPath)) throw new FileNotFoundException("Source EPUB was not found.", inputPath);
            if (File.Exists(outputPath)) throw new IOException($"Preview output already exists: '{outputPath}'.");
            if (PathsEqual(inputPath, outputPath)) throw new ArgumentException("Preview output must not overwrite the source EPUB.");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(auditPath)!);

            var totalWatch = Stopwatch.StartNew();
            var sourceHash = SHA256.HashData(File.ReadAllBytes(inputPath));
            using var analyzer = new FomaTurkishMorphologyAnalyzer();
            var package = new EpubPackageReader().Read(inputPath);

            Console.WriteLine("Phase: frozen hyphenation V1/V2");
            ApplyFrozenHyphenation(package, analyzer);
            var stream = LogicalTextStreamBuilder.Build(package.SpineDocuments);
            var postHyphenNodes = CaptureTextNodes(package);

            var batch = (IBatchTurkishMorphologicalParser)analyzer;
            Console.WriteLine("Phase: detector prewarm and corrupted-region detection");
            AnalyzeInChunks(batch, CollectDetectorInputs(stream.Text));
            var regions = new OcrRegionDetector().Detect(stream.Text, analyzer);
            var book = new BookLexiconBuilder().Build(stream);
            var cleanPath = Path.Combine(AppContext.BaseDirectory, "Resources", "OcrReconstruction", "tr_50k.txt");
            var clean = CleanTurkishLexicon.Load(cleanPath, analyzer);
            var noisy = new NoisyChannelRegionReconstructor(clean, book, analyzer);

            Console.WriteLine($"Phase: NoisyChannel prewarm and Top5 generation ({regions.Count} regions)");
            AnalyzeInChunks(batch, book.Entries.Keys.Concat(regions.SelectMany(noisy.CollectMorphologyInputs)));
            var generationWatch = Stopwatch.StartNew();
            var generated = regions.Select(region => noisy.ReconstructDetailed(region)).ToArray();
            generationWatch.Stop();

            var contextIndex = BookContextIndex.Build(stream.Text, regions);
            var reranker = new DeterministicOcrCandidateReranker(analyzer);
            var contexts = regions.Select(region => CreateContext(stream.Text, region, contextIndex)).ToArray();
            Console.WriteLine("Phase: reranker prewarm and V3 scoring");
            AnalyzeInChunks(batch, generated.SelectMany(items => items).Select(item => item.Text)
                .Concat(contexts.SelectMany(context => context.PreviousWords.Concat(context.NextWords))));
            var rerankWatch = Stopwatch.StartNew();
            var rows = regions.Select((region, index) =>
            {
                var candidates = generated[index].Select((candidate, rank) =>
                    new ReconstructionCandidate(candidate.Text, candidate.Score, rank + 1,
                        ReconstructionSource.NoisyChannel, [candidate.Evidence])).ToArray();
                var details = reranker.RerankDetailed(region, candidates, contexts[index]);
                return new PreviewRow(index + 1, region, generated[index], details);
            }).ToArray();
            rerankWatch.Stop();

            var decisions = rows.Where(row => row.Top1 is not null)
                .Select(row => CreateExperimentalDecision(row.Region, row.Top1!.Candidate.Text, stream))
                .ToArray();
            var plan = new OcrCorrectionMutationPlanner().Create(decisions, stream);
            if (!plan.IsValid)
                throw new InvalidDataException("Experimental source-span plan failed: " + string.Join(", ", plan.Failures.Select(f => f.Reason)));

            var mutationWatch = Stopwatch.StartNew();
            Console.WriteLine($"Phase: safe source-span mutation ({decisions.Length} regions)");
            var mutation = new OcrCorrectionMutationApplier().Apply(package, plan);
            mutationWatch.Stop();
            if (!mutation.Succeeded)
                throw new InvalidDataException("Experimental source-span mutation failed: " + string.Join(", ", mutation.Failures.Select(f => f.Reason)));

            var changedNodeKeys = ChangedNodeKeys(postHyphenNodes, package);
            var intendedNodeKeys = plan.Mutations.SelectMany(item => item.SourceSpans)
                .Select(span => (span.DocumentPath, span.TextNodeIndex)).ToHashSet();
            if (!changedNodeKeys.IsSubsetOf(intendedNodeKeys))
                throw new InvalidDataException("A text node outside the planned corrupted-region source spans changed.");

            var temporaryPath = Path.Combine(Path.GetDirectoryName(outputPath)!, $".{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.tmp");
            EpubWriteResult writeResult;
            try
            {
                Console.WriteLine("Phase: rebuild and structural validation");
                writeResult = new EpubPackageWriter().Write(inputPath, temporaryPath, package);
                var validation = Validate(inputPath, temporaryPath, sourceHash, package, writeResult);
                File.Move(temporaryPath, outputPath, false);
                totalWatch.Stop();

                var stats = batch.CacheStatistics;
                var audit = RenderAudit(inputPath, outputPath, rows, stream, book, clean, analyzer,
                    writeResult, validation, totalWatch.Elapsed, generationWatch.Elapsed,
                    rerankWatch.Elapsed, mutationWatch.Elapsed, stats);
                File.WriteAllText(auditPath, audit, new UTF8Encoding(false));

                Console.WriteLine($"Source EPUB: {inputPath}");
                Console.WriteLine($"Output EPUB: {outputPath}");
                Console.WriteLine($"Detected regions: {regions.Count}");
                Console.WriteLine($"Mutated regions: {mutation.AppliedCount}");
                Console.WriteLine($"Unchanged/no-candidate: {regions.Count - mutation.AppliedCount}");
                Console.WriteLine($"Changed entries: {string.Join(", ", writeResult.ModifiedDocumentPaths)}");
                Console.WriteLine($"Structural validation: {validation}");
                Console.WriteLine($"Total runtime ms: {totalWatch.Elapsed.TotalMilliseconds:0.000}");
                Console.WriteLine($"TRmorph: invocations={stats.ProcessInvocations}; batches={stats.BatchRequests}; average={stats.AverageBatchSize:0.000}; hit-ratio={HitRatio(stats):P2}");
                Console.WriteLine($"Audit report: {auditPath}");
                return 0;
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException or UnauthorizedAccessException or TurkishMorphologyException)
        {
            Console.Error.WriteLine($"Error: {exception.Message}");
            return 2;
        }
    }

    public static int InspectPrewarm(string[] arguments)
    {
        if (arguments.Length != 4 || !int.TryParse(arguments[2], out var firstBatch) || !int.TryParse(arguments[3], out var lastBatch))
        {
            Console.Error.WriteLine("Usage: inspect-reader-preview-prewarm <original.epub> <first-batch> <last-batch>");
            return 1;
        }
        using var analyzer = new FomaTurkishMorphologyAnalyzer();
        var package = new EpubPackageReader().Read(Path.GetFullPath(arguments[1]));
        ApplyFrozenHyphenation(package, analyzer);
        var stream = LogicalTextStreamBuilder.Build(package.SpineDocuments);
        AnalyzeInChunks((IBatchTurkishMorphologicalParser)analyzer, CollectDetectorInputs(stream.Text));
        var regions = new OcrRegionDetector().Detect(stream.Text, analyzer);
        var book = new BookLexiconBuilder().Build(stream);
        var clean = CleanTurkishLexicon.Load(Path.Combine(AppContext.BaseDirectory, "Resources", "OcrReconstruction", "tr_50k.txt"), analyzer);
        var noisy = new NoisyChannelRegionReconstructor(clean, book, analyzer);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new List<(string Word, string Source)>();
        var batchNumber = 0;

        void Add(string word, string source)
        {
            if (string.IsNullOrWhiteSpace(word) || !seen.Add(word)) return;
            pending.Add((word, source));
            if (pending.Count != 16) return;
            batchNumber++;
            if (batchNumber >= firstBatch && batchNumber <= lastBatch)
                Console.WriteLine($"BATCH {batchNumber}\t{string.Join(" || ", pending.Select(item => $"{item.Source}: {item.Word.Replace("\r", "\\r").Replace("\n", "\\n")}"))}");
            pending.Clear();
        }

        foreach (var word in book.Entries.Keys) Add(word, "BookLexicon");
        var regionInputs = new IReadOnlyCollection<string>[regions.Count];
        Parallel.For(0, regions.Count, index => regionInputs[index] = noisy.CollectMorphologyInputs(regions[index]));
        foreach (var (region, index) in regions.Select((region, index) => (region, index + 1)))
            foreach (var word in regionInputs[index - 1]) Add(word, $"region #{index} `{region.RawText.Replace("\r", "\\r").Replace("\n", "\\n")}`");
        if (pending.Count > 0)
        {
            batchNumber++;
            if (batchNumber >= firstBatch && batchNumber <= lastBatch)
                Console.WriteLine($"BATCH {batchNumber}\t{string.Join(" || ", pending.Select(item => $"{item.Source}: {item.Word.Replace("\r", "\\r").Replace("\n", "\\n")}"))}");
        }
        Console.WriteLine($"TOTAL PREWARM BATCHES: {batchNumber}");
        return 0;
    }

    private static void ApplyFrozenHyphenation(EpubPackage package, ITurkishMorphologyAnalyzer analyzer)
    {
        static (IReadOnlyList<HyphenationCorrectionPlan> Plans, IReadOnlyList<EpubFixer.Core.Detection.Models.HyphenationCandidate> Candidates) Analyze(LogicalTextStream value)
        {
            var candidates = new HyphenationDetector().Detect(value);
            var lexicon = new BookLexiconBuilder().Build(value);
            var evidence = new HyphenationEvidenceEvaluator().Evaluate(candidates, lexicon, value);
            var decisions = new EpubFixer.Core.Decision.HyphenationDecisionEvaluator().Evaluate(evidence);
            return (new HyphenationCorrectionPlanner().Plan(decisions), candidates);
        }

        static IReadOnlyList<HyphenationCorrectionPlan> AnalyzeV2(LogicalTextStream value, ITurkishMorphologyAnalyzer morph)
        {
            var candidates = new HyphenationDetector().Detect(value);
            var lexicon = new BookLexiconBuilder().Build(value);
            var evidence = new HyphenationEvidenceEvaluator().Evaluate(candidates, lexicon, value);
            var morphology = new EpubFixer.Core.Morphology.HyphenationMorphologyAnalyzer().Analyze(evidence, morph);
            var decisions = new EpubFixer.Core.Decision.HyphenationV2DecisionEvaluator().Evaluate(evidence, morphology);
            return new HyphenationCorrectionPlanner().Plan(decisions);
        }

        var v1 = Analyze(package.LogicalText);
        new HyphenationCorrectionApplier().Apply(v1.Plans.Where(p => p.CorrectionKind == HyphenationCorrectionKind.Inline).ToArray());
        var v1Refreshed = Analyze(LogicalTextStreamBuilder.Build(package.SpineDocuments));
        new CrossParagraphHyphenationCorrectionApplier().Apply(v1Refreshed.Plans.Where(p => p.CorrectionKind == HyphenationCorrectionKind.CrossParagraph).ToArray());

        var v2 = AnalyzeV2(LogicalTextStreamBuilder.Build(package.SpineDocuments), analyzer);
        new HyphenationCorrectionApplier().Apply(v2.Where(p => p.CorrectionKind == HyphenationCorrectionKind.Inline).ToArray());
        var v2Refreshed = AnalyzeV2(LogicalTextStreamBuilder.Build(package.SpineDocuments), analyzer);
        new CrossParagraphHyphenationCorrectionApplier().Apply(v2Refreshed.Where(p => p.CorrectionKind == HyphenationCorrectionKind.CrossParagraph).ToArray());
    }

    private static OcrCorrectionDecision CreateExperimentalDecision(CorruptedTextRegion region, string replacement, LogicalTextStream stream)
    {
        var locations = Enumerable.Range(region.Start, region.Length).Select(stream.GetSourceLocationAt).ToArray();
        if (locations.Length == 0) throw new InvalidDataException("A detected region has no source locations.");
        var source = new OcrWordCandidate(region.RawText, region.Start, locations, locations[0].DocumentPath, region.ContextBefore, region.ContextAfter);
        var evidence = new OcrWordEvidence(source, 0, region.RawText, 0, false, [], OcrConfidence.EvidenceOnly);
        var occurrence = new OcrCorrectionOccurrence(evidence, source, "", region.RawText, "", []);
        var proposal = new OcrCorrectionCandidate(source, OcrConfidence.EvidenceOnly, replacement, [], 0, 0, false, 0, 1, [source], 0, false);
        var selected = new OcrCorrectionProposalSafetyEvidence(proposal, OcrCasePattern.Mixed, true, false);
        return new OcrCorrectionDecision(occurrence, OcrCorrectionDecisionKind.AutoFixCandidate, selected,
            [OcrCorrectionDecisionReason.UniqueStructuralWinner], [], OcrCasePattern.Mixed);
    }

    private static OcrContext CreateContext(string text, CorruptedTextRegion region, IOcrBookContextLookup book)
    {
        static string[] Words(string value) => Regex.Matches(value, "[^\\s\\p{P}]+").Select(match => match.Value).ToArray();
        return new OcrContext(Words(text[..region.Start]).TakeLast(4).ToArray(),
            Words(text[region.EndExclusive..]).Take(4).ToArray(),
            new string(text[..region.Start].TakeLast(512).ToArray()),
            new string(text[region.EndExclusive..].Take(512).ToArray()), book);
    }

    private static IEnumerable<string> CollectDetectorInputs(string text)
    {
        foreach (Match match in Regex.Matches(text, @"[\p{L}\p{N}][\p{L}\p{N}'’.,;:^<>()!?-]*", RegexOptions.CultureInvariant))
        {
            var value = match.Value;
            var start = 0;
            var end = value.Length;
            while (start < end && value[start] is ',' or '.' or ':' or ';' or '!' or '?') start++;
            while (end > start && value[end - 1] is ',' or '.' or ':' or ';' or '!' or '?') end--;
            if (end > start) yield return value[start..end];
        }
    }

    private static void AnalyzeInChunks(IBatchTurkishMorphologicalParser analyzer, IEnumerable<string> words)
    {
        var completed = 0;
        foreach (var chunk in words.Where(word => !string.IsNullOrWhiteSpace(word)).Distinct(StringComparer.Ordinal).Chunk(16))
        {
            analyzer.AnalyzeBatch(chunk);
            completed++;
            if (completed % 100 == 0) Console.WriteLine($"  TRmorph batches completed in phase: {completed}");
        }
    }

    private static Dictionary<(string Document, int Node), string> CaptureTextNodes(EpubPackage package) =>
        package.SpineDocuments.SelectMany(document => EnumerateTextNodes(document.Document.Body!)
            .Select((node, index) => new KeyValuePair<(string, int), string>((document.Path, index), node.Data)))
            .ToDictionary(item => item.Key, item => item.Value);

    private static HashSet<(string Document, int Node)> ChangedNodeKeys(
        IReadOnlyDictionary<(string Document, int Node), string> before, EpubPackage package)
    {
        var after = CaptureTextNodes(package);
        if (!before.Keys.ToHashSet().SetEquals(after.Keys)) throw new InvalidDataException("Text-node inventory changed during OCR mutation.");
        return before.Keys.Where(key => !string.Equals(before[key], after[key], StringComparison.Ordinal)).ToHashSet();
    }

    private static IEnumerable<IText> EnumerateTextNodes(INode node)
    {
        foreach (var child in node.ChildNodes)
        {
            if (child is IText text) yield return text;
            foreach (var descendant in EnumerateTextNodes(child)) yield return descendant;
        }
    }

    private static string Validate(string inputPath, string outputPath, byte[] sourceHash, EpubPackage expected, EpubWriteResult writeResult)
    {
        if (!sourceHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(inputPath))))
            throw new InvalidDataException("The original source EPUB changed during the preview run.");

        using var inputFile = File.OpenRead(inputPath);
        using var outputFile = File.OpenRead(outputPath);
        using var input = new ZipArchive(inputFile, ZipArchiveMode.Read);
        using var output = new ZipArchive(outputFile, ZipArchiveMode.Read);
        var inputNames = input.Entries.Select(entry => entry.FullName).ToArray();
        var outputNames = output.Entries.Select(entry => entry.FullName).ToArray();
        if (!inputNames.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(outputNames.OrderBy(x => x, StringComparer.Ordinal)))
            throw new InvalidDataException("ZIP entry inventory changed.");

        var changed = new List<string>();
        foreach (var entry in input.Entries)
        {
            var other = output.GetEntry(entry.FullName) ?? throw new InvalidDataException($"Missing output entry '{entry.FullName}'.");
            using var left = entry.Open();
            using var right = other.Open();
            if (!SHA256.HashData(left).SequenceEqual(SHA256.HashData(right))) changed.Add(entry.FullName);
        }
        if (!changed.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(writeResult.ModifiedDocumentPaths.OrderBy(x => x, StringComparer.Ordinal)))
            throw new InvalidDataException("Actual changed ZIP entries differ from the writer's intended XHTML entries.");

        foreach (var entry in output.Entries.Where(entry => entry.FullName.EndsWith(".xhtml", StringComparison.OrdinalIgnoreCase)))
        {
            using var content = entry.Open();
            using var reader = new StreamReader(content, new UTF8Encoding(false, true), true);
            var source = reader.ReadToEnd();
            using var xml = XmlReader.Create(new StringReader(source), new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null });
            while (xml.Read()) { }
        }

        var actual = new EpubPackageReader().Read(outputPath);
        if (expected.SpineDocuments.Count != actual.SpineDocuments.Count) throw new InvalidDataException("Spine document count changed.");
        for (var index = 0; index < expected.SpineDocuments.Count; index++)
        {
            var left = expected.SpineDocuments[index];
            var right = actual.SpineDocuments[index];
            if (left.Path != right.Path || StructuralSignature(left.Document) != StructuralSignature(right.Document))
                throw new InvalidDataException($"Tag structure or attributes changed in '{left.Path}'.");
            if (left.Document.QuerySelectorAll("p").Length != right.Document.QuerySelectorAll("p").Length)
                throw new InvalidDataException($"Paragraph count changed in '{left.Path}'.");
        }
        if (!string.Equals(LogicalTextStreamBuilder.Build(expected.SpineDocuments).Text, actual.LogicalText.Text, StringComparison.Ordinal))
            throw new InvalidDataException("Read-back logical text differs from the safely mutated working DOM.");

        var mime = output.Entries.FirstOrDefault();
        if (input.GetEntry("mimetype") is not null && (mime?.FullName != "mimetype" || IsCompressed(outputPath)))
            throw new InvalidDataException("EPUB mimetype packaging is invalid.");
        return $"PASS — ZIP integrity, {output.Entries.Count} entries, all XHTML XML-parsed, tag/attribute topology preserved, intended text-node changes only, paragraph inventory preserved, UTF-8/read-back valid";
    }

    private static bool IsCompressed(string path)
    {
        using var file = File.OpenRead(path);
        Span<byte> header = stackalloc byte[10];
        file.ReadExactly(header);
        return BitConverter.ToUInt16(header.Slice(8, 2)) != 0;
    }

    private static string StructuralSignature(INode node)
    {
        var builder = new StringBuilder();
        void Append(INode current)
        {
            builder.Append((int)current.NodeType).Append(':').Append(current.NodeName).Append('|');
            if (current is IElement element)
                foreach (var attribute in element.Attributes.Select(a => $"{a.NamespaceUri}\u001f{a.Prefix}\u001f{a.LocalName}\u001f{a.Value}").OrderBy(x => x, StringComparer.Ordinal))
                    builder.Append(attribute.Length).Append(':').Append(attribute);
            builder.Append('#').Append(current.ChildNodes.Length).Append(';');
            foreach (var child in current.ChildNodes) Append(child);
        }
        Append(node);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static string RenderAudit(string inputPath, string outputPath, IReadOnlyList<PreviewRow> rows,
        LogicalTextStream stream, BookLexicon book, CleanTurkishLexicon clean, ITurkishMorphologyAnalyzer analyzer,
        EpubWriteResult writeResult, string validation, TimeSpan total, TimeSpan generation, TimeSpan reranking,
        TimeSpan mutation, TurkishMorphologyCacheStatistics stats)
    {
        var builder = new StringBuilder("# Full-Book Reader Preview\n\n");
        builder.AppendLine($"- Source EPUB: `{Escape(inputPath)}`")
            .AppendLine($"- Output EPUB: `{Escape(outputPath)}`")
            .AppendLine($"- Total detected corrupted regions: {rows.Count}")
            .AppendLine($"- Regions with candidates: {rows.Count(row => row.Top1 is not null)}")
            .AppendLine($"- Regions mutated: {rows.Count(row => row.Top1 is not null)}")
            .AppendLine($"- Regions left unchanged: {rows.Count(row => row.Top1 is null)}")
            .AppendLine($"- Changed EPUB entries: {string.Join(", ", writeResult.ModifiedDocumentPaths.Select(path => $"`{Escape(path)}`"))}")
            .AppendLine($"- Structural validation: {validation}")
            .AppendLine();
        builder.AppendLine("## Performance\n")
            .AppendLine($"- Total runtime: {total.TotalMilliseconds:0.000} ms")
            .AppendLine($"- TRmorph process invocations: {stats.ProcessInvocations}")
            .AppendLine($"- Batch requests: {stats.BatchRequests}")
            .AppendLine($"- Average batch size: {stats.AverageBatchSize:0.000}")
            .AppendLine($"- Cache hit ratio: {HitRatio(stats):P2}")
            .AppendLine($"- Detected region count: {rows.Count}")
            .AppendLine($"- Candidate generation time: {generation.TotalMilliseconds:0.000} ms")
            .AppendLine($"- Reranking time: {reranking.TotalMilliseconds:0.000} ms")
            .AppendLine($"- Mutation time: {mutation.TotalMilliseconds:0.000} ms")
            .AppendLine();

        builder.AppendLine("## Region audit\n");
        foreach (var row in rows) AppendRow(builder, row, stream, book, clean, analyzer);

        builder.AppendLine("## Suspicious / low-margin mutations\n");
        foreach (var row in rows.Where(row => row.Top1 is not null).OrderBy(row => row.Margin))
            builder.AppendLine($"- #{row.Index} margin `{row.Margin:0.000}`: `{Escape(row.Region.RawText)}` → `{Escape(row.Top1!.Candidate.Text)}` ({Location(row.Region, stream)})");

        builder.AppendLine("\n## Known-fixture sanity check\n");
        foreach (var target in SanityTargets)
        {
            var matches = rows.Where(row => string.Equals(row.Region.RawText, target, StringComparison.Ordinal)
                || row.Region.RawText.Contains(target, StringComparison.Ordinal)).ToArray();
            if (matches.Length == 0) builder.AppendLine($"- `{Escape(target)}`: not detected after frozen hyphenation; unchanged by OCR mutation.");
            else foreach (var row in matches)
                builder.AppendLine($"- `{Escape(target)}`: detected as region #{row.Index} `{Escape(row.Region.RawText)}`; " +
                    (row.Top1 is null ? "no candidate, unchanged." : $"mutated to `{Escape(row.Top1.Candidate.Text)}`; score {row.Top1.FinalScore:0.000}; margin {row.Margin:0.000}."));
        }
        builder.AppendLine("\n## Reader success criterion\n\nManual Reader inspection is still required to decide whether the actual book is visibly better; this audit does not declare reader-quality success.");
        return builder.ToString();
    }

    private static void AppendRow(StringBuilder builder, PreviewRow row, LogicalTextStream stream,
        BookLexicon book, CleanTurkishLexicon clean, ITurkishMorphologyAnalyzer analyzer)
    {
        var top = row.Top1;
        builder.AppendLine($"### #{row.Index}: `{Escape(row.Region.RawText)}`\n")
            .AppendLine($"- Top1 candidate source: {(top is null ? "none" : "NoisyChannel Top5 → Self-Corpus Reranker V3")}")
            .AppendLine($"- Score: {(top is null ? "—" : top.FinalScore.ToString("0.000"))}")
            .AppendLine($"- Top2 score: {(row.Top2 is null ? "—" : row.Top2.FinalScore.ToString("0.000"))}")
            .AppendLine($"- Score margin: {(top is null ? "—" : row.Margin.ToString("0.000"))}")
            .AppendLine($"- BookLexicon evidence: {(top is null ? "—" : $"contains={book.Contains(top.Candidate.Text)}, frequency={book.GetCount(top.Candidate.Text)}")}")
            .AppendLine($"- CleanTurkishLexicon evidence: {(top is null ? "—" : $"contains={clean.Contains(top.Candidate.Text)}, frequency={clean.GetFrequency(top.Candidate.Text)}")}")
            .AppendLine($"- TRmorph evidence: {(top is null ? "—" : analyzer.IsValidWord(top.Candidate.Text).ToString())}")
            .AppendLine($"- Self-corpus evidence: {(top is null ? "—" : $"bookDelta={top.BookDelta:0.000}, frequency={top.FrequencyEvidence:0.000}, left={top.LeftEvidence:0.000}, right={top.RightEvidence:0.000}, bigram={top.BigramEvidence:0.000}, trigram={top.TrigramEvidence:0.000}, consensus={top.ConsensusEvidence:0.000}")}")
            .AppendLine($"- Source text: `{Escape(row.Region.RawText)}`")
            .AppendLine($"- Replacement: {(top is null ? "*(unchanged)*" : $"`{Escape(top.Candidate.Text)}`")}")
            .AppendLine($"- Document/XHTML: `{Escape(stream.GetSourceLocationAt(row.Region.Start).DocumentPath)}`")
            .AppendLine($"- Source location: {Location(row.Region, stream)}")
            .AppendLine();
    }

    private static string Location(CorruptedTextRegion region, LogicalTextStream stream)
    {
        var first = stream.GetSourceLocationAt(region.Start);
        var last = stream.GetSourceLocationAt(region.EndExclusive - 1);
        return $"logical [{region.Start},{region.EndExclusive}); source {first.DocumentPath} node {first.TextNodeIndex} offset {first.Start} → {last.DocumentPath} node {last.TextNodeIndex} offset {last.Start + 1}";
    }

    private static double HitRatio(TurkishMorphologyCacheStatistics stats) =>
        stats.Hits + stats.Misses == 0 ? 0 : stats.Hits / (double)(stats.Hits + stats.Misses);

    private static bool PathsEqual(string left, string right) => string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    private static string Escape(string value) => value.Replace("\r", "\\r").Replace("\n", "\\n").Replace("|", "\\|");

    private sealed record PreviewRow(int Index, CorruptedTextRegion Region,
        IReadOnlyList<NoisyChannelCandidateDetail> Generated,
        IReadOnlyList<DeterministicOcrCandidateReranker.RerankDetail> Reranked)
    {
        public DeterministicOcrCandidateReranker.RerankDetail? Top1 => Reranked.FirstOrDefault();
        public DeterministicOcrCandidateReranker.RerankDetail? Top2 => Reranked.Skip(1).FirstOrDefault();
        public double Margin => Top1 is null ? double.NaN : Top1.FinalScore - (Top2?.FinalScore ?? 0);
    }
}
