using EpubFixer.Core.Ocr.Lattice.Models;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Core.Ocr.Lattice;

public interface IWordLatticeBuilder
{
    WordLattice Build(CorruptedTextRegion region, string fullText, LatticeOptions options);
}
