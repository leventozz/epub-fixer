namespace EpubFixer.Core.Ocr.Models;

public sealed record OcrCorrectionOccurrence(
    OcrWordEvidence Source,
    OcrWordCandidate WorkingSource,
    string PrefixPunctuation,
    string LexicalCore,
    string SuffixPunctuation,
    IReadOnlyList<OcrCorrectionCandidate> Proposals);
