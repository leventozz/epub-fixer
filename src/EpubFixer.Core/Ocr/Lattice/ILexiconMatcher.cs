namespace EpubFixer.Core.Ocr.Lattice;

public readonly record struct LexiconMatch(string Word, double Cost);

public interface ILexiconMatcher
{
    IReadOnlyList<LexiconMatch> Match(ReadOnlySpan<char> span, double budget);
}
