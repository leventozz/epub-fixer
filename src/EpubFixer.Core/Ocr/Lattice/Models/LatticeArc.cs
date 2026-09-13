namespace EpubFixer.Core.Ocr.Lattice.Models;

public enum LatticeArcKind
{
    Word,
    Identity,
    Literal
}

public sealed record LatticeArc(int From, int To, string Word, double Cost, LatticeArcKind Kind);
