namespace EpubFixer.Core.Lexicon;

/// <summary>A frequency-ranked word list. Pure policy: holds no path, opens no file.</summary>
public interface ITurkishFrequencyList
{
    bool Contains(string normalizedWord);

    long GetFrequency(string normalizedWord);

    long TotalFrequency { get; }

    IReadOnlyCollection<string> Words { get; }
}
