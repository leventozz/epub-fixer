using EpubFixer.Core.Lexicon;

namespace EpubFixer.Tests;

public sealed class VocabularyCoverageMeterTests
{
    [Fact]
    public void Measure_EmptyTargetsDoesNotPretendFullCoverage()
    {
        var vocabulary = BuildVocabulary("kitap kitap");
        var stream = TestStreamFactory.FromSingleSegment("kitap kitap");

        var report = new VocabularyCoverageMeter().Measure(vocabulary, vocabulary, stream, [], []);

        Assert.Equal(0, report.TargetCoverage);
        Assert.Empty(report.MissingTargets);
    }

    [Fact]
    public void Measure_ReportsMissingTargets()
    {
        var vocabulary = BuildVocabulary("kitap kitap");
        var stream = TestStreamFactory.FromSingleSegment("kitap kitap");

        var report = new VocabularyCoverageMeter().Measure(vocabulary, vocabulary, stream, [], ["kitap", "eksik"]);

        Assert.Equal(0.5, report.TargetCoverage);
        Assert.Equal(["eksik"], report.MissingTargets);
    }

    [Fact]
    public void Measure_UsesTokenMassHeldOutSplit()
    {
        var fullVocabulary = BuildVocabulary("aa aa aa aa zz");
        var trainingVocabulary = BuildVocabulary("aa aa aa aa");
        var stream = TestStreamFactory.FromSingleSegment("aa aa aa aa zz");

        var report = new VocabularyCoverageMeter().Measure(fullVocabulary, trainingVocabulary, stream, [], ["aa"]);

        Assert.Equal(0, report.HeldOutCoverage);
    }

    [Fact]
    public void Measure_ReportsSuspiciousEntries()
    {
        var vocabulary = BuildVocabulary("aa aa");
        var stream = TestStreamFactory.FromSingleSegment("aa aa");

        var report = new VocabularyCoverageMeter().Measure(vocabulary, vocabulary, stream, [], ["aa"]);

        Assert.Equal(0, report.SuspiciousEntries);
    }

    private static BookVocabulary BuildVocabulary(string text)
    {
        var stream = TestStreamFactory.FromSingleSegment(text);
        return new BookVocabularyBuilder(TurkishFrequencyList.FromLines([]), new BookVocabularyOptions(MinBookCount: 1))
            .Build(stream, [], new FakeMorphologyOracle(text.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
    }
}
