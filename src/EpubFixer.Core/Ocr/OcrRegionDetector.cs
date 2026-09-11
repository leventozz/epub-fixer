using System.Text.RegularExpressions;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Core.Ocr;

public sealed class OcrRegionDetector
{
    private static readonly Regex FragmentPattern = new(@"[\p{L}\p{N}][\p{L}\p{N}'’.,;:^<>()!?-]*", RegexOptions.Compiled);
    private static readonly HashSet<char> Garbage = ['^', ';', '<', '>', ':'];

    public IReadOnlyList<CorruptedTextRegion> Detect(string text, ITurkishMorphologyAnalyzer analyzer)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(analyzer);
        var fragments = FragmentPattern.Matches(text).Cast<Match>().Select(m => new Fragment(m.Index, m.Length, m.Value)).ToArray();
        var anchors = new List<Anchor>();
        for (var i = 0; i < fragments.Length; i++)
        {
            var f = fragments[i];
            var reasons = new HashSet<OcrRegionDetectionReason>();
            var invalid = !analyzer.IsValidWord(f.Value);
            if (invalid) reasons.Add(OcrRegionDetectionReason.MorphologyInvalid);
            if (f.Value.Length <= 2 && !analyzer.IsValidWord(f.Value)) reasons.Add(OcrRegionDetectionReason.ShortFragment);
            if (f.Value.Length == 1 && "ıil1".Contains(f.Value, StringComparison.OrdinalIgnoreCase))
                reasons.Add(OcrRegionDetectionReason.IsolatedOcrGlyph);
            if (f.Value.Any(Garbage.Contains)) reasons.Add(OcrRegionDetectionReason.EmbeddedGarbageGlyph);
            if (f.Value.Contains('-') && invalid) reasons.Add(OcrRegionDetectionReason.MalformedHyphenContinuation);
            var gapBefore = i == 0 ? text[..f.Index] : text[(fragments[i - 1].Index + fragments[i - 1].Length)..f.Index];
            var gapAfter = i + 1 == fragments.Length ? text[(f.Index + f.Length)..] : text[(f.Index + f.Length)..fragments[i + 1].Index];
            if ((gapBefore.Contains('\n') || gapBefore.Contains('\r') || gapAfter.Contains('\n') || gapAfter.Contains('\r')) && (f.Value.Length <= 2 || invalid))
                reasons.Add(OcrRegionDetectionReason.SuspiciousLineBreak);
            if (f.Value.Any(Garbage.Contains) || f.Value.StartsWith("(", StringComparison.Ordinal)
                || f.Value.IndexOf(',') >= 0 && f.Value.IndexOf(',') < f.Value.Length - 1)
                reasons.Add(OcrRegionDetectionReason.SuspiciousPunctuation);
            if (reasons.Count > 0) anchors.Add(new Anchor(i, reasons));
        }

        var regions = new List<CorruptedTextRegion>();
        foreach (var anchor in anchors)
        {
            var first = anchor.FragmentIndex;
            var last = first;
            while (last + 1 < fragments.Length && last - first + 1 < 3 && IsSuspiciousNeighbor(fragments[last + 1], text[(fragments[last].Index + fragments[last].Length)..fragments[last + 1].Index], analyzer)) last++;
            while (first > 0 && last - first + 1 < 3 && IsSuspiciousNeighbor(fragments[first - 1], text[(fragments[first - 1].Index + fragments[first - 1].Length)..fragments[first].Index], analyzer)) first--;
            var start = fragments[first].Index;
            var end = fragments[last].Index + fragments[last].Length;
            var reasons = new HashSet<OcrRegionDetectionReason>();
            foreach (var a in anchors.Where(a => a.FragmentIndex >= first && a.FragmentIndex <= last)) reasons.UnionWith(a.Reasons);
            if (reasons.Count == 1 && reasons.Contains(OcrRegionDetectionReason.ShortFragment) && !reasons.Contains(OcrRegionDetectionReason.IsolatedOcrGlyph)) continue;
            if (regions.Any(r => r.Start < end && r.EndExclusive > start)) continue;
            var before = text[Math.Max(0, start - 80)..start];
            var after = text[end..Math.Min(text.Length, end + 80)];
            regions.Add(new CorruptedTextRegion(text[start..end], start, end,
                fragments[first..(last + 1)].Select(f => f.Value).ToArray(), before, after,
                reasons.OrderBy(r => r).ToArray()));
        }
        return regions.OrderBy(r => r.Start).ToArray();
    }

    private static bool IsSuspiciousNeighbor(Fragment fragment, string gap, ITurkishMorphologyAnalyzer analyzer)
    {
        if (gap.Any(c => c is '\n' or '\r')) return fragment.Value.Length <= 2 || !analyzer.IsValidWord(fragment.Value);
        return fragment.Value.Length <= 2 || fragment.Value.Any(Garbage.Contains) || (fragment.Value.Contains('-') && !analyzer.IsValidWord(fragment.Value));
    }

    private sealed record Fragment(int Index, int Length, string Value);
    private sealed record Anchor(int FragmentIndex, HashSet<OcrRegionDetectionReason> Reasons);
}
