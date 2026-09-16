using EpubFixer.Adapters.Ocr;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using EpubFixer.Core.Epub;
using EpubFixer.Core.Fix;
using EpubFixer.Core.Morphology;
using EpubFixer.TrMorph;

namespace EpubFixer.Tests;

public sealed class MorphologyCallTraceTests
{
    [Fact]
    public void CountingAnalyzer_DelegatesEveryCallAndCountsIt()
    {
        using var inner = new StubBatchParser(new Dictionary<string, IReadOnlyList<TurkishMorphologicalAnalysis>>(StringComparer.Ordinal)
        {
            ["valid"] = [new TurkishMorphologicalAnalysis("valid", new HashSet<string>(StringComparer.Ordinal))],
            ["missing"] = []
        });
        using var counter = new CountingMorphologyAnalyzer(inner);

        Assert.True(counter.IsValidWord("valid"));
        Assert.Empty(counter.Analyze("missing"));
        var batch = counter.AnalyzeBatch(["valid", "valid", "missing"]);

        Assert.Equal(1, counter.IsValidWordCalls);
        Assert.Equal(1, counter.AnalyzeCalls);
        Assert.Equal(1, counter.AnalyzeBatchCalls);
        Assert.Equal(2, counter.DistinctWords);
        Assert.True(counter.MorphologyElapsed >= TimeSpan.Zero);
        Assert.True(batch.ContainsKey("valid"));
    }

    [Fact]
    public void CountingAnalyzer_ForwardsCacheStatistics()
    {
        using var inner = new StubBatchParser(new Dictionary<string, IReadOnlyList<TurkishMorphologicalAnalysis>>(StringComparer.Ordinal));
        using var counter = new CountingMorphologyAnalyzer(inner);

        Assert.Equal(inner.CacheStatistics, counter.CacheStatistics);
    }

    [Fact]
    [Trait("Category", "Slow")]
    public void FullBookFix_MorphologyCallProfileIsRecorded()
    {
        var input = FindRepositoryFile(Path.Combine("test-data", "odun-kesmek", "input.epub"));
        var output = Path.Combine(Path.GetTempPath(), $"epubfixer-morphology-{Guid.NewGuid():N}.epub");
        try
        {
            using var counter = new CountingMorphologyAnalyzer(new FomaTurkishMorphologyAnalyzer());
            var elapsed = Stopwatch.StartNew();
            _ = new EpubFixService(new BatchMorphologyOracleBuilder(counter)).Fix(input, output, applyOcrCorrections: true);
            elapsed.Stop();

            Console.WriteLine($"IsValidWordCalls={counter.IsValidWordCalls}");
            Console.WriteLine($"AnalyzeCalls={counter.AnalyzeCalls}");
            Console.WriteLine($"AnalyzeBatchCalls={counter.AnalyzeBatchCalls}");
            Console.WriteLine($"DistinctWords={counter.DistinctWords}");
            Console.WriteLine($"MorphologyElapsedSeconds={counter.MorphologyElapsed.TotalSeconds:0.000}");
            Console.WriteLine($"TotalElapsedSeconds={elapsed.Elapsed.TotalSeconds:0.000}");
            Console.WriteLine($"BatchRequests={counter.CacheStatistics.BatchRequests}");
            Console.WriteLine($"BatchedWords={counter.CacheStatistics.BatchedWords}");

            Assert.Equal(0, counter.IsValidWordCalls);
            Assert.Equal(0, counter.AnalyzeCalls);
            Assert.Equal(4, counter.AnalyzeBatchCalls);
            Assert.Equal(4, counter.CacheStatistics.BatchRequests);
            Assert.Equal(13_215, counter.DistinctWords);
            Assert.True(elapsed.Elapsed.TotalSeconds <= 120, $"Fix took {elapsed.Elapsed.TotalSeconds:0.00}s.");
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
    public void FullBookFix_ProducesGoldenLogicalText()
    {
        var input = FindRepositoryFile(Path.Combine("test-data", "odun-kesmek", "input.epub"));
        var output = Path.Combine(Path.GetTempPath(), $"epubfixer-golden-{Guid.NewGuid():N}.epub");
        try
        {
            using var counter = new CountingMorphologyAnalyzer(new FomaTurkishMorphologyAnalyzer());
            _ = new EpubFixService(new BatchMorphologyOracleBuilder(counter)).Fix(input, output, applyOcrCorrections: true);
            var package = new EpubPackageReader().Read(output);
            var logicalText = LogicalTextStreamBuilder.Build(package.SpineDocuments).Text;
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(logicalText))).ToLowerInvariant();
            Console.WriteLine($"GoldenLogicalTextSha256={hash}");

            Assert.Equal("37b72bc8508ceb6a06e9f8a5c7f41f65503e5bb99c9a436b82c37e960b2018be", hash);
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
    public void FullBookFix_HybridEngineProducesGoldenLogicalText()
    {
        // H5 / decision (a): the legacy golden above pins EpubFixService's own default, which stays
        // LegacyOcrCorrectionPlanner because Core cannot construct the lattice half (D27/D58 - the
        // matcher lives in Adapters). This second golden pins what the CLI actually ships after H5.
        // Without it the flip would leave production output guarded by nothing.
        var input = FindRepositoryFile(Path.Combine("test-data", "odun-kesmek", "input.epub"));
        var output = Path.Combine(Path.GetTempPath(), $"epubfixer-golden-hybrid-{Guid.NewGuid():N}.epub");
        try
        {
            using var counter = new CountingMorphologyAnalyzer(new FomaTurkishMorphologyAnalyzer());
            _ = new EpubFixService(
                    new BatchMorphologyOracleBuilder(counter),
                    OcrPlannerFactory.Resolve("hybrid"))
                .Fix(input, output, applyOcrCorrections: true);
            var package = new EpubPackageReader().Read(output);
            var logicalText = LogicalTextStreamBuilder.Build(package.SpineDocuments).Text;
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(logicalText))).ToLowerInvariant();
            Console.WriteLine($"HybridGoldenLogicalTextSha256={hash}");

            Assert.Equal("9f5faea2ee2ab6b64a974cf3fde7f69a1a659217b28a1704e1deae771f6cdda2", hash);
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

    private sealed class StubBatchParser(
        IReadOnlyDictionary<string, IReadOnlyList<TurkishMorphologicalAnalysis>> analyses)
        : IBatchTurkishMorphologicalParser, IDisposable
    {
        public TurkishMorphologyCacheStatistics CacheStatistics { get; } = new(1, 2, 3, 4, 5, 6);

        public IReadOnlyList<TurkishMorphologicalAnalysis> Analyze(string word) =>
            AnalyzeBatch([word])[word];

        public IReadOnlyDictionary<string, IReadOnlyList<TurkishMorphologicalAnalysis>> AnalyzeBatch(IEnumerable<string> words) =>
            words.Distinct(StringComparer.Ordinal).ToDictionary(
                word => word,
                word => analyses.TryGetValue(word, out var result) ? result : [],
                StringComparer.Ordinal);

        public bool IsValidWord(string word) => Analyze(word).Count > 0;

        public void Dispose()
        {
        }
    }
}
