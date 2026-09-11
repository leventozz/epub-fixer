using EpubFixer.Cli.OcrReconstruction;
using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Tests;

public sealed class NoisyChannelDiagnosticTests
{
    [Fact]
    public void Diagnose_RetainsLexicalCandidateThatWasPreviouslyPruned()
    {
        var clean = LoadClean("özellikle 10\nhiç 10\n");
        var book = new BookLexiconBuilder().Build("özellikle hiçe");
        var reconstructor = new NoisyChannelRegionReconstructor(clean, book, new FakeAnalyzer());
        var region = Region("ı ızellikle");

        var trace = reconstructor.Diagnose(region, "özellikle");

        Assert.Equal("GeneratedAndVisible", trace.Classification);
        Assert.True(trace.Visible);
        Assert.NotNull(trace.MinimumPath);
        Assert.DoesNotContain(trace.Depths, item => item.ExpectedGenerated && !item.ExpectedRetained);
    }

    [Fact]
    public void Diagnose_UsesGenericPairContractionForLocalSubstitutionAndDeletion()
    {
        var clean = LoadClean("kendimi 10\n");
        var book = new BookLexiconBuilder().Build("kendimi");
        var reconstructor = new NoisyChannelRegionReconstructor(clean, book, new FakeAnalyzer());

        var trace = reconstructor.Diagnose(Region("kendi-ıni"), "kendimi");

        Assert.Equal("GeneratedAndVisible", trace.Classification);
        Assert.NotNull(trace.MinimumPath);
        Assert.Contains(trace.MinimumPath!, step => step.Operation.Contains("pair contraction", StringComparison.Ordinal));
        Assert.True(trace.Generated);
    }

    private static CorruptedTextRegion Region(string text) => new(text, 0, text.Length, [text], "", "", []);

    private static CleanTurkishLexicon LoadClean(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"clean-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, content);
        try { return CleanTurkishLexicon.Load(path, new FakeAnalyzer()); }
        finally { File.Delete(path); }
    }

    private sealed class FakeAnalyzer : ITurkishMorphologyAnalyzer
    {
        public bool IsValidWord(string word) => word is "özellikle" or "hiç" or "kendimi";
    }
}
