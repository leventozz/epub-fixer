using System.Text.Json;
using EpubFixer.Cli.Quality;
using EpubFixer.Core.Quality;

namespace EpubFixer.Tests;

public sealed class MeasureCommandTests
{
    [Fact]
    public void Run_MissingArgumentsPrintsUsageAndReturnsOne()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new MeasureCommand(_ => SampleHealth()).Run(["measure"], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("epubfixer measure", error.ToString());
    }

    [Fact]
    public void Run_WritesJsonWhenRequested()
    {
        var path = Path.Combine(Path.GetTempPath(), $"health-{Guid.NewGuid():N}.json");
        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = new MeasureCommand(_ => SampleHealth()).Run(["measure", "book.epub", "--json", path], output, error);

            Assert.Equal(0, exitCode);
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal(10, json.RootElement.GetProperty("totalTokens").GetInt32());
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Run_PrintsDeltaAgainstBaseline()
    {
        var baseline = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.json");
        File.WriteAllText(baseline, """{"unresolvableRate":5.0}""");
        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = new MeasureCommand(_ => SampleHealth()).Run(["measure", "book.epub", "--baseline", baseline], output, error);

            Assert.Equal(0, exitCode);
            Assert.Contains("UnresolvableRate: 5.00 -> 20.00", output.ToString());
        }
        finally
        {
            File.Delete(baseline);
        }
    }

    private static BookHealth SampleHealth() => new(10, 2, 1, 20d, ["bozuk"]);
}
