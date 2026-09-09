namespace EpubFixer.Core.Morphology;

/// <summary>Answers whether a surface form has at least one Turkish morphological parse.</summary>
public interface ITurkishMorphologyAnalyzer
{
    bool IsValidWord(string word);
}
