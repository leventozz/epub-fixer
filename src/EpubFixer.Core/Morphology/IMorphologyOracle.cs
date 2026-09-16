namespace EpubFixer.Core.Morphology;

/// <summary>Pre-resolved morphology answers. Never performs I/O.</summary>
public interface IMorphologyOracle
{
    /// <summary>Whether the word has at least one parse. Throws when the word was not pre-filled.</summary>
    bool IsValid(string word);

    /// <summary>Every parse for the word. Throws when the word was not pre-filled.</summary>
    IReadOnlyList<TurkishMorphologicalAnalysis> Analyze(string word);

    /// <summary>Whether the oracle holds an answer for the word. Never throws for a valid word.</summary>
    bool IsKnown(string word);
}
