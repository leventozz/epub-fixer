using EpubFixer.Core.Ocr;
using EpubFixer.Core.Epub;
using EpubFixer.Adapters.Lexicon;
using EpubFixer.Cli.OcrReconstruction;
using EpubFixer.Adapters.Ocr.Lattice;
using EpubFixer.Core.Fix;
using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Ocr.Lattice;
using EpubFixer.Core.Ocr.Models;
using EpubFixer.TrMorph;

namespace EpubFixer.Tests;

[Collection(NonParallelPerformanceCollection.Name)]
public sealed class PerformanceBudgetTests
{
    [Fact]
    public void SearchBudget_CountsVisitsAndFlagsExceeded()
    {
        var budget = new SearchBudget(2);
        budget.Visit();
        budget.Visit();
        Assert.False(budget.Exceeded);
        budget.Visit();
        Assert.Equal(3, budget.Visited);
        Assert.True(budget.Exceeded);
    }

    [Fact]
    public void SearchBudget_NullBudgetDoesNotChangeReconstructionOutput()
    {
        var clean = LoadClean("kendimi 10\n");
        var book = new BookLexiconBuilder().Build("kendimi");
        var withoutBudget = new NoisyChannelRegionReconstructor(clean, book, new AlwaysFalseAnalyzer())
            .Reconstruct(Region("kendi-ıni"))
            .Select(candidate => candidate.Text)
            .ToArray();
        var withBudget = new NoisyChannelRegionReconstructor(clean, book, new AlwaysFalseAnalyzer(), budget: new SearchBudget(2_000_000))
            .Reconstruct(Region("kendi-ıni"))
            .Select(candidate => candidate.Text)
            .ToArray();

        Assert.Equal(withoutBudget, withBudget);
    }

    [Fact]
    public void RegionSearchStaysWithinStateCeiling()
    {
        var budget = new SearchBudget(2_000_000);
        var clean = LoadClean("kendimi 10\nözellikle 10\nhiç 10\ngeçen 10\nyürümeye 10\ndikkatle 10\n");
        var book = new BookLexiconBuilder().Build("kendimi özellikle hiç geçen yürümeye dikkatle");

        _ = new NoisyChannelRegionReconstructor(clean, book, new AlwaysFalseAnalyzer(), budget: budget)
            .Reconstruct(Region("dikkat-:;i zlikle"));

        Assert.Equal(225_617, budget.Visited);
        Assert.False(budget.Exceeded);
    }

    [Fact]
    [Trait("Category", "Slow")]
    public void FullBookFixCompletesWithinBudget()
    {
        var input = FindRepositoryFile(Path.Combine("test-data", "odun-kesmek", "input.epub"));
        var output = Path.Combine(Path.GetTempPath(), $"epubfixer-budget-{Guid.NewGuid():N}.epub");
        try
        {
            using var analyzer = new FomaTurkishMorphologyAnalyzer();
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            _ = new EpubFixService(new BatchMorphologyOracleBuilder(analyzer)).Fix(input, output, applyOcrCorrections: true);
            elapsed.Stop();

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
    public void FullBookLatticePassCompletesWithinBudget()
    {
        var input = FindRepositoryFile(Path.Combine("test-data", "odun-kesmek", "input.epub"));
        var package = new EpubPackageReader().Read(input);
        using var analyzer = new FomaTurkishMorphologyAnalyzer();
        var frequency = FileTurkishFrequencyListSource.Load();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var knowledge = new BookKnowledgeBuilder(frequency)
            .Build(package.LogicalText, new BatchMorphologyOracleBuilder(analyzer));
        var options = new LatticeOptions();
        var matcher = new SymSpellLexiconMatcher(knowledge.Vocabulary);
        var reconstructor = new LatticeRegionReconstructor(
            package.LogicalText.Text,
            new WordLatticeBuilder(matcher, HardBoundaryOffsets(package.LogicalText)),
            new LatticeDecoder(knowledge.LanguageModel, options),
            new CorrectionAcceptanceGate(knowledge.Vocabulary, options),
            options);

        _ = knowledge.Regions.Select(reconstructor.Evaluate).ToArray();
        watch.Stop();

        Console.WriteLine($"LatticeElapsedSeconds={watch.Elapsed.TotalSeconds:0.000}");
        Console.WriteLine($"Regions={reconstructor.Statistics.Regions}");
        Console.WriteLine($"Applied={reconstructor.Statistics.Applied}");
        Console.WriteLine($"MaxVisitedStates={reconstructor.Statistics.MaxVisitedStates}");

        Assert.True(watch.Elapsed.TotalSeconds <= 30, $"Lattice pass took {watch.Elapsed.TotalSeconds:0.00}s.");
        Assert.True(reconstructor.Statistics.MaxVisitedStates <= 2_500, $"Max visited states was {reconstructor.Statistics.MaxVisitedStates}.");
        Assert.Equal(563, reconstructor.Statistics.Regions);
        Assert.Equal(31, reconstructor.Statistics.Applied);
    }

    private static CorruptedTextRegion Region(string text) => new(text, 0, text.Length, [text], "", "", []);

    private static TurkishFrequencyList LoadClean(string content) =>
        TurkishFrequencyList.FromLines(content.Split('\n', StringSplitOptions.RemoveEmptyEntries));

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

    private static IReadOnlyCollection<int> HardBoundaryOffsets(LogicalTextStream stream) =>
        stream.Boundaries
            .Where(boundary => boundary.Kind is not TextBoundaryKind.TextNode)
            .Select(boundary => stream.Segments[boundary.AfterSegmentIndex].LogicalStart)
            .Distinct()
            .OrderBy(offset => offset)
            .ToArray();

    private sealed class AlwaysFalseAnalyzer : ITurkishMorphologyAnalyzer
    {
        public bool IsValidWord(string word) => false;
    }
}
