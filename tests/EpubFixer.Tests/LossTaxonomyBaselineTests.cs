using System.Text.Json;
using System.Text.Json.Serialization;
using EpubFixer.Adapters.Lexicon;
using EpubFixer.Adapters.Ocr.Lattice;
using EpubFixer.Core.Fix;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Ocr.Lattice;
using EpubFixer.Core.Ocr.Lattice.Models;
using EpubFixer.TrMorph;
using EpubFixer.Tests.LossTaxonomy;

namespace EpubFixer.Tests;

/// <summary>
/// R5.0b (docs/phase-5-plan.md@adc202d section 7.2): attributes each of the 119 losses in
/// docs/baselines/odun-kesmek.engine-diff.json to exactly one first-failure reason in the
/// lattice pipeline, and pins the result as docs/baselines/odun-kesmek.loss-taxonomy.json -
/// the same regenerate-and-compare pattern <see cref="OcrMutationBaselineTests"/> uses.
///
/// This test changes nothing in src/: it drives the real production lattice engine (via
/// <see cref="DiagnosticLatticeOcrCorrectionPlanner"/>, which duplicates
/// <c>LatticeOcrCorrectionPlanner.CreatePlan</c>'s steps read-only) over the real book, and only
/// READS the resulting regions/lattices/paths/gate verdicts to classify each loss.
/// </summary>
public sealed class LossTaxonomyBaselineTests
{
    private const string MeasuredOn = "2026-09-14";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    [Trait("Category", "Slow")]
    public void LossTaxonomy_AttributesEveryLossToExactlyOneReason()
    {
        var engineDiffPath = FindRepositoryFile(Path.Combine("docs", "baselines", "odun-kesmek.engine-diff.json"));
        var baselinePath = FindRepositoryFile(Path.Combine("docs", "baselines", "odun-kesmek.loss-taxonomy.json"));
        var input = FindRepositoryFile(Path.Combine("test-data", "odun-kesmek", "input.epub"));
        var output = Path.Combine(Path.GetTempPath(), $"epubfixer-loss-taxonomy-{Guid.NewGuid():N}.epub");

        var engineDiff = JsonSerializer.Deserialize<EngineDiffDocument>(File.ReadAllText(engineDiffPath), JsonOptions)
            ?? throw new InvalidOperationException("engine-diff.json is empty.");
        var lossCases = engineDiff.Loss.Items
            .Select(item => new LossCase(item.DocumentPath, item.LogicalStart, item.Original, item.Replacement, item.DecisionRule))
            .ToArray();
        Assert.Equal(engineDiff.Loss.Count, lossCases.Length);

        var options = new LatticeOptions();
        var planner = new DiagnosticLatticeOcrCorrectionPlanner(
            FileTurkishFrequencyListSource.Load(),
            vocabulary => new SymSpellLexiconMatcher(vocabulary),
            options);

        try
        {
            using var analyzer = new FomaTurkishMorphologyAnalyzer();
            // applyOcrCorrections:true so CreatePlan runs against the exact same post-hyphenation
            // LogicalTextStream production used for the fix-lattice.json / engine-diff.json run
            // (D66: the OCR stage sees text only after every hyphenation pass has already mutated
            // the package). Anything less faithful would put logicalStart/original out of sync
            // with the coordinates engine-diff.json actually recorded.
            new EpubFixService(new BatchMorphologyOracleBuilder(analyzer), planner)
                .Fix(input, output, applyOcrCorrections: true);
        }
        finally
        {
            if (File.Exists(output))
            {
                File.Delete(output);
            }
        }

        Assert.NotNull(planner.CapturedStream);
        Assert.NotNull(planner.CapturedVocabulary);
        Assert.NotNull(planner.CapturedMatcher);

        var diagnoses = new List<LossCaseDiagnosis>(lossCases.Length);
        foreach (var lossCase in lossCases)
        {
            var sourceLocation = planner.CapturedStream!.GetSourceLocationAt(lossCase.LogicalStart);
            Assert.Equal(lossCase.DocumentPath, sourceLocation.DocumentPath);
            Assert.Equal(
                lossCase.Original,
                planner.CapturedStream.Text.Substring(lossCase.LogicalStart, lossCase.Original.Length));

            var matched = planner.CapturedDecisions.FirstOrDefault(decision =>
                decision.Region.Start <= lossCase.LogicalStart
                && lossCase.LogicalStart + lossCase.Original.Length <= decision.Region.EndExclusive);

            var diagnosis = matched is null
                ? LossTaxonomyProbe.DiagnoseRegionNotDetected(lossCase)
                : matched.Lattice.Outcome != LatticeBuildOutcome.Built
                    ? LossTaxonomyProbe.DiagnoseWindowNotBuilt(lossCase, matched.Lattice.Outcome)
                    : LossTaxonomyProbe.DiagnoseBuiltWindow(
                        lossCase, planner.CapturedVocabulary!, planner.CapturedMatcher!, options,
                        matched.Lattice, matched.Paths, matched.Result);

            diagnoses.Add(diagnosis);
        }

        var reasonHistogram = diagnoses
            .GroupBy(d => d.LossReason, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var gateRejectedHistogram = diagnoses
            .Where(d => d.LossReason.StartsWith(LossReasonCodes.GateRejectedPrefix, StringComparison.Ordinal))
            .GroupBy(d => d.LossReason[LossReasonCodes.GateRejectedPrefix.Length..], StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var tokenHypothesisCases = diagnoses
            .Where(d => string.Equals(d.GateReason, "OriginalTokenIsValid", StringComparison.Ordinal))
            .ToArray();
        var confirmed = tokenHypothesisCases.Count(d => d.ValidToken is not null && LossTaxonomyProbe.MatchesGarbageStripHypothesis(d.Original, d.ValidToken));
        var refutedExamples = tokenHypothesisCases
            .Where(d => d.ValidToken is null || !LossTaxonomyProbe.MatchesGarbageStripHypothesis(d.Original, d.ValidToken))
            .Select(d => $"{d.Original} -> valid token: {d.ValidToken ?? "(none found)"}")
            .OrderBy(text => text, StringComparer.Ordinal)
            .ToArray();
        var hypothesisSummary = new OriginalTokenIsValidHypothesisSummary(
            tokenHypothesisCases.Length,
            confirmed,
            tokenHypothesisCases.Length - confirmed,
            refutedExamples);

        var unattributedCount = diagnoses.Count(d => string.IsNullOrWhiteSpace(d.LossReason));

        var report = new LossTaxonomyReport(
            "odun-kesmek",
            MeasuredOn,
            "Each loss case is re-run through the real production lattice pipeline " +
            "(region detection -> window -> lattice -> decoder -> gate, on the same post-" +
            "hyphenation text the production engine used) and attributed to the first stage " +
            "at which it diverges from applying the correct replacement. See " +
            "docs/phase-5-plan.md@adc202d section 7.2 for the reason codes and docs/baselines/" +
            "odun-kesmek.loss-taxonomy.json's own 'notes' field per case for known " +
            "approximations (composite multi-arc reconstructions are not exhaustively modeled " +
            "in the TargetNotInLattice/MatcherMissedTarget split).",
            lossCases.Length,
            unattributedCount,
            reasonHistogram,
            gateRejectedHistogram,
            hypothesisSummary,
            diagnoses
                .OrderBy(d => d.DocumentPath, StringComparer.Ordinal)
                .ThenBy(d => d.LogicalStart)
                .ToArray());

        var actualJson = JsonSerializer.Serialize(report, JsonOptions);
        if (string.Equals(Environment.GetEnvironmentVariable("EPUBFIXER_UPDATE_BASELINES"), "1", StringComparison.Ordinal))
        {
            File.WriteAllText(baselinePath, actualJson + Environment.NewLine);
        }

        Assert.Equal(0, unattributedCount);
        Assert.Equal(lossCases.Length, diagnoses.Count);

        var expected = JsonSerializer.Deserialize<LossTaxonomyReport>(File.ReadAllText(baselinePath), JsonOptions)
            ?? throw new InvalidOperationException("loss-taxonomy.json is empty.");
        Assert.Equal(JsonSerializer.Serialize(expected, JsonOptions), actualJson);
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

        throw new FileNotFoundException("Repository file was not found.", relativePath);
    }

    private sealed record EngineDiffLossItem(string DocumentPath, int LogicalStart, string Original, string Replacement, string DecisionRule);

    private sealed record EngineDiffLossSection(int Count, IReadOnlyList<EngineDiffLossItem> Items);

    private sealed record EngineDiffDocument(EngineDiffLossSection Loss);
}
