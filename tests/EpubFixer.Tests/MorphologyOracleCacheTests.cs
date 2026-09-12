using EpubFixer.Cli.Morphology;
using EpubFixer.Core.Fix;
using EpubFixer.Core.Morphology;
using EpubFixer.TrMorph;

namespace EpubFixer.Tests;

public sealed class MorphologyOracleCacheTests
{
    [Fact]
    public void CacheKey_UsesSourceAndTransducerHashes()
    {
        using var files = CacheFiles();

        var first = new MorphologyOracleCache(files.Source, files.Transducer, files.Directory);
        File.WriteAllText(files.Transducer, "fst-2");
        var second = new MorphologyOracleCache(files.Source, files.Transducer, files.Directory);

        Assert.NotEqual(first.CacheKey, second.CacheKey);
    }

    [Fact]
    public void Build_UsesCacheAndResolvesOnlyMissingWords()
    {
        using var files = CacheFiles();
        var cache = new MorphologyOracleCache(files.Source, files.Transducer, files.Directory);
        cache.Write(new Dictionary<string, IReadOnlyList<TurkishMorphologicalAnalysis>>(StringComparer.Ordinal)
        {
            ["cached"] = [new TurkishMorphologicalAnalysis("cached", new HashSet<string>(StringComparer.Ordinal))]
        });
        var inner = new RecordingOracleBuilder();
        using var builder = new CachingMorphologyOracleBuilder(inner, cache);

        var oracle = builder.Build(["cached", "missing"]);

        Assert.True(oracle.IsKnown("cached"));
        Assert.True(oracle.IsKnown("missing"));
        Assert.Equal([["missing"]], inner.BuildInputs);
    }

    [Fact]
    public void CorruptCacheIsIgnoredAndRewritten()
    {
        using var files = CacheFiles();
        var cache = new MorphologyOracleCache(files.Source, files.Transducer, files.Directory);
        Directory.CreateDirectory(files.Directory);
        File.WriteAllText(cache.FilePath, "{nope");
        var inner = new RecordingOracleBuilder();
        using var builder = new CachingMorphologyOracleBuilder(inner, cache);

        var oracle = builder.Build(["word"]);
        builder.Flush();

        Assert.True(oracle.IsKnown("word"));
        Assert.Contains("\"word\"", File.ReadAllText(cache.FilePath));
    }

    [Fact]
    public void Write_IsDeterministicAndCreatesDirectory()
    {
        using var files = CacheFiles();
        var cache = new MorphologyOracleCache(files.Source, files.Transducer, files.Directory);
        var analyses = new Dictionary<string, IReadOnlyList<TurkishMorphologicalAnalysis>>(StringComparer.Ordinal)
        {
            ["b"] = [new TurkishMorphologicalAnalysis("b", new HashSet<string>(["Z", "A"], StringComparer.Ordinal))],
            ["a"] = []
        };

        cache.Write(analyses);
        var first = File.ReadAllText(cache.FilePath);
        cache.Write(analyses);
        var second = File.ReadAllText(cache.FilePath);

        Assert.True(Directory.Exists(files.Directory));
        Assert.Equal(first, second);
        Assert.True(first.IndexOf("\"a\"", StringComparison.Ordinal) < first.IndexOf("\"b\"", StringComparison.Ordinal));
        Assert.True(first.IndexOf("\"A\"", StringComparison.Ordinal) < first.IndexOf("\"Z\"", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Slow")]
    public void FullRun_SecondInvocationStartsNoFlookupProcess()
    {
        var input = FindRepositoryFile(Path.Combine("test-data", "odun-kesmek", "input.epub"));
        using var files = CacheFiles();
        var firstOutput = Path.Combine(Path.GetTempPath(), $"epubfixer-cache-first-{Guid.NewGuid():N}.epub");
        var secondOutput = Path.Combine(Path.GetTempPath(), $"epubfixer-cache-second-{Guid.NewGuid():N}.epub");
        var created = 0;
        try
        {
            using (var builder = new CachingMorphologyOracleBuilder(
                new FomaMorphologyOracleBuilder(() =>
                {
                    created++;
                    return new FomaTurkishMorphologyAnalyzer();
                }),
                new MorphologyOracleCache(input, directory: files.Directory)))
            {
                _ = new EpubFixService(builder).Fix(input, firstOutput, applyOcrCorrections: true);
            }

            using (var builder = new CachingMorphologyOracleBuilder(
                new FomaMorphologyOracleBuilder(() =>
                {
                    created++;
                    return new FomaTurkishMorphologyAnalyzer();
                }),
                new MorphologyOracleCache(input, directory: files.Directory)))
            {
                _ = new EpubFixService(builder).Fix(input, secondOutput, applyOcrCorrections: true);
            }

            Assert.Equal(1, created);
        }
        finally
        {
            if (File.Exists(firstOutput)) File.Delete(firstOutput);
            if (File.Exists(secondOutput)) File.Delete(secondOutput);
        }
    }

    private static TempCacheFiles CacheFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"epubfixer-cache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "input.epub");
        var transducer = Path.Combine(directory, "trmorph.fst");
        File.WriteAllText(source, "epub");
        File.WriteAllText(transducer, "fst");
        return new TempCacheFiles(directory, source, transducer);
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

    private sealed class RecordingOracleBuilder : IMorphologyOracleBuilder
    {
        public List<string[]> BuildInputs { get; } = [];

        public IMorphologyOracle Build(IEnumerable<string> vocabulary)
        {
            var input = vocabulary.ToArray();
            BuildInputs.Add(input);
            return new MorphologyOracle(input.ToDictionary(
                word => word,
                word => (IReadOnlyList<TurkishMorphologicalAnalysis>)[new TurkishMorphologicalAnalysis(word, new HashSet<string>(StringComparer.Ordinal))],
                StringComparer.Ordinal));
        }
    }

    private sealed record TempCacheFiles(string Directory, string Source, string Transducer) : IDisposable
    {
        public void Dispose()
        {
            if (System.IO.Directory.Exists(Directory))
            {
                System.IO.Directory.Delete(Directory, recursive: true);
            }
        }
    }
}
