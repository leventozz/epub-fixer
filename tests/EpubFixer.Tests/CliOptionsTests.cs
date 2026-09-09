namespace EpubFixer.Tests;

public sealed class CliOptionsTests
{
    [Fact]
    public void TryParse_AcceptsFixWithRequiredOutput()
    {
        var parsed = CliOptions.TryParse(
            ["fix", "book.epub", "-o", "book.fixed.epub"],
            out var options);

        Assert.True(parsed);
        Assert.Equal(CliCommand.Fix, options.Command);
        Assert.Equal("book.epub", options.EpubPath);
        Assert.Equal("book.fixed.epub", options.OutputEpubPath);
    }

    [Theory]
    [InlineData("fix", "book.epub")]
    [InlineData("fix", "book.epub", "-o")]
    [InlineData("fix", "book.epub", "-o", "a.epub", "-o", "b.epub")]
    [InlineData("fix", "book.epub", "--apply-inline", "book.fixed.epub")]
    [InlineData("analyze", "book.epub", "-o", "book.fixed.epub")]
    public void TryParse_RejectsInvalidFixSyntax(params string[] arguments)
    {
        Assert.False(CliOptions.TryParse(arguments, out _));
    }

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

    [Fact]
    public void TryParse_AcceptsApplyParagraphWithApplyInline()
    {
        var parsed = CliOptions.TryParse(
            ["analyze", "book.epub", "--apply-paragraph", "--apply-inline"],
            out var options);

        Assert.True(parsed);
        Assert.True(options.ApplyInline);
        Assert.True(options.ApplyParagraph);
    }

    [Fact]
    public void TryParse_AcceptsPostFixLexiconReport()
    {
        var parsed = CliOptions.TryParse(
            ["analyze", "book.epub", "--post-fix-lexicon-report", "post-fix.md"],
            out var options);

        Assert.True(parsed);
        Assert.Equal("post-fix.md", options.PostFixLexiconReportPath);
        Assert.False(options.ApplyParagraph);
    }

    [Fact]
    public void TryParse_AcceptsOcrReport()
    {
        var parsed = CliOptions.TryParse(
            ["analyze", "book.epub", "--ocr-report", "ocr.md"],
            out var options);

        Assert.True(parsed);
        Assert.Equal("ocr.md", options.OcrReportPath);
    }

    [Fact]
    public void TryParse_AcceptsOcrCorrectionReport()
    {
        var parsed = CliOptions.TryParse(
            ["analyze", "book.epub", "--ocr-correction-report", "ocr-candidates.md"],
            out var options);

        Assert.True(parsed);
        Assert.Equal("ocr-candidates.md", options.OcrCorrectionReportPath);
    }

    [Fact]
    public void TryParse_AcceptsStandaloneApplyParagraph()
    {
        var parsed = CliOptions.TryParse(
            ["analyze", "book.epub", "--apply-paragraph"],
            out var options);

        Assert.True(parsed);
        Assert.False(options.ApplyInline);
        Assert.True(options.ApplyParagraph);
    }

    [Fact]
    public void TryParse_AcceptsTrMorphReportOnlyWithGroundTruth()
    {
        var parsed = CliOptions.TryParse(
            ["analyze", "book.epub", "--trmorph-report", "report.md", "--ground-truth", "gt.json"],
            out var options);

        Assert.True(parsed);
        Assert.Equal("report.md", options.TrMorphReportPath);
        Assert.Equal("gt.json", options.GroundTruthPath);
    }

    [Theory]
    [InlineData("--trmorph-report", "report.md")]
    [InlineData("--ground-truth", "gt.json")]
    public void TryParse_RequiresTrMorphReportAndGroundTruthTogether(string option, string value)
    {
        Assert.False(CliOptions.TryParse(["analyze", "book.epub", option, value], out _));
    }

    [Fact]
    public void TryParse_RejectsDuplicateApplyParagraphFlag()
    {
        Assert.False(CliOptions.TryParse(
            ["analyze", "book.epub", "--apply-paragraph", "--apply-paragraph"],
            out _));
    }

    [Fact]
    public void TryParse_RejectsMissingValuedOptionArgumentBeforeParagraphFlag()
    {
        Assert.False(CliOptions.TryParse(
            ["analyze", "book.epub", "--dump-text", "--apply-paragraph"],
            out _));
    }
}
