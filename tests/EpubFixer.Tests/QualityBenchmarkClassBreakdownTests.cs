using EpubFixer.QualityBenchmarks;
using EpubFixer.QualityBenchmarks.Models;

namespace EpubFixer.Tests;

/// <summary>
/// R5.0a alt adim 1: docs/phase-5-plan.md@adc202d bolum 6.2, tespit edilmemis (deferred) bir kaydin
/// sinif kirilimindaki "correct" sayacina yanlislikla dahil edilip edilmedigini dogrular.
///
/// Bulgu (kural 4.4 - once alet dogrulanir): "correct" sayaci zaten "failures" listesindeki
/// HER kaydi (hem WronglyFixed hem Deferred siniflandirmasi) diskarte ediyor, cunku
/// QualityBenchmarkCorrectionEvaluator her known error icin tam olarak bir sinif uretiyor
/// (CorrectlyFixed => failures'a hic girmez; Deferred/WronglyFixed => ikisi de failures'a
/// girer). Dolayisiyla "correct" zaten yalnizca gercekten CorrectlyFixed olan kayitlari
/// sayiyor; plandaki 6.2 tespiti bugunku koda uymuyor (bkz. bitis raporu).
///
/// Bu testler o degismezi kilitler ve "detected" (missed-listesi, kaba sezgisel tespit)
/// ile "correct" (failures-listesi, gercek duzeltme sonucu) sutunlarinin birbirinden
/// bagimsiz kaldigini kanitlar.
/// </summary>
public sealed class QualityBenchmarkClassBreakdownTests
{
    [Fact]
    public void DeferredRecord_IsExcludedFromCorrect()
    {
        var knownErrors = new[]
        {
            KnownError("a", OcrErrorClass.GlyphConfusion),
            KnownError("b", OcrErrorClass.GlyphConfusion)
        };
        var failures = new[]
        {
            new KnownErrorCorrectionFailure(knownErrors[0], "Deferred", knownErrors[0].Original)
        };

        var breakdowns = QualityBenchmarkRunner.CreateClassBreakdowns(knownErrors, missed: [], failures);

        var glyph = Assert.Single(breakdowns);
        Assert.Equal(2, glyph.Known);
        Assert.Equal(1, glyph.Correct);
        Assert.Equal(0, glyph.Wrong);
        Assert.Equal(0.5, glyph.Recall);
        Assert.Equal(1.0, glyph.Precision); // the one attempt that was made was correct
    }

    [Fact]
    public void WronglyFixedRecord_ReducesCorrectAndCountsAsWrong()
    {
        var knownErrors = new[]
        {
            KnownError("a", OcrErrorClass.SpuriousSpace),
            KnownError("b", OcrErrorClass.SpuriousSpace)
        };
        var failures = new[]
        {
            new KnownErrorCorrectionFailure(knownErrors[0], "WronglyFixed", "garbled")
        };

        var breakdowns = QualityBenchmarkRunner.CreateClassBreakdowns(knownErrors, missed: [], failures);

        var cls = Assert.Single(breakdowns);
        Assert.Equal(1, cls.Correct);
        Assert.Equal(1, cls.Wrong);
        Assert.Equal(0.5, cls.Precision);
    }

    [Fact]
    public void AllRecordsCorrectlyFixed_NoneCountedAsWrongOrExcluded()
    {
        var knownErrors = new[]
        {
            KnownError("a", OcrErrorClass.Hyphenation),
            KnownError("b", OcrErrorClass.Hyphenation)
        };

        var breakdowns = QualityBenchmarkRunner.CreateClassBreakdowns(knownErrors, missed: [], failures: []);

        var cls = Assert.Single(breakdowns);
        Assert.Equal(2, cls.Correct);
        Assert.Equal(0, cls.Wrong);
        Assert.Equal(1.0, cls.Precision);
        Assert.Equal(1.0, cls.Recall);
    }

    [Fact]
    public void DetectionMissed_DoesNotLeakIntoCorrectOrWrong()
    {
        // "missed" (detection-stage, coarse heuristic) and "failures" (correction-stage,
        // actual observed text) are independent measurements. A record the detector never
        // flagged as a candidate can still have been corrected by the engine - Detected and
        // Correct must not be conflated.
        var knownErrors = new[] { KnownError("a", OcrErrorClass.Fragmentation) };

        var breakdowns = QualityBenchmarkRunner.CreateClassBreakdowns(
            knownErrors,
            missed: knownErrors,
            failures: []);

        var cls = Assert.Single(breakdowns);
        Assert.Equal(0, cls.Detected);
        Assert.Equal(1, cls.Correct);
        Assert.Equal(0, cls.Wrong);
    }

    private static KnownErrorOccurrence KnownError(string id, OcrErrorClass errorClass) =>
        new(id, "doc.xhtml", "original", "expected", [])
        {
            ErrorClass = errorClass
        };
}
