using System.Text.Json;
using System.Text.Json.Serialization;
using EpubFixer.Core.Fix;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Mutation.Models;
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
        public static OcrMutationBaseline From(OcrMutationResult result) =>
            new(
                "legacy",
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
