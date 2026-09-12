using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Morphology;
using EpubFixer.TrMorph;

namespace EpubFixer.Tests;

[Collection(NonParallelPerformanceCollection.Name)]
public sealed class BookKnowledgeBuilderTests
{
    [Fact]
    public void Build_PrefillsOracleForEveryQueryItAsks()
    {
        var stream = TestStreamFactory.FromSingleSegment("Berjer koltukta Berjer temiz");
        var frequency = TurkishFrequencyList.FromLines(["temiz 10"]);
        var builder = new RecordingOracleBuilder(["Berjer", "koltukta"]);

        _ = new BookKnowledgeBuilder(frequency, new BookVocabularyOptions(MinBookCount: 1))
            .Build(stream, builder, ["Berjer", "koltukta"]);

        Assert.Empty(builder.UnknownQueries);
    }

    [Fact]
    [Trait("Category", "Slow")]
    public void FullBook_KnowledgeBuildStaysWithinBudget()
    {
        var input = FindRepositoryFile(Path.Combine("test-data", "odun-kesmek", "input.epub"));
        var frequency = TurkishFrequencyList.FromLines(File.ReadLines(
            FindRepositoryFile(Path.Combine("src", "EpubFixer.Cli", "Resources", "OcrReconstruction", "tr_50k.txt"))));
        using var analyzer = new FomaTurkishMorphologyAnalyzer();
        var package = new EpubFixer.Core.Epub.EpubPackageReader().Read(input);
        var oracleBuilder = new BatchMorphologyOracleBuilder(analyzer);
        _ = new BookKnowledgeBuilder(frequency).Build(
            package.LogicalText,
            oracleBuilder,
            ["koltukta"]);
        var elapsed = System.Diagnostics.Stopwatch.StartNew();

        _ = new BookKnowledgeBuilder(frequency).Build(
            package.LogicalText,
            oracleBuilder,
            ["koltukta"]);

        elapsed.Stop();
        Assert.True(elapsed.Elapsed.TotalSeconds <= 3, $"Knowledge build took {elapsed.Elapsed.TotalSeconds:0.000}s.");
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

    private sealed class RecordingOracleBuilder(IReadOnlyCollection<string> validWords) : IMorphologyOracleBuilder
    {
        public List<string> UnknownQueries { get; } = [];

        public IMorphologyOracle Build(IEnumerable<string> vocabulary)
        {
            return new RecordingOracle(
                vocabulary.ToHashSet(StringComparer.Ordinal),
                validWords.ToHashSet(StringComparer.Ordinal),
                UnknownQueries);
        }
    }

    private sealed class RecordingOracle(
        IReadOnlySet<string> known,
        IReadOnlySet<string> valid,
        ICollection<string> unknownQueries) : IMorphologyOracle
    {
        public bool IsValid(string word)
        {
            if (!known.Contains(word))
            {
                unknownQueries.Add(word);
                return false;
            }

            return valid.Contains(word);
        }

        public IReadOnlyList<TurkishMorphologicalAnalysis> Analyze(string word) =>
            IsValid(word) ? [new TurkishMorphologicalAnalysis(word, new HashSet<string>(StringComparer.Ordinal))] : [];

        public bool IsKnown(string word) => known.Contains(word);
    }
}
