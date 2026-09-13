namespace EpubFixer.Core.Ocr.Lattice.Models;

public sealed record DecodedPath(string Text, double Cost, IReadOnlyList<LatticeArc> Arcs)
{
    public double EditCost { get; } = Arcs.Sum(arc => arc.Kind == LatticeArcKind.Word ? arc.Cost : 0);
}
