namespace EpubFixer.Core.Ocr.Lattice.Models;

public enum LatticeBuildOutcome
{
    Built,
    SkippedTooLong,
    BudgetExceeded
}

public sealed record WordLattice(
    string Window,
    int WindowOffset,
    IReadOnlyList<LatticeArc> Arcs,
    LatticeBuildOutcome Outcome,
    int VisitedStates);
