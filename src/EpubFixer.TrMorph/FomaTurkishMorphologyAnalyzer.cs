using System.Diagnostics;
using System.Text;
using EpubFixer.Core.Morphology;

namespace EpubFixer.TrMorph;

public sealed class FomaTurkishMorphologyAnalyzer : ITurkishMorphologyAnalyzer, IBatchTurkishMorphologicalParser, IDisposable
{
    private readonly Process process;
    private readonly StreamWriter input;
    private readonly StreamReader output;
    private readonly object sync = new();
    private readonly Dictionary<string, IReadOnlyList<TurkishMorphologicalAnalysis>> cache = new(StringComparer.Ordinal);
    private long hits;
    private long misses;
    private long batchRequests;
    private long batchedWords;
    private bool disposed;

    public FomaTurkishMorphologyAnalyzer(string? flookupPath = null, string? transducerPath = null)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new TurkishMorphologyException("TRmorph currently supports only win-x64.");
        }

        var resourceDirectory = Path.Combine(AppContext.BaseDirectory, "Resources", "win-x64");
        var executable = flookupPath ?? Path.Combine(resourceDirectory, "flookup.exe");
        var transducer = transducerPath ?? Path.Combine(resourceDirectory, "trmorph.fst");
        RequireFile(executable, "flookup executable");
        RequireFile(transducer, "TRmorph transducer");

        process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = $"-b -x \"{transducer.Replace("\"", "\\\"")}\"",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardInputEncoding = new UTF8Encoding(false),
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = new UTF8Encoding(false)
            }
        };

        try
        {
            if (!process.Start())
            {
                throw new TurkishMorphologyException("TRmorph flookup process could not be started.");
            }
        }
        catch (Exception exception) when (exception is not TurkishMorphologyException)
        {
            throw new TurkishMorphologyException("TRmorph flookup process could not be started.", exception);
        }

        input = process.StandardInput;
        output = process.StandardOutput;
    }

    public bool IsValidWord(string word)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(word);
        return Analyze(word).Count > 0;
    }

    public IReadOnlyList<TurkishMorphologicalAnalysis> Analyze(string word)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(word);
        return AnalyzeBatch(new[] { word })[word.Normalize()];
    }

    public TurkishMorphologyCacheStatistics CacheStatistics
    {
        get { lock (sync) return new(hits, misses, cache.Count, batchRequests, batchedWords); }
    }

    public IReadOnlyDictionary<string, IReadOnlyList<TurkishMorphologicalAnalysis>> AnalyzeBatch(IEnumerable<string> words)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(words);
        var requested = words.Select(word => { ArgumentException.ThrowIfNullOrWhiteSpace(word); return word.Normalize(); }).ToList();
        lock (sync)
        {
            batchRequests++;
            batchedWords += requested.Count;
            var missing = requested.Distinct(StringComparer.Ordinal).Where(word => !cache.ContainsKey(word)).ToList();
            hits += requested.Count(word => cache.ContainsKey(word));
            misses += missing.Count;
            foreach (var word in missing) input.WriteLine(word);
            if (missing.Count > 0) input.Flush();
            foreach (var word in missing) cache[word] = ReadResult();
            return requested.Distinct(StringComparer.Ordinal).ToDictionary(word => word, word => cache[word], StringComparer.Ordinal);
        }
    }

    private IReadOnlyList<TurkishMorphologicalAnalysis> ReadResult()
    {
        if (process.HasExited) throw Failure("TRmorph flookup exited before the query completed.");
        var analyses = new List<TurkishMorphologicalAnalysis>();
        while (true)
        {
            var line = output.ReadLine();
            if (line is null) throw Failure("TRmorph flookup ended unexpectedly.");
            if (line.Length == 0) break;
            if (line == "+?") continue;
            var first = line.IndexOf('<');
            var root = first < 0 ? line : line[..first];
            var tags = new HashSet<string>(StringComparer.Ordinal);
            if (first >= 0)
            {
                foreach (var tag in line[first..].Split('<', StringSplitOptions.RemoveEmptyEntries))
                    tags.Add(tag.TrimEnd('>'));
            }
            analyses.Add(new(root, tags));
        }
        return analyses;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try { input.Close(); } catch { }
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
        process.Dispose();
    }

    private TurkishMorphologyException Failure(string message)
    {
        var error = process.StandardError.ReadToEnd();
        return new TurkishMorphologyException(
            string.IsNullOrWhiteSpace(error) ? message : $"{message} {error.Trim()}");
    }

    private static void RequireFile(string path, string description)
    {
        if (!File.Exists(path))
            throw new TurkishMorphologyException($"TRmorph {description} is missing: '{path}'.");
    }
}
