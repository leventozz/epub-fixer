using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Mutation.Models;

namespace EpubFixer.Core.Ocr;

/// <summary>
/// Merges a primary and a secondary <see cref="IOcrCorrectionPlanner"/> at the plan level (D82):
/// both planners see the same <see cref="LogicalTextStream"/>, and their mutations are combined
/// by logical-range geometry rather than by reconciling their decision models.
/// </summary>
public sealed class CompositeOcrCorrectionPlanner : IOcrCorrectionPlanner
{
    private readonly IOcrCorrectionPlanner primary;
    private readonly IOcrCorrectionPlanner secondary;

    public CompositeOcrCorrectionPlanner(IOcrCorrectionPlanner primary, IOcrCorrectionPlanner secondary)
    {
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(secondary);

        this.primary = primary;
        this.secondary = secondary;
    }

    public OcrCorrectionPlanResult CreatePlan(LogicalTextStream stream, IMorphologyOracleBuilder oracleBuilder)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(oracleBuilder);

        var primaryResult = primary.CreatePlan(stream, oracleBuilder);
        var secondaryResult = secondary.CreatePlan(stream, oracleBuilder);

        var diagnostics = new List<string>(primaryResult.Diagnostics.Count + secondaryResult.Diagnostics.Count);
        diagnostics.AddRange(primaryResult.Diagnostics);
        diagnostics.AddRange(secondaryResult.Diagnostics);

        var primaryMutations = primaryResult.Plan.Mutations;
        var mutations = new List<OcrCorrectionMutation>(primaryMutations);

        foreach (var candidate in secondaryResult.Plan.Mutations)
        {
            if (OverlapsAny(candidate, primaryMutations))
            {
                diagnostics.Add(
                    $"Composite: skipped secondary mutation at '{candidate.DocumentPath}' " +
                    $"[{candidate.LogicalStart}, {candidate.LogicalStart + candidate.LogicalLength}) " +
                    "- overlaps a primary mutation (D83: primary wins).");
                continue;
            }

            mutations.Add(candidate);
        }

        mutations.Sort((left, right) =>
        {
            var pathComparison = string.CompareOrdinal(left.DocumentPath, right.DocumentPath);
            return pathComparison != 0 ? pathComparison : left.LogicalStart.CompareTo(right.LogicalStart);
        });

        // D88: book-level validity rides on the primary plan alone. A secondary-only failure is
        // recorded for visibility (D56) but must not veto a book the primary planner fixes cleanly.
        foreach (var failure in secondaryResult.Plan.Failures)
        {
            diagnostics.Add($"Composite: secondary planner reported a failure (not propagated): {failure.Message}");
        }

        var plan = new OcrCorrectionMutationPlan(primaryResult.Plan.SnapshotText, mutations, primaryResult.Plan.Failures);
        return new OcrCorrectionPlanResult(plan, OcrCorrectionEngine.Hybrid, diagnostics);
    }

    private static bool OverlapsAny(OcrCorrectionMutation candidate, IReadOnlyList<OcrCorrectionMutation> existing)
    {
        foreach (var mutation in existing)
        {
            if (string.Equals(mutation.DocumentPath, candidate.DocumentPath, StringComparison.Ordinal) &&
                Overlaps(mutation, candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Overlaps(OcrCorrectionMutation a, OcrCorrectionMutation b)
    {
        var aEnd = a.LogicalStart + a.LogicalLength;
        var bEnd = b.LogicalStart + b.LogicalLength;
        return a.LogicalStart < bEnd && b.LogicalStart < aEnd;
    }
}
