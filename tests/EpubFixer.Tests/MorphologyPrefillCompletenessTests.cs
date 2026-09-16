using EpubFixer.Core.Fix;
using EpubFixer.Core.Morphology;
using EpubFixer.TrMorph;

namespace EpubFixer.Tests;

public sealed class MorphologyPrefillCompletenessTests
{
    [Fact]
    [Trait("Category", "Slow")]
    public void FullBookFix_RecordsNoUnknownQueries()
    {
        var input = FindRepositoryFile(Path.Combine("test-data", "odun-kesmek", "input.epub"));
        var output = Path.Combine(Path.GetTempPath(), $"epubfixer-prefill-{Guid.NewGuid():N}.epub");
        try
        {
            using var analyzer = new FomaTurkishMorphologyAnalyzer();
            var builder = new RecordingFallbackOracleBuilder(analyzer);

            _ = new EpubFixService(builder).Fix(input, output, applyOcrCorrections: true);

            Assert.True(builder.UnknownQueries.Count == 0,
                "Unknown morphology queries: " + string.Join(", ", builder.UnknownQueries.OrderBy(word => word, StringComparer.Ordinal).Take(50)));
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

        throw new FileNotFoundException("Repository test data file was not found.", relativePath);
    }

    private sealed class RecordingFallbackOracleBuilder(IBatchTurkishMorphologicalParser parser) : IMorphologyOracleBuilder
    {
        private readonly Dictionary<string, IReadOnlyList<TurkishMorphologicalAnalysis>> analyses = new(StringComparer.Ordinal);
        public HashSet<string> UnknownQueries { get; } = new(StringComparer.Ordinal);

        public IMorphologyOracle Build(IEnumerable<string> vocabulary)
        {
            var missing = MorphologyPrefill.NormalizeDistinctOrder(vocabulary)
                .Where(word => !analyses.ContainsKey(word))
                .ToArray();
            if (missing.Length > 0)
            {
                foreach (var pair in parser.AnalyzeBatch(missing))
                {
                    analyses[pair.Key] = pair.Value;
                }
            }

            return new RecordingFallbackOracle(analyses, UnknownQueries, parser);
        }
    }

    private sealed class RecordingFallbackOracle(
        Dictionary<string, IReadOnlyList<TurkishMorphologicalAnalysis>> analyses,
        HashSet<string> unknownQueries,
        IBatchTurkishMorphologicalParser parser) : IMorphologyOracle
    {
        public bool IsValid(string word) => Analyze(word).Count > 0;

        public IReadOnlyList<TurkishMorphologicalAnalysis> Analyze(string word)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(word);
            var key = word.Normalize();
            if (analyses.TryGetValue(key, out var result))
            {
                return result;
            }

            unknownQueries.Add(key);
            result = parser.Analyze(key);
            analyses[key] = result;
            return result;
        }

        public bool IsKnown(string word)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(word);
            return analyses.ContainsKey(word.Normalize());
        }
    }
}
