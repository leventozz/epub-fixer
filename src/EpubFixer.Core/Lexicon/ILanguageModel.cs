namespace EpubFixer.Core.Lexicon;

public interface ILanguageModel
{
    /// <summary>Stupid-backoff score in log space; this is not a normalized probability. A null previousWord means segment start.</summary>
    double LogProbability(string word, string? previousWord);
}
