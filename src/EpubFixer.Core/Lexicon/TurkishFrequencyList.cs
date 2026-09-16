using System.Text;

namespace EpubFixer.Core.Lexicon;

/// <summary>Immutable in-memory frequency list. Keys are TurkishWordNormalizer-normalized.</summary>
public sealed class TurkishFrequencyList : ITurkishFrequencyList
{
    private readonly IReadOnlyDictionary<string, long> entries;
    private readonly IReadOnlyCollection<string> words;

    private TurkishFrequencyList(IReadOnlyDictionary<string, long> entries)
    {
        this.entries = entries;
        words = Array.AsReadOnly(entries.Keys.OrderBy(word => word, StringComparer.Ordinal).ToArray());
        TotalFrequency = entries.Values.Sum();
    }

    public long TotalFrequency { get; }

    public IReadOnlyCollection<string> Words => words;

    public IReadOnlyDictionary<string, long> Entries => entries;

    public bool Contains(string normalizedWord) =>
        entries.ContainsKey(TurkishWordNormalizer.Normalize(normalizedWord));

    public long GetFrequency(string normalizedWord) =>
        entries.GetValueOrDefault(TurkishWordNormalizer.Normalize(normalizedWord));

    public static TurkishFrequencyList FromLines(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 2
                || !long.TryParse(fields[^1], out var frequency)
                || frequency <= 0)
            {
                continue;
            }

            var word = TurkishWordNormalizer.Normalize(fields[0]);
            if (!IsLexical(word))
            {
                continue;
            }

            counts[word] = counts.GetValueOrDefault(word) + frequency;
        }

        return new TurkishFrequencyList(counts);
    }

    private static bool IsLexical(string word)
    {
        var hasLetter = false;
        foreach (var rune in word.EnumerateRunes())
        {
            if (Rune.IsLetter(rune))
            {
                hasLetter = true;
                continue;
            }

            if (rune.Value is '\'' or '’')
            {
                continue;
            }

            return false;
        }

        return hasLetter;
    }
}
