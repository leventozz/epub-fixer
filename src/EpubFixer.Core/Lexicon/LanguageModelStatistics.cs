namespace EpubFixer.Core.Lexicon;

public sealed record LanguageModelStatistics(
    int UnigramTypes,
    int UnigramTokens,
    int BigramTypes,
    int BigramTokens,
    double HeldOutBigramHitRate);
