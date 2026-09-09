using System.Security.Cryptography;
using System.Text;
using AngleSharp.Dom;

namespace EpubFixer.Core.Epub;

internal static class DomStructure
{
    public static string GetFingerprint(INode node)
    {
        var builder = new StringBuilder();
        AppendNode(builder, node);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    public static bool TryFindDifference(INode expected, INode actual, out string difference)
    {
        return TryFindDifference(expected, actual, "/", out difference);
    }

    private static bool TryFindDifference(
        INode expected,
        INode actual,
        string path,
        out string difference)
    {
        if (expected.NodeType != actual.NodeType
            || !string.Equals(expected.NodeName, actual.NodeName, StringComparison.Ordinal)
            || !string.Equals(NormalizeNodeValue(expected), NormalizeNodeValue(actual), StringComparison.Ordinal))
        {
            difference = $"{path}: node differs ('{expected.NodeName}' versus '{actual.NodeName}').";
            return true;
        }

        if (expected is IElement expectedElement && actual is IElement actualElement)
        {
            if (!string.Equals(expectedElement.NamespaceUri, actualElement.NamespaceUri, StringComparison.Ordinal)
                || !string.Equals(expectedElement.Prefix, actualElement.Prefix, StringComparison.Ordinal))
            {
                difference = $"{path}: namespace or prefix differs for '{expectedElement.LocalName}'.";
                return true;
            }

            var expectedAttributes = GetAttributes(expectedElement);
            var actualAttributes = GetAttributes(actualElement);

            if (!expectedAttributes.SequenceEqual(actualAttributes, StringComparer.Ordinal))
            {
                difference = $"{path}: attributes differ for '{expectedElement.LocalName}'.";
                return true;
            }
        }

        if (expected is IDocumentType expectedType && actual is IDocumentType actualType
            && (!string.Equals(expectedType.PublicIdentifier, actualType.PublicIdentifier, StringComparison.Ordinal)
                || !string.Equals(expectedType.SystemIdentifier, actualType.SystemIdentifier, StringComparison.Ordinal)))
        {
            difference = $"{path}: document type identifiers differ.";
            return true;
        }

        if (expected.ChildNodes.Length != actual.ChildNodes.Length)
        {
            difference = $"{path}: child count differs ({expected.ChildNodes.Length} versus {actual.ChildNodes.Length}).";
            return true;
        }

        for (var index = 0; index < expected.ChildNodes.Length; index++)
        {
            if (TryFindDifference(
                    expected.ChildNodes[index],
                    actual.ChildNodes[index],
                    $"{path}{expected.ChildNodes[index].NodeName}[{index}]/",
                    out difference))
            {
                return true;
            }
        }

        difference = string.Empty;
        return false;
    }

    private static void AppendNode(StringBuilder builder, INode node)
    {
        Append(builder, ((int)node.NodeType).ToString());
        Append(builder, node.NodeName);
        Append(builder, NormalizeNodeValue(node));

        if (node is IElement element)
        {
            Append(builder, element.NamespaceUri);
            Append(builder, element.Prefix);

            foreach (var attribute in GetAttributes(element))
            {
                Append(builder, attribute);
            }
        }

        if (node is IDocumentType documentType)
        {
            Append(builder, documentType.PublicIdentifier);
            Append(builder, documentType.SystemIdentifier);
        }

        Append(builder, node.ChildNodes.Length.ToString());

        foreach (var child in node.ChildNodes)
        {
            AppendNode(builder, child);
        }
    }

    private static string NormalizeNodeValue(INode node)
    {
        if (node is IComment comment
            && comment.Data.Trim().StartsWith("?xml", StringComparison.OrdinalIgnoreCase))
        {
            return "?xml declaration?";
        }

        return node.NodeValue ?? string.Empty;
    }

    private static string[] GetAttributes(IElement element)
    {
        return element.Attributes
            .Select(attribute => string.Join(
                "\u001f",
                attribute.NamespaceUri ?? string.Empty,
                attribute.Prefix ?? string.Empty,
                attribute.LocalName,
                attribute.Value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static void Append(StringBuilder builder, string? value)
    {
        value ??= string.Empty;
        builder.Append(value.Length).Append(':').Append(value);
    }
}
