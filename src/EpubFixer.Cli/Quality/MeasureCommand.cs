using System.Globalization;
using System.Text;
using System.Text.Json;
using EpubFixer.Core.Epub;
using EpubFixer.Core.Quality;
using EpubFixer.Core.Tokenization;
using EpubFixer.TrMorph;

namespace EpubFixer.Cli.Quality;

public sealed class MeasureCommand
{
    private readonly Func<string, BookHealth> measure;

    public MeasureCommand()
        : this(MeasureEpub)
    {
    }

    internal MeasureCommand(Func<string, BookHealth> measure)
    {
        this.measure = measure;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public int Run(string[] arguments, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (arguments.Length < 2 || string.IsNullOrWhiteSpace(arguments[1]))
        {
            PrintUsage(error);
            return 1;
        }

        string? jsonPath = null;
        string? baselinePath = null;
        for (var index = 2; index < arguments.Length; index += 2)
        {
            if (index + 1 >= arguments.Length || string.IsNullOrWhiteSpace(arguments[index + 1]))
            {
                PrintUsage(error);
                return 1;
            }

            if (string.Equals(arguments[index], "--json", StringComparison.OrdinalIgnoreCase))
            {
                jsonPath = arguments[index + 1];
            }
            else if (string.Equals(arguments[index], "--baseline", StringComparison.OrdinalIgnoreCase))
            {
                baselinePath = arguments[index + 1];
            }
            else
            {
                PrintUsage(error);
                return 1;
            }
        }

        try
        {
            var health = measure(arguments[1]);

            WriteHuman(output, arguments[1], health);

            if (jsonPath is not null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(jsonPath))!);
                File.WriteAllText(jsonPath, JsonSerializer.Serialize(health, JsonOptions), new UTF8Encoding(false));
            }

            if (baselinePath is not null)
            {
                WriteBaselineDelta(output, baselinePath, health);
            }

            return 0;
        }
        catch (Exception exception) when (exception is ArgumentException
            or IOException
            or InvalidDataException
            or UnauthorizedAccessException
            or TurkishMorphologyException
            or JsonException)
        {
            error.WriteLine($"Error: {exception.Message}");
            return 2;
        }
    }

    private static BookHealth MeasureEpub(string path)
    {
        var package = new EpubPackageReader().Read(path);
        var tokens = new WordTokenizer().Tokenize(package.LogicalText).Select(token => token.Text).ToArray();
        using var analyzer = new FomaTurkishMorphologyAnalyzer();
        var recognizer = new FrequencyMorphologyWordRecognizer(
            tokens,
            FrequencyMorphologyWordRecognizer.DefaultFrequencyListPath,
            analyzer);
        return new BookHealthMeter(recognizer).Measure(package.LogicalText);
    }

    private static void WriteHuman(TextWriter output, string path, BookHealth health)
    {
        output.WriteLine($"Input: {path}");
        output.WriteLine($"TotalTokens: {health.TotalTokens}");
        output.WriteLine($"UnresolvableTokens: {health.UnresolvableTokens}");
        output.WriteLine($"SuspiciousTokens: {health.SuspiciousTokens}");
        output.WriteLine($"UnresolvableRate: {health.UnresolvableRate.ToString("F2", CultureInfo.InvariantCulture)} per 1000 tokens");
        output.WriteLine("WorstExamples: " + string.Join(", ", health.WorstExamples));
    }

    private static void WriteBaselineDelta(TextWriter output, string baselinePath, BookHealth health)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(baselinePath));
        var baseline = document.RootElement.TryGetProperty("source", out var source)
            ? source
            : document.RootElement;
        if (!baseline.TryGetProperty("unresolvableRate", out var rateElement))
        {
            throw new InvalidDataException("Baseline does not contain unresolvableRate.");
        }

        var oldRate = rateElement.GetDouble();
        output.WriteLine();
        output.WriteLine("Baseline delta:");
        output.WriteLine(
            $"UnresolvableRate: {oldRate.ToString("F2", CultureInfo.InvariantCulture)} -> {health.UnresolvableRate.ToString("F2", CultureInfo.InvariantCulture)}  ({(health.UnresolvableRate - oldRate).ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture)})");
    }

    public static void PrintUsage(TextWriter error) =>
        error.WriteLine("       epubfixer measure <book.epub> [--json <out.json>] [--baseline <baseline.json>]");
}
