using System.Text;
using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Lexicon.Models;
using EpubFixer.Core.Tokenization;

namespace EpubFixer.Core.Lexicon;

public sealed class BookLexiconBuilder
{
    public BookLexicon Build(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var start = -1;
        for (var index = 0; index <= text.Length; index++)
        {
            var isLetter = index < text.Length && Rune.IsLetter(Rune.GetRuneAt(text, index));
            if (isLetter)
            {
                start = start < 0 ? index : start;
                continue;
            }

            if (index < text.Length && text[index] is '\'' or '’'
                && start >= 0 && index + 1 < text.Length
                && Rune.IsLetter(Rune.GetRuneAt(text, index + 1)))
            {
                continue;
            }

            if (start >= 0)
            {
                var token = text[start..index];
                counts[token] = counts.GetValueOrDefault(token) + 1;
                start = -1;
            }
        }

        return new BookLexicon(counts);
    }

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
