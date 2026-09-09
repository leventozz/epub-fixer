namespace EpubFixer.Tests;

public sealed class CliOptionsTests
{
    [Fact]
    public void TryParse_AcceptsApplyInlineAmongValuedOptions()
    {
        var parsed = CliOptions.TryParse(
            [
                "analyze",
                "book.epub",
                "--dump-text",
                "book.txt",
                "--apply-inline",
                "--hyphen-report",
                "hyphens.json"
            ],
            out var options);

        Assert.True(parsed);
        Assert.True(options.ApplyInline);
        Assert.Equal("book.epub", options.EpubPath);
        Assert.Equal("book.txt", options.DumpPath);
        Assert.Equal("hyphens.json", options.HyphenReportPath);
    }

    [Fact]
    public void TryParse_RejectsDuplicateApplyInlineFlag()
    {
        Assert.False(CliOptions.TryParse(
            ["analyze", "book.epub", "--apply-inline", "--apply-inline"],
            out _));
    }

    [Fact]
    public void TryParse_RejectsMissingValuedOptionArgumentBeforeApplyFlag()
    {
        Assert.False(CliOptions.TryParse(
            ["analyze", "book.epub", "--dump-text", "--apply-inline"],
            out _));
    }
}
