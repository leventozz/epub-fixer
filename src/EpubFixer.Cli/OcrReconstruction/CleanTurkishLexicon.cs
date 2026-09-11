using System.Globalization;
using System.Text;
using EpubFixer.Core.Morphology;

namespace EpubFixer.Cli.OcrReconstruction;

public sealed class CleanTurkishLexicon
{
    private readonly IReadOnlyDictionary<string, long> entries;

    private CleanTurkishLexicon(IReadOnlyDictionary<string, long> entries) => this.entries = entries;

    public IReadOnlyDictionary<string, long> Entries => entries;

    public long GetFrequency(string word) => entries.GetValueOrDefault(Normalize(word));

    public bool Contains(string word) => entries.ContainsKey(Normalize(word));

    public static CleanTurkishLexicon Load(string path, ITurkishMorphologyAnalyzer analyzer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(analyzer);
        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(path, Encoding.UTF8))
        {
            var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 2 || !long.TryParse(fields[^1], out var frequency) || frequency <= 0) continue;
            var word = Normalize(fields[0]);
            // Do not send the entire 50K corpus through the native flookup
            // process at startup. TRmorph validation is applied lazily to
            // reconstruction candidates; this keeps the debug command stable
            // on machines where long flookup sessions can terminate natively.
            if (!IsLexical(word)) continue;
            counts[word] = counts.GetValueOrDefault(word) + frequency;
        }
        return new CleanTurkishLexicon(counts);
    }

    public static string Normalize(string value) =>
        value.Normalize(NormalizationForm.FormC).ToLower(new CultureInfo("tr-TR"));

    private static bool IsLexical(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return false;
        var apostropheSeen = false;
        foreach (var rune in word.EnumerateRunes())
        {
            if (Rune.IsLetter(rune)) continue;
            if (rune.Value is '\'' or '’' && !apostropheSeen) { apostropheSeen = true; continue; }
            return false;
        }
        return true;
    }
}
