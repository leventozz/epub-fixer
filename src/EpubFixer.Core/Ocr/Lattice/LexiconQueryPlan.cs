using EpubFixer.Core.Lexicon;

namespace EpubFixer.Core.Ocr.Lattice;

public sealed class LexiconQueryPlan
{
    public const int MaxConfusionPositions = 4;

    private readonly OcrConfusionSet confusionSet;

    public LexiconQueryPlan(OcrConfusionSet? confusionSet = null)
    {
        this.confusionSet = confusionSet ?? OcrConfusionSet.Default;
    }

    public IReadOnlyList<string> CreateTierOne(ReadOnlySpan<char> span)
    {
        var normalized = TurkishWordNormalizer.Normalize(span.ToString());
        var noSeparators = StripSeparators(normalized);
        var noGarbage = StripGarbage(normalized);
        var compact = StripGarbage(noSeparators);
        return [normalized, noSeparators, noGarbage, compact];
    }

    public IReadOnlyList<string> CreateTierTwo(ReadOnlySpan<char> span, int maxQueries)
    {
        if (maxQueries <= 0)
        {
            return Array.Empty<string>();
        }

        var compact = StripGarbage(StripSeparators(TurkishWordNormalizer.Normalize(span.ToString())));
        var results = new List<string>(maxQueries);
        var positions = 0;
        for (var i = 0; i < compact.Length && positions < MaxConfusionPositions && results.Count < maxQueries; i++)
        {
            var replacements = confusionSet.Replacements(compact[i]);
            if (replacements.Count == 0)
            {
                continue;
            }

            positions++;
            foreach (var replacement in replacements)
            {
                if (results.Count >= maxQueries)
                {
                    break;
                }

                if (replacement == compact[i])
                {
                    continue;
                }

                results.Add(compact[..i] + replacement + compact[(i + 1)..]);
            }
        }

        return Distinct(results);
    }

    private static IReadOnlyList<string> Distinct(IEnumerable<string> values) =>
        values
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static string StripSeparators(string value) =>
        new(value.Where(character => character is not (' ' or '\t' or '-' or '\u00ad')).ToArray());

    private string StripGarbage(string value) =>
        new(value.Where(character => !confusionSet.IsGarbageGlyph(character)).ToArray());
}
