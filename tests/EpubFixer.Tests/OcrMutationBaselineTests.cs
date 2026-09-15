using System.Text.Json;
using System.Text.Json.Serialization;
using EpubFixer.Adapters.Ocr;
using EpubFixer.Core.Fix;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Mutation.Models;
using EpubFixer.Core.Ocr;
using EpubFixer.Core.Quality;
using EpubFixer.Core.Tokenization;
using EpubFixer.Cli.Quality;
using EpubFixer.Core.Epub;
using EpubFixer.TrMorph;

namespace EpubFixer.Tests;

public sealed class OcrMutationBaselineTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    [Trait("Category", "Slow")]
    public void FullBookFix_LegacyEngineMutationProfileIsPinned()
    {
        var input = FindRepositoryFile(Path.Combine("test-data", "odun-kesmek", "input.epub"));
        var baselinePath = FindRepositoryFile(Path.Combine("docs", "baselines", "odun-kesmek.fix-legacy.json"));
        var output = Path.Combine(Path.GetTempPath(), $"epubfixer-ocr-baseline-{Guid.NewGuid():N}.epub");
        try
        {
            using var analyzer = new FomaTurkishMorphologyAnalyzer();
            var result = new EpubFixService(new BatchMorphologyOracleBuilder(analyzer))
                .Fix(input, output, applyOcrCorrections: true);

            var mutation = Assert.IsType<OcrMutationResult>(result.OcrMutation);
            var actual = OcrMutationBaseline.From(mutation);
            var actualJson = JsonSerializer.Serialize(actual, JsonOptions);
            if (string.Equals(Environment.GetEnvironmentVariable("EPUBFIXER_UPDATE_BASELINES"), "1", StringComparison.Ordinal))
            {
                File.WriteAllText(baselinePath, actualJson + Environment.NewLine);
            }

            Assert.Equal(120, mutation.PlannedCount);
            Assert.Equal(120, mutation.AppliedCount);
            Assert.Equal(118, mutation.SingleSourceCount);
            Assert.Equal(2, mutation.MultiSourceCount);
            Assert.Equal(2, mutation.DocumentsChanged);
            Assert.Empty(mutation.Failures);
            Assert.Equal(0, mutation.UnexpectedTextChanges);

            var expected = JsonSerializer.Deserialize<OcrMutationBaseline>(
                File.ReadAllText(baselinePath),
                JsonOptions) ?? throw new InvalidOperationException("Baseline JSON is empty.");

            Assert.Equal(
                JsonSerializer.Serialize(expected, JsonOptions),
                actualJson);
        }
        finally
        {
            if (File.Exists(output))
            {
                File.Delete(output);
            }
        }
    }

    [Fact]
    [Trait("Category", "Slow")]
    public void FullBookFix_LatticeEngineMutationProfileIsPinned()
    {
        // Measurement only: the production default stays legacy (D60/D65). This pins what
        // the lattice engine would write so R5.4 can judge the MaxArcLength trade-off
        // (D44) against recorded evidence instead of re-running the whole attempt.
        var input = FindRepositoryFile(Path.Combine("test-data", "odun-kesmek", "input.epub"));
        var baselinePath = FindRepositoryFile(Path.Combine("docs", "baselines", "odun-kesmek.fix-lattice.json"));
        var output = Path.Combine(Path.GetTempPath(), $"epubfixer-ocr-lattice-{Guid.NewGuid():N}.epub");
        try
        {
            using var analyzer = new FomaTurkishMorphologyAnalyzer();
            var result = new EpubFixService(
                    new BatchMorphologyOracleBuilder(analyzer),
                    LatticeOcrPlannerFactory.Create())
                .Fix(input, output, applyOcrCorrections: true);

            var mutation = Assert.IsType<OcrMutationResult>(result.OcrMutation);
            Assert.Equal(OcrCorrectionEngine.Lattice, mutation.Engine);
            var actual = OcrMutationBaseline.From(mutation, "lattice");
            var actualJson = JsonSerializer.Serialize(actual, JsonOptions);
            if (string.Equals(Environment.GetEnvironmentVariable("EPUBFIXER_UPDATE_BASELINES"), "1", StringComparison.Ordinal))
            {
                File.WriteAllText(baselinePath, actualJson + Environment.NewLine);
            }

            var expected = JsonSerializer.Deserialize<OcrMutationBaseline>(
                File.ReadAllText(baselinePath),
                JsonOptions) ?? throw new InvalidOperationException("Baseline JSON is empty.");

            Assert.Equal(
                JsonSerializer.Serialize(expected, JsonOptions),
                actualJson);
        }
        finally
        {
            if (File.Exists(output))
            {
                File.Delete(output);
            }
        }
    }

    [Fact]
    [Trait("Category", "Slow")]
    public void FullBookFix_HybridEngineMutationProfileIsPinned()
    {
        // H3: the roadmap's own written prediction (docs/ocr-correction-roadmap.md section 3.1)
        // is that the composite planner must apply exactly 129 mutations - 120 legacy + 9 lattice
        // GAIN, with the one known conflict ('kol-1 ıı kta', main-3.xhtml logicalStart 152467)
        // resolving to the primary (legacy). If this number is not 129, the bug is in the merge
        // (CompositeOcrCorrectionPlanner), not in either engine - see decisions.md D82/D83.
        var input = FindRepositoryFile(Path.Combine("test-data", "odun-kesmek", "input.epub"));
        var baselinePath = FindRepositoryFile(Path.Combine("docs", "baselines", "odun-kesmek.fix-hybrid.json"));
        var output = Path.Combine(Path.GetTempPath(), $"epubfixer-ocr-hybrid-{Guid.NewGuid():N}.epub");
        try
        {
            using var analyzer = new FomaTurkishMorphologyAnalyzer();
            var result = new EpubFixService(
                    new BatchMorphologyOracleBuilder(analyzer),
                    new CompositeOcrCorrectionPlanner(new LegacyOcrCorrectionPlanner(), LatticeOcrPlannerFactory.Create()))
                .Fix(input, output, applyOcrCorrections: true);

            var mutation = Assert.IsType<OcrMutationResult>(result.OcrMutation);
            Assert.Equal(OcrCorrectionEngine.Hybrid, mutation.Engine);

            // DUR gate (H3): assert the literal acceptance criterion before touching the baseline
            // file at all, so a wrong count never gets pinned as if it were the measured answer.
            Assert.Equal(129, mutation.PlannedCount);
            Assert.Equal(129, mutation.AppliedCount);

            var actual = OcrMutationBaseline.From(mutation, "hybrid");
            var actualJson = JsonSerializer.Serialize(actual, JsonOptions);
            if (string.Equals(Environment.GetEnvironmentVariable("EPUBFIXER_UPDATE_BASELINES"), "1", StringComparison.Ordinal))
            {
                File.WriteAllText(baselinePath, actualJson + Environment.NewLine);
            }

            var expected = JsonSerializer.Deserialize<OcrMutationBaseline>(
                File.ReadAllText(baselinePath),
                JsonOptions) ?? throw new InvalidOperationException("Baseline JSON is empty.");

            Assert.Equal(
                JsonSerializer.Serialize(expected, JsonOptions),
                actualJson);

            // Book health (H3 output 4): measured in-process against this same fix run's output,
            // so H3 does not pay for a second full hybrid fix pipeline just to get these 4 numbers
            // (roadmap section 6 rule 2 - at most a handful of full-book runs per item). Mirrors
            // MeasureCommand.MeasureEpub (src/EpubFixer.Cli/Quality/MeasureCommand.cs) exactly.
            var bookHealth = MeasureBookHealth(output);
            Console.WriteLine($"HybridBookHealth.TotalTokens={bookHealth.TotalTokens}");
            Console.WriteLine($"HybridBookHealth.UnresolvableTokens={bookHealth.UnresolvableTokens}");
            Console.WriteLine($"HybridBookHealth.SuspiciousTokens={bookHealth.SuspiciousTokens}");
            Console.WriteLine(
                "HybridBookHealth.UnresolvableRatePer1000="
                + bookHealth.UnresolvableRate.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
        }
        finally
        {
            if (File.Exists(output))
            {
                File.Delete(output);
            }
        }
    }

    private static BookHealth MeasureBookHealth(string epubPath)
    {
        var package = new EpubPackageReader().Read(epubPath);
        var tokens = new WordTokenizer().Tokenize(package.LogicalText).Select(token => token.Text).ToArray();
        using var analyzer = new FomaTurkishMorphologyAnalyzer();
        var recognizer = new FrequencyMorphologyWordRecognizer(
            tokens,
            FrequencyMorphologyWordRecognizer.DefaultFrequencyListPath,
            analyzer);
        return new BookHealthMeter(recognizer).Measure(package.LogicalText);
    }

    private static string FindRepositoryFile(string relativePath)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("Repository file was not found.", relativePath);
    }

    private sealed record OcrMutationBaseline(
        string Engine,
        int PlannedCount,
        int AppliedCount,
        int SingleSourceCount,
        int MultiSourceCount,
        int DocumentsChanged,
        int UnexpectedTextChanges,
        IReadOnlyList<OcrMutationSignature> Mutations)
    {
        public static OcrMutationBaseline From(OcrMutationResult result, string engine = "legacy") =>
            new(
                engine,
                result.PlannedCount,
                result.AppliedCount,
                result.SingleSourceCount,
                result.MultiSourceCount,
                result.DocumentsChanged,
                result.UnexpectedTextChanges,
                result.AppliedMutations
                    .OrderBy(item => item.DocumentPath, StringComparer.Ordinal)
                    .ThenBy(item => item.LogicalStart)
                    .ThenBy(item => item.LogicalLength)
                    .Select(OcrMutationSignature.From)
                    .ToArray());
    }

    private sealed record OcrMutationSignature(
        string DocumentPath,
        int LogicalStart,
        int LogicalLength,
        string OriginalSourceText,
        string ReplacementText,
        string DecisionRule,
        string Confidence,
        int SourceSpanCount)
    {
        public static OcrMutationSignature From(OcrCorrectionMutation mutation) =>
            new(
                mutation.DocumentPath,
                mutation.LogicalStart,
                mutation.LogicalLength,
                mutation.OriginalSourceText,
                mutation.ReplacementText,
                mutation.DecisionRule,
                mutation.Confidence?.ToString() ?? "None",
                mutation.SourceSpans.Count);
    }
}
