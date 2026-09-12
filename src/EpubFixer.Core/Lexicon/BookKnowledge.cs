using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Core.Lexicon;

public sealed record BookKnowledge(
    BookVocabulary Vocabulary,
    ILanguageModel LanguageModel,
    IReadOnlyList<CorruptedTextRegion> Regions,
    VocabularyCoverageReport Coverage);
