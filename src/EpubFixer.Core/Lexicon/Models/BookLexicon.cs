namespace EpubFixer.Core.Lexicon.Models;

public sealed class BookLexicon
{
    private readonly Dictionary<string, int> _counts;
    private readonly IReadOnlyDictionary<string, int> _entries;

    internal BookLexicon(IReadOnlyDictionary<string, int> counts)
    {
        ArgumentNullException.ThrowIfNull(counts);

        _counts = new Dictionary<string, int>(counts, StringComparer.Ordinal);
        _entries = new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(_counts);
    }

    public IReadOnlyDictionary<string, int> Entries => _entries;

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
