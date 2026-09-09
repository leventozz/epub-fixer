using System.Text;

namespace EpubFixer.Core.Lexicon.Models;

public sealed class BookLexicon
{
    private readonly Dictionary<string, int> _counts;
    private readonly IReadOnlyDictionary<string, int> _entries;
    private readonly Dictionary<string, int> _baseFormCounts;
    private readonly HashSet<string> _apostropheBaseForms;

    internal BookLexicon(IReadOnlyDictionary<string, int> counts)
    {
        ArgumentNullException.ThrowIfNull(counts);

        _counts = new Dictionary<string, int>(counts, StringComparer.Ordinal);
        _entries = new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(_counts);
        _baseFormCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        _apostropheBaseForms = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in _counts)
        {
            var baseForm = GetBaseForm(entry.Key);
            _baseFormCounts[baseForm] = _baseFormCounts.GetValueOrDefault(baseForm) + entry.Value;
            if (!string.Equals(baseForm, entry.Key, StringComparison.Ordinal))
                _apostropheBaseForms.Add(baseForm);
        }
    }

    public IReadOnlyDictionary<string, int> Entries => _entries;

    public int UniqueApostropheBaseForms => _apostropheBaseForms.Count;

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

    public string GetBaseForm(string word)
    {
        ArgumentNullException.ThrowIfNull(word);

        var apostrophe = word.IndexOfAny(['\'', '’']);
        if (apostrophe <= 0 || apostrophe >= word.Length - 1)
            return word;

        return AreLetters(word.AsSpan(0, apostrophe))
            && AreLetters(word.AsSpan(apostrophe + 1))
            ? word[..apostrophe]
            : word;
    }

    public int GetBaseFormCount(string word)
    {
        ArgumentNullException.ThrowIfNull(word);

        return _baseFormCounts.GetValueOrDefault(GetBaseForm(word));
    }

    private static bool AreLetters(ReadOnlySpan<char> text)
    {
        while (!text.IsEmpty)
        {
            if (Rune.DecodeFromUtf16(text, out var rune, out var consumed) != System.Buffers.OperationStatus.Done
                || !Rune.IsLetter(rune))
                return false;
            text = text[consumed..];
        }

        return true;
    }
}
