namespace EpubFixer.Tests;

public sealed class CoreLayeringTests
{
    [Fact]
    public void CoreHasNoNewFileSystemDependencies()
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine("Epub", "EpubPackageReader.cs"),
            Path.Combine("Epub", "EpubPackageWriter.cs"),
            Path.Combine("Fix", "EpubFixService.cs"),
            Path.Combine("Fix", "EpubOutputValidator.cs")
        };
        var core = FindRepositoryDirectory(Path.Combine("src", "EpubFixer.Core"));
        var offenders = Directory.EnumerateFiles(core, "*.cs", SearchOption.AllDirectories)
            .Where(path =>
            {
                var relative = Path.GetRelativePath(core, path);
                return !relative.StartsWith("obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    && !relative.StartsWith("bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            })
            .Where(path =>
            {
                var text = File.ReadAllText(path);
                return text.Contains("using System.IO", StringComparison.Ordinal)
                    || text.Contains("System.Diagnostics.Process", StringComparison.Ordinal)
                    || text.Contains("File.", StringComparison.Ordinal)
                    || text.Contains("Directory.", StringComparison.Ordinal)
                    || text.Contains("ZipArchive", StringComparison.Ordinal);
            })
            .Select(path => Path.GetRelativePath(core, path))
            .Where(path => !allowed.Contains(path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    private static string FindRepositoryDirectory(string relativePath)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException(relativePath);
    }
}
