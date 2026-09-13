using EpubFixer.Core.Ocr.Lattice;

namespace EpubFixer.Tests;

public sealed class LexiconQueryPlanTests
{
    private readonly LexiconQueryPlan plan = new();

    [Fact]
    public void QueryPlan_StripsSpacesAndGarbage()
    {
        Assert.Contains("ohbet", plan.CreateTierOne(":,ohbet"));
    }

    [Fact]
    public void QueryPlan_TierTwoOnlyOnEmptyTierOne()
    {
        Assert.Equal(4, plan.CreateTierOne("yü-ıiimeye").Count);
    }

    [Fact]
    public void QueryPlan_IsBounded()
    {
        var span = new string('ı', 48);

        var total = plan.CreateTierOne(span).Count + plan.CreateTierTwo(span, 20).Count;

        Assert.True(total <= 24);
    }

    [Fact]
    public void QueryPlan_IsDeterministic()
    {
        var first = plan.CreateTierTwo("yü-ıiimeye", 20);
        var second = plan.CreateTierTwo("yü-ıiimeye", 20);

        Assert.Equal(first, second);
    }
}
