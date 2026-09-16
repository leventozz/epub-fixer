# Baselines

Bu klasordeki dosyalar Faz 0 olcumlerinin anlik kayitlaridir. Metrik tanimi degisirse baseline
gecersizdir ve yeni tanimla yeniden uretilmelidir.

## Book health definition v2

- `totalTokens`: `WordTokenizer().Tokenize(stream).Count`.
- Normalizasyon: NFC + `ToLower(tr-TR)`.
- `unresolvableTokens`: normalize token ve kesme isareti varsa kok parcasi `tr_50k` veya TRmorph
  tarafindan taninmiyorsa sayilir.
- `suspiciousTokens`: bosluksuz ham kelimelerde gomulu rakam, garbage glyph, ic/onde cift
  noktalama ve komsu tek karakterli `i/ı/l/1/I/İ` kurallariyla sayilir.
  Ham kelimeler TextNode sinirinda devam eder; Paragraph ve Document sinirinda kesilir.
- `unresolvableRate`: 1000 token basina cozumlenemeyen token.
- `worstExamples`: cozumlenemeyen yuzey bicimleri; frekans azalan, esitlikte ordinal artan.

## Commands

- `dotnet run --project src/EpubFixer.Cli -- measure test-data/odun-kesmek/input.epub`
- `dotnet run --project src/EpubFixer.Cli -- debug-vocabulary test-data/odun-kesmek/input.epub --json docs/baselines/odun-kesmek.vocabulary.json`
- `dotnet run --project benchmarks/EpubFixer.QualityBenchmarks -- test-data/odun-kesmek`
- `dotnet test tests/EpubFixer.Tests --filter "FullyQualifiedName~MorphologyCallTraceTests" --logger "console;verbosity=detailed"`

## Morphology baseline

- `odun-kesmek.morphology.json`: `fix --apply-ocr-corrections` kosusu icin Faz 1 oncesi ve
  sonrasi morfoloji cagri profili. Bu dosya wall-clock hizlanma iddiasi degildir; ayni makinede
  sicak Debug kosularinda olculen sonuc hizlanma olmadigini gosterir.
- Faz 1 sonrasi profil: 4 stage-level batch request, 0 hot-loop `IsValidWord`/`Analyze`
  cagrisi. Faz 1 oncesi profil: 13.177 batch request, 40.224 hit.
- Faz 1'in performans degeri wall-clock kazanci degil, morfolojinin politika sicak dongusunden
  cikarilmasi ve ikinci kosuda disk cache ile flookup'in hic baslamamasidir.
- `goldenLogicalTextSha256`: EPUB zip dosyasinin degil, yazilan paketin
  `LogicalTextStreamBuilder.Build(package.SpineDocuments).Text` degerinin SHA-256 hash'idir.

## Phase 2 vocabulary and language-model baselines

- `odun-kesmek.vocabulary.json`: kitap haznesinin boyutu, kaynak kirilimi, held-out kapsama,
  hedef kapsamasi, supheli girdi sayisi ve ornek kitap girdileri.
- `odun-kesmek.language-model.json`: ayni temiz token akisi uzerinden kurulan unigram/bigram
  tur ve token sayilari ile held-out bigram isabet orani. Stupid backoff normalize olasilik
  olmadigi icin perplexity raporlanmaz.
- `odun-kesmek.lattice.json`: Faz 3 lattice motorunun full-book debug kosusu. Exact decoder
  state birlestirme ve lattice fanout sinirlari sonrasi gecerlidir; kosu 563 OCR region icin
  30 saniye butcesinin altinda tamamlanir ve gerekce histogramini raporlar.

## Phase 4 engine baselines

- `odun-kesmek.fix-legacy.json`: `fix --apply-ocr-corrections` legacy motorunun uyguladigi 120
  mutation'in imzasi. `OcrMutationBaselineTests.FullBookFix_LegacyEngineMutationProfileIsPinned`
  bu dosyaya karsi dogrular.
- `odun-kesmek.fix-lattice.json`: ayni kosunun `--ocr-engine lattice` ile hali (10 mutation).
  **Olcum kaydidir; uretim varsayilani legacy'dir (D60/D65).**
- `odun-kesmek.engine-diff.json`: iki motorun uc yonlu farki (KAZANC 9 / KAYIP 119 / CATISMA 1),
  kitap sagligi karsilastirmasi ve `rawVersusProduction` bulgusu (D66). R4.3'un alt kume
  kanitinin girdisidir (D67).
- Baseline'lari yeniden uretmek icin:
  `EPUBFIXER_UPDATE_BASELINES=1 dotnet test tests/EpubFixer.Tests --filter "FullyQualifiedName~OcrMutationBaselineTests"`

## H3 hybrid engine baseline (surum 2, M2)

- `odun-kesmek.fix-hybrid.json`: `fix --apply-ocr-corrections` hibrit (`CompositeOcrCorrectionPlanner`)
  motorunun uyguladigi **129 mutation**'in imzasi (120 legacy + 9 lattice KAZANC, tek catisma
  D83 geregi legacy'ye gitti - roadmap bolum 3.1'in tahminiyle birebir eslesir).
  `OcrMutationBaselineTests.FullBookFix_HybridEngineMutationProfileIsPinned` bu dosyaya karsi
  dogrular ve ayni kosunun cikti epub'u uzerinde kitap sagligini in-process olcer (Console.WriteLine
  ile: `HybridBookHealth.*`) - ikinci bir tam-kitap `fix`+`measure` kosusuna gerek kalmadan.
  Olculen kitap sagligi: totalTokens 46656, unresolvableTokens 2666, suspiciousTokens 166,
  unresolvableRatePer1000 57.14 - legacy'den (57.30, `odun-kesmek.engine-diff.json`) biraz daha iyi,
  cunku 9 lattice KAZANCI dogru duzeltmeler.
  Süre (ayni oturumda, `PerformanceBudgetTests.FullBookHybridFixStaysWithinBudgetOfLegacy`):
  legacy 25.62s, hybrid 44.57s - hem 120s sabit kapiyi hem legacy+35s (D61) yumusak kapiyi geciyor.
  Benchmark (`--ocr-engine hybrid`) sonucu ve kalite kapisi FAIL/PASS detayi:
  `docs/baselines/quality-gate.json` -> `current.ocrStageMeasurement.hybrid`.

