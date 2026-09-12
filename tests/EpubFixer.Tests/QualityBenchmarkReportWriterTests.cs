using EpubFixer.QualityBenchmarks;
using EpubFixer.QualityBenchmarks.Models;

namespace EpubFixer.Tests;

public sealed class QualityBenchmarkReportWriterTests
{
    [Fact]
    public void ReportWriter_PrintsPerClassPrecisionAndRecall()
    {
        var result = new QualityBenchmarkResult(2, 1, 1, 0.5d, [])
        {
            CorrectlyFixed = 1,
            WronglyFixed = 1,
            ClassBreakdowns =
            [
                new QualityBenchmarkClassBreakdown(
                    OcrErrorClass.GlyphConfusion,
                    2,
                    1,
                    1,
                    1,
                    0.5d,
                    0.5d)
            ]
        };
        using var writer = new StringWriter();

        QualityBenchmarkReportWriter.Write(writer, "synthetic", result);

        var text = writer.ToString();
        Assert.Contains("Per-class breakdown:", text);
        Assert.Contains("GlyphConfusion", text);
        Assert.Contains("50.00%", text);
    }

    [Fact]
    public void ReportWriter_PrintsNotAvailableWhenClassHasNoDetections()
    {
        var result = new QualityBenchmarkResult(1, 0, 1, 0d, [])
        {
            ClassBreakdowns =
            [
                new QualityBenchmarkClassBreakdown(
                    OcrErrorClass.SpuriousSpace,
                    1,
                    0,
                    0,
                    0,
                    null,
                    0d)
            ]
        };
        using var writer = new StringWriter();

        QualityBenchmarkReportWriter.Write(writer, "synthetic", result);

        Assert.Contains("N/A", writer.ToString());
    }
}
