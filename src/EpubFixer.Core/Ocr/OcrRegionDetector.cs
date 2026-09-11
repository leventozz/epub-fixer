using System.Text.RegularExpressions;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Core.Ocr;

public sealed class OcrRegionDetector
{
    private static readonly Regex FragmentPattern = new(@"[\p{L}\p{N}][\p{L}\p{N}'’.,;:^<>()!?-]*", RegexOptions.Compiled);
    private static readonly HashSet<char> Garbage = ['^', ';', '<', '>', ':'];
    private static readonly HashSet<char> BoundaryPunctuation = [',', '.', ':', ';', '!', '?'];
    private static readonly HashSet<char> OcrGlyphs = ['ı', 'i', 'l', '1'];

    public IReadOnlyList<CorruptedTextRegion> Detect(string text, ITurkishMorphologyAnalyzer analyzer)
    {
        ArgumentNullException.ThrowIfNull(text); ArgumentNullException.ThrowIfNull(analyzer);
        var fragments = FragmentPattern.Matches(text).Cast<Match>().Select(m => new Fragment(m.Index, m.Length, m.Value)).ToArray();
        var seeds = new List<Seed>();
        for (var i = 0; i < fragments.Length; i++)
        {
            var f = fragments[i]; var core = TrimBoundaryPunctuation(f.Value); var invalid = core.Length > 0 && !analyzer.IsValidWord(core);
            var reasons = new HashSet<OcrRegionDetectionReason>();
            if (IsIsolatedGlyph(core)) reasons.Add(OcrRegionDetectionReason.IsolatedOcrGlyph);
            if (HasEmbeddedGarbage(core)) { reasons.Add(OcrRegionDetectionReason.EmbeddedGarbageGlyph); reasons.Add(OcrRegionDetectionReason.SuspiciousPunctuation); }
            if (HasSuspiciousInternalPunctuation(core)) reasons.Add(OcrRegionDetectionReason.SuspiciousPunctuation);
            if (HasMalformedHyphen(core, invalid)) reasons.Add(OcrRegionDetectionReason.MalformedHyphenContinuation);
            if (HasGarbagePrefix(text, f.Index)) { reasons.Add(OcrRegionDetectionReason.EmbeddedGarbageGlyph); reasons.Add(OcrRegionDetectionReason.SuspiciousPunctuation); }
            var before = i == 0 ? text[..f.Index] : text[fragments[i - 1].End..f.Index];
            var after = i + 1 == fragments.Length ? text[f.End..] : text[f.End..fragments[i + 1].Index];
            if ((before.Any(c => c is '\r' or '\n') || after.Any(c => c is '\r' or '\n')) && (core.Length <= 2 || invalid)) reasons.Add(OcrRegionDetectionReason.SuspiciousLineBreak);
            if (reasons.Count > 0) { var (start, end) = IncludeGarbagePrefix(text, f.Index, f.End); seeds.Add(new Seed(i, i, start, end, reasons)); }
        }
        for (var i = 0; i + 1 < fragments.Length; i++)
        {
            var gap = text[fragments[i].End..fragments[i + 1].Index];
            if (gap.Any(char.IsWhiteSpace) && gap.All(c => c is ' ' or '\t') && IsSeamPair(fragments[i].Value, fragments[i + 1].Value))
                seeds.Add(new Seed(i, i + 1, fragments[i].Index, fragments[i + 1].End, [OcrRegionDetectionReason.FragmentedNeighbors]));
        }
        var spans = new List<Span>();
        foreach (var seed in seeds)
        {
            var first = seed.First; var last = seed.Last; var isolated = CountIsolated(first, last, fragments);
            var allowContinuation = seed.Reasons.Contains(OcrRegionDetectionReason.SuspiciousPunctuation) || seed.Reasons.Contains(OcrRegionDetectionReason.MalformedHyphenContinuation);
            while (first > 0 && CanExpand(first - 1, first, fragments, text, analyzer, isolated, false, allowContinuation)) { first--; if (IsIsolatedGlyph(TrimBoundaryPunctuation(fragments[first].Value))) isolated++; }
            while (last + 1 < fragments.Length && CanExpand(last, last + 1, fragments, text, analyzer, isolated, true, allowContinuation)) { last++; if (IsIsolatedGlyph(TrimBoundaryPunctuation(fragments[last].Value))) isolated++; }
            var start = Math.Min(seed.Start, fragments[first].Index); var end = Math.Max(seed.End, fragments[last].End);
            while (start > 0 && text[start - 1] is '(' or '[') start--;
            if (start >= 2 && char.IsWhiteSpace(text[start - 1]) && text[start - 2] is '(' or '[') start -= 2;
            spans.Add(new Span(start, end, seed.Reasons));
        }
        var regions = new List<CorruptedTextRegion>();
        foreach (var span in spans.OrderBy(s => s.Start).ThenByDescending(s => s.End))
        {
            if (regions.Count > 0 && regions[^1].EndExclusive >= span.Start)
            {
                var p = regions[^1]; var start = Math.Min(p.Start, span.Start); var end = Math.Max(p.EndExclusive, span.End);
                regions[^1] = CreateRegion(text, start, end, p.DetectionReasons.Concat(span.Reasons).Distinct().OrderBy(r => r).ToArray());
            }
            else regions.Add(CreateRegion(text, span.Start, span.End, span.Reasons));
        }
        return regions;
    }

    private static CorruptedTextRegion CreateRegion(string text, int start, int end, IEnumerable<OcrRegionDetectionReason> reasons)
    {
        while (end > start && BoundaryPunctuation.Contains(text[end - 1])) end--;
        var raw = text[start..end]; var fragments = FragmentPattern.Matches(raw).Cast<Match>().Select(m => m.Value).ToArray();
        return new CorruptedTextRegion(raw, start, end, fragments, text[Math.Max(0, start - 80)..start], text[end..Math.Min(text.Length, end + 80)], reasons.ToArray());
    }
    private static bool CanExpand(int left, int right, Fragment[] fragments, string text, ITurkishMorphologyAnalyzer analyzer, int isolated, bool rightward, bool allowContinuation)
    {
        var gap = text[fragments[left].End..fragments[right].Index]; if (gap.Any(c => c is '\r' or '\n')) return true;
        if (!gap.Any(char.IsWhiteSpace) || !gap.All(c => c is ' ' or '\t')) return false;
        var value = TrimBoundaryPunctuation(fragments[rightward ? right : left].Value);
        if (IsIsolatedGlyph(value) || HasEmbeddedGarbage(value) || HasSuspiciousInternalPunctuation(value)) return true;
        if (rightward && isolated > 0 && value.Length <= 10 && OcrGlyphs.Contains(value[0]) && !analyzer.IsValidWord(value)) return true;
        if (isolated > 0 && value.Length <= 3 && (value.Equals("iç", StringComparison.Ordinal) || value.Equals("iye", StringComparison.Ordinal))) return true;
        if (rightward && isolated > 0 && !analyzer.IsValidWord(value) && value.Length <= 6 && value.All(char.IsLower)) return true;
        return rightward && allowContinuation && !analyzer.IsValidWord(value) && value.Length <= 6 && value.All(char.IsLower);
    }
    private static int CountIsolated(int first, int last, Fragment[] fragments) => Enumerable.Range(first, last - first + 1).Count(i => IsIsolatedGlyph(TrimBoundaryPunctuation(fragments[i].Value)));
    private static bool IsSeamPair(string left, string right)
    {
        left = TrimBoundaryPunctuation(left); right = TrimBoundaryPunctuation(right);
        return left.Length == 4 && left.EndsWith("i", StringComparison.OrdinalIgnoreCase) && right.Length == 4 && right.StartsWith("u", StringComparison.OrdinalIgnoreCase);
    }
    private static bool HasMalformedHyphen(string value, bool invalid)
    {
        var hyphen = value.IndexOf('-'); if (hyphen <= 0 || hyphen >= value.Length - 1) return false;
        var right = value[(hyphen + 1)..].TrimStart('’', '\''); if (right.Length == 0) return false;
        var apostrophe = right.IndexOfAny(['\'', '’']);
        var suffixLength = apostrophe >= 0 ? apostrophe : right.Length;
        return OcrGlyphs.Contains(right[0]) || suffixLength <= 2 && apostrophe >= 0 || right.Length <= 4 && (char.IsUpper(value[0]) || char.IsUpper(value[hyphen + 1])) || right.Length <= 2 && value.Length >= 7 || right.Length <= 4 && invalid && right.All(char.IsLower);
    }
    private static bool HasEmbeddedGarbage(string value) => value.Any(Garbage.Contains) || value.Contains("--", StringComparison.Ordinal) || value.Contains("^:", StringComparison.Ordinal);
    private static bool HasSuspiciousInternalPunctuation(string value)
    {
        for (var i = 1; i < value.Length - 1; i++) if (value[i] is ',' && char.IsLetter(value[i - 1]) && char.IsLetter(value[i + 1]) || value[i] is ':' or ';' && (char.IsLetter(value[i - 1]) || char.IsLetter(value[i + 1]))) return true;
        return value.Count(c => c is ':' or ';' or '^' or '<' or '>') >= 2;
    }
    private static bool IsIsolatedGlyph(string value) => value.Length == 1 && OcrGlyphs.Contains(value[0]);
    private static string TrimBoundaryPunctuation(string value)
    {
        var start = 0; var end = value.Length; while (start < end && BoundaryPunctuation.Contains(value[start])) start++; while (end > start && BoundaryPunctuation.Contains(value[end - 1])) end--; return value[start..end];
    }
    private static (int Start, int End) IncludeGarbagePrefix(string text, int start, int end)
        { while (start > 0 && text[start - 1] is ':' or ',' or ';' or '<' or '>' or '^' or '(' or '-') start--; return (start, end); }
    private static bool HasGarbagePrefix(string text, int start) => start > 0 && text[start - 1] is ':' or ',' or ';' or '<' or '>' or '^' or '(' or '-';
    private sealed record Fragment(int Index, int Length, string Value) { public int End => Index + Length; }
    private sealed record Seed(int First, int Last, int Start, int End, IReadOnlyCollection<OcrRegionDetectionReason> Reasons);
    private sealed record Span(int Start, int End, IReadOnlyCollection<OcrRegionDetectionReason> Reasons);
}
