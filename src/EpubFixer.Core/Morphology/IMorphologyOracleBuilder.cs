namespace EpubFixer.Core.Morphology;

/// <summary>Resolves a vocabulary into an oracle. Implemented at the adapter edge.</summary>
public interface IMorphologyOracleBuilder
{
    IMorphologyOracle Build(IEnumerable<string> vocabulary);
}
