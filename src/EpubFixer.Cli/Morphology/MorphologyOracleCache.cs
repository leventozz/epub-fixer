using System.Security.Cryptography;
using System.Text.Json;
using EpubFixer.Core.Morphology;

namespace EpubFixer.Cli.Morphology;

public sealed class MorphologyOracleCache
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string path;

    public MorphologyOracleCache(string sourceEpubPath, string? transducerPath = null, string? directory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEpubPath);
        var fullSource = System.IO.Path.GetFullPath(sourceEpubPath);
        if (!File.Exists(fullSource))
        {
            throw new FileNotFoundException("The EPUB file was not found.", fullSource);
        }

        var fullTransducer = transducerPath ?? System.IO.Path.Combine(AppContext.BaseDirectory, "Resources", "win-x64", "trmorph.fst");
        if (!File.Exists(fullTransducer))
        {
            throw new FileNotFoundException("The TRmorph transducer was not found.", fullTransducer);
        }

        CacheKey = $"{Sha256File(fullSource)}-{Sha256File(fullTransducer)[..16]}";
        DirectoryPath = directory ?? DefaultDirectory;
        path = System.IO.Path.Combine(DirectoryPath, CacheKey + ".json");
    }

    public static string DefaultDirectory =>
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EpubFixer", "morphology");

    public string CacheKey { get; }
    public string DirectoryPath { get; }
    public string FilePath => path;

    public IReadOnlyDictionary<string, IReadOnlyList<TurkishMorphologicalAnalysis>> Read()
    {
        try
        {
            if (!File.Exists(path))
            {
                return new Dictionary<string, IReadOnlyList<TurkishMorphologicalAnalysis>>(StringComparer.Ordinal);
            }

            var document = JsonSerializer.Deserialize<MorphologyCacheDocument>(File.ReadAllText(path), JsonOptions);
            if (document?.SchemaVersion != 1)
            {
                return new Dictionary<string, IReadOnlyList<TurkishMorphologicalAnalysis>>(StringComparer.Ordinal);
            }

            return document.Entries.ToDictionary(
                entry => entry.Word,
                entry => (IReadOnlyList<TurkishMorphologicalAnalysis>)entry.Analyses
                    .Select(analysis => analysis.ToAnalysis())
                    .ToArray(),
                StringComparer.Ordinal);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new Dictionary<string, IReadOnlyList<TurkishMorphologicalAnalysis>>(StringComparer.Ordinal);
        }
    }

    public void Write(IReadOnlyDictionary<string, IReadOnlyList<TurkishMorphologicalAnalysis>> analyses)
    {
        ArgumentNullException.ThrowIfNull(analyses);
        Directory.CreateDirectory(DirectoryPath);
        var document = new MorphologyCacheDocument
        {
            Entries = analyses
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new MorphologyCacheEntry
                {
                    Word = pair.Key,
                    Analyses = pair.Value
                        .OrderBy(analysis => analysis.Root, StringComparer.Ordinal)
                        .ThenBy(analysis => string.Join("\u001f", analysis.Tags.OrderBy(tag => tag, StringComparer.Ordinal)), StringComparer.Ordinal)
                        .Select(MorphologyCacheAnalysis.FromAnalysis)
                        .ToList()
                })
                .ToList()
        };
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(document, JsonOptions));
        File.Move(temporary, path, overwrite: true);
    }

    private static string Sha256File(string file)
    {
        using var stream = File.OpenRead(file);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
