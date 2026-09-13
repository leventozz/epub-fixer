using EpubFixer.Core.Ocr.Lattice.Models;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Core.Ocr.Lattice;

public interface ICorrectionAcceptanceGate
{
    AcceptanceResult Evaluate(CorruptedTextRegion region, WordLattice lattice, IReadOnlyList<DecodedPath> paths);
}
