using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Core.Ocr;

public interface IOcrRegionReconstructor
{
    IReadOnlyList<ReconstructionCandidate> Reconstruct(
        CorruptedTextRegion region,
        int maxCandidates = 5);
}
