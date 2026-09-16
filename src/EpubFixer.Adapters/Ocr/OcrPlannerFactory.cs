using EpubFixer.Core.Ocr;

namespace EpubFixer.Adapters.Ocr;

/// <summary>
/// The single place that maps an <c>--ocr-engine</c> name to an <see cref="IOcrCorrectionPlanner"/>.
/// Both composition roots - EpubFixer.Cli and EpubFixer.QualityBenchmarks - call this instead of
/// each keeping its own name list and switch (H2: a third engine name must only be taught here).
/// </summary>
public static class OcrPlannerFactory
{
    public static readonly IReadOnlyList<string> KnownEngineNames = ["legacy", "lattice", "hybrid"];

    public static bool IsKnownEngine(string engine)
    {
        foreach (var known in KnownEngineNames)
        {
            if (string.Equals(known, engine, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Resolves <paramref name="engine"/> to the planner the composition root should use.
    /// "legacy" maps to <see langword="null"/> so callers fall back to their own default
    /// (<c>LegacyOcrCorrectionPlanner</c>), matching behavior from before this flag existed.
    /// An unrecognized name throws rather than silently falling back to legacy.
    /// </summary>
    public static IOcrCorrectionPlanner? Resolve(string engine)
    {
        if (string.Equals(engine, "legacy", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.Equals(engine, "lattice", StringComparison.OrdinalIgnoreCase))
        {
            return LatticeOcrPlannerFactory.Create();
        }

        if (string.Equals(engine, "hybrid", StringComparison.OrdinalIgnoreCase))
        {
            return new CompositeOcrCorrectionPlanner(new LegacyOcrCorrectionPlanner(), LatticeOcrPlannerFactory.Create());
        }

        throw new ArgumentException(
            $"Unknown OCR engine '{engine}'. Expected one of: {string.Join(", ", KnownEngineNames)}.",
            nameof(engine));
    }
}
