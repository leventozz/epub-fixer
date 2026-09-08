namespace EpubFixer.Core.Lexicon.Models;

public sealed class BookLexicon
{
    private readonly Dictionary<string, int> _counts;

    internal BookLexicon(IReadOnlyDictionary<string, int> counts)
    {
        ArgumentNullException.ThrowIfNull(counts);

        _counts = new Dictionary<string, int>(counts, StringComparer.Ordinal);
    }

    public int GetCount(string word)
    {
        ArgumentNullException.ThrowIfNull(word);

        return _counts.GetValueOrDefault(word);
    }

    public bool Contains(string word)
    {
        ArgumentNullException.ThrowIfNull(word);

        return _counts.ContainsKey(word);
    }
}
