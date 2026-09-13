using EpubFixer.Core.Ocr;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Tests;

public sealed class WeightedEditAlignerTests
{
    private readonly WeightedEditAligner aligner = new();

    [Fact]
    public void Align_IdenticalIsZero()
    {
        var alignment = Align("üç", "üç");

        Assert.Equal(0, alignment.Cost);
        Assert.All(alignment.Operations, operation => Assert.Equal(EditOperationKind.Keep, operation.Kind));
    }

    [Fact]
    public void Align_SpaceDeletionUsesModelValue()
    {
        var alignment = Align("ko li", "koli");

        Assert.Equal(0.30, alignment.Cost, 9);
    }

    [Fact]
    public void Align_KnownGlyphIsCheaperThanOrdinary()
    {
        Assert.Equal(0.25, Align("ıç", "üç").Cost, 9);
        Assert.Equal(1.00, Align("zç", "üç").Cost, 9);
    }

    [Fact]
    public void Align_CountsOrdinarySubstitutions()
    {
        var alignment = Align("zç", "üç");

        Assert.Equal(1, alignment.OrdinarySubstitutions);
    }

    [Fact]
    public void Align_CaseDifferenceIsFree()
    {
        Assert.Equal(0, Align("cebimde", "Cebimde").Cost);
    }

    [Fact]
    public void Align_GarbagePrefixIsCheap()
    {
        Assert.Equal(1.20, Align(":,ohbet", "sohbet").Cost, 9);
    }

    [Fact]
    public void Align_ReturnsFalseAboveBudget()
    {
        Assert.False(aligner.TryAlign("koltukta", "merhaba", 1.0, out _));
    }

    [Fact]
    public void Align_TargetFixtureCostsAreUnderBudget()
    {
        var expected = new (string Source, string Target, double Cost)[]
        {
            ("ı ıç", "üç", 1.55),
            ("kendi-ıni", "kendimi", 2.25),
            ("1 ı iç", "hiç", 2.60),
            (":,ohbet", "sohbet", 1.20),
            ("koli ukta", "koltukta", 1.30),
            ("ı ızellikle", "özellikle", 1.55),
            ("ı ılduğu", "olduğu", 1.55),
            ("Ce-lıimde", "Cebimde", 1.50),
            ("ge-^:cn", "geçen", 0.95),
            ("yü-ıiimeye", "yürümeye", 2.50)
        };

        foreach (var (source, target, cost) in expected)
        {
            var alignment = Align(source, target);
            Assert.True(Math.Abs(cost - alignment.Cost) < 1e-9, $"{source} -> {target}: expected {cost}, actual {alignment.Cost}");
            Assert.True(alignment.Cost <= 3.0, $"{source} -> {target} cost {alignment.Cost}");
        }
    }

    [Fact]
    public void Align_IsDeterministic()
    {
        var first = Align("ge-^:cn", "geçen").Operations;

        for (var i = 0; i < 100; i++)
        {
            var next = Align("ge-^:cn", "geçen").Operations;
            Assert.Equal(first, next);
        }
    }

    private EditAlignment Align(string source, string target)
    {
        Assert.True(aligner.TryAlign(source, target, 10, out var alignment));
        return alignment;
    }
}
