using EpubFixer.Core.Quality;
using EpubFixer.Core.Epub;

namespace EpubFixer.Tests;

public sealed class SuspiciousTokenRulesTests
{
    [Fact]
    public void S1_EmbeddedDigitIsSuspicious() =>
        Assert.True(SuspiciousTokenRules.IsSuspicious("ge1en"));

    [Fact]
    public void S2_GarbageGlyphIsSuspicious() =>
        Assert.True(SuspiciousTokenRules.IsSuspicious("ge-^:cn"));

    [Fact]
    public void S3_InnerDoublePunctuationIsSuspicious() =>
        Assert.True(SuspiciousTokenRules.IsSuspicious(":,ohbet"));

    [Fact]
    public void S3_TrailingPunctuationIsNotSuspicious() =>
        Assert.False(SuspiciousTokenRules.IsSuspicious("sohbet."));

    [Fact]
    public void S4_IsolatedSingleLetterNextToSingleLetterIsSuspicious() =>
        Assert.True(SuspiciousTokenRules.IsSuspicious("ı", nextRawWord: "i"));

    [Fact]
    public void S4_SingleLetterWordAloneIsNotSuspicious() =>
        Assert.False(SuspiciousTokenRules.IsSuspicious("o", nextRawWord: "adam"));

    [Fact]
    public void MultipleRulesCountTokenOnce() =>
        Assert.Equal(1, SuspiciousTokenRules.CountSuspiciousRawWords("ge1^en"));

    [Fact]
    public void TextNodeBoundaryKeepsRawWordTogether()
    {
        using var epub = TemporaryEpub.Create(
            [new TestDocument("chapter", "chapter.xhtml", """
                <html xmlns="http://www.w3.org/1999/xhtml"><body><p>ge<span>1</span>en</p></body></html>
                """)],
            [new TestSpineItem("chapter")]);
        var stream = new EpubPackageReader().Read(epub.Path).LogicalText;

        Assert.Equal(1, SuspiciousTokenRules.CountSuspiciousRawWords(stream));
    }
}
