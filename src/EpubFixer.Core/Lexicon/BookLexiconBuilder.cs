using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Lexicon.Models;
using EpubFixer.Core.Tokenization;

namespace EpubFixer.Core.Lexicon;

public sealed class BookLexiconBuilder
{
    public BookLexicon Build(LogicalTextStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var token in new WordTokenizer().Tokenize(stream))
        {
            counts[token.Text] = counts.GetValueOrDefault(token.Text) + 1;
        }

        return new BookLexicon(counts);
    }
}
