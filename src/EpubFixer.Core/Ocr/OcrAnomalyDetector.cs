using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Ocr.Models;
using EpubFixer.Core.Tokenization;

namespace EpubFixer.Core.Ocr;

public sealed class OcrAnomalyDetector
{
    private static readonly Regex EmbeddedDigitPattern = new(@"(?=[\p{L}\p{N}'’.-]*\p{N})[\p{L}\p{N}'’.-]*\p{L}[\p{L}\p{N}'’.-]*", RegexOptions.Compiled);
    private static readonly Regex SequencePattern = new(@"ıı|l1|-;|-^|;\.|<", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex EmptyParenthesesPattern = new(@"\(\)", RegexOptions.Compiled);

    public OcrAnalysisReport Analyze(LogicalTextStream stream, ITurkishMorphologyAnalyzer analyzer)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(analyzer);

        var lexicon = new BookLexiconBuilder().Build(stream);
        var wordTokens = new WordTokenizer().Tokenize(stream);
        var candidates = new Dictionary<(int Start, int Length), CandidateDraft>();
        var examined = new HashSet<(int, int)>();

        foreach (var token in wordTokens)
        {
            examined.Add((token.LogicalStart, token.Length));
            Add(candidates, stream, token.LogicalStart, token.Length, []);
        }

        foreach (Match match in EmbeddedDigitPattern.Matches(stream.Text))
        {
            if (match.Length > 0 && char.IsLetter(match.Value[0]) && IsValidSpan(stream, match.Index, match.Length))
            {
                examined.Add((match.Index, match.Length));
                Add(candidates, stream, match.Index, match.Length, [OcrDetectionReason.EmbeddedDigit]);
            }
        }

        foreach (Match match in SequencePattern.Matches(stream.Text))
        {
            var span = ExpandFragment(stream.Text, match.Index, match.Length);
            if (!IsValidSpan(stream, span.Start, span.Length)) continue;
            examined.Add((span.Start, span.Length));
            Add(candidates, stream, span.Start, span.Length, [OcrDetectionReason.SuspiciousCharacterSequence]);
        }

        foreach (Match match in EmptyParenthesesPattern.Matches(stream.Text))
        {
            if (!IsAdjacentToLetter(stream.Text, match.Index, match.Length)) continue;
            var span = ExpandEmptyParenthesesFragment(stream.Text, match.Index, match.Length);
            if (!IsValidSpan(stream, span.Start, span.Length)) continue;
            examined.Add((span.Start, span.Length));
            Add(candidates, stream, span.Start, span.Length,
                [OcrDetectionReason.SuspiciousCharacter, OcrDetectionReason.SuspiciousPunctuation,
                    OcrDetectionReason.SuspiciousCharacterSequence]);
        }

        foreach (var span in FindSuspiciousPunctuationSpans(stream))
        {
            examined.Add((span.Start, span.Length));
            Add(candidates, stream, span.Start, span.Length, span.Reasons);
        }

        foreach (var span in FindFragmentationSpans(stream, wordTokens))
        {
            examined.Add(span);
            Add(candidates, stream, span.Start, span.Length, [OcrDetectionReason.IsolatedLetterFragmentation]);
        }

        var structuralSpans = candidates.Values
            .Where(item => item.Reasons.Count > 0)
            .ToArray();
        foreach (var key in candidates.Keys.ToArray())
        {
            if (structuralSpans.Any(parent => parent.Start <= key.Start
                && parent.Start + parent.Length >= key.Start + key.Length
                && (parent.Start != key.Start || parent.Length != key.Length)))
                candidates.Remove(key);
        }

        var evidence = new List<OcrWordEvidence>();
        var rareSuppressed = new List<OcrWordEvidence>();
        var invalidTotal = 0;
        foreach (var draft in candidates.Values.OrderBy(item => item.Start))
        {
            var candidate = CreateCandidate(stream, draft.Start, draft.Length);
            var frequency = lexicon.GetCount(candidate.Text);
            var baseForm = lexicon.GetBaseForm(candidate.Text);
            var baseFormFrequency = lexicon.GetBaseFormCount(candidate.Text);
            var valid = IsMorphologyInput(candidate.Text) && analyzer.IsValidWord(candidate.Text);
            if (!valid) invalidTotal++;
            var reasons = draft.Reasons.ToHashSet();
            if (!valid) reasons.Add(OcrDetectionReason.MorphologyInvalid);
            var suppressRareInBook = frequency == 1 && baseFormFrequency > 1;
            if (frequency == 1 && !suppressRareInBook) reasons.Add(OcrDetectionReason.RareInBook);

            var structural = reasons.Any(reason => reason is not OcrDetectionReason.MorphologyInvalid and not OcrDetectionReason.RareInBook);
            if (!structural && !(reasons.Contains(OcrDetectionReason.MorphologyInvalid) && reasons.Contains(OcrDetectionReason.RareInBook)))
            {
                if (suppressRareInBook && reasons.Contains(OcrDetectionReason.MorphologyInvalid))
                    rareSuppressed.Add(new OcrWordEvidence(candidate, frequency, baseForm, baseFormFrequency,
                        valid, reasons.OrderBy(item => item).ToArray(), OcrConfidence.EvidenceOnly));
                continue;
            }

            var confidence = structural
                ? reasons.Any(reason => reason is OcrDetectionReason.SuspiciousCharacter or OcrDetectionReason.EmbeddedDigit)
                    ? OcrConfidence.High
                    : OcrConfidence.Medium
                : OcrConfidence.EvidenceOnly;
            evidence.Add(new OcrWordEvidence(candidate, frequency, baseForm, baseFormFrequency,
                valid, reasons.OrderBy(item => item).ToArray(), confidence));
        }

        var strongInvalid = evidence.Count(item => !item.TrMorphValid && item.Confidence == OcrConfidence.High);
        return new OcrAnalysisReport(examined.Count, invalidTotal, strongInvalid, evidence,
            rareSuppressed, lexicon.UniqueApostropheBaseForms);
    }

    private static bool IsMorphologyInput(string text) => text.EnumerateRunes().Any(Rune.IsLetter);

    private static void Add(Dictionary<(int Start, int Length), CandidateDraft> map, LogicalTextStream stream, int start, int length, IEnumerable<OcrDetectionReason> reasons)
    {
        if (!IsValidSpan(stream, start, length)) return;
        var key = (start, length);
        if (!map.TryGetValue(key, out var draft)) map[key] = draft = new CandidateDraft(start, length);
        draft.Reasons.UnionWith(reasons);
    }

    private static OcrWordCandidate CreateCandidate(LogicalTextStream stream, int start, int length)
    {
        var sources = new List<TextSourceLocation>();
        for (var index = start; index < start + length; index++)
        {
            var source = stream.GetSourceLocationAt(index);
            if (sources.Count > 0 && ReferenceEquals(sources[^1].SourceNode, source.SourceNode) && sources[^1].Start + sources[^1].Length == source.Start)
                sources[^1] = sources[^1] with { Length = sources[^1].Length + 1 };
            else sources.Add(source);
        }
        var left = Math.Max(0, start - 160);
        var right = Math.Min(stream.Text.Length, start + length + 160);
        var before = TakeRunes(stream.Text[left..start], 80, fromEnd: true);
        var after = TakeRunes(stream.Text[(start + length)..right], 80, fromEnd: false);
        return new OcrWordCandidate(stream.Text.Substring(start, length), start, sources, sources[0].DocumentPath, before, after);
    }

    private static string TakeRunes(string value, int count, bool fromEnd)
    {
        var runes = value.EnumerateRunes().ToArray();
        return fromEnd ? string.Concat(runes.TakeLast(count)) : string.Concat(runes.Take(count));
    }

    private static bool IsValidSpan(LogicalTextStream stream, int start, int length)
    {
        if (length <= 0 || start < 0 || start + length > stream.Text.Length) return false;
        return !stream.Boundaries.Any(boundary => boundary.Kind != TextBoundaryKind.TextNode &&
            stream.Segments[boundary.BeforeSegmentIndex].LogicalStart + stream.Segments[boundary.BeforeSegmentIndex].Length > start &&
            stream.Segments[boundary.AfterSegmentIndex].LogicalStart < start + length);
    }

    private static IEnumerable<(int Start, int Length, IReadOnlyList<OcrDetectionReason> Reasons)> FindSuspiciousPunctuationSpans(LogicalTextStream stream)
    {
        var text = stream.Text;
        var anchors = new[] { '<', '>', '^', ';' };
        for (var index = 0; index < text.Length; index++)
        {
            if (!anchors.Contains(text[index])) continue;
            var previous = index > 0 ? text[index - 1] : '\0';
            var next = index + 1 < text.Length ? text[index + 1] : '\0';
            if ((char.IsWhiteSpace(previous) || char.IsWhiteSpace(next))
                && !(IsPunctuation(previous) || IsPunctuation(next))) continue;
            var start = index;
            var end = index + 1;
            var spaces = 0;
            while (start > 0 && IsFragmentChar(text[start - 1])) { start--; }
            while (end < text.Length)
            {
                if (IsFragmentChar(text[end])) { end++; continue; }
                if (char.IsWhiteSpace(text[end]) && spaces++ == 0 && end + 1 < text.Length && IsPunctuation(text[end + 1])) { end++; continue; }
                break;
            }
            while (start < end && char.IsWhiteSpace(text[start])) start++;
            while (end > start && char.IsWhiteSpace(text[end - 1])) end--;
            if (end > start && IsValidSpan(stream, start, end - start))
                yield return (start, end - start, [OcrDetectionReason.SuspiciousCharacter, OcrDetectionReason.SuspiciousPunctuation,
                    OcrDetectionReason.SuspiciousCharacterSequence]);
        }
    }

    private static bool IsFragmentChar(char value) => char.IsLetterOrDigit(value) || value is '\'' or '’' or '-' or '.' or '<' or '>' or '^' or ';';
    private static bool IsPunctuation(char value) => value is '\'' or '’' or '-' or '.' or '<' or '>' or '^' or ';';

    private static (int Start, int Length) ExpandFragment(string text, int start, int length)
    {
        var end = start + length;
        while (start > 0 && IsFragmentChar(text[start - 1])) start--;
        while (end < text.Length && IsFragmentChar(text[end])) end++;
        return (start, end - start);
    }

    private static bool IsAdjacentToLetter(string text, int start, int length)
    {
        return TryDecodePreviousRune(text, start, out var previous, out _) && Rune.IsLetter(previous)
            || TryDecodeNextRune(text, start + length, out var next, out _) && Rune.IsLetter(next);
    }

    private static (int Start, int Length) ExpandEmptyParenthesesFragment(string text, int start, int length)
    {
        var end = start + length;
        while (TryDecodePreviousRune(text, start, out var previous, out var previousLength) && IsFragmentRune(previous))
            start -= previousLength;
        while (TryDecodeNextRune(text, end, out var next, out var nextLength) && IsFragmentRune(next))
            end += nextLength;
        return (start, end - start);
    }

    private static bool TryDecodePreviousRune(string text, int end, out Rune rune, out int length) =>
        Rune.DecodeLastFromUtf16(text.AsSpan(0, end), out rune, out length) == OperationStatus.Done;

    private static bool TryDecodeNextRune(string text, int start, out Rune rune, out int length) =>
        Rune.DecodeFromUtf16(text.AsSpan(start), out rune, out length) == OperationStatus.Done;

    private static bool IsFragmentRune(Rune value) =>
        Rune.IsLetterOrDigit(value) || value.Value is '\'' or '’' or '-' or '.' or '<' or '>' or '^' or ';';

    private static IEnumerable<(int Start, int Length)> FindFragmentationSpans(LogicalTextStream stream, IReadOnlyList<EpubFixer.Core.Tokenization.Models.WordToken> tokens)
    {
        for (var index = 0; index + 2 < tokens.Count; index++)
        {
            if (tokens[index].Length != 1 || tokens[index + 1].Length != 1 || tokens[index + 2].Length < 2) continue;
            var firstEnd = tokens[index].LogicalStart + tokens[index].Length;
            var gap1 = stream.Text[firstEnd..tokens[index + 1].LogicalStart];
            var gap2 = stream.Text[(tokens[index + 1].LogicalStart + tokens[index + 1].Length)..tokens[index + 2].LogicalStart];
            if (gap1.Any(char.IsWhiteSpace) && gap1.All(char.IsWhiteSpace) && gap2.Any(char.IsWhiteSpace) && gap2.All(char.IsWhiteSpace)
                && IsValidSpan(stream, tokens[index].LogicalStart, tokens[index + 2].LogicalStart + tokens[index + 2].Length - tokens[index].LogicalStart))
                yield return (tokens[index].LogicalStart, tokens[index + 2].LogicalStart + tokens[index + 2].Length - tokens[index].LogicalStart);
        }
    }

    private sealed class CandidateDraft(int start, int length)
    {
        public int Start { get; } = start;
        public int Length { get; } = length;
        public HashSet<OcrDetectionReason> Reasons { get; } = new();
    }
}
