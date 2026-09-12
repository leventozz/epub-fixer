using AngleSharp.Html.Parser;
using EpubFixer.Core.Epub.Models;

namespace EpubFixer.Tests;

internal static class TestStreamFactory
{
    public static LogicalTextStream FromSingleSegment(string text)
    {
        var parser = new HtmlParser();
        var document = parser.ParseDocument("<html><body><p></p></body></html>");
        var paragraph = document.QuerySelector("p")!;
        var node = document.CreateTextNode(text);
        paragraph.AppendChild(node);
        var source = new TextSourceLocation("test.xhtml", 0, node, 0, text.Length);
        var segment = new TextSegment(text, 0, source);
        return (LogicalTextStream)Activator.CreateInstance(
            typeof(LogicalTextStream),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            binder: null,
            args: [Array.AsReadOnly(new[] { segment }), Array.AsReadOnly(Array.Empty<TextBoundary>()), text],
            culture: null)!;
    }
}
