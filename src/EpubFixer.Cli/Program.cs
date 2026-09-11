using System.Text;
using EpubFixer.Core.Correction;
using EpubFixer.Core.Correction.Models;
using EpubFixer.Core.Decision;
using EpubFixer.Core.Decision.Models;
using EpubFixer.Core.Detection;
using EpubFixer.Core.Detection.Models;
using EpubFixer.Core.Epub;
using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Evidence;
using EpubFixer.Core.Evidence.Models;
using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Lexicon.Models;
using EpubFixer.Core.Fix;
using EpubFixer.Core.Fix.Models;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Ocr;
using EpubFixer.Core.Ocr.Models;
using EpubFixer.Core.Mutation.Models;
using EpubFixer.TrMorph;
using EpubFixer.Cli.OcrReconstruction;

return Run(args);

static int Run(string[] arguments)
{
    if (arguments.Length > 0 && string.Equals(arguments[0], "debug-ocr-region", StringComparison.OrdinalIgnoreCase))
        return RunDebugOcrRegion(arguments);
    if (arguments.Length > 0 && string.Equals(arguments[0], "debug-ocr-reconstruction", StringComparison.OrdinalIgnoreCase))
        return RunDebugOcrReconstruction(arguments);

    if (!CliOptions.TryParse(arguments, out var options))
    {
        PrintUsage();
        return 1;
    }

    try
    {
        ValidateOutputPaths(options);

        if (options.Command == CliCommand.Fix)
        {
            using var analyzer = new FomaTurkishMorphologyAnalyzer();
            var result = new EpubFixService(analyzer).Fix(
                options.EpubPath,
                options.OutputEpubPath!,
                options.ApplyOcrCorrections);
            PrintFixSummary(result);
            if (options.OcrMutationReportPath is not null && result.OcrMutation is not null)
            {
                WriteOcrMutationReport(options.OcrMutationReportPath, result.OcrMutation);
                Console.WriteLine($"OCR mutation report written to: {options.OcrMutationReportPath}");
            }
            return 0;
        }

    var package = new EpubPackageReader().Read(options.EpubPath);
    OcrCorrectionAnalysisReport? correctionReport = null;
    OcrCorrectionDecisionAnalysisReport? decisionReport = null;
    if (options.OcrCorrectionReportPath is not null || options.OcrDecisionReportPath is not null)
    {
        using var ocrAnalyzer = new FomaTurkishMorphologyAnalyzer();
        correctionReport = new OcrAnalysisService().AnalyzeCorrections(options.EpubPath, ocrAnalyzer);
        if (options.OcrCorrectionReportPath is not null)
        {
            File.WriteAllText(options.OcrCorrectionReportPath, OcrCorrectionAnalysisReporting.SerializeMarkdown(correctionReport), new UTF8Encoding(false));
            Console.WriteLine($"OCR correction report written to: {options.OcrCorrectionReportPath}");
        }
        if (options.OcrDecisionReportPath is not null)
        {
            decisionReport = new OcrCorrectionDecisionEvaluator().Evaluate(correctionReport);
            File.WriteAllText(options.OcrDecisionReportPath, OcrDecisionReporting.SerializeMarkdown(decisionReport), new UTF8Encoding(false));
            Console.WriteLine($"OCR decision report written to: {options.OcrDecisionReportPath}");
        }
    }
    if (options.OcrReportPath is not null)
    {
        OcrAnalysisReport ocrReport;
        if (correctionReport is not null) ocrReport = correctionReport.SourceAnalysis;
        else
        {
            using var ocrAnalyzer = new FomaTurkishMorphologyAnalyzer();
            ocrReport = new OcrAnalysisService().Analyze(options.EpubPath, ocrAnalyzer);
        }
        File.WriteAllText(options.OcrReportPath, OcrAnalysisReporting.SerializeMarkdown(ocrReport), new UTF8Encoding(false));
        Console.WriteLine($"OCR report written to: {options.OcrReportPath}");
    }
    var originalPipeline = AnalyzeHyphenation(package.LogicalText);
    var candidates = originalPipeline.Candidates;
    var lexicon = originalPipeline.Lexicon;
    var evidence = originalPipeline.Evidence;
    var evidenceSummary = HyphenationEvidenceReporting.CreateSummary(evidence);
    var decisions = originalPipeline.Decisions;
        var decisionSummary = HyphenationDecisionReporting.CreateSummary(decisions);
        var correctionPlans = new HyphenationCorrectionPlanner().Plan(decisions);
        var correctionSummary = HyphenationCorrectionReporting.CreateSummary(
            decisions,
            correctionPlans);

        PrintSummary(package);
        PrintHyphenationSummary(candidates);
        HyphenationEvidenceReporting.Print(Console.Out, evidenceSummary);
        HyphenationDecisionReporting.Print(Console.Out, decisionSummary);
        HyphenationCorrectionReporting.Print(Console.Out, correctionSummary);

        if (options.DumpPath is not null)
        {
            File.WriteAllText(
                options.DumpPath,
                package.LogicalText.CreateDebugText(),
                new UTF8Encoding(false));
            Console.WriteLine();
            Console.WriteLine($"Logical text written to: {options.DumpPath}");
        }

        if (options.HyphenReportPath is not null)
        {
            WriteHyphenationReport(
                options.HyphenReportPath,
                decisions,
                evidenceSummary,
                decisionSummary);
            Console.WriteLine();
            Console.WriteLine($"Hyphenation report written to: {options.HyphenReportPath}");
        }

        if (options.HyphenAnalysisReportPath is not null)
        {
            WriteHyphenationAnalysisReport(
                options.HyphenAnalysisReportPath,
                evidence,
                package.LogicalText);
            Console.WriteLine();
            Console.WriteLine(
                $"Hyphenation analysis report written to: {options.HyphenAnalysisReportPath}");
        }

        if (options.ApplyParagraph
            || options.PostFixLexiconReportPath is not null
            || options.TrMorphReportPath is not null)
        {
            var postFix = RunPostFixAnalysis(package, originalPipeline);
            var inlinePlans = postFix.InlinePlans;
            var inlineApplyResult = postFix.InlineApplyResult;
            var crossParagraphPlans = postFix.CrossParagraphPlans;
            var crossParagraphApplyResult = postFix.CrossParagraphApplyResult;
            var afterInlinePipeline = postFix.AfterInline;
            var afterCrossParagraphPipeline = postFix.Final;
            var remainingOriginalAutoFixOccurrences =
                CountRemainingOriginalAutoFixOccurrences(
                    decisions,
                    afterCrossParagraphPipeline.Candidates);

            if (options.ApplyParagraph)
            {
                PrintParagraphApplySummary(
                    inlineApplyResult,
                    crossParagraphPlans,
                    crossParagraphApplyResult,
                    candidates,
                    afterInlinePipeline.Candidates,
                    afterCrossParagraphPipeline.Candidates,
                    afterCrossParagraphPipeline.Decisions,
                    remainingOriginalAutoFixOccurrences);
            }

            if (options.PostFixLexiconReportPath is not null)
            {
                WritePostFixLexiconReport(options.PostFixLexiconReportPath, postFix);
                Console.WriteLine();
                Console.WriteLine(
                    $"Post-fix lexicon report written to: {options.PostFixLexiconReportPath}");
            }

            if (options.TrMorphReportPath is not null)
            {
                var protectedOccurrences = GroundTruthProtectedOccurrenceLoader.Load(
                    options.GroundTruthPath!);
                using var analyzer = new FomaTurkishMorphologyAnalyzer();
                var morphology = new HyphenationMorphologyAnalyzer().Analyze(
                    afterCrossParagraphPipeline.Evidence,
                    analyzer);
                File.WriteAllText(
                    options.TrMorphReportPath,
                    TrMorphReport.Serialize(
                        morphology,
                        protectedOccurrences,
                        analyzer,
                        originalPipeline.Decisions.Count(
                            item => item.DecisionKind == HyphenationDecisionKind.AutoFixCandidate)),
                    new UTF8Encoding(false));
                Console.WriteLine();
                Console.WriteLine($"TRmorph report written to: {options.TrMorphReportPath}");
            }
        }
        else if (options.ApplyInline)
        {
            var applyResult = new HyphenationCorrectionApplier().Apply(correctionPlans);
            var rebuiltLogicalText = LogicalTextStreamBuilder.Build(package.SpineDocuments);
            var rebuiltCandidates = new HyphenationDetector().Detect(rebuiltLogicalText);

            PrintInlineApplySummary(
                correctionPlans,
                applyResult,
                candidates,
                rebuiltCandidates);
        }

        return 0;
    }
    catch (Exception exception) when (exception is ArgumentException
        or IOException
        or InvalidDataException
        or UnauthorizedAccessException
        or TurkishMorphologyException)
    {
        Console.Error.WriteLine($"Error: {exception.Message}");
        return 2;
    }
}

static int RunDebugOcrRegion(string[] arguments)
{
    if (arguments.Length < 2) { PrintUsage(); return 1; }
    var input = arguments[1];
    var reportPath = "artifacts/debug/ocr-region-01.md";
    var outputPath = "artifacts/debug/ocr-region-01-output.txt";
    for (var i = 2; i < arguments.Length; i += 2)
    {
        if (i + 1 >= arguments.Length) { PrintUsage(); return 1; }
        if (string.Equals(arguments[i], "--report", StringComparison.OrdinalIgnoreCase)) reportPath = arguments[i + 1];
        else if (string.Equals(arguments[i], "--output", StringComparison.OrdinalIgnoreCase)) outputPath = arguments[i + 1];
        else { PrintUsage(); return 1; }
    }
    try
    {
        var text = File.ReadAllText(input, new UTF8Encoding(false, true));
        using var analyzer = new FomaTurkishMorphologyAnalyzer();
        var regions = new OcrRegionDetector().Detect(text, analyzer);
        var targets = new[] { "ya\n\ndn", "ı,ırarını", "ı ıç", "kendi-ıni", "dikkat-:;i zlikle", "1 ı iç", ":,ohbet", "koli ukta", "(le", "ı ızellikle", "ı ılduğu", "Ce-lıimde", "( 1 iye", "--:<lbaha", "Anacadde-si'ni", "Schwarzen-herg", "ge-^:cn", "Simmerin-ger", "yü-ıiimeye" };
        var clean = new[] { "de yıllarca,", "düşünüyorum,", "eve,", "başladığında,", "dolaştım,", "denilebilir, o en", "o zamanlar.", "ve içtim,", "anlatılmaz,", "yoktu,", "oldum,", "ettim,", "Jeannie", "Billroth", "Auersberger", "Rennweg", "Schwarzenberg", "Wahring", "Simmeringer", "Joana'ya" };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        File.WriteAllText(outputPath, text, new UTF8Encoding(false));
        var builder = new StringBuilder("# OCR Region Debug\n\n## RAW TEXT\n\n");
        builder.AppendLine(text).AppendLine("\n## DETECTED CORRUPTED REGIONS\n");
        var targetOccurrences = targets.SelectMany(target => Occurrences(text, target).Select(start => (Target: target, Start: start, End: start + target.Length))).ToArray();
        var targetRows = targetOccurrences.Select(item => (item, Region: regions.FirstOrDefault(r => r.Start <= item.Start && r.EndExclusive >= item.End))).ToArray();
        builder.AppendLine($"Previous detected region count: 77\nNew detected region count: {regions.Count}\nTarget occurrence count: {targetRows.Length}\nTarget detected count: {targetRows.Count(x => x.Region is not null)}\nTarget missed count: {targetRows.Count(x => x.Region is null)}\nBoundary-correct target count: {targetRows.Count(x => x.Region is not null && x.Region.Start == x.item.Start && x.Region.EndExclusive == x.item.End)}\n");
        builder.AppendLine("## TARGET REGION AUDIT\n");
        foreach (var row in targetRows)
        {
            var r = row.Region;
            builder.AppendLine($"- Target: `{row.item.Target.Replace("\r", "\\r").Replace("\n", "\\n")}` | Detected: {r is not null} | Raw region: `{r?.RawText.Replace("\r", "\\r").Replace("\n", "\\n") ?? ""}` | Start: {r?.Start.ToString() ?? "-"} | EndExclusive: {r?.EndExclusive.ToString() ?? "-"} | Reasons: {(r is null ? "-" : string.Join(", ", r.DetectionReasons))} | Boundary-correct: {r is not null && r.Start == row.item.Start && r.EndExclusive == row.item.End}");
        }
        builder.AppendLine("\n## CLEAN TEXT FALSE-POSITIVE AUDIT\n");
        var cleanRows = clean.SelectMany(target => Occurrences(text, target).Select(start => (Target: target, Start: start, End: start + target.Length))).ToArray();
        foreach (var row in cleanRows)
        {
            var overlap = regions.FirstOrDefault(r => r.Start < row.End && r.EndExclusive > row.Start);
            builder.AppendLine($"- `{row.Target}` [{row.Start},{row.End}): Region: {overlap is not null} | Raw: `{overlap?.RawText ?? ""}` | Reasons: {(overlap is null ? "-" : string.Join(", ", overlap.DetectionReasons))}");
        }
        builder.AppendLine($"\nClean false-positive count: {cleanRows.Count(row => regions.Any(r => r.Start < row.End && r.EndExclusive > row.Start))}\n");
        foreach (var (r, index) in regions.Select((r, i) => (r, i + 1)))
            builder.AppendLine($"### #{index}\n- Raw: `{r.RawText.Replace("\r", "\\r").Replace("\n", "\\n")}`\n- Start: {r.Start}\n- EndExclusive: {r.EndExclusive}\n- Fragments: {string.Join(" | ", r.LogicalFragments)}\n- Reasons: {string.Join(", ", r.DetectionReasons)}\n- Context: `{r.ContextBefore}⟦{r.RawText}⟧{r.ContextAfter}`\n");
        builder.AppendLine("## RECONSTRUCTION CANDIDATES\n\nDeferred.\n\n## DECISIONS\n\nDeferred.\n\n## RECONSTRUCTED TEXT\n\nRaw.\n");
        File.WriteAllText(reportPath, builder.ToString(), new UTF8Encoding(false));
        Console.WriteLine("=== DETECTED CORRUPTED REGIONS ===");
        Console.WriteLine($"Detected regions: {regions.Count}");
        foreach (var (r, index) in regions.Select((r, i) => (r, i + 1))) Console.WriteLine($"#{index} [{r.Start},{r.EndExclusive}): {r.RawText.Replace("\n", "\\n")}");
        Console.WriteLine($"Debug report written to: {reportPath}");
        Console.WriteLine($"Reconstructed output written to: {outputPath}");
        return 0;
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DecoderFallbackException)
    { Console.Error.WriteLine($"Error: {ex.Message}"); return 2; }
}

static int RunDebugOcrReconstruction(string[] arguments)
{
    if (arguments.Length < 2) { PrintUsage(); return 1; }
    string? expected = null;
    string? report = null;
    for (var i = 2; i < arguments.Length; i += 2)
    {
        if (i + 1 >= arguments.Length) { PrintUsage(); return 1; }
        if (string.Equals(arguments[i], "--expected", StringComparison.OrdinalIgnoreCase)) expected = arguments[i + 1];
        else if (string.Equals(arguments[i], "--report", StringComparison.OrdinalIgnoreCase)) report = arguments[i + 1];
        else { PrintUsage(); return 1; }
    }
    try
    {
        using var analyzer = new FomaTurkishMorphologyAnalyzer();
        return new OcrReconstructionComparison().Run(arguments[1], expected, report, analyzer);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or TurkishMorphologyException)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        return 2;
    }
}

static IEnumerable<int> Occurrences(string text, string value)
{
    for (var start = 0; (start = text.IndexOf(value, start, StringComparison.Ordinal)) >= 0; start += Math.Max(1, value.Length)) yield return start;
}

static void ValidateOutputPaths(CliOptions options)
{
    var outputPaths = new[]
    {
        options.DumpPath,
        options.HyphenReportPath,
        options.HyphenAnalysisReportPath,
        options.PostFixLexiconReportPath,
        options.TrMorphReportPath,
        options.OcrReportPath,
        options.OcrCorrectionReportPath,
        options.OcrDecisionReportPath,
        options.OcrMutationReportPath,
        options.OutputEpubPath
    }.Where(path => path is not null).Cast<string>().ToArray();

    foreach (var outputPath in outputPaths)
    {
        if (PathsReferToSameFile(options.EpubPath, outputPath))
        {
            throw new ArgumentException("Output paths must be different from the source EPUB path.");
        }
    }

    for (var firstIndex = 0; firstIndex < outputPaths.Length; firstIndex++)
    {
        for (var secondIndex = firstIndex + 1; secondIndex < outputPaths.Length; secondIndex++)
        {
            if (PathsReferToSameFile(outputPaths[firstIndex], outputPaths[secondIndex]))
            {
                throw new ArgumentException("Output paths must be different from each other.");
            }
        }
    }
}

static void PrintFixSummary(EpubFixResult result)
{
    var skipped = result.InlineApplyResult.SkippedCount
        + result.CrossParagraphApplyResult.SkippedCount;
    var totalApplied = result.InlineApplyResult.AppliedCount
        + result.CrossParagraphApplyResult.AppliedCount
        + result.V2InlineApplyResult.AppliedCount
        + result.V2CrossParagraphApplyResult.AppliedCount;

    Console.WriteLine($"Input:  {result.InputPath}");
    Console.WriteLine($"Output: {result.OutputPath}");
    Console.WriteLine();
    Console.WriteLine("Hyphenation:");
    Console.WriteLine($"  Original candidates: {result.OriginalCandidateCount}");
    Console.WriteLine($"  Original AutoFix candidates: {result.OriginalAutoFixCandidateCount}");
    Console.WriteLine($"  Inline applied: {result.InlineApplyResult.AppliedCount}");
    Console.WriteLine($"  CrossParagraph applied: {result.CrossParagraphApplyResult.AppliedCount}");
    Console.WriteLine($"  V2 AutoFix candidates: {result.V2AutoFixCandidateCount}");
    Console.WriteLine($"  V2 DetectionKind: Inline={result.V2DetectionKinds.Inline}, CrossParagraph={result.V2DetectionKinds.CrossParagraph}, DocumentBoundary={result.V2DetectionKinds.DocumentBoundary}, Other={result.V2DetectionKinds.Other}");
    Console.WriteLine($"  V2 planned: {result.V2PlannedCount}");
    Console.WriteLine($"  V2 Inline applied: {result.V2InlineApplyResult.AppliedCount}");
    Console.WriteLine($"  V2 CrossParagraph applied: {result.V2CrossParagraphApplyResult.AppliedCount}");
    Console.WriteLine($"  V2 unsupported DocumentBoundary: {result.V2UnsupportedDocumentBoundaryCount}");
    Console.WriteLine($"  Skipped: {skipped}");
    Console.WriteLine();
    Console.WriteLine($"Total applied corrections: {totalApplied}");
    if (result.OcrMutation is not null)
    {
        Console.WriteLine();
        Console.WriteLine("OCR mutation:");
        Console.WriteLine($"  Planned: {result.OcrMutation.PlannedCount}");
        Console.WriteLine($"  Applied: {result.OcrMutation.AppliedCount}");
        Console.WriteLine($"  Failed: {result.OcrMutation.Failures.Count}");
        Console.WriteLine($"  Conflicts: {result.OcrMutation.ConflictCount}");
    }
    Console.WriteLine();
    Console.WriteLine($"Remaining candidates: {result.RemainingCandidateCount}");
    Console.WriteLine($"Remaining AutoFix candidates: {result.RemainingAutoFixCandidateCount}");
    Console.WriteLine();
    Console.WriteLine($"Input SHA-256:  {result.InputSha256}");
    Console.WriteLine($"Output SHA-256: {result.OutputSha256}");
    Console.WriteLine();
    Console.WriteLine(
        $"Resource integrity: {result.Integrity.EntryCount} entries; "
        + $"{result.Integrity.UntouchedEntryCount} untouched entries byte-identical.");
    Console.WriteLine(
        "Modified XHTML: "
        + (result.Integrity.ModifiedDocumentPaths.Count == 0
            ? "none"
            : string.Join(", ", result.Integrity.ModifiedDocumentPaths)));

    foreach (var diagnostic in result.WriteResult.DocumentDiagnostics)
    {
        Console.WriteLine(
            $"  {diagnostic.Path}: {diagnostic.OriginalByteCount} -> "
            + $"{diagnostic.OutputByteCount} bytes");
    }

    Console.WriteLine("Read-back validation: successful.");
    Console.WriteLine("Output written successfully.");
}

static void WriteOcrMutationReport(string path, OcrMutationResult result)
{
    var builder = new StringBuilder();
    builder.AppendLine("# OCR Correction Mutation V1");
    builder.AppendLine();
    builder.AppendLine($"- Planned mutations: {result.PlannedCount}");
    builder.AppendLine($"- Applied mutations: {result.AppliedCount}");
    builder.AppendLine($"- Failed mutations: {result.Failures.Count}");
    builder.AppendLine($"- Conflict count: {result.ConflictCount}");
    builder.AppendLine($"- Single-source mutations: {result.SingleSourceCount}");
    builder.AppendLine($"- Multi-source mutations: {result.MultiSourceCount}");
    builder.AppendLine();
    builder.AppendLine("## All Applied OCR Mutations");
    builder.AppendLine();
    builder.AppendLine("| # | Document | Source | Replacement | DecisionRule | Confidence | SourceSpans | Applied | FailureReason |");
    builder.AppendLine("|---:|---|---|---|---|---|---:|---|---|");
    var index = 1;
    foreach (var mutation in result.AppliedMutations)
    {
        builder.AppendLine($"| {index++} | {mutation.DocumentPath} | {mutation.OriginalSourceText.Replace("|", "\\|", StringComparison.Ordinal)} | {mutation.ReplacementText.Replace("|", "\\|", StringComparison.Ordinal)} | {mutation.DecisionRule} | {mutation.Confidence} | {mutation.SourceSpans.Count} | true | |");
    }
    builder.AppendLine();
    builder.AppendLine("## Mutation Risk Audit");
    builder.AppendLine();
    builder.AppendLine("Risk flags are preserved from Decision V1.2 metadata; no correction intelligence is applied by this layer.");
    File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
}

static void PrintSummary(EpubPackage package)
{
    var logicalText = package.LogicalText;
    var linearDocumentCount = package.SpineDocuments.Count(document => document.IsLinear);

    Console.WriteLine("EPUB loaded successfully.");
    Console.WriteLine();
    Console.WriteLine("Package:");
    Console.WriteLine($"  Version: {package.Version}");
    Console.WriteLine();
    Console.WriteLine($"Spine documents: {package.SpineDocuments.Count} ({linearDocumentCount} linear)");
    Console.WriteLine($"Text segments: {logicalText.Segments.Count}");
    Console.WriteLine($"Characters: {logicalText.CharacterCount}");
    Console.WriteLine();
    Console.WriteLine("Boundaries:");
    Console.WriteLine($"  Text node: {CountBoundaries(logicalText, TextBoundaryKind.TextNode)}");
    Console.WriteLine($"  Paragraph: {CountBoundaries(logicalText, TextBoundaryKind.Paragraph)}");
    Console.WriteLine($"  Document: {CountBoundaries(logicalText, TextBoundaryKind.Document)}");
    Console.WriteLine();
    Console.WriteLine("Documents:");

    foreach (var document in package.SpineDocuments)
    {
        var suffix = document.IsLinear ? string.Empty : " [non-linear]";
        Console.WriteLine($"  {document.SpineIndex + 1}. {document.Path}{suffix}");
    }
}

static void PrintHyphenationSummary(IReadOnlyList<HyphenationCandidate> candidates)
{
    Console.WriteLine();
    Console.WriteLine($"Hyphenation candidates: {candidates.Count}");
    Console.WriteLine();
    Console.WriteLine($"  Inline: {CountCandidates(candidates, HyphenationDetectionKind.Inline)}");
    Console.WriteLine($"  Text node boundary: {CountCandidates(candidates, HyphenationDetectionKind.TextNodeBoundary)}");
    Console.WriteLine($"  Paragraph boundary: {CountCandidates(candidates, HyphenationDetectionKind.ParagraphBoundary)}");
    Console.WriteLine($"  Document boundary: {CountCandidates(candidates, HyphenationDetectionKind.DocumentBoundary)}");

    var aggregates = candidates
        .GroupBy(candidate => new CandidateKey(
            candidate.LeftPart,
            candidate.RightPart,
            candidate.UnhyphenatedText))
        .Select(group => new CandidateAggregate(group.Key, group.Count()))
        .OrderByDescending(aggregate => aggregate.Count)
        .ThenBy(aggregate => aggregate.Key.LeftPart, StringComparer.Ordinal)
        .ThenBy(aggregate => aggregate.Key.RightPart, StringComparer.Ordinal)
        .Take(15)
        .ToArray();

    if (aggregates.Length > 0)
    {
        Console.WriteLine();
        Console.WriteLine("Most frequent candidates:");

        foreach (var aggregate in aggregates)
        {
            Console.WriteLine(
                $"  {aggregate.Key.LeftPart}-{aggregate.Key.RightPart} -> "
                + $"{aggregate.Key.UnhyphenatedText}   {aggregate.Count}x");
        }
    }

    if (candidates.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("Sample occurrences:");

        foreach (var candidate in candidates.Take(10))
        {
            PrintCandidate(candidate);
        }
    }
}

static void PrintInlineApplySummary(
    IReadOnlyList<HyphenationCorrectionPlan> plans,
    HyphenationCorrectionApplyResult applyResult,
    IReadOnlyList<HyphenationCandidate> beforeCandidates,
    IReadOnlyList<HyphenationCandidate> afterCandidates)
{
    var inlinePlans = plans
        .Where(plan => plan.CorrectionKind == HyphenationCorrectionKind.Inline)
        .ToArray();
    var plannedTransformations = inlinePlans
        .Select(plan => new CandidateKey(
            plan.Decision.Evidence.Candidate.LeftPart,
            plan.Decision.Evidence.Candidate.RightPart,
            plan.UnhyphenatedText))
        .ToHashSet();
    var remainingPlannedInlineCandidates = afterCandidates.Count(candidate =>
        candidate.DetectionKind == HyphenationDetectionKind.Inline
        && plannedTransformations.Contains(new CandidateKey(
            candidate.LeftPart,
            candidate.RightPart,
            candidate.UnhyphenatedText)));

    Console.WriteLine();
    Console.WriteLine("Inline correction apply");
    Console.WriteLine();
    Console.WriteLine($"Inline plans: {inlinePlans.Length}");
    Console.WriteLine($"Applied inline corrections: {applyResult.AppliedCount}");
    Console.WriteLine($"Skipped plans: {applyResult.SkippedCount}");
    Console.WriteLine();
    Console.WriteLine("Before inline apply:");
    PrintCandidateCounts(beforeCandidates);
    Console.WriteLine();
    Console.WriteLine("After inline apply:");
    PrintCandidateCounts(afterCandidates);
    Console.WriteLine();
    Console.WriteLine(
        "Remaining AutoFixCandidate inline transformations: "
        + remainingPlannedInlineCandidates);
}

static HyphenationPipelineResult AnalyzeHyphenation(LogicalTextStream logicalText)
{
    var candidates = new HyphenationDetector().Detect(logicalText);
    var lexicon = new BookLexiconBuilder().Build(logicalText);
    var evidence = new HyphenationEvidenceEvaluator().Evaluate(
        candidates,
        lexicon,
        logicalText);
    var decisions = new HyphenationDecisionEvaluator().Evaluate(evidence);
    var plans = new HyphenationCorrectionPlanner().Plan(decisions);

    return new HyphenationPipelineResult(logicalText, lexicon, evidence, candidates, decisions, plans);
}

static PostFixLexiconAnalysisResult RunPostFixAnalysis(
    EpubPackage package,
    HyphenationPipelineResult original)
{
    var inlinePlans = original.Plans
        .Where(plan => plan.CorrectionKind == HyphenationCorrectionKind.Inline)
        .ToArray();
    var inlineApplyResult = new HyphenationCorrectionApplier().Apply(inlinePlans);
    var afterInline = AnalyzeHyphenation(
        LogicalTextStreamBuilder.Build(package.SpineDocuments));
    var crossParagraphPlans = afterInline.Plans
        .Where(plan => plan.CorrectionKind == HyphenationCorrectionKind.CrossParagraph)
        .ToArray();
    var crossParagraphApplyResult =
        new CrossParagraphHyphenationCorrectionApplier().Apply(crossParagraphPlans);
    var final = AnalyzeHyphenation(
        LogicalTextStreamBuilder.Build(package.SpineDocuments));

    return new PostFixLexiconAnalysisResult(
        original,
        inlinePlans,
        inlineApplyResult,
        afterInline,
        crossParagraphPlans,
        crossParagraphApplyResult,
        final);
}

static void WritePostFixLexiconReport(
    string reportPath,
    PostFixLexiconAnalysisResult analysis)
{
    File.WriteAllText(
        reportPath,
        PostFixLexiconReporting.SerializeMarkdown(analysis),
        new UTF8Encoding(false));
}

static int CountRemainingOriginalAutoFixOccurrences(
    IReadOnlyList<HyphenationDecision> originalDecisions,
    IReadOnlyList<HyphenationCandidate> afterCandidates)
{
    var originalCandidates = originalDecisions
        .Where(decision => decision.DecisionKind == HyphenationDecisionKind.AutoFixCandidate)
        .Select(decision => decision.Evidence.Candidate)
        .ToArray();

    return afterCandidates.Count(candidate => originalCandidates.Any(original =>
        ReferenceEquals(
            original.HyphenSource.SourceNode,
            candidate.HyphenSource.SourceNode)
        && string.Equals(original.LeftPart, candidate.LeftPart, StringComparison.Ordinal)
        && string.Equals(original.RightPart, candidate.RightPart, StringComparison.Ordinal)
        && string.Equals(
            original.UnhyphenatedText,
            candidate.UnhyphenatedText,
            StringComparison.Ordinal)));
}

static void PrintParagraphApplySummary(
    HyphenationCorrectionApplyResult inlineApplyResult,
    IReadOnlyList<HyphenationCorrectionPlan> crossParagraphPlans,
    HyphenationCorrectionApplyResult crossParagraphApplyResult,
    IReadOnlyList<HyphenationCandidate> originalCandidates,
    IReadOnlyList<HyphenationCandidate> afterInlineCandidates,
    IReadOnlyList<HyphenationCandidate> afterCrossParagraphCandidates,
    IReadOnlyList<HyphenationDecision> finalDecisions,
    int remainingOriginalAutoFixOccurrences)
{
    var finalAutoFixCandidateCount = finalDecisions.Count(
        decision => decision.DecisionKind == HyphenationDecisionKind.AutoFixCandidate);

    Console.WriteLine();
    Console.WriteLine("Cross-paragraph correction apply");
    Console.WriteLine();
    Console.WriteLine($"Inline applied: {inlineApplyResult.AppliedCount}");
    Console.WriteLine($"CrossParagraph plans: {crossParagraphPlans.Count}");
    Console.WriteLine($"CrossParagraph applied: {crossParagraphApplyResult.AppliedCount}");
    Console.WriteLine($"CrossParagraph skipped: {crossParagraphApplyResult.SkippedCount}");
    Console.WriteLine();
    Console.WriteLine($"Original candidates: {originalCandidates.Count}");
    Console.WriteLine($"After inline apply: {afterInlineCandidates.Count}");
    Console.WriteLine(
        $"After cross-paragraph apply: {afterCrossParagraphCandidates.Count}");
    Console.WriteLine($"Final AutoFixCandidate: {finalAutoFixCandidateCount}");
    Console.WriteLine(
        "Remaining original AutoFixCandidate occurrences: "
        + remainingOriginalAutoFixOccurrences);
    Console.WriteLine();
    Console.WriteLine("After cross-paragraph apply:");
    PrintCandidateCounts(afterCrossParagraphCandidates);
}

static void PrintCandidateCounts(IReadOnlyList<HyphenationCandidate> candidates)
{
    Console.WriteLine($"  Total candidates: {candidates.Count}");
    Console.WriteLine($"  Inline: {CountCandidates(candidates, HyphenationDetectionKind.Inline)}");
    Console.WriteLine(
        $"  Text node boundary: {CountCandidates(candidates, HyphenationDetectionKind.TextNodeBoundary)}");
    Console.WriteLine(
        $"  Paragraph boundary: {CountCandidates(candidates, HyphenationDetectionKind.ParagraphBoundary)}");
    Console.WriteLine(
        $"  Document boundary: {CountCandidates(candidates, HyphenationDetectionKind.DocumentBoundary)}");
}

static void PrintCandidate(HyphenationCandidate candidate)
{
    Console.WriteLine();
    Console.WriteLine($"[{FormatDetectionKind(candidate.DetectionKind)}]");

    var original = candidate.DetectionKind == HyphenationDetectionKind.Inline
        ? $"{candidate.LeftPart}-{candidate.RightPart}"
        : $"{candidate.LeftPart}- + {candidate.RightPart}";

    Console.WriteLine($"{original} -> {candidate.UnhyphenatedText}");
    Console.WriteLine("Source:");
    Console.WriteLine($"  {candidate.LeftSource.DocumentPath}");

    if (!string.Equals(
            candidate.LeftSource.DocumentPath,
            candidate.RightSource.DocumentPath,
            StringComparison.Ordinal))
    {
        Console.WriteLine($"  {candidate.RightSource.DocumentPath}");
    }
}

static void WriteHyphenationReport(
    string reportPath,
    IReadOnlyList<HyphenationDecision> decisions,
    HyphenationEvidenceSummary evidenceSummary,
    HyphenationDecisionSummary decisionSummary)
{
    var json = HyphenationEvidenceReporting.SerializeJson(
        decisions,
        evidenceSummary,
        decisionSummary);
    File.WriteAllText(reportPath, json, new UTF8Encoding(false));
}

static void WriteHyphenationAnalysisReport(
    string reportPath,
    IReadOnlyList<HyphenationEvidence> evidence,
    LogicalTextStream logicalText)
{
    var analysis = HyphenationEvidenceReporting.CreateAnalysis(evidence, logicalText);
    var markdown = HyphenationEvidenceReporting.SerializeMarkdown(analysis);
    File.WriteAllText(reportPath, markdown, new UTF8Encoding(false));
}

static int CountBoundaries(LogicalTextStream stream, TextBoundaryKind kind)
{
    return stream.Boundaries.Count(boundary => boundary.Kind == kind);
}

static int CountCandidates(
    IReadOnlyList<HyphenationCandidate> candidates,
    HyphenationDetectionKind kind)
{
    return candidates.Count(candidate => candidate.DetectionKind == kind);
}

static string FormatDetectionKind(HyphenationDetectionKind kind)
{
    return kind switch
    {
        HyphenationDetectionKind.Inline => "Inline",
        HyphenationDetectionKind.TextNodeBoundary => "TextNodeBoundary",
        HyphenationDetectionKind.ParagraphBoundary => "ParagraphBoundary",
        HyphenationDetectionKind.DocumentBoundary => "DocumentBoundary",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}

static bool PathsReferToSameFile(string firstPath, string secondPath)
{
    var comparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    return string.Equals(Path.GetFullPath(firstPath), Path.GetFullPath(secondPath), comparison);
}

static void PrintUsage()
{
    Console.Error.WriteLine("       epubfixer debug-ocr-region <input.txt> [--report <report.md>] [--output <output.txt>]");
    Console.Error.WriteLine("       epubfixer debug-ocr-reconstruction <input.txt> [--expected <expected.json>] [--report <report.md>]");
    Console.Error.WriteLine(
        "Usage: epubfixer analyze <book.epub> "
        + "[--apply-inline] "
        + "[--apply-paragraph] "
        + "[--dump-text <output.txt>] [--hyphen-report <hyphens.json>] "
        + "[--hyphen-analysis-report <analysis.md>] "
        + "[--post-fix-lexicon-report <post-fix.md>] "
        + "[--ocr-report <ocr.md>] "
        + "[--ocr-correction-report <ocr-candidates.md>] "
        + "[--ocr-decision-report <ocr-decisions.md>] "
        + "[--trmorph-report <trmorph.md> --ground-truth <ground-truth.json>]");
    Console.Error.WriteLine("       epubfixer fix <book.epub> -o <book.fixed.epub> [--apply-ocr-corrections] [--ocr-mutation-report <report.md>]");
}

internal sealed record CliOptions(
    CliCommand Command,
    string EpubPath,
    string? OutputEpubPath,
    string? DumpPath,
    string? HyphenReportPath,
    string? HyphenAnalysisReportPath,
    string? PostFixLexiconReportPath,
    string? TrMorphReportPath,
    string? OcrReportPath,
    string? OcrCorrectionReportPath,
    string? OcrDecisionReportPath,
    string? GroundTruthPath,
    bool ApplyInline,
    bool ApplyParagraph)
{
    public bool ApplyOcrCorrections { get; init; }
    public string? OcrMutationReportPath { get; init; }
    public static bool TryParse(string[] arguments, out CliOptions options)
    {
        options = null!;

        if (arguments.Length < 2 || string.IsNullOrWhiteSpace(arguments[1]))
        {
            return false;
        }

        if (string.Equals(arguments[0], "fix", StringComparison.OrdinalIgnoreCase))
        {
            if (arguments.Length < 4
                || !string.Equals(arguments[2], "-o", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(arguments[3]))
            {
                return false;
            }

            var applyOcr = false;
            string? mutationReport = null;
            var fixIndex = 4;
            while (fixIndex < arguments.Length)
            {
                if (string.Equals(arguments[fixIndex], "--apply-ocr-corrections", StringComparison.OrdinalIgnoreCase) && !applyOcr)
                { applyOcr = true; fixIndex++; continue; }
                if (string.Equals(arguments[fixIndex], "--ocr-mutation-report", StringComparison.OrdinalIgnoreCase)
                    && mutationReport is null && fixIndex + 1 < arguments.Length && !string.IsNullOrWhiteSpace(arguments[fixIndex + 1]))
                { mutationReport = arguments[fixIndex + 1]; fixIndex += 2; continue; }
                return false;
            }
            if (mutationReport is not null && !applyOcr) return false;
            options = new CliOptions(
                CliCommand.Fix,
                arguments[1],
                arguments[3],
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                false,
                false)
            { ApplyOcrCorrections = applyOcr, OcrMutationReportPath = mutationReport };
            return true;
        }

        if (!string.Equals(arguments[0], "analyze", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string? dumpPath = null;
        string? hyphenReportPath = null;
        string? hyphenAnalysisReportPath = null;
        string? postFixLexiconReportPath = null;
        string? trMorphReportPath = null;
    string? ocrReportPath = null;
        string? ocrCorrectionReportPath = null;
        string? ocrDecisionReportPath = null;
        string? groundTruthPath = null;
        var applyInline = false;
        var applyParagraph = false;
        var index = 2;

        while (index < arguments.Length)
        {
            var option = arguments[index];

            if (string.Equals(option, "--apply-inline", StringComparison.OrdinalIgnoreCase))
            {
                if (applyInline)
                {
                    return false;
                }

                applyInline = true;
                index++;
                continue;
            }

            if (string.Equals(option, "--apply-paragraph", StringComparison.OrdinalIgnoreCase))
            {
                if (applyParagraph)
                {
                    return false;
                }

                applyParagraph = true;
                index++;
                continue;
            }

            if (index + 1 >= arguments.Length
                || string.IsNullOrWhiteSpace(arguments[index + 1])
                || arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                return false;
            }

            var value = arguments[index + 1];

            if (string.Equals(option, "--dump-text", StringComparison.OrdinalIgnoreCase))
            {
                if (dumpPath is not null)
                {
                    return false;
                }

                dumpPath = value;
            }
            else if (string.Equals(option, "--hyphen-report", StringComparison.OrdinalIgnoreCase))
            {
                if (hyphenReportPath is not null)
                {
                    return false;
                }

                hyphenReportPath = value;
            }
            else if (string.Equals(option, "--hyphen-analysis-report", StringComparison.OrdinalIgnoreCase))
            {
                if (hyphenAnalysisReportPath is not null)
                {
                    return false;
                }

                hyphenAnalysisReportPath = value;
            }
            else if (string.Equals(option, "--post-fix-lexicon-report", StringComparison.OrdinalIgnoreCase))
            {
                if (postFixLexiconReportPath is not null)
                {
                    return false;
                }

                postFixLexiconReportPath = value;
            }
            else if (string.Equals(option, "--trmorph-report", StringComparison.OrdinalIgnoreCase))
            {
                if (trMorphReportPath is not null) return false;
                trMorphReportPath = value;
            }
            else if (string.Equals(option, "--ocr-report", StringComparison.OrdinalIgnoreCase))
            {
                if (ocrReportPath is not null) return false;
                ocrReportPath = value;
            }
            else if (string.Equals(option, "--ocr-correction-report", StringComparison.OrdinalIgnoreCase))
            {
                if (ocrCorrectionReportPath is not null) return false;
                ocrCorrectionReportPath = value;
            }
            else if (string.Equals(option, "--ocr-decision-report", StringComparison.OrdinalIgnoreCase))
            {
                if (ocrDecisionReportPath is not null) return false;
                ocrDecisionReportPath = value;
            }
            else if (string.Equals(option, "--ground-truth", StringComparison.OrdinalIgnoreCase))
            {
                if (groundTruthPath is not null) return false;
                groundTruthPath = value;
            }
            else
            {
                return false;
            }

            index += 2;
        }

        if ((trMorphReportPath is null) != (groundTruthPath is null)) return false;

        options = new CliOptions(
            CliCommand.Analyze,
            arguments[1],
            null,
            dumpPath,
            hyphenReportPath,
            hyphenAnalysisReportPath,
            postFixLexiconReportPath,
            trMorphReportPath,
            ocrReportPath,
            ocrCorrectionReportPath,
            ocrDecisionReportPath,
            groundTruthPath,
            applyInline,
            applyParagraph);
        return true;
    }
}

internal enum CliCommand
{
    Analyze,
    Fix
}

internal sealed record CandidateKey(
    string LeftPart,
    string RightPart,
    string UnhyphenatedText);

internal sealed record CandidateAggregate(CandidateKey Key, int Count);

internal sealed record HyphenationPipelineResult(
    LogicalTextStream LogicalText,
    BookLexicon Lexicon,
    IReadOnlyList<HyphenationEvidence> Evidence,
    IReadOnlyList<HyphenationCandidate> Candidates,
    IReadOnlyList<HyphenationDecision> Decisions,
    IReadOnlyList<HyphenationCorrectionPlan> Plans);

internal sealed record PostFixLexiconAnalysisResult(
    HyphenationPipelineResult Original,
    IReadOnlyList<HyphenationCorrectionPlan> InlinePlans,
    HyphenationCorrectionApplyResult InlineApplyResult,
    HyphenationPipelineResult AfterInline,
    IReadOnlyList<HyphenationCorrectionPlan> CrossParagraphPlans,
    HyphenationCorrectionApplyResult CrossParagraphApplyResult,
    HyphenationPipelineResult Final);
