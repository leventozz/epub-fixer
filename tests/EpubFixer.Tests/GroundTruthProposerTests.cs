using EpubFixer.Core.Epub;
using EpubFixer.QualityBenchmarks;
using EpubFixer.QualityBenchmarks.Models;

namespace EpubFixer.Tests;

public sealed class GroundTruthProposerTests
{
    [Fact]
    public void Proposer_IsDeterministic()
    {
        using var epub = CreateEpub("<p>koli ukta</p><p> ı ıç kendi-ıni :,ohbet</p>");
        var stream = new EpubPackageReader().Read(epub.Path).LogicalText;
        var proposer = new GroundTruthProposer();

        var first = proposer.Propose(stream);
        var second = proposer.Propose(stream);

        Assert.Equal(Project(first), Project(second));
    }

    [Fact]
    public void Proposer_ProducesSpansThatPassGroundTruthValidator()
    {
        using var epub = CreateEpub("<p>koli ukta</p><p> ı ıç kendi-ıni :,ohbet</p>");
        var stream = new EpubPackageReader().Read(epub.Path).LogicalText;
        var proposals = new GroundTruthProposer().Propose(stream);
        var groundTruth = new GroundTruthDocument(
            2,
            proposals.Select(proposal => new KnownErrorOccurrence(
                proposal.Id,
                proposal.DocumentPath,
                proposal.Original,
                "placeholder",
                proposal.SourceSpans)
            { ErrorClass = proposal.ProposedClass }).ToArray());

        new QualityBenchmarkGroundTruthValidator().Validate(groundTruth, stream);
    }

    [Fact]
    public void Proposer_ClassifiesKnownPatterns()
    {
        Assert.Contains(Propose("<p>ı ıç</p>"), item => item.ProposedClass == OcrErrorClass.Fragmentation);
        Assert.Contains(Propose("<p>kendi-ıni</p>"), item => item.ProposedClass == OcrErrorClass.GlyphConfusion);
        Assert.Contains(Propose("<p>:,ohbet</p>"), item => item.ProposedClass == OcrErrorClass.GarbageInsertion);
        Assert.Contains(Propose("<p>koli ukta</p>"), item => item.ProposedClass == OcrErrorClass.SpuriousSpace);
    }

    private static IReadOnlyList<GroundTruthProposal> Propose(string body)
    {
        using var epub = CreateEpub(body);
        var stream = new EpubPackageReader().Read(epub.Path).LogicalText;
        return new GroundTruthProposer().Propose(stream);
    }

    private static TemporaryEpub CreateEpub(string body) =>
        TemporaryEpub.Create(
            [new TestDocument("chapter", "chapter.xhtml", $"""
                <html xmlns="http://www.w3.org/1999/xhtml"><body>{body}</body></html>
                """)],
            [new TestSpineItem("chapter")]);

    private static IReadOnlyList<string> Project(IReadOnlyList<GroundTruthProposal> proposals) =>
        proposals
            .Select(proposal =>
                $"{proposal.Id}|{proposal.DocumentPath}|{proposal.Original}|{proposal.ProposedClass}|"
                + string.Join(",", proposal.SourceSpans.Select(span =>
                    $"{span.DocumentPath}:{span.TextNodeIndex}:{span.Start}:{span.Length}")))
            .ToArray();
}
