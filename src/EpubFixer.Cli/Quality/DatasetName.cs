namespace EpubFixer.Cli.Quality;

/// <summary>
/// Names the book a report or baseline was measured on, derived from the input path.
/// B1-a: both report writers used to stamp the literal "odun-kesmek" regardless of the book they
/// were handed, so running them on a second book produced a file that named the wrong source.
/// Every downstream decision cites these files, so a mislabelled one poisons the record silently.
/// </summary>
public static class DatasetName
{
    private const string DatasetEntryPointStem = "input";

    /// <summary>
    /// A dataset directory holds its book as <c>input.epub</c> (the convention
    /// <c>QualityBenchmarkDatasetLoader</c> enforces), so there the directory carries the name.
    /// Any other file is named by its own stem.
    /// </summary>
    public static string From(string inputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);

        var stem = Path.GetFileNameWithoutExtension(inputPath);
        if (!string.Equals(stem, DatasetEntryPointStem, StringComparison.OrdinalIgnoreCase))
        {
            return stem;
        }

        var directory = Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(inputPath)));
        return string.IsNullOrEmpty(directory) ? stem : directory;
    }
}
