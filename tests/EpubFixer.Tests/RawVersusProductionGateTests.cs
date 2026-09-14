using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using EpubFixer.Adapters.Lexicon;
using EpubFixer.Adapters.Ocr.Lattice;
using EpubFixer.Core.Epub;
using EpubFixer.Core.Fix;
using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Morphology;
using EpubFixer.Core.Ocr;
using EpubFixer.Core.Ocr.Lattice;
using EpubFixer.Core.Ocr.Lattice.Models;
using EpubFixer.TrMorph;
using EpubFixer.Tests.LossTaxonomy;

namespace EpubFixer.Tests;

/// <summary>
/// R5.0c (docs/phase-5-plan.md section 7.3): D66 left one question open. The corrupted fragment
/// "kol-1 ıı kta" gets Leave/TooManyOrdinaryEdits in Faz 3's raw (pre-hyphenation) lattice run
/// (docs/baselines/odun-kesmek.lattice.md line 451) but Apply/Accepted in the production run
/// (post-hyphenation stream, docs/baselines/odun-kesmek.fix-lattice.json / engine-diff.json's
/// conflict item). D65 explains why the CORRECT answer ("koltukta") can never be produced
/// (MaxArcLength=11 &lt; the 12-char span) but not why the gate ACCEPTED a wrong one.
///
/// This test drives the real production lattice pipeline twice - once over the untouched raw
/// stream (exactly debug-lattice's setup), once over the post-hyphenation stream EpubFixService
/// actually feeds the OCR stage (exactly LossTaxonomyBaselineTests' setup, reusing
/// <see cref="DiagnosticLatticeOcrCorrectionPlanner"/> from R5.0b per the plan's "reuse it"
/// instruction) - and compares the two runs' lattice, decoded paths and OrdinaryEdits computation
/// for this one region side by side. It changes nothing in src/; it only reads and pins what the
/// real engine already does.
///
/// Finding (see the pinned assertions below and docs/baselines/odun-kesmek.loss-taxonomy.json's
/// "rawVersusProduction" node for the full writeup): the local window text and the full set of
/// candidate word-arcs (koli, açıkta, nokta - with identical per-arc edit costs) are BYTE-IDENTICAL
/// between the two runs. What differs is which arc-segmentation the decoder ranks #1, because the
/// language model's bigram score for the continuation "berjer" -&gt; "kol" collapses between runs
/// (see the LogProbability assertions): the raw corpus still contains other un-hyphenation-fixed
/// occurrences of the same repeating "berjer kol[-...]tukta" OCR defect, which inflate that
/// bigram's count; production's hyphenation passes clean most of them into "berjer koltukta"
/// before the OCR stage ever runs, starving the bigram. That cost swing (not any change in
/// vocabulary candidate membership, matcher behavior, edit-cost tables, or gate logic) is what
/// flips the decoder's Top-1 segmentation from "kol" + "-" + "açıkta" (2 ordinary edits, rejected)
/// to "koli" + " " + "nokta" (1 ordinary edit, accepted).
/// </summary>
public sealed class RawVersusProductionGateTests
{
    private const string MeasuredOn = "2026-09-14";
    private const string Fragment = "kol-1 ıı kta";
    private const string ExpectedWindow = "berjer kol-1 ıı kta, yirmi";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    [Trait("Category", "Slow")]
    public void RawVersusProductionGate_ExplainsWhyProductionAcceptedWhatRawRejected()
    {
        var input = FindRepositoryFile(Path.Combine("test-data", "odun-kesmek", "input.epub"));
        var baselinePath = FindRepositoryFile(Path.Combine("docs", "baselines", "odun-kesmek.loss-taxonomy.json"));
        var output = Path.Combine(Path.GetTempPath(), $"epubfixer-raw-vs-prod-{Guid.NewGuid():N}.epub");
        var frequencyList = FileTurkishFrequencyListSource.Load();
        var options = new LatticeOptions();

        // RAW run: the untouched pre-hyphenation stream, exactly what debug-lattice (Faz 3) used.
        var rawPackage = new EpubPackageReader().Read(input);
        var rawPlanner = new DiagnosticLatticeOcrCorrectionPlanner(frequencyList, v => new SymSpellLexiconMatcher(v), options);
        using (var rawAnalyzer = new FomaTurkishMorphologyAnalyzer())
        {
            rawPlanner.CreatePlan(rawPackage.LogicalText, new BatchMorphologyOracleBuilder(rawAnalyzer));
        }

        // PRODUCTION run: the exact post-hyphenation stream EpubFixService feeds the OCR stage
        // (D66) - same setup LossTaxonomyBaselineTests uses for the other 119 loss cases.
        var productionPlanner = new DiagnosticLatticeOcrCorrectionPlanner(frequencyList, v => new SymSpellLexiconMatcher(v), options);
        try
        {
            using var productionAnalyzer = new FomaTurkishMorphologyAnalyzer();
            new EpubFixService(new BatchMorphologyOracleBuilder(productionAnalyzer), productionPlanner)
                .Fix(input, output, applyOcrCorrections: true);
        }
        finally
        {
            if (File.Exists(output))
            {
                File.Delete(output);
            }
        }

        var rawDecision = rawPlanner.CapturedDecisions.SingleOrDefault(d => d.Region.RawText == Fragment);
        var productionDecision = productionPlanner.CapturedDecisions.SingleOrDefault(d => d.Region.RawText == Fragment);
        Assert.NotNull(rawDecision);
        Assert.NotNull(productionDecision);

        // The local window (the corrupted fragment plus its one word of context on each side) is
        // byte-identical in both runs: this is the same physical text, not a shifted/rewritten span.
        Assert.Equal(ExpectedWindow, rawDecision!.Lattice.Window);
        Assert.Equal(ExpectedWindow, productionDecision!.Lattice.Window);
        Assert.Equal(LatticeBuildOutcome.Built, rawDecision.Lattice.Outcome);
        Assert.Equal(LatticeBuildOutcome.Built, productionDecision.Lattice.Outcome);

        // D66's own recorded coordinates (engine-diff.json -> rawVersusProduction.productionRun).
        Assert.Equal("main-3.xhtml", DocumentPathOf(productionPlanner.CapturedStream!, productionDecision.Result));
        Assert.Equal(152467, productionDecision.Result.LogicalStart);

        // The gate verdicts D66 already measured (Leave/TooManyOrdinaryEdits vs Apply/Accepted).
        Assert.Equal(AcceptanceVerdict.Leave, rawDecision.Result.Verdict);
        Assert.Equal("TooManyOrdinaryEdits", Assert.Single(rawDecision.Result.Reasons));
        Assert.Equal(AcceptanceVerdict.Apply, productionDecision.Result.Verdict);
        Assert.Equal("Accepted", Assert.Single(productionDecision.Result.Reasons));
        Assert.Equal("koli nokta", productionDecision.Result.Replacement);

        // The decoder's Top-1 segmentation differs between runs for the identical window: raw
        // reconstructs "kol" + "-" + "açıkta"; production reconstructs "koli" + " " + "nokta".
        Assert.NotEmpty(rawDecision.Paths);
        Assert.NotEmpty(productionDecision.Paths);
        var rawTop1 = rawDecision.Paths[0];
        var productionTop1 = productionDecision.Paths[0];
        Assert.Equal("berjer kol-açıkta, yirmi", rawTop1.Text);
        Assert.Equal("berjer koli nokta, yirmi", productionTop1.Text);

        // Word-arc parity: the SAME candidate substitutions, at the SAME edit cost, exist in BOTH
        // lattices. This rules out "the matcher/vocabulary offered a different candidate" as the
        // mechanism - whatever changed, it isn't candidate availability.
        AssertSameCostArc(rawDecision.Lattice, productionDecision.Lattice, from: 7, to: 12, word: "koli", expectedCost: 0.5);
        AssertSameCostArc(rawDecision.Lattice, productionDecision.Lattice, from: 11, to: 19, word: "açıkta", expectedCost: 2.6);
        AssertSameCostArc(rawDecision.Lattice, productionDecision.Lattice, from: 13, to: 19, word: "nokta", expectedCost: 1.55);

        // OrdinaryEdits for each run's own Top-1 path, computed the same way
        // CorrectionAcceptanceGate.OrdinaryEdits does (reproduced verbatim below, diagnostic-only,
        // per the same pattern LossTaxonomyProbe.cs already uses for this class of test).
        var rawOrdinaryEdits = OrdinaryEdits(rawDecision.Lattice, rawTop1, options);
        var productionOrdinaryEdits = OrdinaryEdits(productionDecision.Lattice, productionTop1, options);
        Assert.Equal(2, rawOrdinaryEdits);
        Assert.Equal(1, productionOrdinaryEdits);
        Assert.True(rawOrdinaryEdits > options.MaxOrdinarySubstitutions, "Raw run's own Top-1 must exceed the gate's threshold (that's why it Leaves).");
        Assert.True(productionOrdinaryEdits <= options.MaxOrdinarySubstitutions, "Production run's own Top-1 must be within the gate's threshold (that's why it Applies).");

        // The mechanism: rebuild BookKnowledge (deterministic per rule 4.7 - same stream, same
        // frequency list, same morphology oracle answers) purely to reach each run's
        // ILanguageModel (DiagnosticLatticeOcrCorrectionPlanner does not expose it), then decode
        // the SAME captured, already-built lattices again. This must reproduce the identical Top-1
        // as above (asserted below) before its language-model numbers can be trusted.
        using var rebuildAnalyzerRaw = new FomaTurkishMorphologyAnalyzer();
        var rawKnowledge = new BookKnowledgeBuilder(frequencyList).Build(rawPackage.LogicalText, new BatchMorphologyOracleBuilder(rebuildAnalyzerRaw));
        using var rebuildAnalyzerProduction = new FomaTurkishMorphologyAnalyzer();
        var productionKnowledge = new BookKnowledgeBuilder(frequencyList).Build(productionPlanner.CapturedStream!, new BatchMorphologyOracleBuilder(rebuildAnalyzerProduction));

        var rawRedecoded = new LatticeDecoder(rawKnowledge.LanguageModel, options).Decode(rawDecision.Lattice, 3);
        var productionRedecoded = new LatticeDecoder(productionKnowledge.LanguageModel, options).Decode(productionDecision.Lattice, 3);
        Assert.Equal(rawTop1.Text, rawRedecoded[0].Text);
        Assert.Equal(rawTop1.Cost, rawRedecoded[0].Cost, precision: 6);
        Assert.Equal(productionTop1.Text, productionRedecoded[0].Text);
        Assert.Equal(productionTop1.Cost, productionRedecoded[0].Cost, precision: 6);

        // The smoking gun: the bigram continuation "berjer" -> "kol" that raw's winning path relies
        // on (to keep "kol" itself free of any edit) is well-attested in the raw corpus but nearly
        // unattested in production's - while the OTHER three bigrams both paths depend on
        // ("kol"->"açıkta", "berjer"->"koli", "koli"->"nokta") barely move between runs.
        var rawKolAfterBerjer = rawKnowledge.LanguageModel.LogProbability("kol", "berjer");
        var productionKolAfterBerjer = productionKnowledge.LanguageModel.LogProbability("kol", "berjer");
        Assert.Equal(-5.4337, rawKolAfterBerjer, precision: 4);
        Assert.Equal(-11.6939, productionKolAfterBerjer, precision: 4);
        Assert.True(productionKolAfterBerjer - rawKolAfterBerjer < -5, "'kol' after 'berjer' must be dramatically less probable in production than in raw.");

        var rawAciktaAfterKol = rawKnowledge.LanguageModel.LogProbability("açıkta", "kol");
        var productionAciktaAfterKol = productionKnowledge.LanguageModel.LogProbability("açıkta", "kol");
        var rawKoliAfterBerjer = rawKnowledge.LanguageModel.LogProbability("koli", "berjer");
        var productionKoliAfterBerjer = productionKnowledge.LanguageModel.LogProbability("koli", "berjer");
        var rawNoktaAfterKoli = rawKnowledge.LanguageModel.LogProbability("nokta", "koli");
        var productionNoktaAfterKoli = productionKnowledge.LanguageModel.LogProbability("nokta", "koli");
        Assert.True(Math.Abs(rawAciktaAfterKol - productionAciktaAfterKol) < 0.001, "'açıkta' after 'kol' should be essentially unchanged between runs.");
        Assert.Equal(rawKoliAfterBerjer, productionKoliAfterBerjer, precision: 6);
        Assert.Equal(rawNoktaAfterKoli, productionNoktaAfterKoli, precision: 6);

        // Supporting corpus evidence for WHY the bigram starved: hyphenation correction (which
        // runs before the OCR stage, D66) resolves other occurrences of the SAME repeating
        // "berjer kol[-...]tukta" OCR defect elsewhere in the book into clean "berjer koltukta"
        // text, one book-count point moving from the standalone token "kol" onto "koltukta".
        var rawKolEntry = rawPlanner.CapturedVocabulary!.Find("kol");
        var productionKolEntry = productionPlanner.CapturedVocabulary!.Find("kol");
        var rawKoltuktaEntry = rawPlanner.CapturedVocabulary!.Find("koltukta");
        var productionKoltuktaEntry = productionPlanner.CapturedVocabulary!.Find("koltukta");
        Assert.NotNull(rawKolEntry);
        Assert.NotNull(productionKolEntry);
        Assert.NotNull(rawKoltuktaEntry);
        Assert.NotNull(productionKoltuktaEntry);
        Assert.Equal(2, rawKolEntry!.BookCount);
        Assert.Equal(1, productionKolEntry!.BookCount);
        Assert.Equal(220, rawKoltuktaEntry!.BookCount);
        Assert.Equal(221, productionKoltuktaEntry!.BookCount);

        // Pin the writeup into docs/baselines/odun-kesmek.loss-taxonomy.json's rawVersusProduction
        // node (added field; every other field in the file is left untouched).
        var finding = new JsonObject
        {
            ["status"] = "explained",
            ["measuredOn"] = MeasuredOn,
            ["documentPath"] = "main-3.xhtml",
            ["fragment"] = Fragment,
            ["window"] = ExpectedWindow,
            ["question"] = "D66: aynı 'kol-1 ıı kta' parçası ham koşuda Leave/TooManyOrdinaryEdits, üretim koşusunda Apply/Accepted alıyor. MaxArcLength (D65) doğru cevabın (koltukta) üretilememesini açıklıyor; bu düğüm kapının neden KABUL ettiğini açıklıyor.",
            ["mechanism"] =
                "Pencere metni ve aday kelime-ark kümesi (koli, açıkta, nokta - üçü de aynı edit " +
                "maliyetiyle) iki koşuda birebir aynı: fark matcher/vocabulary aday kümesinden, " +
                "edit-maliyet tablosundan ya da kapının kendi mantığından gelmiyor. Fark, decoder'ın " +
                "aynı pencereyi HANGİ ark dizilimine böldüğünde: ham koşu 'kol' + '-' + 'açıkta' " +
                "dizilimini (2 ordinary edit -> TooManyOrdinaryEdits) en ucuz buluyor, üretim koşusu " +
                "'koli' + ' ' + 'nokta' dizilimini (1 ordinary edit -> Accepted) en ucuz buluyor. Bu " +
                "ayrım dil modelinin 'berjer'->'kol' bigram olasılığından geliyor: ham hazne bu " +
                "bigramı iyi destekliyor (logProb=-5.4337) çünkü kitaptaki AYNI tekrarlayan " +
                "'berjer kol[-...]tukta' OCR bozukluğunun düzeltilmemiş diğer örnekleri hâlâ ham " +
                "gövdede duruyor; üretim koşusunda bu örneklerin çoğu OCR aşamasından ÖNCE çalışan " +
                "tirelemeler tarafından zaten 'berjer koltukta'ya düzeltilmiş olduğundan bigram " +
                "neredeyse hiç kanıtlanmamış hâle geliyor (logProb=-11.6939, aynı pencerede " +
                "'açıkta'nın 'kol'dan sonraki olasılığıyla aynı seviyeye düşüyor). Kanıt: hazne " +
                "'kol' BookCount'u 2 (ham) -> 1 (üretim), 'koltukta' BookCount'u 220 (ham) -> 221 " +
                "(üretim) - tam olarak bir tekrarlanan kusur örneğinin 'kol'dan 'koltukta'ya taşındığını " +
                "gösteriyor. Yol boyunca kullanılan diğer üç bigram ('kol'->'açıkta', 'berjer'->'koli', " +
                "'koli'->'nokta') iki koşu arasında pratikte değişmiyor (fark < 0.001).",
            ["conclusion"] =
                "İkinci koşul (D65'in tek başına açıklamadığı 'kapı neden kabul etti' sorusu) " +
                "kapının bir gevşemesi DEĞİL: kapı her iki koşuda da AYNI eşiği (MaxOrdinarySubstitutions=1) " +
                "aynı biçimde uyguluyor. Değişen şey decoder'a giden dil modelinin, OCR aşamasından " +
                "önce çalışan tirelemelerin kitabı bir bütün olarak değiştirmesinden dolayı farklı " +
                "bir korpus üzerinde eğitilmiş olması - bu da decoder'ın aynı bozuk pencere için " +
                "farklı bir en-ucuz-yol seçmesine yol açıyor.",
            ["maxArcLengthGrowthBlockedByD75"] = false,
            ["note"] =
                "D75'in bloke koşulu ('R5.0c açıklanamadıysa') burada gerçekleşmedi: mekanizma tek " +
                "ve doğrulanmış. Ama bulgu R5.4b için bir uyarı taşıyor: kapının kabul/red kararı " +
                "decoder'ın MaxOrdinarySubstitutions eşiği etrafındaki dar bir maliyet yarışına " +
                "bağlı, ve bu yarış hyphenation'ın korpusu değiştirmesiyle KAYABILIYOR. MaxArcLength " +
                "büyütülüp 'koltukta' tek/çoklu ark olarak kafese girebilir hâle gelse bile, hangi " +
                "yolun Top-1 olacağı yine dil modelinin o koşudaki korpusuna bağlı olacaktır - " +
                "R5.4b'nin süpürmesi bunu ayrı bir boyut olarak ele almalıdır (yalnızca eşik değil, " +
                "hazne/LM'in hangi metinden kurulduğu).",
            ["evidence"] = new JsonObject
            {
                ["rawRun"] = new JsonObject
                {
                    ["verdict"] = rawDecision.Result.Verdict.ToString(),
                    ["reason"] = rawDecision.Result.Reasons[0],
                    ["top1Path"] = rawTop1.Text,
                    ["top1Cost"] = Math.Round(rawTop1.Cost, 4),
                    ["ordinaryEdits"] = rawOrdinaryEdits
                },
                ["productionRun"] = new JsonObject
                {
                    ["verdict"] = productionDecision.Result.Verdict.ToString(),
                    ["reason"] = productionDecision.Result.Reasons[0],
                    ["replacement"] = productionDecision.Result.Replacement,
                    ["top1Path"] = productionTop1.Text,
                    ["top1Cost"] = Math.Round(productionTop1.Cost, 4),
                    ["ordinaryEdits"] = productionOrdinaryEdits
                },
                ["maxOrdinarySubstitutions"] = options.MaxOrdinarySubstitutions,
                ["wordArcParity"] = new JsonArray(
                    ArcParityNode(rawDecision.Lattice, 7, 12, "koli"),
                    ArcParityNode(rawDecision.Lattice, 11, 19, "açıkta"),
                    ArcParityNode(rawDecision.Lattice, 13, 19, "nokta")),
                ["languageModelLogProbability"] = new JsonObject
                {
                    ["kol_after_berjer"] = new JsonObject { ["raw"] = Math.Round(rawKolAfterBerjer, 4), ["production"] = Math.Round(productionKolAfterBerjer, 4) },
                    ["açıkta_after_kol"] = new JsonObject { ["raw"] = Math.Round(rawAciktaAfterKol, 4), ["production"] = Math.Round(productionAciktaAfterKol, 4) },
                    ["koli_after_berjer"] = new JsonObject { ["raw"] = Math.Round(rawKoliAfterBerjer, 4), ["production"] = Math.Round(productionKoliAfterBerjer, 4) },
                    ["nokta_after_koli"] = new JsonObject { ["raw"] = Math.Round(rawNoktaAfterKoli, 4), ["production"] = Math.Round(productionNoktaAfterKoli, 4) }
                },
                ["vocabularyBookCount"] = new JsonObject
                {
                    ["kol"] = new JsonObject { ["raw"] = rawKolEntry.BookCount, ["production"] = productionKolEntry.BookCount },
                    ["koltukta"] = new JsonObject { ["raw"] = rawKoltuktaEntry.BookCount, ["production"] = productionKoltuktaEntry.BookCount }
                }
            }
        };

        var actualNode = JsonNode.Parse(finding.ToJsonString(JsonOptions))!;
        if (string.Equals(Environment.GetEnvironmentVariable("EPUBFIXER_UPDATE_BASELINES"), "1", StringComparison.Ordinal))
        {
            var document = JsonNode.Parse(File.ReadAllText(baselinePath))!.AsObject();
            document["rawVersusProduction"] = actualNode.DeepClone();
            File.WriteAllText(baselinePath, document.ToJsonString(JsonOptions) + Environment.NewLine);
        }

        var persisted = JsonNode.Parse(File.ReadAllText(baselinePath))!.AsObject();
        Assert.True(persisted.ContainsKey("rawVersusProduction"), "loss-taxonomy.json must carry a rawVersusProduction node (R5.0c).");
        Assert.Equal(actualNode.ToJsonString(JsonOptions), persisted["rawVersusProduction"]!.ToJsonString(JsonOptions));
    }

    /// <summary>
    /// Reproduces <c>CorrectionAcceptanceGate.OrdinaryEdits</c> (private) verbatim for diagnostic
    /// purposes only - same pattern <see cref="LossTaxonomyProbe"/> already uses in this test
    /// project. Sums <see cref="EditAlignment.OrdinaryEdits"/> over every Word-kind arc on the
    /// path; Identity/Literal arcs are kept as-is and contribute no edits by definition.
    /// </summary>
    private static int OrdinaryEdits(WordLattice lattice, DecodedPath path, LatticeOptions options)
    {
        var aligner = new WeightedEditAligner();
        var total = 0;
        foreach (var arc in path.Arcs.Where(arc => arc.Kind == LatticeArcKind.Word))
        {
            var source = lattice.Window.Substring(arc.From, arc.To - arc.From);
            Assert.True(aligner.TryAlign(source, arc.Word, options.BudgetCap, out var alignment), $"Top-1 path arc '{source}'->'{arc.Word}' must align within budget.");
            total += alignment.OrdinaryEdits;
        }

        return total;
    }

    private static void AssertSameCostArc(WordLattice raw, WordLattice production, int from, int to, string word, double expectedCost)
    {
        var rawArc = raw.Arcs.SingleOrDefault(a => a.Kind == LatticeArcKind.Word && a.From == from && a.To == to && a.Word == word);
        var productionArc = production.Arcs.SingleOrDefault(a => a.Kind == LatticeArcKind.Word && a.From == from && a.To == to && a.Word == word);
        Assert.True(rawArc is not null, $"Raw lattice must offer arc [{from}->{to}] '{word}'.");
        Assert.True(productionArc is not null, $"Production lattice must offer arc [{from}->{to}] '{word}'.");
        Assert.Equal(expectedCost, rawArc!.Cost, precision: 4);
        Assert.Equal(expectedCost, productionArc!.Cost, precision: 4);
    }

    private static JsonObject ArcParityNode(WordLattice lattice, int from, int to, string word)
    {
        var arc = lattice.Arcs.Single(a => a.Kind == LatticeArcKind.Word && a.From == from && a.To == to && a.Word == word);
        return new JsonObject
        {
            ["from"] = from,
            ["to"] = to,
            ["sourceSpan"] = lattice.Window[from..to],
            ["word"] = word,
            ["cost"] = Math.Round(arc.Cost, 4)
        };
    }

    private static string DocumentPathOf(EpubFixer.Core.Epub.Models.LogicalTextStream stream, AcceptanceResult result) =>
        stream.GetSourceLocationAt(result.LogicalStart).DocumentPath;

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
}
