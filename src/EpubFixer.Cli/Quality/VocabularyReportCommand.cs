using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using EpubFixer.Adapters.Lexicon;
using EpubFixer.Cli.Morphology;
using EpubFixer.Core.Epub;
using EpubFixer.Core.Lexicon;

namespace EpubFixer.Cli.Quality;

public sealed class VocabularyReportCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public int Run(string[] arguments, TextWriter output, TextWriter error)
    {
        if (arguments.Length < 2 || string.IsNullOrWhiteSpace(arguments[1]))
        {
            PrintUsage(error);
            return 1;
        }

        string? jsonPath = null;
        for (var index = 2; index < arguments.Length; index += 2)
        {
            if (index + 1 >= arguments.Length
                || !string.Equals(arguments[index], "--json", StringComparison.OrdinalIgnoreCase))
            {
                PrintUsage(error);
                return 1;
            }

            jsonPath = arguments[index + 1];
        }

        try
        {
            var inputPath = arguments[1];
            var package = new EpubPackageReader().Read(inputPath);
            using var oracleBuilder = new CachingMorphologyOracleBuilder(
                new FomaMorphologyOracleBuilder(),
                new MorphologyOracleCache(inputPath));
            var frequencyList = FileTurkishFrequencyListSource.Load();
            var targets = LoadTargets(inputPath);
            var stopwatch = Stopwatch.StartNew();
            var knowledge = new BookKnowledgeBuilder(frequencyList).Build(package.LogicalText, oracleBuilder, targets);
            stopwatch.Stop();

            var commit = ResolveGitCommit(inputPath);
            var vocabularyReport = VocabularyReport.Create(DatasetName.From(inputPath), knowledge, stopwatch.Elapsed.TotalSeconds, commit);
            var languageReport = LanguageModelReport.Create(DatasetName.From(inputPath), knowledge, stopwatch.Elapsed.TotalSeconds);
            WriteHuman(output, vocabularyReport, languageReport);

            if (jsonPath is not null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(jsonPath))!);
                File.WriteAllText(jsonPath, JsonSerializer.Serialize(vocabularyReport, JsonOptions), new UTF8Encoding(false));
                var languagePath = jsonPath.Replace(".vocabulary.", ".language-model.", StringComparison.Ordinal);
                if (!string.Equals(languagePath, jsonPath, StringComparison.Ordinal))
                {
                    File.WriteAllText(languagePath, JsonSerializer.Serialize(languageReport, JsonOptions), new UTF8Encoding(false));
                }
            }

            return 0;
        }
        catch (Exception exception) when (exception is ArgumentException
            or IOException
            or InvalidDataException
            or UnauthorizedAccessException
            or JsonException)
        {
            error.WriteLine($"Error: {exception.Message}");
            return 2;
        }
    }

    public static void PrintUsage(TextWriter error) =>
        error.WriteLine("       epubfixer debug-vocabulary <book.epub> [--json <out.vocabulary.json>]");

    private static void WriteHuman(TextWriter output, VocabularyReport vocabulary, LanguageModelReport language)
    {
        output.WriteLine($"VocabularySize: {vocabulary.VocabularySize}");
        output.WriteLine($"BookSourced: {vocabulary.BookSourced}");
        output.WriteLine($"FrequencySourced: {vocabulary.FrequencySourced}");
        output.WriteLine($"MorphologySourced: {vocabulary.MorphologySourced}");
        output.WriteLine($"HeldOutCoverage: {vocabulary.HeldOutCoverage.ToString("P2", CultureInfo.InvariantCulture)}");
        output.WriteLine($"TargetCoverage: {vocabulary.TargetCoverage.ToString("P2", CultureInfo.InvariantCulture)}");
        output.WriteLine($"SuspiciousEntries: {vocabulary.SuspiciousEntries}");
        output.WriteLine($"BuildSeconds: {vocabulary.BuildSeconds.ToString("0.000", CultureInfo.InvariantCulture)}");
        output.WriteLine($"UnigramTypes: {language.UnigramTypes}");
        output.WriteLine($"BigramTypes: {language.BigramTypes}");
        output.WriteLine($"HeldOutBigramHitRate: {language.HeldOutBigramHitRate.ToString("P2", CultureInfo.InvariantCulture)}");
    }

    private sealed record VocabularyReport(
        string Dataset,
        string MeasuredOn,
        string Commit,
        int VocabularySize,
        int BookSourced,
        int FrequencySourced,
        int MorphologySourced,
        double HeldOutCoverage,
        double TargetCoverage,
        IReadOnlyList<string> MissingTargets,
        int SuspiciousEntries,
        IReadOnlyList<string> SampledBookEntries,
        double BuildSeconds)
    {
        public static VocabularyReport Create(string dataset, BookKnowledge knowledge, double buildSeconds, string commit)
        {
            var entries = knowledge.Vocabulary.Words.Select(word => knowledge.Vocabulary.Find(word)!).ToArray();
            return new VocabularyReport(
                dataset,
                DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                commit,
                knowledge.Vocabulary.Count,
                entries.Count(entry => entry.Source == EpubFixer.Core.Lexicon.Models.VocabularySource.Book),
                entries.Count(entry => entry.Source == EpubFixer.Core.Lexicon.Models.VocabularySource.Frequency),
                entries.Count(entry => entry.Source == EpubFixer.Core.Lexicon.Models.VocabularySource.Morphology),
                knowledge.Coverage.HeldOutCoverage,
                knowledge.Coverage.TargetCoverage,
                knowledge.Coverage.MissingTargets,
                knowledge.Coverage.SuspiciousEntries,
                entries.Where(entry => entry.Source == EpubFixer.Core.Lexicon.Models.VocabularySource.Book)
                    .OrderBy(entry => entry.Normalized, StringComparer.Ordinal)
                    .Take(100)
                    .Select(entry => entry.PreferredSurface)
                    .ToArray(),
                Math.Round(buildSeconds, 3));
        }
    }

    private sealed record LanguageModelReport(
        string Dataset,
        string MeasuredOn,
        int UnigramTypes,
        int UnigramTokens,
        int BigramTypes,
        int BigramTokens,
        double HeldOutBigramHitRate,
        double BuildSeconds)
    {
        public static LanguageModelReport Create(string dataset, BookKnowledge knowledge, double buildSeconds)
        {
            var statistics = ((BookLanguageModel)knowledge.LanguageModel).Statistics;
            return new LanguageModelReport(
                dataset,
                DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                statistics.UnigramTypes,
                statistics.UnigramTokens,
                statistics.BigramTypes,
                statistics.BigramTokens,
                statistics.HeldOutBigramHitRate,
                Math.Round(buildSeconds, 3));
        }
    }

    private static IReadOnlyList<string> LoadTargets(string inputPath)
    {
        var groundTruthPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(inputPath))!, "ground-truth.json");
        if (!File.Exists(groundTruthPath))
        {
            return [];
        }

        using var document = JsonDocument.Parse(File.ReadAllText(groundTruthPath));
        if (!document.RootElement.TryGetProperty("knownErrors", out var knownErrors)
            || knownErrors.ValueKind is not JsonValueKind.Array)
        {
            return [];
        }

        return knownErrors
            .EnumerateArray()
            .Select(item => item.TryGetProperty("expected", out var expected) ? expected.GetString() : null)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static string ResolveGitCommit(string inputPath)
    {
        for (var directory = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(inputPath))!);
             directory is not null;
             directory = directory.Parent)
        {
            var git = Path.Combine(directory.FullName, ".git");
            if (Directory.Exists(git))
            {
                return ReadGitHead(git);
            }
        }

        return "unknown";
    }

    private static string ReadGitHead(string gitDirectory)
    {
        try
        {
            var head = File.ReadAllText(Path.Combine(gitDirectory, "HEAD")).Trim();
            const string refPrefix = "ref: ";
            if (!head.StartsWith(refPrefix, StringComparison.Ordinal))
            {
                return head;
            }

            var refPath = head[refPrefix.Length..].Replace('/', Path.DirectorySeparatorChar);
            var resolved = Path.Combine(gitDirectory, refPath);
            return File.Exists(resolved)
                ? File.ReadAllText(resolved).Trim()
                : "unknown";
        }
        catch (IOException)
        {
            return "unknown";
        }
        catch (UnauthorizedAccessException)
        {
            return "unknown";
        }
    }
}
