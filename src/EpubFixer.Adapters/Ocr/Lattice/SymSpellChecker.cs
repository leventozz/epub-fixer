namespace EpubFixer.Adapters.Ocr.Lattice;

/// <summary>
/// Thin wrapper around the SymSpell package so consumers depend on this type name
/// rather than the third-party package directly. Shared by <see cref="SymSpellLexiconMatcher"/>
/// and (via project reference) EpubFixer.Cli's SymSpellRegionReconstructor.
/// </summary>
public sealed class SymSpellChecker : SymSpell
{
    public SymSpellChecker(int initialCapacity, int maxEditDistanceDictionary)
        : base(initialCapacity, maxEditDistanceDictionary)
    {
    }
}
