using System.Text;
using EpubFixer.Core.Epub;
using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Morphology;
using EpubFixer.TrMorph;

namespace EpubFixer.Tests;

public sealed class BookLanguageModelRealBookTests
{
    [Fact]
    [Trait("Category", "Slow")]
    public void RealBook_ContextualScoreBeatsContextFree()
    {
        using var analyzer = new FomaTurkishMorphologyAnalyzer();
        var knowledge = BuildKnowledge(analyzer);

        Assert.True(
            knowledge.LanguageModel.LogProbability("koltukta", "berjer")
            > knowledge.LanguageModel.LogProbability("koltukta", null));
    }

    [Fact]
    [Trait("Category", "Slow")]
    public void RealBook_ContextualScoreBeatsUnrelatedContext()
    {
        using var analyzer = new FomaTurkishMorphologyAnalyzer();
        var knowledge = BuildKnowledge(analyzer);

        Assert.True(
            knowledge.LanguageModel.LogProbability("koltukta", "berjer")
            > knowledge.LanguageModel.LogProbability("koltukta", "graben"));
    }

    private static BookKnowledge BuildKnowledge(IBatchTurkishMorphologicalParser parser)
    {
        var input = FindRepositoryFile(Path.Combine("test-data", "odun-kesmek", "input.epub"));
        var package = new EpubPackageReader().Read(input);
        var frequency = TurkishFrequencyList.FromLines(File.ReadLines(
            FindRepositoryFile(Path.Combine("src", "EpubFixer.Adapters", "Resources", "OcrReconstruction", "tr_50k.txt")),
            Encoding.UTF8));
        return new BookKnowledgeBuilder(frequency).Build(package.LogicalText, new BatchMorphologyOracleBuilder(parser));
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
}
