using System.Text;
using System.Text.Json;
using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Lexicon.Models;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Ocr;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Cli.OcrReconstruction;

public sealed class OcrReconstructionComparison
{
    public int Run(string inputPath, string? expectedPath, string? reportPath, ITurkishMorphologyAnalyzer analyzer, string? diagnosticReportPath = null)
    {
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
        var rows = regions.Select((region, index) => new Row(index + 1, region, reconstructors.Select(r => r.Reconstruct(region)).ToArray())).ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath ?? "artifacts/debug/ocr-reconstruction-comparison.md"))!);
        var output = reportPath ?? "artifacts/debug/ocr-reconstruction-comparison.md";
        var noisy = (NoisyChannelRegionReconstructor)reconstructors[2];
        File.WriteAllText(output, Render(rows, expected, noisy), new UTF8Encoding(false));
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
    private sealed record Expected(string Source, string ExpectedText);
    private sealed record ExpectedFile(ExpectedItem[] Occurrences, string? Fixture);
    private sealed record ExpectedItem(int Start, string Source, string Expected);
}
