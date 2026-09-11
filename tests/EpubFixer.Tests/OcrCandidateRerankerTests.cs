using EpubFixer.Cli.OcrReconstruction;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Ocr;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Tests;

public sealed class OcrCandidateRerankerTests
{
    [Fact]
    public void RerankerPreservesCandidatesAndUsesContextEvidence()
    {
        var region = new CorruptedTextRegion("x", 0, 1, ["x"], "", "", []);
        var candidates = new[]
        {
            new ReconstructionCandidate("wrong", 1, 1, ReconstructionSource.NoisyChannel, ["base"]),
            new ReconstructionCandidate("right", .5, 2, ReconstructionSource.NoisyChannel, ["base"])
        };
        var context = new OcrContext(["left"], ["right"], "", "", new FakeLookup("right"));
        var result = new DeterministicOcrCandidateReranker().Rerank(region, candidates, context);
        Assert.Equal(["wrong", "right"], candidates.Select(x => x.Text));
        Assert.Equal("right", result[0].Text);
        Assert.Equal(2, result.Count);
        Assert.Contains("base", result[0].Evidence);
    }

    private sealed class FakeLookup(string winner) : IOcrBookContextLookup
    {
        public OcrBookContextMatch? FindBest(string candidate, IReadOnlyList<string> previous, IReadOnlyList<string> next) =>
            candidate == winner ? new(false, 2, 1, true, true, 1) : null;
    }
}
