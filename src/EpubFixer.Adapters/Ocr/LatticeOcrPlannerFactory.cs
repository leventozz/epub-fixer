using EpubFixer.Adapters.Lexicon;
using EpubFixer.Adapters.Ocr.Lattice;
using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Ocr;
using EpubFixer.Core.Ocr.Lattice;

namespace EpubFixer.Adapters.Ocr;

/// <summary>
/// The single place that wires the lattice engine's detail (SymSpell, the tr_50k
/// frequency list) into <see cref="LatticeOcrCorrectionPlanner"/>. Both composition
/// roots - EpubFixer.Cli and EpubFixer.QualityBenchmarks - call this instead of
/// repeating the setup (plan section 3.2: composition lives in one place).
/// </summary>
public static class LatticeOcrPlannerFactory
{
    public static IOcrCorrectionPlanner Create(string? frequencyListPath = null, LatticeOptions? options = null)
    {
        var frequencyList = FileTurkishFrequencyListSource.Load(frequencyListPath);
        return new LatticeOcrCorrectionPlanner(
            frequencyList,
            vocabulary => new SymSpellLexiconMatcher(vocabulary),
            options);
    }
}
