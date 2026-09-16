using System.Text;

namespace EpubFixer.Core.Morphology;

public static class MorphologyPrefill
{
    public static string[] NormalizeDistinctOrder(IEnumerable<string> vocabulary)
    {
        ArgumentNullException.ThrowIfNull(vocabulary);
        return vocabulary
            .Where(word => !string.IsNullOrWhiteSpace(word))
            .Select(word =>
            {
                return word.Normalize(NormalizationForm.FormC);
            })
            .Distinct(StringComparer.Ordinal)
            .OrderBy(word => word, StringComparer.Ordinal)
            .ToArray();
    }
}
