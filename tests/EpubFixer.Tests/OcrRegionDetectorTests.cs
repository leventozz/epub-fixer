using EpubFixer.Core.Morphology;
using EpubFixer.Core.Ocr;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Tests;

public sealed class OcrRegionDetectorTests
{
    [Fact]
    public void Detect_PreservesLineBreakAndLimitsExpansion()
    {
        var text = "temiz ya\n\ndn dörtte temiz";
        var regions = new OcrRegionDetector().Detect(text, new FakeAnalyzer(["dn"]));
        var region = Assert.Single(regions);
        Assert.Equal("ya\n\ndn", region.RawText);
        Assert.Equal(text.IndexOf("ya", StringComparison.Ordinal), region.Start);
        Assert.Contains(OcrRegionDetectionReason.SuspiciousLineBreak, region.DetectionReasons);
    }

    [Theory]
    [InlineData("e-posta")]
    [InlineData("Sankt-Pölten")]
    [InlineData("normal ifade")]
    public void Detect_DoesNotFlagCleanText(string text)
    {
        Assert.Empty(new OcrRegionDetector().Detect(text, new FakeAnalyzer([])));
    }

    [Fact]
    public void Detect_UsesPhysicalGarbageBoundaryAndStopsAtCleanWords()
    {
        var regions = new OcrRegionDetector().Detect("temiz :,ohbet ve temiz", new FakeAnalyzer([]));
        var region = Assert.Single(regions);
        Assert.Equal(":,ohbet", region.RawText);
    }

    [Fact]
    public void Detect_DoesNotUseMorphologyInvalidAsSeed()
    {
        var regions = new OcrRegionDetector().Detect("düşünüyorum, eve, Jeannie Billroth", new FakeAnalyzer(["düşünüyorum", "eve", "Jeannie", "Billroth"]));
        Assert.Empty(regions);
    }

    [Fact]
    public void Detect_ExpandsFragmentedGlyphChainButNotTrailingCleanToken()
    {
        var regions = new OcrRegionDetector().Detect("1 ı iç ve", new FakeAnalyzer(["1", "ı"]));
        var region = Assert.Single(regions);
        Assert.Equal("1 ı iç", region.RawText);
    }

    [Fact]
    public void Detect_RecognizesMalformedHyphenShapes()
    {
        var regions = new OcrRegionDetector().Detect("Anacadde-si'ni Sankt-Pölten", new FakeAnalyzer([]));
        var region = Assert.Single(regions);
        Assert.Equal("Anacadde-si'ni", region.RawText);
    }

    private sealed class FakeAnalyzer(IEnumerable<string> invalid) : ITurkishMorphologyAnalyzer
    {
        private readonly HashSet<string> invalid = invalid.ToHashSet(StringComparer.Ordinal);
        public bool IsValidWord(string word) => !this.invalid.Contains(word);
    }
}
