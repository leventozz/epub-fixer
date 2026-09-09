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

return Run(args);

static int Run(string[] arguments)
{
    if (!CliOptions.TryParse(arguments, out var options))
    {
        PrintUsage();
        return 1;
    }

    try
    {
        ValidateOutputPaths(options);

        var package = new EpubPackageReader().Read(options.EpubPath);
        var candidates = new HyphenationDetector().Detect(package.LogicalText);
        var lexicon = new BookLexiconBuilder().Build(package.LogicalText);
        var evidence = new HyphenationEvidenceEvaluator().Evaluate(
            candidates,
            lexicon,
            package.LogicalText);
        var evidenceSummary = HyphenationEvidenceReporting.CreateSummary(evidence);
        var decisions = new HyphenationDecisionEvaluator().Evaluate(evidence);
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

        if (options.ApplyInline)
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
        or UnauthorizedAccessException)
    {
        Console.Error.WriteLine($"Error: {exception.Message}");
        return 2;
    }
}

static void ValidateOutputPaths(CliOptions options)
{
    var outputPaths = new[]
    {
        options.DumpPath,
        options.HyphenReportPath,
        options.HyphenAnalysisReportPath
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
    Console.Error.WriteLine(
        "Usage: epubfixer analyze <book.epub> "
        + "[--apply-inline] "
        + "[--dump-text <output.txt>] [--hyphen-report <hyphens.json>] "
        + "[--hyphen-analysis-report <analysis.md>]");
}

internal sealed record CliOptions(
    string EpubPath,
    string? DumpPath,
    string? HyphenReportPath,
    string? HyphenAnalysisReportPath,
    bool ApplyInline)
{
    public static bool TryParse(string[] arguments, out CliOptions options)
    {
        options = null!;

        if (arguments.Length < 2
            || !string.Equals(arguments[0], "analyze", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(arguments[1]))
        {
            return false;
        }

        string? dumpPath = null;
        string? hyphenReportPath = null;
        string? hyphenAnalysisReportPath = null;
        var applyInline = false;
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
            else
            {
                return false;
            }

            index += 2;
        }

        options = new CliOptions(
            arguments[1],
            dumpPath,
            hyphenReportPath,
            hyphenAnalysisReportPath,
            applyInline);
        return true;
    }
}

internal sealed record CandidateKey(
    string LeftPart,
    string RightPart,
    string UnhyphenatedText);

internal sealed record CandidateAggregate(CandidateKey Key, int Count);
