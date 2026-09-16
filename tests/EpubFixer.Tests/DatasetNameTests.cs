using EpubFixer.Cli.Quality;

namespace EpubFixer.Tests;

/// <summary>
/// B1-a: report output used to stamp the literal string "odun-kesmek" into every baseline it
/// produced, whatever book was actually measured. A baseline that lies about its own provenance
/// is worse than a missing one - every later decision cites it. The name now comes from the input.
/// </summary>
public sealed class DatasetNameTests
{
    [Theory]
    // The dataset convention the benchmark loader enforces: a named directory holding input.epub.
    [InlineData("test-data/odun-kesmek/input.epub", "odun-kesmek")]
    [InlineData("/abs/path/second-book/input.epub", "second-book")]
    [InlineData("test-data/odun-kesmek/INPUT.EPUB", "odun-kesmek")]
    // Anything else is named by its own file stem.
    [InlineData("books/Odun Kesmek_recognized.epub", "Odun Kesmek_recognized")]
    [InlineData("book.epub", "book")]
    public void From_DerivesNameFromPath(string path, string expected) =>
        Assert.Equal(expected, DatasetName.From(path));

    [Fact]
    public void From_RejectsEmptyPath() =>
        Assert.Throws<ArgumentException>(() => DatasetName.From(" "));
}
