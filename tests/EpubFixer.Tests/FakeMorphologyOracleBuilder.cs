using EpubFixer.Core.Morphology;

namespace EpubFixer.Tests;

internal sealed class FakeMorphologyOracleBuilder : IMorphologyOracleBuilder
{
    private readonly FakeMorphologyOracle oracle;

    public FakeMorphologyOracleBuilder(IEnumerable<string>? validWords = null)
    {
        oracle = new FakeMorphologyOracle(validWords);
    }

    public List<IReadOnlyList<string>> Builds { get; } = [];
    public FakeMorphologyOracle Oracle => oracle;

    public IMorphologyOracle Build(IEnumerable<string> vocabulary)
    {
        Builds.Add(vocabulary.ToArray());
        return oracle;
    }
}
