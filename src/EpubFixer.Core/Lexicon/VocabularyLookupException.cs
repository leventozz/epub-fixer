namespace EpubFixer.Core.Lexicon;

public sealed class VocabularyLookupException(string word)
    : KeyNotFoundException($"The word '{word}' is not present in the book vocabulary.");
