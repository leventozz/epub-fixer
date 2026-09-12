using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Ocr.Models;
using EpubFixer.Core.Tokenization;
using EpubFixer.Core.Tokenization.Models;

namespace EpubFixer.Core.Lexicon;

public sealed record CleanToken(string Normalized, string Surface, int LogicalStart, string DocumentPath, bool StartsSegment);

public static class CleanTokenSequence
{
    public static IReadOnlyList<CleanToken> Build(
        LogicalTextStream stream,
        IReadOnlyList<CorruptedTextRegion> regions)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(regions);

        var orderedRegions = regions.OrderBy(region => region.Start).ToArray();
        var tokens = new WordTokenizer().Tokenize(stream);
        var clean = new List<CleanToken>();
        var regionIndex = 0;
        var startsNewSegment = true;
        WordToken? previousAccepted = null;
        var hardBoundaries = stream.Boundaries
            .Where(boundary => boundary.Kind is not TextBoundaryKind.TextNode)
            .Select(boundary => new
            {
                Start = stream.Segments[boundary.BeforeSegmentIndex].LogicalStart
                    + stream.Segments[boundary.BeforeSegmentIndex].Length,
                End = stream.Segments[boundary.AfterSegmentIndex].LogicalStart
            })
            .OrderBy(boundary => boundary.Start)
            .ToArray();
        var boundaryIndex = 0;

        foreach (var token in tokens)
        {
            while (regionIndex < orderedRegions.Length
                && orderedRegions[regionIndex].EndExclusive <= token.LogicalStart)
            {
                regionIndex++;
            }

            var tokenEnd = token.LogicalStart + token.Length;
            var intersectsRegion = regionIndex < orderedRegions.Length
                && orderedRegions[regionIndex].Start < tokenEnd
                && orderedRegions[regionIndex].EndExclusive > token.LogicalStart;

            if (intersectsRegion)
            {
                startsNewSegment = true;
                previousAccepted = null;
                continue;
            }

            var previousEnd = previousAccepted?.LogicalStart + previousAccepted?.Length;
            while (boundaryIndex < hardBoundaries.Length
                && previousEnd is not null
                && hardBoundaries[boundaryIndex].End < previousEnd)
            {
                boundaryIndex++;
            }

            if (previousEnd is not null
                && boundaryIndex < hardBoundaries.Length
                && hardBoundaries[boundaryIndex].Start >= previousEnd
                && hardBoundaries[boundaryIndex].End <= token.LogicalStart)
            {
                startsNewSegment = true;
            }

            clean.Add(new CleanToken(
                TurkishWordNormalizer.Normalize(token.Text),
                token.Text.Normalize(),
                token.LogicalStart,
                token.Sources[0].DocumentPath,
                startsNewSegment));
            startsNewSegment = false;
            previousAccepted = token;
        }

        return Array.AsReadOnly(clean.ToArray());
    }
}
