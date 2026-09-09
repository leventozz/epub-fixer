using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using AngleSharp.Xhtml;
using EpubFixer.Core.Epub.Models;

namespace EpubFixer.Core.Epub;

public sealed class EpubPackageWriter
{
    private const string MimetypePath = "mimetype";
    private static readonly UTF8Encoding Utf8WithoutBom = new(false, true);
    private static readonly Regex EncodingAttributePattern = new(
        @"\bencoding\s*=\s*(['""])[^'""]*\1",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public EpubWriteResult Write(
        string sourceEpubPath,
        string outputEpubPath,
        EpubPackage package)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEpubPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputEpubPath);
        ArgumentNullException.ThrowIfNull(package);

        var documentsByPath = package.SpineDocuments
            .GroupBy(document => document.Path, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var modifiedDocuments = documentsByPath.Values
            .Where(document => !string.Equals(
                document.OriginalStructureFingerprint,
                DomStructure.GetFingerprint(document.Document),
                StringComparison.Ordinal))
            .ToDictionary(document => document.Path, StringComparer.Ordinal);
        var serializedDocuments = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        foreach (var document in modifiedDocuments.Values)
        {
            serializedDocuments.Add(document.Path, SerializeAndValidate(document));
        }

        var diagnostics = new List<EpubDocumentWriteDiagnostic>();

        using var inputFile = new FileStream(
            sourceEpubPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        using var inputArchive = new ZipArchive(inputFile, ZipArchiveMode.Read, leaveOpen: false);

        EnsureUniqueEntryNames(inputArchive);

        using var outputFile = new FileStream(
            outputEpubPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        using var outputArchive = new ZipArchive(outputFile, ZipArchiveMode.Create, leaveOpen: false);

        var mimetype = inputArchive.Entries.FirstOrDefault(entry =>
            string.Equals(entry.FullName, MimetypePath, StringComparison.Ordinal));
        IEnumerable<ZipArchiveEntry> orderedEntries = mimetype is null
            ? inputArchive.Entries
            : new[] { mimetype }
                .Concat(inputArchive.Entries.Where(entry => !ReferenceEquals(entry, mimetype)))
                .ToArray();

        foreach (var inputEntry in orderedEntries)
        {
            var compressionLevel = ReferenceEquals(inputEntry, mimetype)
                ? CompressionLevel.NoCompression
                : CompressionLevel.Optimal;
            var outputEntry = outputArchive.CreateEntry(inputEntry.FullName, compressionLevel);
            outputEntry.LastWriteTime = inputEntry.LastWriteTime;
            outputEntry.ExternalAttributes = inputEntry.ExternalAttributes;

            using var outputStream = outputEntry.Open();

            if (serializedDocuments.TryGetValue(inputEntry.FullName, out var serialized))
            {
                outputStream.Write(serialized);
                diagnostics.Add(new EpubDocumentWriteDiagnostic(
                    inputEntry.FullName,
                    inputEntry.Length,
                    serialized.LongLength));
            }
            else
            {
                using var inputStream = inputEntry.Open();
                inputStream.CopyTo(outputStream);
            }
        }

        foreach (var modifiedPath in modifiedDocuments.Keys)
        {
            if (inputArchive.GetEntry(modifiedPath) is null)
            {
                throw new InvalidDataException(
                    $"Mutated XHTML document '{modifiedPath}' is missing from the source archive.");
            }
        }

        return new EpubWriteResult(
            modifiedDocuments.Keys.OrderBy(path => path, StringComparer.Ordinal).ToArray(),
            diagnostics.OrderBy(item => item.Path, StringComparer.Ordinal).ToArray());
    }

    private static byte[] SerializeAndValidate(EpubContentDocument document)
    {
        // HTML parsing does not honor XML self-closing syntax for non-void HTML
        // elements (for example, <title />). Keep explicit end tags so that the
        // serialized XHTML round-trips through the same parser without swallowing
        // following markup.
        var formatter = new XhtmlMarkupFormatter(false);
        var writer = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
        var declarationComment = document.XmlDeclaration is null
            ? null
            : document.Document.ChildNodes
                .OfType<IComment>()
                .FirstOrDefault(comment =>
                    comment.Data.Trim().StartsWith("?xml", StringComparison.OrdinalIgnoreCase));

        if (document.XmlDeclaration is not null)
        {
            writer.Write(NormalizeXmlDeclaration(document.XmlDeclaration));
            writer.Write('\n');
        }

        foreach (var child in document.Document.ChildNodes)
        {
            if (ReferenceEquals(child, declarationComment))
            {
                continue;
            }

            child.ToHtml(writer, formatter);
        }

        var serialized = writer.ToString();
        ValidateWellFormedXml(document.Path, serialized);

        var reparsed = new HtmlParser(new HtmlParserOptions { IsScripting = false })
            .ParseDocument(serialized);

        if (DomStructure.TryFindDifference(document.Document, reparsed, out var difference))
        {
            throw new InvalidDataException(
                $"XHTML serialization changed the DOM structure for '{document.Path}': {difference}");
        }

        return Utf8WithoutBom.GetBytes(serialized);
    }

    private static void ValidateWellFormedXml(string path, string serialized)
    {
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                XmlResolver = null
            };

            using var textReader = new StringReader(serialized);
            using var xmlReader = XmlReader.Create(textReader, settings);

            while (xmlReader.Read())
            {
            }
        }
        catch (XmlException exception)
        {
            throw new InvalidDataException(
                $"Serialized XHTML document '{path}' is not well-formed XML.",
                exception);
        }
    }

    private static string NormalizeXmlDeclaration(string declaration)
    {
        if (EncodingAttributePattern.IsMatch(declaration))
        {
            return EncodingAttributePattern.Replace(declaration, "encoding=\"utf-8\"", 1);
        }

        var closingIndex = declaration.LastIndexOf("?>", StringComparison.Ordinal);
        return closingIndex < 0
            ? "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
            : declaration.Insert(closingIndex, " encoding=\"utf-8\"");
    }

    private static void EnsureUniqueEntryNames(ZipArchive archive)
    {
        var duplicate = archive.Entries
            .GroupBy(entry => entry.FullName, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new InvalidDataException(
                $"EPUB archive contains duplicate entry '{duplicate.Key}'.");
        }
    }
}
