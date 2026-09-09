using System.Diagnostics;
using System.Text;
using EpubFixer.Core.Morphology;

namespace EpubFixer.TrMorph;

public sealed class FomaTurkishMorphologyAnalyzer : ITurkishMorphologyAnalyzer, IDisposable
{
    private readonly Process process;
    private readonly StreamWriter input;
    private readonly StreamReader output;
    private readonly Dictionary<string, bool> cache = new(StringComparer.Ordinal);
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
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(word);
        if (cache.TryGetValue(word, out var cached)) return cached;
        if (process.HasExited) throw Failure("TRmorph flookup exited before the query completed.");

        input.WriteLine(word);
        input.Flush();
        var valid = false;
        while (true)
        {
            var line = output.ReadLine();
            if (line is null) throw Failure("TRmorph flookup ended unexpectedly.");
            if (line.Length == 0) break;
            if (!string.Equals(line, "+?", StringComparison.Ordinal)) valid = true;
        }

        cache[word] = valid;
        return valid;
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
