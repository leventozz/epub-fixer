namespace EpubFixer.Core.Lexicon;

public sealed record BookVocabularyOptions(
    int MinBookCount = 2,
    int MinUnverifiedBookCount = 3,
    double AddK = 0.5,
    double FrequencyListDiscount = 0.1,
    double MorphologyOnlyLogProbability = -14.0,
    double UnknownLogProbability = -18.0);
