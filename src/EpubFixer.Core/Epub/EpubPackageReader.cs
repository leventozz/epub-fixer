using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using EpubFixer.Core.Epub.Models;

namespace EpubFixer.Core.Epub;

public sealed class EpubPackageReader
{
    private const string ContainerPath = "META-INF/container.xml";
    private const string XhtmlMediaType = "application/xhtml+xml";
    private static readonly Regex XmlDeclarationPattern = new(
        @"^\uFEFF?\s*(<\?xml\s+[^?]*\?>)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public EpubPackage Read(string epubPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(epubPath);

        if (!File.Exists(epubPath))
        {
            throw new FileNotFoundException("The EPUB file was not found.", epubPath);
        }

        using var file = new FileStream(epubPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var archive = new ZipArchive(file, ZipArchiveMode.Read, leaveOpen: false);

        var container = ReadXml(GetRequiredEntry(archive, ContainerPath), ContainerPath);
        var packagePath = GetPackagePath(container);
        var packageDocument = ReadXml(GetRequiredEntry(archive, packagePath), packagePath);
        var version = GetRequiredAttribute(packageDocument.Root, "version", packagePath);
        var manifest = ReadManifest(packageDocument, packagePath);
        var spineDocuments = ReadSpineDocuments(archive, packageDocument, packagePath, manifest);
        var logicalText = LogicalTextStreamBuilder.Build(spineDocuments);

        return new EpubPackage(version, spineDocuments, logicalText);
    }

    private static string GetPackagePath(XDocument container)
    {
        var rootFiles = container
            .Descendants()
            .Where(element => element.Name.LocalName == "rootfile")
            .ToArray();

        var rootFile = rootFiles.FirstOrDefault(element =>
                string.Equals((string?)element.Attribute("media-type"), "application/oebps-package+xml", StringComparison.OrdinalIgnoreCase))
            ?? rootFiles.FirstOrDefault()
            ?? throw new InvalidDataException($"'{ContainerPath}' does not contain a rootfile entry.");

        var fullPath = (string?)rootFile.Attribute("full-path");

        if (string.IsNullOrWhiteSpace(fullPath))
        {
            throw new InvalidDataException($"The rootfile in '{ContainerPath}' has no full-path.");
        }

        return NormalizeArchivePath(fullPath);
    }

    private static IReadOnlyDictionary<string, ManifestItem> ReadManifest(XDocument packageDocument, string packagePath)
    {
        var manifestElement = packageDocument.Root?
            .Elements()
            .FirstOrDefault(element => element.Name.LocalName == "manifest")
            ?? throw new InvalidDataException($"Package document '{packagePath}' has no manifest.");

        var manifest = new Dictionary<string, ManifestItem>(StringComparer.Ordinal);

        foreach (var itemElement in manifestElement.Elements().Where(element => element.Name.LocalName == "item"))
        {
            var id = GetRequiredAttribute(itemElement, "id", packagePath);
            var href = GetRequiredAttribute(itemElement, "href", packagePath);
            var mediaType = GetRequiredAttribute(itemElement, "media-type", packagePath);

            if (!manifest.TryAdd(id, new ManifestItem(href, mediaType)))
            {
                throw new InvalidDataException($"Package document '{packagePath}' contains duplicate manifest id '{id}'.");
            }
        }

        return manifest;
    }

    private static IReadOnlyList<EpubContentDocument> ReadSpineDocuments(
        ZipArchive archive,
        XDocument packageDocument,
        string packagePath,
        IReadOnlyDictionary<string, ManifestItem> manifest)
    {
        var spineElement = packageDocument.Root?
            .Elements()
            .FirstOrDefault(element => element.Name.LocalName == "spine")
            ?? throw new InvalidDataException($"Package document '{packagePath}' has no spine.");

        var parser = new HtmlParser(new HtmlParserOptions { IsScripting = false });
        var documents = new List<EpubContentDocument>();

        foreach (var itemReference in spineElement.Elements().Where(element => element.Name.LocalName == "itemref"))
        {
            var idReference = GetRequiredAttribute(itemReference, "idref", packagePath);

            if (!manifest.TryGetValue(idReference, out var manifestItem))
            {
                throw new InvalidDataException($"Spine item '{idReference}' in '{packagePath}' is missing from the manifest.");
            }

            if (!string.Equals(manifestItem.MediaType, XhtmlMediaType, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Spine item '{idReference}' uses unsupported media type '{manifestItem.MediaType}'.");
            }

            var documentPath = ResolveArchivePath(packagePath, manifestItem.Href);
            var entry = GetRequiredEntry(archive, documentPath);

            using var entryStream = entry.Open();
            using var reader = new StreamReader(entryStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var source = reader.ReadToEnd();
            var document = parser.ParseDocument(source);
            var isLinear = !string.Equals((string?)itemReference.Attribute("linear"), "no", StringComparison.OrdinalIgnoreCase);

            documents.Add(new EpubContentDocument(
                documentPath,
                documents.Count,
                isLinear,
                document,
                GetXmlDeclaration(source)));
        }

        return Array.AsReadOnly(documents.ToArray());
    }

    private static string? GetXmlDeclaration(string source)
    {
        var match = XmlDeclarationPattern.Match(source);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static XDocument ReadXml(ZipArchiveEntry entry, string path)
    {
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };

            using var stream = entry.Open();
            using var reader = XmlReader.Create(stream, settings);
            return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        }
        catch (XmlException exception)
        {
            throw new InvalidDataException($"EPUB XML document '{path}' is invalid.", exception);
        }
    }

    private static ZipArchiveEntry GetRequiredEntry(ZipArchive archive, string path)
    {
        return archive.GetEntry(path)
            ?? throw new InvalidDataException($"EPUB archive entry '{path}' was not found.");
    }

    private static string ResolveArchivePath(string packagePath, string href)
    {
        var fragmentIndex = href.IndexOfAny(['?', '#']);
        var hrefPath = fragmentIndex >= 0 ? href[..fragmentIndex] : href;

        if (Uri.TryCreate(hrefPath, UriKind.Absolute, out _))
        {
            throw new InvalidDataException($"Manifest href '{href}' must be relative to the package document.");
        }

        var packageDirectory = packagePath.Contains('/')
            ? packagePath[..(packagePath.LastIndexOf('/') + 1)]
            : string.Empty;

        return NormalizeArchivePath(packageDirectory + Uri.UnescapeDataString(hrefPath));
    }

    private static string NormalizeArchivePath(string path)
    {
        if (path.StartsWith('/') || path.StartsWith('\\'))
        {
            throw new InvalidDataException($"EPUB archive path '{path}' must be relative.");
        }

        var segments = new List<string>();

        foreach (var segment in path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (segments.Count == 0)
                {
                    throw new InvalidDataException($"EPUB archive path '{path}' escapes the archive root.");
                }

                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            segments.Add(segment);
        }

        if (segments.Count == 0)
        {
            throw new InvalidDataException("EPUB archive path is empty.");
        }

        return string.Join('/', segments);
    }

    private static string GetRequiredAttribute(XElement? element, string attributeName, string documentPath)
    {
        var value = (string?)element?.Attribute(attributeName);

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"Element in '{documentPath}' is missing required attribute '{attributeName}'.");
        }

        return value;
    }

    private sealed record ManifestItem(string Href, string MediaType);
}
