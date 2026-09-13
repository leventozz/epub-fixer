namespace EpubFixer.Core.Ocr;

public sealed class OcrConfusionSet
{
    public static OcrConfusionSet Default { get; } = CreateDefault();

    private readonly Dictionary<char, char[]> replacements;
    private readonly HashSet<(char Source, char Target)> knownPairs;
    private readonly HashSet<char> garbageGlyphs = ['^', ';', ':', '<', '>', ','];

    private OcrConfusionSet(Dictionary<char, char[]> replacements, HashSet<(char Source, char Target)> knownPairs)
    {
        this.replacements = replacements;
        this.knownPairs = knownPairs;
    }

    public bool IsKnownConfusion(char source, char target) => knownPairs.Contains((source, target));

    public IReadOnlyList<char> Replacements(char source) =>
        replacements.TryGetValue(source, out var values) ? values : Array.Empty<char>();

    public bool IsGarbageGlyph(char value) => garbageGlyphs.Contains(value);

    private static OcrConfusionSet CreateDefault()
    {
        var pairs = new HashSet<(char Source, char Target)>();

        AddClique(['1', 'l', 'ı', 'i', 'I']);
        AddPair('ı', 'ü');
        AddPair('ı', 'ö');
        AddPair('ı', 'o');
        AddPair('ı', 'r');
        AddPair('l', 'b');
        AddPair('i', 'h');
        AddPair('0', 'o');
        AddPair('0', 'ö');
        AddPair('3', 'e');
        AddPair('^', 'ş');
        AddPair('^', 'ç');
        AddPair('c', 'e');
        AddPair('c', 'ç');
        AddPair('s', 'ş');
        AddPair('g', 'ğ');
        AddPair('u', 'ü');
        AddPair('o', 'ö');

        var map = pairs
            .GroupBy(x => x.Source)
            .ToDictionary(
                x => x.Key,
                x => x.Select(pair => pair.Target).Distinct().OrderBy(value => value).ToArray());

        return new OcrConfusionSet(map, pairs);

        void AddClique(ReadOnlySpan<char> values)
        {
            for (var i = 0; i < values.Length; i++)
            {
                for (var j = 0; j < values.Length; j++)
                {
                    if (i != j)
                    {
                        AddDirected(values[i], values[j]);
                    }
                }
            }
        }

        void AddPair(char first, char second)
        {
            AddDirected(first, second);
            AddDirected(second, first);
        }

        void AddDirected(char source, char target) => pairs.Add((source, target));
    }
}
