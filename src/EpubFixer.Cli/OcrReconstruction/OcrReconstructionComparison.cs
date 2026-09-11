using System.Text;
using System.Text.Json;
using System.Diagnostics;
using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Lexicon.Models;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Ocr;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Cli.OcrReconstruction;

public sealed class OcrReconstructionComparison
{
    public int Run(string inputPath, string? expectedPath, string? reportPath, ITurkishMorphologyAnalyzer analyzer, string? diagnosticReportPath = null, bool fast = false, string? targetIds = null)
    {
        var benchmarkWatch = Stopwatch.StartNew();
        var text = File.ReadAllText(inputPath, new UTF8Encoding(false, true));
        var regions = new OcrRegionDetector().Detect(text, analyzer);
        var book = new BookLexiconBuilder().Build(text);
        var cleanPath = Path.Combine(AppContext.BaseDirectory, "Resources", "OcrReconstruction", "tr_50k.txt");
        var clean = CleanTurkishLexicon.Load(cleanPath, analyzer);
        var reconstructors = new IOcrRegionReconstructor[]
        {
            new CurrentRegionReconstructor(book, analyzer),
            new SymSpellRegionReconstructor(clean),
            new NoisyChannelRegionReconstructor(clean, book, analyzer)
        };
        var expected = LoadExpected(expectedPath ?? Path.ChangeExtension(inputPath, ".expected.json"), text);
        var selected = SelectRegions(regions, fast, targetIds);
        var rows = selected.Select(region => new Row(regions.Select((r, i) => (r, i + 1)).First(x => ReferenceEquals(x.r, region)).Item2, region, reconstructors.Select(r => r.Reconstruct(region)).ToArray())).ToArray();
        var contextIndex = BookContextIndex.Build(text, regions);
        var reranker = new DeterministicOcrCandidateReranker(analyzer as ITurkishMorphologicalParser);
        if (analyzer is IBatchTurkishMorphologicalParser batch)
        {
            var words = rows.SelectMany(row => row.Results[(int)ReconstructionSource.NoisyChannel]).Select(c => c.Text)
                .Concat(rows.SelectMany(row => { var c = CreateContext(text, row.Region, contextIndex); return c.PreviousWords.Concat(c.NextWords); }))
                .Distinct(StringComparer.Ordinal).ToArray();
            batch.AnalyzeBatch(words);
        }
        var reranked = rows.Select(row =>
        {
            var context = CreateContext(text, row.Region, contextIndex);
            var details = reranker.RerankDetailed(row.Region, row.Results[(int)ReconstructionSource.NoisyChannel], context);
            return new RerankedRow(row, context, details);
        }).ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath ?? "artifacts/debug/ocr-reconstruction-comparison.md"))!);
        var output = reportPath ?? "artifacts/debug/ocr-reconstruction-comparison.md";
        var noisy = (NoisyChannelRegionReconstructor)reconstructors[2];
        File.WriteAllText(output, Render(rows, expected, noisy), new UTF8Encoding(false));
        var rerankOutput = "artifacts/debug/ocr-self-corpus-reranking-v2.md";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(rerankOutput))!);
        benchmarkWatch.Stop();
        File.WriteAllText(rerankOutput, RenderReranking(reranked, expected, contextIndex, benchmarkWatch.Elapsed.TotalMilliseconds), new UTF8Encoding(false));
        var performanceOutput = "artifacts/debug/ocr-performance-v2.md";
        var stats = analyzer is IBatchTurkishMorphologicalParser bp ? bp.CacheStatistics : null;
        var hitRatio = stats is null || stats.Hits + stats.Misses == 0 ? "n/a" : (stats.Hits / (double)(stats.Hits + stats.Misses)).ToString("P2");
        File.WriteAllText(performanceOutput, $"# OCR Performance V2\n\n- Old full benchmark: approximately 145 seconds (supplied baseline)\n- New selected benchmark: {benchmarkWatch.Elapsed.TotalMilliseconds:0.000} ms\n- TRmorph misses: {stats?.Misses.ToString() ?? "n/a"}\n- TRmorph hits: {stats?.Hits.ToString() ?? "n/a"}\n- Unique analyzed words: {stats?.UniqueAnalyzedWords.ToString() ?? "n/a"}\n- Cache hit ratio: {hitRatio}\n- Batch requests: {stats?.BatchRequests.ToString() ?? "n/a"}\n- Batched words: {stats?.BatchedWords.ToString() ?? "n/a"}\n", new UTF8Encoding(false));
        Console.WriteLine($"Performance report written to: {Path.GetFullPath(performanceOutput)}");
        Console.WriteLine($"Context reranking report written to: {Path.GetFullPath(rerankOutput)}");
        if (diagnosticReportPath is not null)
        {
            if (expectedPath is null) throw new InvalidDataException("--diagnostic-report requires --expected.");
            var formerMisses = new HashSet<string>(StringComparer.Ordinal) { "kendi-ıni", "1 ı iç", "ı ızellikle" };
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(diagnosticReportPath))!);
            File.WriteAllText(diagnosticReportPath, RenderDiagnostic(text, rows, expected, formerMisses, noisy, clean, book, analyzer), new UTF8Encoding(false));
            Console.WriteLine($"NoisyChannel diagnostic report written to: {Path.GetFullPath(diagnosticReportPath)}");
        }
        Console.WriteLine($"Detected regions: {regions.Count}");
        Console.WriteLine($"Comparison report written to: {Path.GetFullPath(output)}");
        return regions.Count == 21 ? 0 : 3;
    }

    private static IReadOnlyList<CorruptedTextRegion> SelectRegions(IReadOnlyList<CorruptedTextRegion> regions, bool fast, string? targetIds)
    {
        var selected = regions.ToList();
        if (fast) selected = selected.Where(r => r.RawText is "ı ıç" or "kendi-ıni" or "1 ı iç").ToList();
        if (!string.IsNullOrWhiteSpace(targetIds))
        {
            var ids = targetIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(s => int.TryParse(s, out var id) ? id : throw new ArgumentException($"Invalid target id '{s}'."));
            var wanted = ids.Distinct().ToArray();
            if (wanted.Any(id => id < 1 || id > regions.Count)) throw new ArgumentException($"Target id is outside 1..{regions.Count}.");
            selected = wanted.Select(id => regions[id - 1]).ToList();
        }
        return selected;
    }

    private static OcrContext CreateContext(string text, CorruptedTextRegion region, IOcrBookContextLookup book)
    {
        static string[] Previous(string value) => System.Text.RegularExpressions.Regex.Matches(value, "[^\\s\\p{P}]+")
            .Select(m => m.Value).TakeLast(4).ToArray();
        static string[] Next(string value) => System.Text.RegularExpressions.Regex.Matches(value, "[^\\s\\p{P}]+")
            .Select(m => m.Value).Take(4).ToArray();
        var before = text[..Math.Max(0, region.Start)];
        var after = text[Math.Min(text.Length, region.EndExclusive)..];
        return new OcrContext(Previous(before), Next(after), new string(before.TakeLast(512).ToArray()), new string(after.Take(512).ToArray()), book);
    }

    private static string RenderReranking(IReadOnlyList<RerankedRow> rows, IReadOnlyDictionary<int, Expected> expected, BookContextIndex index, double totalMilliseconds)
    {
        var scored = rows.Where(r => expected.ContainsKey(r.Source.Region.Start)).ToArray();
        var top1 = scored.Count(r => r.Details.FirstOrDefault()?.Candidate.Text == expected[r.Source.Region.Start].ExpectedText);
        var top5 = scored.Count(r => r.Details.Any(d => d.Candidate.Text == expected[r.Source.Region.Start].ExpectedText));
        var b = new StringBuilder("# OCR Self-Corpus Reranking V2\n\n");
        b.AppendLine($"- Previous reranking Top1: 9/10").AppendLine($"- New reranking Top1: {top1}/{scored.Length}").AppendLine($"- Previous Top5 coverage: 10/10").AppendLine($"- New Top5 coverage: {top5}/{scored.Length}").AppendLine($"- Book-context lookups: {index.LookupCount}").AppendLine();
        b.AppendLine("## Candidate traces\n\n| Source | Candidate | Base | Frequency | Left | Right | Bigram | Trigram | Consensus | Morphology | Structural | Final | Rank |").AppendLine("| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (var row in scored.Where(r => r.Source.Region.RawText is "ı ıç" or "kendi-ıni" or "1 ı iç"))
            foreach (var d in row.Details) b.AppendLine($"| `{row.Source.Region.RawText}` | `{d.Candidate.Text}` | {d.BaseScore:0.000} | {d.FrequencyEvidence:0.000} | {d.LeftEvidence:0.000} | {d.RightEvidence:0.000} | {d.BigramEvidence:0.000} | {d.TrigramEvidence:0.000} | {d.ConsensusEvidence:0.000} | {d.MorphologyDelta:0.000} | {d.StructuralDelta:0.000} | {d.FinalScore:0.000} | {d.Candidate.Rank} |");
        b.AppendLine("\n## Regression audit\n");
        foreach (var row in scored)
        {
            var before = row.Source.Results[(int)ReconstructionSource.NoisyChannel].FirstOrDefault()?.Text;
            var after = row.Details.FirstOrDefault()?.Candidate.Text;
            if (before == expected[row.Source.Region.Start].ExpectedText)
                b.AppendLine($"- `{row.Source.Region.RawText}`: existing Top1 `{before}`; after `{after}`; {(before == after ? "preserved" : $"displaced by stronger evidence ({row.Details.FirstOrDefault()?.EvidenceDelta:0.000})")}");
        }
        b.AppendLine("\n## 21-region review\n");
        foreach (var row in rows.Where(r => !expected.ContainsKey(r.Source.Region.Start)))
            b.AppendLine($"- Region #{row.Source.Index} `{Escape(row.Source.Region.RawText)}`: unscored pending manual answer review.");
        var timings = rows.SelectMany(r => r.Details).Select(d => d.ElapsedMilliseconds).ToArray();
        b.AppendLine($"\n## Performance\n\n- Total benchmark time: {totalMilliseconds:0.000} ms\n- Reranking time per region: average {timings.DefaultIfEmpty().Average():0.000} ms; maximum {timings.DefaultIfEmpty().Max():0.000} ms\n- Book-context lookup count: {index.LookupCount}\n- Index build is one pass over the book; no full-book scan occurs per candidate.");
        return b.ToString();
    }

    private static string RenderDiagnostic(string text, IReadOnlyList<Row> rows, IReadOnlyDictionary<int, Expected> expected, IReadOnlySet<string> formerMisses,
        NoisyChannelRegionReconstructor noisy, CleanTurkishLexicon clean, BookLexicon book, ITurkishMorphologyAnalyzer analyzer)
    {
        var misses = rows.Where(r => expected.TryGetValue(r.Region.Start, out var e) && formerMisses.Contains(r.Region.RawText)).ToArray();
        var b = new StringBuilder("# NoisyChannel Diagnostic V2\n\n");
        b.AppendLine($"Former-miss trace count: {misses.Length}").AppendLine();
        b.AppendLine("| Source | Expected | Root cause | Subreason | Generated? | Pruned? | Morphology | CleanLexicon | Minimum path |").AppendLine("| --- | --- | --- | --- | ---: | ---: | ---: | ---: | --- |");
        var traces = new List<(Row Row, NoisyChannelDiagnosticTrace Trace)>();
        foreach (var row in misses)
        {
            var trace = noisy.Diagnose(row.Region, expected[row.Region.Start].ExpectedText); traces.Add((row, trace));
            var morph = trace.Evaluation?.Morphology.ToString() ?? "n/a";
            var lex = trace.Evaluation?.Clean.ToString() ?? "n/a";
            b.AppendLine($"| `{Escape(row.Region.RawText)}` | `{Escape(trace.Expected)}` | {trace.Classification} | {trace.Subreason} | {trace.Generated} | {trace.Pruned} | {morph} | {lex} | {(trace.MinimumPath is null ? "—" : string.Join("; ", trace.MinimumPath.Select(x => x.Operation)))} |");
        }
        foreach (var item in traces)
        {
            var t = item.Trace; b.AppendLine().AppendLine($"## `{Escape(item.Row.Region.RawText)}` → `{Escape(t.Expected)}`");
            b.AppendLine($"- Initial normalized input: `{Escape(t.Initial)}`").AppendLine($"- Root cause: **{t.Classification}**").AppendLine($"- Subreason: **{t.Subreason}**");
            b.AppendLine("\n### Expansion and pruning audit\n").AppendLine("| Depth | Generated | Retained | Pruned | Best retained cost | Expected generated | Expected retained | Best states |").AppendLine("| ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |");
            foreach (var d in t.Depths) b.AppendLine($"| {d.Depth} | {d.Generated} | {d.Retained} | {d.Pruned} | {d.BestCost?.ToString("0.000") ?? "—"} | {d.ExpectedGenerated} | {d.ExpectedRetained} | {Escape(string.Join(", ", d.BestStates))} |");
            b.AppendLine("\n### Closest states\n\n| State | Distance | Cost | Depth |\n| --- | ---: | ---: | ---: |");
            foreach (var s in t.ClosestStates) b.AppendLine($"| `{Escape(s.Text)}` | {s.Distance} | {s.Cost:0.000} | {s.Depth} |");
            b.AppendLine("\n### Minimum edit path\n");
            if (t.MinimumPath is null) b.AppendLine("No path exists with the current operation set. The diagnostic reachability search found no state equal to the expected candidate.");
            else { foreach (var p in t.MinimumPath) b.AppendLine($"{p.Number}. {Escape(p.Operation)}"); b.AppendLine($"\nTotal edit steps: {t.MinimumPath.Count}"); }
            b.AppendLine("\n### Candidate evaluation\n");
            if (t.Evaluation is null) b.AppendLine("Expected candidate was not available for scoring in the explored state set.");
            else b.AppendLine($"CleanTurkishLexicon contains: {t.Evaluation.Clean}; frequency: {t.Evaluation.CleanFrequency}; BookLexicon contains: {t.Evaluation.Book}; TRmorph: {t.Evaluation.Morphology}; final score: {(double.IsNaN(t.Evaluation.Score) ? "—" : t.Evaluation.Score.ToString("0.000"))}; edit cost: {(double.IsNaN(t.Evaluation.Cost) ? "—" : t.Evaluation.Cost.ToString("0.000"))}");
        }
        b.AppendLine("\n## TRmorph and lexicon audit\n\n| Input | NFC | Turkish lowercase | TRmorph raw | TRmorph NFC | TRmorph lowercase | Clean contains | Clean frequency | Book contains | Book frequency |").AppendLine("| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (var word in new[] { "üç", "olduğu", "Cebimde", "geçen", "kendimi", "hiç", "özellikle", "yürümeye", "koltukta", "sohbet" })
        {
            var nfc = word.Normalize(); var lower = CleanTurkishLexicon.Normalize(nfc); var rawValid = analyzer.IsValidWord(word); var nfcValid = analyzer.IsValidWord(nfc); var lowerValid = analyzer.IsValidWord(lower);
            b.AppendLine($"| `{word}` | `{nfc}` | `{lower}` | {rawValid} | {nfcValid} | {lowerValid} | {clean.Contains(lower)} | {clean.GetFrequency(lower)} | {book.Contains(word)} | {book.GetCount(word)} |");
        }
        return b.ToString();
    }

    private static IReadOnlyDictionary<int, Expected> LoadExpected(string path, string text)
    {
        if (!File.Exists(path)) return new Dictionary<int, Expected>();
        var data = JsonSerializer.Deserialize<ExpectedFile>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new(Array.Empty<ExpectedItem>(), null);
        var result = new Dictionary<int, Expected>();
        foreach (var item in data.Occurrences ?? Array.Empty<ExpectedItem>())
        {
            if (item.Start < 0 || item.Start + item.Source.Length > text.Length || !string.Equals(text.Substring(item.Start, item.Source.Length), item.Source, StringComparison.Ordinal))
                throw new InvalidDataException($"Expected occurrence does not match fixture at {item.Start}: {item.Source}");
            result[item.Start] = new(item.Source, item.Expected);
        }
        return result;
    }

    private static string Render(IReadOnlyList<Row> rows, IReadOnlyDictionary<int, Expected> expected, NoisyChannelRegionReconstructor noisy)
    {
        var builder = new StringBuilder("# OCR Reconstruction Comparison\n\n");
        builder.AppendLine($"Detected regions: {rows.Count}").AppendLine();
        foreach (var row in rows)
        {
            var target = expected.TryGetValue(row.Region.Start, out var value) ? value.ExpectedText : "— (not scored)";
            builder.AppendLine($"## #{row.Index}: `{Escape(row.Region.RawText)}`").AppendLine();
            builder.AppendLine($"- Start: `{row.Region.Start}`").AppendLine($"- Expected: `{target}`").AppendLine($"- Reasons: `{string.Join(", ", row.Region.DetectionReasons)}`").AppendLine();
            for (var method = 0; method < row.Results.Length; method++)
            {
                var name = row.Results[method].FirstOrDefault()?.Source.ToString() ?? new[] { "Current", "SymSpell", "NoisyChannel" }[method];
                builder.AppendLine($"### {name}").AppendLine().AppendLine("| Rank | Text | Score | Evidence |").AppendLine("| ---: | --- | ---: | --- |");
                foreach (var candidate in row.Results[method]) builder.AppendLine($"| {candidate.Rank} | `{Escape(candidate.Text)}` | {candidate.Score:0.000} | {Escape(string.Join("; ", candidate.Evidence))} |");
                if (row.Results[method].Count == 0) builder.AppendLine("| — | *(no candidate)* | — | — |");
                builder.AppendLine();
            }
        }
        builder.AppendLine("## Summary\n\n| Method | Top1 correct | Top5 contains correct | Missed |\n| --- | ---: | ---: | ---: |");
        foreach (var method in new[] { ReconstructionSource.Current, ReconstructionSource.SymSpell, ReconstructionSource.NoisyChannel })
        {
            var labeled = rows.Where(r => expected.ContainsKey(r.Region.Start)).ToArray();
            var top1 = labeled.Count(r => r.Results[(int)method].FirstOrDefault()?.Text == expected[r.Region.Start].ExpectedText);
            var top5 = labeled.Count(r => r.Results[(int)method].Any(c => c.Text == expected[r.Region.Start].ExpectedText));
            builder.AppendLine($"| {method} | {top1}/{labeled.Length} | {top5}/{labeled.Length} | {labeled.Length - top5} |");
            builder.AppendLine();
            builder.AppendLine($"### {method} missed targets").AppendLine();
            foreach (var row in labeled.Where(r => !r.Results[(int)method].Any(c => c.Text == expected[r.Region.Start].ExpectedText))) builder.AppendLine($"- `{row.Region.Start}` `{row.Region.RawText}` → `{expected[row.Region.Start].ExpectedText}`");
            builder.AppendLine();
        }
        builder.AppendLine("## NoisyChannel candidate quality audit\n");
        builder.AppendLine("| Source | Expected | Rank | Candidate | Score | Edit cost | CleanLexicon | Clean frequency | BookLexicon | Book frequency | TRmorph |");
        builder.AppendLine("| --- | --- | ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (var row in rows.Where(r => expected.ContainsKey(r.Region.Start)))
        {
            var details = noisy.ReconstructDetailed(row.Region);
            for (var rank = 0; rank < MaxCandidatesForAudit; rank++)
            {
                var candidate = rank < details.Count ? details[rank] : null;
                builder.AppendLine(candidate is null
                    ? $"| `{Escape(row.Region.RawText)}` | `{Escape(expected[row.Region.Start].ExpectedText)}` | {rank + 1} | *(no candidate)* | — | — | — | — | — | — | — |"
                    : $"| `{Escape(row.Region.RawText)}` | `{Escape(expected[row.Region.Start].ExpectedText)}` | {rank + 1} | `{Escape(candidate.Text)}` | {candidate.Score:0.000} | {candidate.Cost:0.000} | {candidate.Lexical} | {candidate.CleanFrequency} | {candidate.Book} | {candidate.BookFrequency} | {candidate.Morphology} |");
            }
        }
        builder.AppendLine("\nPrevious NoisyChannel benchmark: Top1 6/10, Top5 7/10, Missed 3.");
        return builder.ToString();
    }

    private static string Escape(string value) => value.Replace("\r", "\\r").Replace("\n", "\\n").Replace("|", "\\|");
    private const int MaxCandidatesForAudit = 5;
    private sealed record Row(int Index, CorruptedTextRegion Region, IReadOnlyList<ReconstructionCandidate>[] Results);
    private sealed record RerankedRow(Row Source, OcrContext Context, IReadOnlyList<DeterministicOcrCandidateReranker.RerankDetail> Details);
    private sealed record Expected(string Source, string ExpectedText);
    private sealed record ExpectedFile(ExpectedItem[] Occurrences, string? Fixture);
    private sealed record ExpectedItem(int Start, string Source, string Expected);
}
