using System.Text;
using EpubFixer.Core.Epub;
using EpubFixer.Core.Epub.Models;

return Run(args);

static int Run(string[] arguments)
{
    if (!TryParseArguments(arguments, out var epubPath, out var dumpPath))
    {
        PrintUsage();
        return 1;
    }

    try
    {
        if (dumpPath is not null && PathsReferToSameFile(epubPath, dumpPath))
        {
            throw new ArgumentException("The dump path must be different from the source EPUB path.");
        }

        var package = new EpubPackageReader().Read(epubPath);

        PrintSummary(package);

        if (dumpPath is not null)
        {
            File.WriteAllText(dumpPath, package.LogicalText.CreateDebugText(), new UTF8Encoding(false));
            Console.WriteLine();
            Console.WriteLine($"Logical text written to: {dumpPath}");
        }

        return 0;
    }
    catch (Exception exception) when (exception is ArgumentException
        or IOException
        or InvalidDataException
        or UnauthorizedAccessException)
    {
        Console.Error.WriteLine($"Error: {exception.Message}");
        return 2;
    }
}

static bool TryParseArguments(string[] arguments, out string epubPath, out string? dumpPath)
{
    epubPath = string.Empty;
    dumpPath = null;

    if (arguments.Length < 2 || !string.Equals(arguments[0], "analyze", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    epubPath = arguments[1];

    if (string.IsNullOrWhiteSpace(epubPath))
    {
        return false;
    }

    if (arguments.Length == 2)
    {
        return true;
    }

    if (arguments.Length == 4 && string.Equals(arguments[2], "--dump-text", StringComparison.OrdinalIgnoreCase))
    {
        dumpPath = arguments[3];
        return !string.IsNullOrWhiteSpace(dumpPath);
    }

    return false;
}

static void PrintSummary(EpubPackage package)
{
    var logicalText = package.LogicalText;
    var linearDocumentCount = package.SpineDocuments.Count(document => document.IsLinear);

    Console.WriteLine("EPUB loaded successfully.");
    Console.WriteLine();
    Console.WriteLine("Package:");
    Console.WriteLine($"  Version: {package.Version}");
    Console.WriteLine();
    Console.WriteLine($"Spine documents: {package.SpineDocuments.Count} ({linearDocumentCount} linear)");
    Console.WriteLine($"Text segments: {logicalText.Segments.Count}");
    Console.WriteLine($"Characters: {logicalText.CharacterCount}");
    Console.WriteLine();
    Console.WriteLine("Boundaries:");
    Console.WriteLine($"  Text node: {CountBoundaries(logicalText, TextBoundaryKind.TextNode)}");
    Console.WriteLine($"  Paragraph: {CountBoundaries(logicalText, TextBoundaryKind.Paragraph)}");
    Console.WriteLine($"  Document: {CountBoundaries(logicalText, TextBoundaryKind.Document)}");
    Console.WriteLine();
    Console.WriteLine("Documents:");

    foreach (var document in package.SpineDocuments)
    {
        var suffix = document.IsLinear ? string.Empty : " [non-linear]";
        Console.WriteLine($"  {document.SpineIndex + 1}. {document.Path}{suffix}");
    }
}

static int CountBoundaries(LogicalTextStream stream, TextBoundaryKind kind)
{
    return stream.Boundaries.Count(boundary => boundary.Kind == kind);
}

static bool PathsReferToSameFile(string firstPath, string secondPath)
{
    var comparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    return string.Equals(Path.GetFullPath(firstPath), Path.GetFullPath(secondPath), comparison);
}

static void PrintUsage()
{
    Console.Error.WriteLine("Usage: epubfixer analyze <book.epub> [--dump-text <output.txt>]");
}
