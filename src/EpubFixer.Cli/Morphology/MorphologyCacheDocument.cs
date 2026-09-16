using EpubFixer.Core.Morphology;

namespace EpubFixer.Cli.Morphology;

internal sealed class MorphologyCacheDocument
{
    public int SchemaVersion { get; init; } = 1;
    public List<MorphologyCacheEntry> Entries { get; init; } = [];
}

internal sealed class MorphologyCacheEntry
{
    public string Word { get; init; } = string.Empty;
    public List<MorphologyCacheAnalysis> Analyses { get; init; } = [];
}

internal sealed class MorphologyCacheAnalysis
{
    public string Root { get; init; } = string.Empty;
    public List<string> Tags { get; init; } = [];

    public TurkishMorphologicalAnalysis ToAnalysis() =>
        new(Root, Tags.ToHashSet(StringComparer.Ordinal));

    public static MorphologyCacheAnalysis FromAnalysis(TurkishMorphologicalAnalysis analysis) =>
        new()
        {
            Root = analysis.Root,
            Tags = analysis.Tags.OrderBy(tag => tag, StringComparer.Ordinal).ToList()
        };
}
