namespace EpubFixer.Core.Ocr.Lattice.Models;

public sealed record DecodedPath(string Text, double Cost, IReadOnlyList<LatticeArc> Arcs);
