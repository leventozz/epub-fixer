using System.Diagnostics;
using EpubFixer.Core.Ocr.Lattice.Models;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Core.Ocr.Lattice;

public sealed class LatticeRegionReconstructor(
    string fullText,
    IWordLatticeBuilder builder,
    ILatticeDecoder decoder,
    ICorrectionAcceptanceGate gate,
    LatticeOptions options) : IOcrRegionReconstructor
{
    public LatticeRunStatistics Statistics { get; private set; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    public IReadOnlyList<ReconstructionCandidate> Reconstruct(CorruptedTextRegion region, int maxCandidates = 5)
    {
        if (maxCandidates <= 0) throw new ArgumentOutOfRangeException(nameof(maxCandidates));
        var (lattice, paths, result, elapsed) = Run(region, maxCandidates);
        UpdateStatistics(lattice, result, elapsed);
        return paths
            .Take(maxCandidates)
            .Select((path, index) => new ReconstructionCandidate(
                path.Text,
                -path.Cost,
                index + 1,
                ReconstructionSource.Lattice,
                [string.Join("; ", result.Reasons), $"arcs={path.Arcs.Count}; cost={path.Cost:0.000}"]))
            .ToArray();
    }

    public AcceptanceResult Evaluate(CorruptedTextRegion region)
    {
        var (lattice, _, result, elapsed) = Run(region, 3);
        UpdateStatistics(lattice, result, elapsed);
        return result;
    }

    private (WordLattice Lattice, IReadOnlyList<DecodedPath> Paths, AcceptanceResult Result, TimeSpan Elapsed) Run(CorruptedTextRegion region, int kBest)
    {
        var watch = Stopwatch.StartNew();
        var lattice = builder.Build(region, fullText, options);
        var paths = decoder.Decode(lattice, kBest);
        var result = gate.Evaluate(region, lattice, paths);
        watch.Stop();
        return (lattice, paths, result, watch.Elapsed);
    }

    private void UpdateStatistics(WordLattice lattice, AcceptanceResult result, TimeSpan elapsed)
    {
        Statistics = Statistics with
        {
            Regions = Statistics.Regions + 1,
            Built = Statistics.Built + (lattice.Outcome == LatticeBuildOutcome.Built ? 1 : 0),
            SkippedTooLong = Statistics.SkippedTooLong + (lattice.Outcome == LatticeBuildOutcome.SkippedTooLong ? 1 : 0),
            BudgetExceeded = Statistics.BudgetExceeded + (lattice.Outcome == LatticeBuildOutcome.BudgetExceeded ? 1 : 0),
            Applied = Statistics.Applied + (result.Verdict == AcceptanceVerdict.Apply ? 1 : 0),
            Reviewed = Statistics.Reviewed + (result.Verdict == AcceptanceVerdict.Review ? 1 : 0),
            Left = Statistics.Left + (result.Verdict == AcceptanceVerdict.Leave ? 1 : 0),
            TotalArcs = Statistics.TotalArcs + lattice.Arcs.Count,
            TotalVisitedStates = Statistics.TotalVisitedStates + lattice.VisitedStates,
            TotalMilliseconds = Statistics.TotalMilliseconds + elapsed.TotalMilliseconds
        };
    }
}

public sealed record LatticeRunStatistics(
    int Regions,
    int Built,
    int SkippedTooLong,
    int BudgetExceeded,
    int Applied,
    int Reviewed,
    int Left,
    long TotalArcs,
    long TotalVisitedStates,
    double TotalMilliseconds);
