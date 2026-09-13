using EpubFixer.Cli.Lexicon;
using EpubFixer.Cli.Ocr.Lattice;
using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Ocr.Lattice;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Tests;

internal static class LatticeFixture
{
    private static readonly Lazy<BookVocabulary> FrequencyVocabulary = new(CreateFrequencyVocabulary);

    public static IReadOnlyList<(string Source, string Target)> Occurrences { get; } =
    [
        ("ı ıç", "üç"),
        ("kendi-ıni", "kendimi"),
        ("1 ı iç", "hiç"),
        (":,ohbet", "sohbet"),
        ("koli ukta", "koltukta"),
        ("ı ızellikle", "özellikle"),
        ("ı ılduğu", "olduğu"),
        ("Ce-lıimde", "Cebimde"),
        ("ge-^:cn", "geçen"),
        ("yü-ıiimeye", "yürümeye")
    ];

    public static string RegionText =>
        File.ReadAllText(FindRepositoryFile(Path.Combine("tests", "Fixtures", "OcrRegion", "odun-kesmek-region-01.txt")));

    public static BookVocabulary Vocabulary => FrequencyVocabulary.Value;

    public static WordLatticeBuilder CreateBuilder() =>
        new(new SymSpellLexiconMatcher(Vocabulary));

    public static CorruptedTextRegion Region(string raw, string fullText)
    {
        var start = fullText.IndexOf(raw, StringComparison.Ordinal);
        Assert.True(start >= 0);
        return new CorruptedTextRegion(raw, start, start + raw.Length, [raw], string.Empty, string.Empty, []);
    }

    private static BookVocabulary CreateFrequencyVocabulary()
    {
        var targets = Occurrences.Select(item => item.Target).Distinct(StringComparer.Ordinal).ToArray();
        var text = string.Join(' ', targets.SelectMany(word => new[] { word, word }));
        return new BookVocabularyBuilder(
                FileTurkishFrequencyListSource.Load(),
                new BookVocabularyOptions(MinBookCount: 1))
            .Build(TestStreamFactory.FromSingleSegment(text), Array.Empty<CorruptedTextRegion>(), new FakeMorphologyOracle(targets));
    }

    private static string FindRepositoryFile(string relativePath)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(relativePath);
    }
}
