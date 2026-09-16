namespace EpubFixer.Core.Quality;

public interface IWordRecognizer
{
    bool IsRecognized(string normalizedWord);
}
