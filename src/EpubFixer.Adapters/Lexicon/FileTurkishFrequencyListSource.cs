using System.Text;
using EpubFixer.Core.Lexicon;

namespace EpubFixer.Adapters.Lexicon;

public static class FileTurkishFrequencyListSource
{
    public static string DefaultPath =>
        Path.Combine(AppContext.BaseDirectory, "Resources", "OcrReconstruction", "tr_50k.txt");

    public static TurkishFrequencyList Load(string? path = null)
    {
        var sourcePath = path ?? DefaultPath;
        return TurkishFrequencyList.FromLines(File.ReadLines(sourcePath, Encoding.UTF8));
    }
}
