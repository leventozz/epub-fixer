using System.Buffers;
using System.Text;
using EpubFixer.Core.Detection.Models;
using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Evidence.Models;
using EpubFixer.Core.Lexicon.Models;

namespace EpubFixer.Core.Evidence;

public sealed class HyphenationEvidenceEvaluator
{
    public IReadOnlyList<HyphenationEvidence> Evaluate(
        IReadOnlyList<HyphenationCandidate> candidates,
        BookLexicon lexicon,
        LogicalTextStream stream)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(lexicon);
        ArgumentNullException.ThrowIfNull(stream);

        var evidence = new HyphenationEvidence[candidates.Count];

        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            var unhyphenatedText = candidate.UnhyphenatedText;

            evidence[index] = new HyphenationEvidence(
                candidate,
                lexicon.GetCount(unhyphenatedText),
                lexicon.Contains(unhyphenatedText),
                CreateContext(candidate, stream));
        }

        return Array.AsReadOnly(evidence);
    }

    private static HyphenationContextEvidence CreateContext(
        HyphenationCandidate candidate,
        LogicalTextStream stream)
    {
        var candidateStart = GetLogicalIndex(stream, candidate.LeftSource);
        var candidateEnd = GetLogicalIndex(stream, candidate.RightSource)
            + candidate.RightSource.Length;
        var hasPreviousRune = TryDecodePreviousRune(stream.Text, candidateStart, out var previousRune);
        var hasNextRune = TryDecodeNextRune(stream.Text, candidateEnd, out var nextRune);

        return new HyphenationContextEvidence(
            hasPreviousRune ? previousRune.ToString() : null,
            hasNextRune ? nextRune.ToString() : null,
            hasPreviousRune && IsHyphen(previousRune)
                || hasNextRune && IsHyphen(nextRune),
            hasPreviousRune && IsSuspicious(previousRune)
                || hasNextRune && IsSuspicious(nextRune));
    }

    private static int GetLogicalIndex(LogicalTextStream stream, TextSourceLocation source)
    {
        var segment = stream.Segments.FirstOrDefault(item =>
            ReferenceEquals(item.Source.SourceNode, source.SourceNode));

        if (segment is null)
        {
            throw new InvalidOperationException(
                "Candidate source was not found in the logical text stream.");
        }

        return segment.LogicalStart + source.Start - segment.Source.Start;
    }

    private static bool TryDecodePreviousRune(string text, int endExclusive, out Rune rune)
    {
        if (endExclusive <= 0)
        {
            rune = default;
            return false;
        }

        return Rune.DecodeLastFromUtf16(
            text.AsSpan(0, endExclusive),
            out rune,
            out _) == OperationStatus.Done;
    }

    private static bool TryDecodeNextRune(string text, int start, out Rune rune)
    {
        if (start >= text.Length)
        {
            rune = default;
            return false;
        }

        return Rune.DecodeFromUtf16(text.AsSpan(start), out rune, out _) == OperationStatus.Done;
    }

    private static bool IsHyphen(Rune rune)
    {
        return rune.Value == '-';
    }

    private static bool IsSuspicious(Rune rune)
    {
        const int MiddleDot = 0x00B7;

        return rune.Value == MiddleDot
            || !Rune.IsLetter(rune)
            && !Rune.IsWhiteSpace(rune)
            && !Rune.IsPunctuation(rune);
    }
}
