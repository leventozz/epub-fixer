using EpubFixer.Core.Ocr.Lattice.Models;

namespace EpubFixer.Core.Ocr.Lattice;

public interface ILatticeDecoder
{
    IReadOnlyList<DecodedPath> Decode(WordLattice lattice, int kBest = 3);
}
