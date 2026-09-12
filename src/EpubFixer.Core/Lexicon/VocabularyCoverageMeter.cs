using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Ocr.Models;
using EpubFixer.Core.Quality;

namespace EpubFixer.Core.Lexicon;

public sealed class VocabularyCoverageMeter
{
    public VocabularyCoverageReport Measure(
        BookVocabulary vocabulary,
        BookVocabulary heldOutVocabulary,
        LogicalTextStream stream,
        IReadOnlyList<CorruptedTextRegion> regions,
        IEnumerable<string>? targets = null)
    {
        ArgumentNullException.ThrowIfNull(vocabulary);
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(regions);

        var cleanTokens = CleanTokenSequence.Build(stream, regions);
        return Measure(vocabulary, heldOutVocabulary, cleanTokens, targets);
    }

    internal VocabularyCoverageReport Measure(
        BookVocabulary vocabulary,
        BookVocabulary heldOutVocabulary,
        IReadOnlyList<CleanToken> cleanTokens,
        IEnumerable<string>? targets = null)
    {
        ArgumentNullException.ThrowIfNull(vocabulary);
        ArgumentNullException.ThrowIfNull(heldOutVocabulary);
        ArgumentNullException.ThrowIfNull(cleanTokens);

        var heldOutStarts = HeldOutTokenSplit.SelectHeldOutLogicalStarts(cleanTokens);
        var heldOut = cleanTokens
            .Where(token => heldOutStarts.Contains(token.LogicalStart))
            .ToArray();
        var coveredHeldOut = heldOut.Count(token => heldOutVocabulary.Contains(token.Normalized));
        var heldOutCoverage = heldOut.Length == 0 ? 1.0 : coveredHeldOut / (double)heldOut.Length;

        var targetWords = (targets ?? []).Select(TurkishWordNormalizer.Normalize).Distinct(StringComparer.Ordinal).ToArray();
        var missingTargets = targetWords.Where(target => !vocabulary.Contains(target)).OrderBy(target => target, StringComparer.Ordinal).ToArray();
        var targetCoverage = targetWords.Length == 0 ? 0.0 : (targetWords.Length - missingTargets.Length) / (double)targetWords.Length;
        var suspiciousEntries = vocabulary.Words
            .Select(word => vocabulary.Find(word)!)
            .Count(entry => SuspiciousTokenRules.IsSuspicious(entry.PreferredSurface));

        return new VocabularyCoverageReport(
            heldOutCoverage,
            targetCoverage,
            Array.AsReadOnly(missingTargets),
            suspiciousEntries);
    }
}
