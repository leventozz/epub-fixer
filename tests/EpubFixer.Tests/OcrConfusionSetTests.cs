using EpubFixer.Core.Ocr;

namespace EpubFixer.Tests;

public sealed class OcrConfusionSetTests
{
    [Fact]
    public void Default_IsSymmetric()
    {
        var confusionSet = OcrConfusionSet.Default;

        Assert.True(confusionSet.IsKnownConfusion('ı', 'ü'));
        Assert.True(confusionSet.IsKnownConfusion('ü', 'ı'));
    }

    [Fact]
    public void Default_RejectsUnrelatedPair()
    {
        Assert.False(OcrConfusionSet.Default.IsKnownConfusion('ı', 'z'));
    }

    [Fact]
    public void Replacements_AreDeterministic()
    {
        var first = OcrConfusionSet.Default.Replacements('ı').ToArray();
        var second = OcrConfusionSet.Default.Replacements('ı').ToArray();

        Assert.Equal(first, second);
        Assert.Equal(first.OrderBy(x => x).ToArray(), first);
    }
}
