using EpubFixer.Core.Ocr.Lattice;
using EpubFixer.Core.Ocr.Lattice.Models;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Tests;

public sealed class LatticeWindowTests
{
    [Fact]
    public void Window_IncludesOneContextTokenEachSide()
    {
        const string fullText = "berjer koli ukta oturdu";
        var region = Region(fullText, "koli ukta");

        var window = LatticeWindow.Select(region, fullText, new LatticeOptions(ContextTokens: 1));

        Assert.Equal("berjer koli ukta oturdu", window.Window);
        Assert.Equal(0, window.WindowOffset);
    }

    [Fact]
    public void Window_StopsAtHardBoundary()
    {
        const string fullText = "önce\n\nberjer koli ukta oturdu";
        var region = Region(fullText, "koli ukta");

        var window = LatticeWindow.Select(region, fullText, new LatticeOptions(ContextTokens: 2));

        Assert.Equal("berjer koli ukta oturdu", window.Window);
    }

    [Fact]
    public void Window_TooLongIsSkipped()
    {
        var fullText = new string('a', 60);
        var region = new CorruptedTextRegion(fullText, 0, fullText.Length, [fullText], string.Empty, string.Empty, []);

        var window = LatticeWindow.Select(region, fullText, new LatticeOptions(MaxWindowLength: 48));

        Assert.Equal(LatticeBuildOutcome.SkippedTooLong, window.Outcome);
        Assert.Equal(string.Empty, window.Window);
    }

    [Fact]
    public void Window_OffsetMapsBackToFullText()
    {
        const string fullText = "önce berjer koli ukta oturdu sonra";
        var region = Region(fullText, "koli ukta");

        var window = LatticeWindow.Select(region, fullText, new LatticeOptions(ContextTokens: 1));

        Assert.Equal(window.Window, fullText.Substring(window.WindowOffset, window.Window.Length));
    }

    private static CorruptedTextRegion Region(string fullText, string raw)
    {
        var start = fullText.IndexOf(raw, StringComparison.Ordinal);
        Assert.True(start >= 0);
        return new CorruptedTextRegion(raw, start, start + raw.Length, [raw], string.Empty, string.Empty, []);
    }
}
