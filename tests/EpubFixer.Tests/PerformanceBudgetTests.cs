using EpubFixer.Core.Ocr;
using EpubFixer.Cli.OcrReconstruction;
using EpubFixer.Core.Fix;
using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Ocr.Models;
using EpubFixer.TrMorph;

namespace EpubFixer.Tests;

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
            _ = new EpubFixService(analyzer).Fix(input, output, applyOcrCorrections: true);
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

    private static CorruptedTextRegion Region(string text) => new(text, 0, text.Length, [text], "", "", []);

    private static CleanTurkishLexicon LoadClean(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"clean-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, content);
        try
        {
            return CleanTurkishLexicon.Load(path, new AlwaysFalseAnalyzer());
        }
        finally
        {
            File.Delete(path);
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

    private sealed class AlwaysFalseAnalyzer : ITurkishMorphologyAnalyzer
    {
        public bool IsValidWord(string word) => false;
    }
}
