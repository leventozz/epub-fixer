namespace EpubFixer.Core.Morphology;

public interface ITurkishMorphologicalParser
{
    IReadOnlyList<TurkishMorphologicalAnalysis> Analyze(string word);
}

public sealed record TurkishMorphologicalAnalysis(
    string Root,
    IReadOnlySet<string> Tags);
