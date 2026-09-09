using System.Text;
using System.Text.Json;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Morphology.Models;

internal sealed record ProtectedMorphologyOccurrence(string Id, string Original, string JoinedForm);

internal static class GroundTruthProtectedOccurrenceLoader
{
    public static IReadOnlyList<ProtectedMorphologyOccurrence> Load(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Ground truth file was not found.", path);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("protectedOccurrences", out var values)
            || values.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Ground truth protectedOccurrences must be an array.");

        var result = new List<ProtectedMorphologyOccurrence>();
        foreach (var value in values.EnumerateArray())
        {
            var id = value.GetProperty("id").GetString();
            var original = value.GetProperty("original").GetString();
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(original))
                throw new InvalidDataException("Each protected occurrence requires id and original.");
            var first = original.IndexOf('-');
            if (first <= 0 || first != original.LastIndexOf('-') || first == original.Length - 1)
                throw new InvalidDataException($"Protected occurrence '{id}' must contain one ASCII hyphen.");
            result.Add(new ProtectedMorphologyOccurrence(
                id,
                original,
                original.Remove(first, 1)));
        }
        return result.AsReadOnly();
    }
}

internal static class TrMorphReport
{
    private static readonly string[] Examples =
    [
        "sosyete-si", "söyle-diklerinden", "kalma-yıp", "yorgun-luktan", "bur-juva",
        "sa-bah", "donuyor-dum", "dönüş-türmek", "istiyor-sa", "Strind-berg",
        "Steier-mark", "Ditt-rich", "Freum-bichler"
    ];

    public static string Serialize(
        IReadOnlyList<HyphenationMorphologyEvidence> evidence,
        IReadOnlyList<ProtectedMorphologyOccurrence> protectedOccurrences,
        ITurkishMorphologyAnalyzer analyzer)
    {
        var builder = new StringBuilder();
        var target = evidence.Where(item => item.LexiconCount < 10 && item.IsClean).ToArray();
        var targetValid = target.Count(item => item.TRmorphValid);
        builder.AppendLine("# TRmorph Hyphenation V2 Evidence");
        builder.AppendLine();
        builder.AppendLine($"Remaining candidates: {evidence.Count}");
        builder.AppendLine();
        builder.AppendLine($"Clean + LexiconCount < 10: {target.Length}");
        builder.AppendLine($"TRmorph valid: {evidence.Count(item => item.TRmorphValid)}");
        builder.AppendLine($"TRmorph invalid: {evidence.Count(item => !item.TRmorphValid)}");
        builder.AppendLine($"Clean + LexiconCount < 10 TRmorph valid: {targetValid}");
        builder.AppendLine($"Clean + LexiconCount < 10 TRmorph invalid: {target.Length - targetValid}");
        builder.AppendLine();

        builder.AppendLine("## All remaining candidate evidence");
        builder.AppendLine();
        builder.AppendLine("| Original | JoinedForm | LexiconCount | DetectionKind | HasAdjacentHyphen | HasAdjacentSuspiciousCharacter | IsClean | TRmorphValid |");
        builder.AppendLine("| --- | --- | ---: | --- | --- | --- | --- | --- |");
        foreach (var item in evidence)
            AppendRow(builder, item);

        builder.AppendLine();
        builder.AppendLine("## Clean + LexiconCount < 10, TRmorph valid");
        builder.AppendLine();
        builder.AppendLine("| Original | JoinedForm | LexiconCount | DetectionKind | TRmorphValid |");
        builder.AppendLine("| --- | --- | ---: | --- | --- |");
        foreach (var item in target.Where(item => item.TRmorphValid))
            builder.AppendLine($"| {item.Original} | {item.JoinedForm} | {item.LexiconCount} | {item.Candidate.DetectionKind} | true |");

        builder.AppendLine();
        builder.AppendLine("## Requested examples");
        builder.AppendLine();
        builder.AppendLine("| Original | JoinedForm | Occurrences | TRmorphValid |");
        builder.AppendLine("| --- | --- | ---: | --- |");
        foreach (var original in Examples)
        {
            var matches = evidence.Where(item => string.Equals(item.Original, original, StringComparison.Ordinal)).ToArray();
            builder.AppendLine(matches.Length == 0
                ? $"| {original} | — | 0 | Not present |"
                : $"| {original} | {matches[0].JoinedForm} | {matches.Length} | {matches.All(item => item.TRmorphValid)} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Protected occurrences");
        builder.AppendLine();
        builder.AppendLine("| ID | Original | JoinedForm | TRmorphValid |");
        builder.AppendLine("| --- | --- | --- | --- |");
        var accepted = 0;
        foreach (var occurrence in protectedOccurrences)
        {
            var valid = analyzer.IsValidWord(occurrence.JoinedForm);
            if (valid) accepted++;
            builder.AppendLine($"| {occurrence.Id} | {occurrence.Original} | {occurrence.JoinedForm} | {valid} |");
        }
        builder.AppendLine();
        builder.AppendLine($"Protected joined form accepted by TRmorph: {accepted}");
        return builder.ToString();
    }

    private static void AppendRow(StringBuilder builder, HyphenationMorphologyEvidence item)
    {
        builder.AppendLine($"| {item.Original} | {item.JoinedForm} | {item.LexiconCount} | {item.Candidate.DetectionKind} | {item.HasAdjacentHyphen} | {item.HasAdjacentSuspiciousCharacter} | {item.IsClean} | {item.TRmorphValid} |");
    }
}
