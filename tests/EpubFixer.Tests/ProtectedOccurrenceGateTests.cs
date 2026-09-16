using System.Text.Json;
using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Ocr.Lattice;
using EpubFixer.Core.Ocr.Lattice.Models;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Tests;

public sealed class ProtectedOccurrenceGateTests
{
    [Fact]
    public void ProtectedOccurrences_AreNeverApplied()
    {
        var originals = ProtectedOriginals();
        var validTokens = originals.SelectMany(Tokenize).Distinct(StringComparer.Ordinal).ToArray();
        var gate = new CorrectionAcceptanceGate(BuildVocabulary(validTokens), new LatticeOptions());

        foreach (var original in originals)
        {
            var replacement = original.Replace("-", string.Empty, StringComparison.Ordinal);
            var lattice = new WordLattice(original, 0, [new LatticeArc(0, original.Length, replacement, 1, LatticeArcKind.Word)], LatticeBuildOutcome.Built, 1);
            var result = gate.Evaluate(
                new CorruptedTextRegion(original, 0, original.Length, [original], string.Empty, string.Empty, []),
                lattice,
                [
                    new DecodedPath(replacement, 1, [lattice.Arcs[0]]),
                    new DecodedPath(original, 3, [])
                ]);

            Assert.NotEqual(AcceptanceVerdict.Apply, result.Verdict);
            Assert.Contains("OriginalTokenIsValid", result.Reasons);
        }
    }

    private static IReadOnlyList<string> ProtectedOriginals()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FindRepositoryFile(Path.Combine("test-data", "odun-kesmek", "ground-truth.json"))));
        return document.RootElement
            .GetProperty("protectedOccurrences")
            .EnumerateArray()
            .Select(item => item.GetProperty("original").GetString()!)
            .ToArray();
    }

    private static IEnumerable<string> Tokenize(string value)
    {
        var start = -1;
        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsLetterOrDigit(value[i]))
            {
                start = start < 0 ? i : start;
                continue;
            }

            if (start >= 0)
            {
                yield return value[start..i];
                start = -1;
            }
        }

        if (start >= 0)
        {
            yield return value[start..];
        }
    }

    private static BookVocabulary BuildVocabulary(IEnumerable<string> words)
    {
        var wordList = words.ToArray();
        var text = string.Join(' ', wordList.SelectMany(word => new[] { word, word }));
        return new BookVocabularyBuilder(
                TurkishFrequencyList.FromLines([]),
                new BookVocabularyOptions(MinBookCount: 1))
            .Build(TestStreamFactory.FromSingleSegment(text), Array.Empty<CorruptedTextRegion>(), new FakeMorphologyOracle(wordList));
    }

    private static string FindRepositoryFile(string relativePath)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(relativePath);
    }
}
