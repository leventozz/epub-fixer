using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Tests;

public sealed class CleanTokenSequenceTests
{
    [Fact]
    public void Build_SkipsTokensIntersectingCorruptedRegionAndStartsNewSegment()
    {
        var stream = TestStreamFactory.FromSingleSegment("temiz bozuk sonra");
        var region = new CorruptedTextRegion("bozuk", 6, 11, ["bozuk"], "", "", []);

        var tokens = CleanTokenSequence.Build(stream, [region]);

        Assert.Equal(["temiz", "sonra"], tokens.Select(token => token.Normalized));
        Assert.True(tokens[1].StartsSegment);
    }
}
