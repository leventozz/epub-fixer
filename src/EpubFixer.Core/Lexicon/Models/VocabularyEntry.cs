namespace EpubFixer.Core.Lexicon.Models;

public sealed record VocabularyEntry(
    string Normalized,
    string PreferredSurface,
    int BookCount,
    VocabularySource Source);
