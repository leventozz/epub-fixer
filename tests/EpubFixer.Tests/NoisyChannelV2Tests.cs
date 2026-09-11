using EpubFixer.Cli.OcrReconstruction;
using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Tests;

public sealed class NoisyChannelV2Tests
{
    [Theory]
    [InlineData("üç")]
    [InlineData("olduğu")]
    [InlineData("Cebimde")]
    [InlineData("geçen")]
    [InlineData("kendimi")]
    public void CleanLexiconKeepsCandidateEligibleWhenMorphologyIsFalse(string word)
    {
        var clean = LoadClean($"{word} 10\n");
        var reconstructor = new NoisyChannelRegionReconstructor(clean, new BookLexiconBuilder().Build(""), new AlwaysFalseAnalyzer());
        var result = reconstructor.Reconstruct(Region(word));
        Assert.Contains(result, candidate => string.Equals(candidate.Text, word, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FormerBeamMissesAreInTopFive()
    {
        var clean = LoadClean("özellikle 10\nhiç 10\nkendimi 10\n");
        var reconstructor = new NoisyChannelRegionReconstructor(clean, new BookLexiconBuilder().Build("özellikle hiç kendimi"), new AlwaysFalseAnalyzer());
        Assert.Contains(reconstructor.Reconstruct(Region("ı ızellikle")), x => x.Text == "özellikle");
        Assert.Contains(reconstructor.Reconstruct(Region("1 ı iç")), x => x.Text == "hiç");
        Assert.Contains(reconstructor.Reconstruct(Region("kendi-ıni")), x => x.Text == "kendimi");
    }

    private static CorruptedTextRegion Region(string text) => new(text, 0, text.Length, [text], "", "", []);
    private static CleanTurkishLexicon LoadClean(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"clean-{Guid.NewGuid():N}.txt"); File.WriteAllText(path, content);
        try { return CleanTurkishLexicon.Load(path, new AlwaysFalseAnalyzer()); } finally { File.Delete(path); }
    }
    private sealed class AlwaysFalseAnalyzer : ITurkishMorphologyAnalyzer { public bool IsValidWord(string word) => false; }
}
