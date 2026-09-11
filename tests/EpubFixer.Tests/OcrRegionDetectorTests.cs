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

    private sealed class FakeAnalyzer(IEnumerable<string> invalid) : ITurkishMorphologyAnalyzer
    {
        private readonly HashSet<string> invalid = invalid.ToHashSet(StringComparer.Ordinal);
        public bool IsValidWord(string word) => !this.invalid.Contains(word);
    }
}
