using EpubFixer.Core.Epub.Models;

namespace EpubFixer.Core.Epub;

/// <summary>
/// The logical offsets a lattice-style engine must never build a word across:
/// paragraph and document boundaries. A <see cref="TextBoundaryKind.TextNode"/>
/// boundary is not hard - a lattice window may span two text nodes within the
/// same paragraph (D42). This is the single source of truth for that rule; both
/// the CLI's debug-lattice command and the production lattice planner call it.
/// </summary>
public static class LogicalTextStreamBoundaries
{
    public static IReadOnlyCollection<int> HardOffsets(LogicalTextStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        return stream.Boundaries
            .Where(boundary => boundary.Kind is not TextBoundaryKind.TextNode)
            .Select(boundary => stream.Segments[boundary.AfterSegmentIndex].LogicalStart)
            .Distinct()
            .OrderBy(offset => offset)
            .ToArray();
    }
}
