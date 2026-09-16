using EpubFixer.Core.Epub.Models;

namespace EpubFixer.Core.Quality;

public interface IBookHealthMeter
{
    BookHealth Measure(LogicalTextStream stream);
}
