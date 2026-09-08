using System.Reflection;

namespace EpubFixer.Tests;

public sealed class InfrastructureTests
{
    [Fact]
    public void CoreAssembly_IsAvailable()
    {
        var assembly = Assembly.Load("EpubFixer.Core");

        Assert.Equal("EpubFixer.Core", assembly.GetName().Name);
    }
}
