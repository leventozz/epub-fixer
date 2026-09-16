using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Mutation.Models;

namespace EpubFixer.Core.Mutation;

/// <summary>
/// Converts a logical text range into source spans by walking
/// <see cref="LogicalTextStream.GetSourceLocationAt"/> character by character and
/// merging adjacent locations. Shared verbatim between <see cref="OcrCorrectionMutationPlanner"/>
/// (legacy, decision-based) and <see cref="RegionMutationPlanner"/> (region-based) so the two
/// mutation sources never drift on how a logical range maps to DOM text.
/// </summary>
internal static class RegionMutationGeometry
{
    public static IReadOnlyList<OcrMutationSourceSpan> BuildSourceSpans(
        LogicalTextStream stream,
        int logicalStart,
        int logicalEndExclusive)
    {
        var spans = new List<OcrMutationSourceSpan>();
        for (var index = logicalStart; index < logicalEndExclusive; index++)
        {
            var location = stream.GetSourceLocationAt(index);
            var expected = stream.Text[index].ToString();
            if (spans.Count > 0
                && spans[^1].DocumentPath == location.DocumentPath
                && spans[^1].TextNodeIndex == location.TextNodeIndex
                && spans[^1].Start + spans[^1].Length == location.Start)
            {
                spans[^1] = spans[^1] with { Length = spans[^1].Length + 1, ExpectedText = spans[^1].ExpectedText + expected };
            }
            else
            {
                spans.Add(new(location.DocumentPath, location.TextNodeIndex, location.Start, 1, expected));
            }
        }
        return spans;
    }
}
