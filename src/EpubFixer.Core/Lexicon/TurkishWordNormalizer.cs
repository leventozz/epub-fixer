using System.Globalization;
using System.Text;

namespace EpubFixer.Core.Lexicon;

public static class TurkishWordNormalizer
{
    private static readonly CultureInfo TurkishCulture = new("tr-TR");

    public static string Normalize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Normalize(NormalizationForm.FormC).ToLower(TurkishCulture);
    }
}
