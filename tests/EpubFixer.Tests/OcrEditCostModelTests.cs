using EpubFixer.Core.Ocr;

namespace EpubFixer.Tests;

public sealed class OcrEditCostModelTests
{
    [Fact]
    public void DefaultsAreUnchanged()
    {
        var model = new OcrEditCostModel();

        Assert.Equal(0, model.Keep);
        Assert.Equal(0.15, model.LineBreakDeletion);
        Assert.Equal(0.20, model.GarbageDeletion);
        Assert.Equal(0.25, model.KnownGlyphSubstitution);
        Assert.Equal(0.25, model.HyphenDeletion);
        Assert.Equal(0.30, model.SpaceDeletion);
        Assert.Equal(0.40, model.ShortFragmentMerge);
        Assert.Equal(0.70, model.SpaceInsertion);
        Assert.Equal(1.00, model.OrdinarySubstitution);
        Assert.Equal(1.00, model.OrdinaryDeletion);
        Assert.Equal(1.00, model.OrdinaryInsertion);
        Assert.Equal(1.00, model.LocalPairContraction);
    }
}
