using EpubFixer.TrMorph;

namespace EpubFixer.Tests;

public sealed class FomaTurkishMorphologyAnalyzerTests
{
    [Fact]
    public void Constructor_ReportsMissingRuntimeResourceExplicitly()
    {
        if (!OperatingSystem.IsWindows()) return;

        var exception = Assert.Throws<TurkishMorphologyException>(() =>
            new FomaTurkishMorphologyAnalyzer(
                Path.Combine(Path.GetTempPath(), "missing-flookup.exe"),
                Path.Combine(Path.GetTempPath(), "missing-trmorph.fst")));

        Assert.Contains("missing", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
