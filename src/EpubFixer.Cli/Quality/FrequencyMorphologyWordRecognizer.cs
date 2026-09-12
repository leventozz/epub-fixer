using System.Text;
using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Quality;

namespace EpubFixer.Cli.Quality;

public sealed class FrequencyMorphologyWordRecognizer : IWordRecognizer
{
    private readonly HashSet<string> frequencyWords;
    private readonly IReadOnlyDictionary<string, bool> morphology;

    public FrequencyMorphologyWordRecognizer(
        IEnumerable<string> tokens,
        string frequencyListPath,
        IBatchTurkishMorphologicalParser parser)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentException.ThrowIfNullOrWhiteSpace(frequencyListPath);
        ArgumentNullException.ThrowIfNull(parser);

        frequencyWords = LoadFrequencyWords(frequencyListPath);
        var uniqueTokens = tokens
            .Select(TurkishWordNormalizer.Normalize)
            .SelectMany(WithApostropheStem)
            .Where(IsLexical)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(token => token, StringComparer.Ordinal)
            .ToArray();
        var analyses = parser.AnalyzeBatch(uniqueTokens);
        morphology = uniqueTokens.ToDictionary(
            token => token,
            token => analyses.TryGetValue(token, out var result) && result.Count > 0,
            StringComparer.Ordinal);
    }

    public bool IsRecognized(string normalizedWord) =>
        frequencyWords.Contains(normalizedWord)
        || morphology.GetValueOrDefault(normalizedWord);

    public static string DefaultFrequencyListPath =>
        Path.Combine(AppContext.BaseDirectory, "Resources", "OcrReconstruction", "tr_50k.txt");

    private static HashSet<string> LoadFrequencyWords(string path)
    {
        var words = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(path, Encoding.UTF8))
        {
            var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length == 0)
            {
                continue;
            }

            var word = TurkishWordNormalizer.Normalize(fields[0]);
            if (IsLexical(word))
            {
                words.Add(word);
            }
        }

        return words;
    }

    private static bool IsLexical(string value)
    {
        var hasLetter = false;
        foreach (var rune in value.EnumerateRunes())
        {
            if (Rune.IsLetter(rune))
            {
                hasLetter = true;
                continue;
            }

            if (rune.Value is '\'' or '’')
            {
                continue;
            }

            return false;
        }

        return hasLetter;
    }

    private static IEnumerable<string> WithApostropheStem(string token)
    {
        yield return token;
        var apostrophe = token.IndexOfAny(['\'', '’']);
        if (apostrophe > 0)
        {
            yield return token[..apostrophe];
        }
    }
}
