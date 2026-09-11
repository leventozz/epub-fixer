using System.Text;
using System.Text.Json;
using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Ocr;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Cli.OcrReconstruction;

public sealed class OcrReconstructionComparison
{
    public int Run(string inputPath, string? expectedPath, string? reportPath, ITurkishMorphologyAnalyzer analyzer)
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
        File.WriteAllText(output, Render(rows, expected), new UTF8Encoding(false));
        Console.WriteLine($"Detected regions: {regions.Count}");
        Console.WriteLine($"Comparison report written to: {Path.GetFullPath(output)}");
        return regions.Count == 21 ? 0 : 3;
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

    private static string Render(IReadOnlyList<Row> rows, IReadOnlyDictionary<int, Expected> expected)
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
        return builder.ToString();
    }

    private static string Escape(string value) => value.Replace("\r", "\\r").Replace("\n", "\\n").Replace("|", "\\|");
    private sealed record Row(int Index, CorruptedTextRegion Region, IReadOnlyList<ReconstructionCandidate>[] Results);
    private sealed record Expected(string Source, string ExpectedText);
    private sealed record ExpectedFile(ExpectedItem[] Occurrences, string? Fixture);
    private sealed record ExpectedItem(int Start, string Source, string Expected);
}
