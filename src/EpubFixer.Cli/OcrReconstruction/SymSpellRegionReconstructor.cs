using EpubFixer.Core.Ocr;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Cli.OcrReconstruction;

public sealed class SymSpellRegionReconstructor(CleanTurkishLexicon lexicon) : IOcrRegionReconstructor
{
    private readonly SymSpellChecker checker = BuildChecker(lexicon);

    public IReadOnlyList<ReconstructionCandidate> Reconstruct(CorruptedTextRegion region, int maxCandidates = 5)
    {
        ArgumentNullException.ThrowIfNull(region);
        if (maxCandidates <= 0) throw new ArgumentOutOfRangeException(nameof(maxCandidates));
        var query = region.RawText.Trim();
        var suggestions = new List<(string Text, double Score, string Evidence)>();
        foreach (var suggestion in checker.Lookup(Clean(query), SymSpell.Verbosity.All, 2))
            suggestions.Add((RestoreCase(suggestion.term, query), -suggestion.distance + Math.Log10(suggestion.count + 1) / 24, $"Lookup; distance={suggestion.distance}; frequency={suggestion.count}"));
        if (query.Any(char.IsWhiteSpace) || region.DetectionReasons.Contains(OcrRegionDetectionReason.FragmentedNeighbors))
        {
            foreach (var suggestion in checker.LookupCompound(Clean(query), 2))
                suggestions.Add((RestoreCase(suggestion.term, query), -suggestion.distance + Math.Log10(suggestion.count + 1) / 24, $"LookupCompound; distance={suggestion.distance}; frequency={suggestion.count}"));
            var segmentation = checker.WordSegmentation(Clean(query));
            suggestions.Add((RestoreCase(segmentation.correctedString, query), -segmentation.distanceSum / 10.0, $"WordSegmentation; distance={segmentation.distanceSum}"));
        }
        return suggestions.Where(x => !string.IsNullOrWhiteSpace(x.Text)).GroupBy(x => x.Text, StringComparer.Ordinal)
            .Select(g => g.OrderByDescending(x => x.Score).First()).OrderByDescending(x => x.Score).ThenBy(x => x.Text, StringComparer.Ordinal)
            .Take(maxCandidates).Select((x, i) => new ReconstructionCandidate(x.Text, x.Score, i + 1, ReconstructionSource.SymSpell, [x.Evidence])).ToArray();
    }

    private static SymSpellChecker BuildChecker(CleanTurkishLexicon lexicon)
    {
        var checker = new SymSpellChecker(lexicon.Entries.Count, 2);
        foreach (var item in lexicon.Entries) checker.CreateDictionaryEntry(item.Key, item.Value);
        return checker;
    }

    private static string Clean(string text) => text.Normalize().ToLower(new System.Globalization.CultureInfo("tr-TR"));
    private static string RestoreCase(string value, string source) => source.Length > 0 && char.IsUpper(source[0]) ? char.ToUpper(value[0], new System.Globalization.CultureInfo("tr-TR")) + value[1..] : value;
}

internal sealed class SymSpellChecker : SymSpell
{
    public SymSpellChecker(int initialCapacity, int maxEditDistanceDictionary) : base(initialCapacity, maxEditDistanceDictionary) { }
}
