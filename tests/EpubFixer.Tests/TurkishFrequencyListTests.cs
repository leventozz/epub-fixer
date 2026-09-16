using System.Security.Cryptography;
using System.Text;
using EpubFixer.Core.Lexicon;

namespace EpubFixer.Tests;

public sealed class TurkishFrequencyListTests
{
    [Fact]
    public void FromLines_SumsFrequenciesOfDuplicateKeys()
    {
        var list = TurkishFrequencyList.FromLines(["Auersberger 2", "auersberger 3"]);

        Assert.Equal(5, list.GetFrequency("auersberger"));
    }

    [Fact]
    public void FromLines_SkipsMalformedAndNonPositiveLines()
    {
        var list = TurkishFrequencyList.FromLines(["iyi nope", "kötü 0", "güzel -1", "temiz 4"]);

        Assert.Equal(["temiz"], list.Words);
        Assert.Equal(4, list.TotalFrequency);
    }

    [Fact]
    public void FromLines_NormalizesKeysToLowercaseNfc()
    {
        var list = TurkishFrequencyList.FromLines(["Auersberger 1", "IŞIK 1"]);

        Assert.True(list.Contains("auersberger"));
        Assert.True(list.Contains("ışık"));
    }

    [Fact]
    public void FromLines_RejectsEntriesWithDigitsOrPunctuation()
    {
        var list = TurkishFrequencyList.FromLines(["a1 1", "a-b 1", "temiz 1"]);

        Assert.Equal(["temiz"], list.Words);
    }

    [Fact]
    public void FromLines_AcceptsApostropheForms()
    {
        var list = TurkishFrequencyList.FromLines(["Joana'nın 7", "Graben’de 3"]);

        Assert.True(list.Contains("joana'nın"));
        Assert.True(list.Contains("graben’de"));
    }

    [Fact]
    public void TotalFrequency_IsTheSumOfAllEntries()
    {
        var list = TurkishFrequencyList.FromLines(["bir 2", "iki 5"]);

        Assert.Equal(7, list.TotalFrequency);
    }

    [Fact]
    [Trait("Category", "Slow")]
    public void RealResource_KeySetMatchesTodaysLoaders()
    {
        var list = TurkishFrequencyList.FromLines(File.ReadLines(FindRepositoryFile(Path.Combine("src", "EpubFixer.Adapters", "Resources", "OcrReconstruction", "tr_50k.txt")), Encoding.UTF8));
        var joined = string.Join("\n", list.Words.OrderBy(word => word, StringComparer.Ordinal));
        var sha = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(joined))).ToLowerInvariant();

        Assert.Equal(49_263, list.Words.Count);
        Assert.Equal("445170ea8db54c22c79dbfe732565bc5c789f8dcbe7ae1f4cf16358fa43fa770", sha);
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
