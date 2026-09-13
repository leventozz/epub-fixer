# Faz 4 — Üretim hattına bağlama — Uygulama planı

> Bu belge [ocr-correction-roadmap.md](ocr-correction-roadmap.md) Faz 4'ün (R4.1–R4.3) uygulama
> planıdır. Her kalem ayrı bir oturumda ayrı bir agent'a devredilebilir; bölüm 12'de kopyala-yapıştır
> devir promptları vardır. Önceki fazların planları: [phase-0-plan.md](phase-0-plan.md),
> [phase-1-plan.md](phase-1-plan.md), [phase-2-plan.md](phase-2-plan.md),
> [phase-3-plan.md](phase-3-plan.md).
>
> Faz 4'ün tek işi vardır: **Faz 3'te ölçülen lattice motorunu `fix --apply-ocr-corrections`
> hattına, çıktının doğruluğunu her adımda kanıtlayarak bağlamak.** Faz 3 hiçbir EPUB'a
> dokunmuyordu; Faz 4 dokunuyor. Bu yüzden yol haritasının **en riskli** fazıdır: burada bir hata
> kullanıcının kitabına yazılır.
>
> Fazın yönetici ilkesi: **önce dikiş, sonra davranış.** Motor değişimi tek bir commit'te olmaz.
> Önce üretim hattının bugünkü çıktısı sayıyla kilitlenir (R4.0), sonra geometri yazılır (R4.1),
> sonra port açılır ve eski motor portun arkasına konur — çıktı **bit düzeyinde aynı** kalır
> (R4.2a), sonra yeni motor aynı portun arkasına takılır ama **kapalı** gelir (R4.2b), en sonunda
> anahtar çevrilir (R4.2c). Her adımda tüm suite yeşildir ve değişen tek şey o adımın kasten
> değiştirdiği şeydir.

---

## 1. Faz 4 bittiğinde elde ne olacak

| Çıktı | Dosya | Ne işe yarar |
|---|---|---|
| Üretim çıktısı sayaç kilidi | `tests/EpubFixer.Tests/OcrMutationBaselineTests.cs` | Motor değişiminin etkisi hash değil, **sayı** olarak görünür |
| Lattice süre testi | `tests/EpubFixer.Tests/PerformanceBudgetTests.cs` (yeni `[Fact]`) | D44'ün daralttığı sınırlar geri açılırken ilk kırılacak şey |
| Benchmark'ın OCR kolu | `benchmarks/EpubFixer.QualityBenchmarks/QualityBenchmarkRunner.cs` | Ground truth **OCR düzeltmelerini de** ölçer; D48 borcu burada kapanır |
| Bölge mutation geometrisi | `EpubFixer.Core/Mutation/RegionMutationPlanner.cs` | Region tabanlı düzeltme → mevcut `Applier`'ın anladığı plan |
| Mutation kökeni | `EpubFixer.Core/Mutation/Models/OcrMutationProvenance.cs` | Mutation artık `OcrCorrectionDecision`'a bağlı değil |
| Motor portu | `EpubFixer.Core/Ocr/IOcrCorrectionPlanner.cs` | `EpubFixService` politikası somut motoru görmez |
| Eski motor, port arkasında | `EpubFixer.Core/Ocr/LegacyOcrCorrectionPlanner.cs` | Bugünkü davranış, değişmeden, artık bir eklenti |
| Yeni motor, port arkasında | `EpubFixer.Core/Ocr/LatticeOcrCorrectionPlanner.cs` | Lattice motorunun üretim yüzü |
| Adapter katmanı | `src/EpubFixer.Adapters/` (yeni proje) | SymSpell ve dosya yükleme tek yerde; Cli ve Benchmarks aynı detayı paylaşır |
| CLI anahtarı | `fix ... [--ocr-engine lattice\|legacy]` | Üretim üzerinde A/B |
| Baseline'lar | `docs/baselines/odun-kesmek.fix-legacy.json`, `.fix-lattice.json` | Faz 5'in kalibrasyon girdisi |

**Faz 4'ün kabulü:**

1. `dotnet test EpubFixer.slnx` yeşil.
2. `fix --apply-ocr-corrections` **varsayılan olarak** lattice motorunu kullanır;
   `--ocr-engine legacy` ile eski davranışa dönülebilir.
3. Çıktı EPUB `EpubOutputValidator`'dan geçer; ZIP envanteri, mimetype ve dokunulmamış kaynaklar
   bit düzeyinde korunur.
4. `QualityBenchmark` full koşusu **OCR kolu açıkken** R0.4 kapısından geçer:
   `ProtectedViolated == 0`, `ProtectedChanged == 0`, `UnexpectedTextChanges == 0`,
   `NonTextChanges == 0`, precision ≥ profildeki eşik, recall ≥ profildeki eşik.
5. R0.1 kitap sağlık metriği (`measure`) lattice çıktısında legacy çıktısına göre
   **kötüleşmemiştir**; ölçülen değerler `docs/baselines/odun-kesmek.book-health.json`'a işlenir.
6. Full `fix --apply-ocr-corrections` koşusu ≤ 120 sn (mevcut hard gate) ve legacy toplamının
   **en fazla 35 sn üstünde** (D61).
7. Uygulanan her düzeltme elle incelenmiştir ve yanlış düzeltme sayısı **sıfırdır** (Faz 3
   kapanışındaki 31/31 doğru ölçümünün üretim hattındaki karşılığı).

**Faz 4'ün kapsamı dışı:** eşik kalibrasyonu (R5.4), öğrenilmiş maliyet tablosu (R5.1), trie
matcher (R5.2), diff raporu (R5.3), ikinci geçiş OCR (R6.1). Bunlar Faz 5'tir; Faz 4 boyunca
`LatticeOptions`'ın hiçbir değeri "iyileştirmek için" değiştirilmez.

---

## 2. Ön koşul

1. **Çalışma ağacı temiz olmalıdır.** Bu plan yazılırken `HEAD = 4ce5da9`
   ("docs: record phase 3 closing status and decisions D40-D48") ve ağaç temizdi. Bir R4 agent'ı
   kirli ağacın üstüne yazmaz; kirliyse durur ve bildirir.
2. **Faz 3 kapanış tablosu okunmuş olmalıdır** ([phase-3-plan.md](phase-3-plan.md) bölüm 1b ve 12).
   Açık kalan kabul maddeleri unutulmuş iş değil, bilinçli olarak bu faza devredilmiş ölçümlerdir.
3. `dotnet test EpubFixer.slnx` yeşil olmalıdır (Faz 3 kapanışında 436/436, ~1 dk 34 sn). Değilse
   önce bu düzeltilir; Faz 4 kırmızı suite üzerine kurulmaz.

---

## 3. Her kalem için geçerli ortak kurallar

Faz 0–3 kuralları aynen geçerlidir. Faz 4'e özgü eklemeler:

### 3.1 Test-first
Kırmızı test → minimum kod → refactor. Testi olmayan üretim kodu yazılmaz. Bu fazın ürünü bir
**geometri dönüşümü ve bir dikiş**tir; testlerin çoğu "şu logical aralık verildiğinde şu source
span'ler çıkar" ve "şu motor seçildiğinde şu plan üretilir" biçimindedir.

### 3.2 Katmanlar
- `EpubFixer.Core` politika kalır. Yeni Core dosyaları `System.IO`, `Process`, `ZipArchive`
  **kullanmaz**; `CoreLayeringTests`'in izin listesine **yeni dosya eklenmez**.
- `EpubFixer.Adapters` (R4.2b'de doğar) detay katmanıdır: SymSpell paketi, `tr_50k` dosya yükleme.
  Core, Adapters'ı **görmez**; bağımlılık yalnızca Adapters → Core yönündedir ve bu bir testtir.
- `EpubFixer.Cli` ve `EpubFixer.QualityBenchmarks` iki ayrı **composition root**'tur. İkisi de aynı
  motoru kurar; kurulum kodu **tek yerde** (Core'daki planner + Adapters'taki fabrika) yaşar, iki
  yerde kopyalanmaz.

### 3.3 Geriye dönük davranış korunur
`fix` komutunun bugünkü davranışı R4.2c'ye kadar **bit düzeyinde** korunur. Bunun kanıtı
`MorphologyCallTraceTests.FullBookFix_ProducesGoldenLogicalText` golden SHA-256'sıdır
([MorphologyCallTraceTests.cs:97](../tests/EpubFixer.Tests/MorphologyCallTraceTests.cs#L97)).
Bu hash R4.0, R4.1, R4.2a ve R4.2b'de **değişmez**. R4.2c'de kasten değişir ve yeni değeri
ölçülerek yazılır.

### 3.4 Sayı uydurulmaz (D41 aynen geçerli)
Ölçülemeyen alan sabitle doldurulmaz. Baseline dosyasında ölçülemeyen bir şey varsa
`status: "aborted"` + `reason` yazılır. Bu fazda geçerli olan özel hâli: **beklenen test değeri
tahminle yazılmaz.** Önce koşulur, çıkan sayı okunur, sonra teste yazılır ve o sayının neden o
olduğu bitiş raporunda açıklanır.

### 3.5 Emin olunmayan durumda "dokunma"
Bir bölge düzeltmesi geometriye çevrilemiyorsa, başka bir düzeltmeyle çakışıyorsa veya sonuç
orijinalle aynıysa **atlanır** — plan başarısız edilmez. Gerekçesi bölüm 7.4'tedir: plan başarısız
olursa `EpubFixService` exception atar ve **hiçbir düzeltme** uygulanmaz.

### 3.6 Determinizm
Aynı girdi → aynı plan → aynı çıktı EPUB → aynı SHA-256. Her sıralamada açık tie-break:
`DocumentPath` (`StringComparer.Ordinal`), sonra `LogicalStart`, sonra `LogicalEndExclusive`.
Sözlük/küme iterasyon sırasına hiçbir yerde güvenilmez.

### 3.7 Komutlar
```bash
dotnet build EpubFixer.slnx
dotnet test EpubFixer.slnx
dotnet test tests/EpubFixer.Tests --filter "FullyQualifiedName~RegionMutation"
dotnet test tests/EpubFixer.Tests --filter "Category=Slow"
dotnet run --project src/EpubFixer.Cli -- fix "test-data/odun-kesmek/input.epub" -o out.epub --apply-ocr-corrections --ocr-mutation-report reports/ocr-mutations.md
dotnet run --project src/EpubFixer.Cli -- fix "test-data/odun-kesmek/input.epub" -o out.epub --apply-ocr-corrections --ocr-engine lattice
dotnet run --project src/EpubFixer.Cli -- measure out.epub
dotnet run --project src/EpubFixer.Cli -- debug-lattice "test-data/odun-kesmek/input.epub" --json docs/baselines/odun-kesmek.lattice.json
dotnet run --project benchmarks/EpubFixer.QualityBenchmarks -- test-data/odun-kesmek
```

### 3.8 Commit disiplini
Kalem başına ayrı commit; ilk satır `R4.x: <ne yapıldı>`. R4.0 dört, R4.2 üç alt adıma
bölünmüştür; her alt adım kendi commit'inde ve **her commit'te tüm suite yeşil**.

### 3.9 Kapsam disiplini
Her agent yalnızca kendi kalemini yapar. Yol boyunca fark edilen sorunlar düzeltilmez, bitiş
raporunda "gözlem" olarak yazılır. Sözleşme değişikliği gerekiyorsa önce gerekçe bildirilir, karar
bölüm 10'a eklenir.

---

## 4. Sıra ve paralellik

```
R4.0  Ölçüm ve emniyet ağı            (a → b → c → d)
   ├──────────────────────────────────────────┐
   │                                          │
   └─► R4.1  RegionMutationPlanner            │   (R4.1, R4.0 ile paralel yürüyebilir)
          └─► R4.2a  Port + eski motor portun arkasına  ◄──┘
                 └─► R4.2b  Adapters + lattice motoru (kapalı)
                        └─► R4.2c  Anahtarı çevir + yeniden ölç
                               └─► R4.3  Eski yolların kaldırılması (KOŞULLU)
```

**Paralellik penceresi:** R4.1 yalnızca `EpubFixer.Core/Mutation` altında çalışır; R4.0 yalnızca
testler ve benchmark'ta çalışır. Dosya çakışması yoktur, ikisi paralel yürütülebilir. Yine de
R4.2a **ikisinin de commit'ini** bekler.

**Kritik yol:** R4.0c → R4.1 → R4.2a → R4.2b → R4.2c.

---

## 5. Mevcut durumun tespiti

Bu bölüm koddan okunmuştur. Yol haritasının Faz 4 metniyle çeliştiği yerler işaretlidir.

### 5.1 Üretim hattının OCR bloğu on satırdır

[EpubFixService.cs:104-114](../src/EpubFixer.Core/Fix/EpubFixService.cs#L104):

```csharp
var analysis = new OcrAnalysisService().AnalyzeCorrections(finalStream, morphologyOracleBuilder);
var report   = new OcrCorrectionDecisionEvaluator().Evaluate(analysis);
var plan     = new OcrCorrectionMutationPlanner().Create(report.Decisions, finalStream);
ocrMutation  = new OcrCorrectionMutationApplier().Apply(package, plan);
if (!ocrMutation.Succeeded) throw new InvalidDataException(...);
finalStream  = LogicalTextStreamBuilder.Build(package.SpineDocuments);
```

Dört somut sınıf, `new` ile, politika sınıfının içinde. Değiştirilecek tek yer budur; `Applier`,
`EpubOutputValidator` ve yazma yolu **hiç değişmez**.

### 5.2 OCR bloğu hyphenation'dan **sonra** çalışır — Faz 3'ün sayıları doğrudan geçerli değil

`finalStream`, V1 inline + V1 cross-paragraph + V2 inline + V2 cross-paragraph düzeltmeleri
uygulandıktan **sonra** kurulur
([EpubFixService.cs:103](../src/EpubFixer.Core/Fix/EpubFixService.cs#L103)). Faz 3'ün
563 region / 31 `Apply` ölçümü ise `debug-lattice` içinde **ham girdi** üzerinde alınmıştır
([Program.cs:355](../src/EpubFixer.Cli/Program.cs#L355)).

Sonuç: Faz 3 kapanış notundaki "31 kararın 24'ü tireleme birleştirmesi" gözlemi üretim hattında
**büyük olasılıkla geçersizdir** — o tirelemelerin çoğunu hyphenation hattı zaten birleştirmiş
olacaktır. Lattice'in üretimdeki net katkısı 31 değil, muhtemelen 7 civarıdır. **Bu bir tahmindir
ve R4.2c'de ölçülecektir.** Faz 4'ün hiçbir kabul kriteri 31 sayısına dayandırılmaz.

### 5.3 "Fix çıktısı değişmedi" testi zaten var (D50)

Faz 3 planı bölüm 15.1 "`Fix_OutputIsUnchanged` yazılmadı" diyor. Bu eksik **yanlış tespit
edilmiş**: `MorphologyCallTraceTests.FullBookFix_ProducesGoldenLogicalText`
([satır 84-106](../tests/EpubFixer.Tests/MorphologyCallTraceTests.cs#L84)) tam olarak bunu
yapıyor — `applyOcrCorrections: true` ile full kitabı düzeltip çıktı EPUB'ın logical text'inin
SHA-256'sını `37b72bc8...` değerine karşı doğruluyor. `[Trait("Category","Slow")]` taşıyor ama
varsayılan koşuda çalışıyor.

Gerçekten eksik olan, **sayaç kilidi**dir: bugün hiçbir test `result.OcrMutation.AppliedCount`
değerini iddia etmiyor. Hash bir şeyin değiştiğini söyler, **neyin** değiştiğini söylemez. R4.0a bu
boşluğu kapatır.

### 5.4 Benchmark OCR düzeltmelerini hiç ölçmüyor

[QualityBenchmarkRunner.cs:17-144](../benchmarks/EpubFixer.QualityBenchmarks/QualityBenchmarkRunner.cs#L17)
yalnızca hyphenation hattını koşuyor: `HyphenationCorrectionApplier` +
`CrossParagraphHyphenationCorrectionApplier`. OCR mutation'ları **hiç uygulanmıyor**.

Bu, yol haritasının R4.2 kabul kriterini ("`QualityBenchmark` full koşusu R0.4 kapısından geçer")
bugünkü hâliyle **boş** yapar: kapı geçse de lattice motoru hakkında hiçbir şey söylemez. R4.0c
bunu düzeltir.

İyi haber: ölçüm altyapısı zaten motor-bağımsız. `QualityBenchmarkOccurrenceTracker` canlı DOM
`IRange`'leri üzerinden çalışıyor
([QualityBenchmarkOccurrenceTracker.cs:33](../benchmarks/EpubFixer.QualityBenchmarks/QualityBenchmarkOccurrenceTracker.cs#L33)),
yani hangi applier metni değiştirirse değiştirsin `CorrectlyFixed` / `WronglyFixed` /
`ProtectedChanged` doğru sayılır. Eklenmesi gereken tek şey, hyphenation aşamalarından sonra bir
**OCR aşaması** ve o aşama için bir `IntegrityEvaluator.Audit` çağrısıdır.

### 5.5 `OcrCorrectionMutation` eski karar modeline çivilenmiş

```csharp
public sealed record OcrCorrectionMutation(
    ..., OcrCorrectionDecision Decision, IReadOnlyList<OcrMutationSourceSpan> SourceSpans)
{
    public string DecisionRule      => Decision.DecisionReasons.FirstOrDefault().ToString();
    public OcrConfidence Confidence => Decision.SourceOccurrence.Source.Confidence;
    public bool IsMultiSource       => Decision.SelectedProposal?.Proposal.ConsumesMultipleOccurrences == true;
}
```

`Decision` zorunlu ve somut. Bölge tabanlı bir düzeltmenin `OcrCorrectionDecision`'ı yoktur. Bu,
yol haritası risk #2'nin ("iki hattın veri modeli uyumsuzluğu") tam olarak durduğu yerdir.

Bu üç türetilmiş alanı okuyan tek yer `OcrCorrectionMutationApplier` (`IsMultiSource`, sayım için)
ve `Program.WriteOcrMutationReport` ([Program.cs:592](../src/EpubFixer.Cli/Program.cs#L592)). İkisi
de bir **köken kaydı** ile beslenebilir; `OcrCorrectionDecision`'a ihtiyaçları yok. Çözüm D54'tedir.

### 5.6 Geometri tekniği hazır, sıfırdan yazılmayacak

[OcrCorrectionMutationPlanner.cs:55-64](../src/EpubFixer.Core/Mutation/OcrCorrectionMutationPlanner.cs#L55)
bir logical aralığı karakter karakter gezip `stream.GetSourceLocationAt(index)` ile source span'lere
çeviriyor ve bitişik olanları birleştiriyor. `LogicalTextStreamBuilder` text node'ları **ayraçsız**
birleştirdiği için
([LogicalTextStreamBuilder.cs:49](../src/EpubFixer.Core/Epub/LogicalTextStreamBuilder.cs#L49))
bu eşleme birebirdir. `Applier` de çok span'li mutation'ı zaten destekliyor: replacement metnin
**tamamı ilk span'e** yazılır, kalan span'ler boşaltılır
([OcrCorrectionMutationApplier.cs:46](../src/EpubFixer.Core/Mutation/OcrCorrectionMutationApplier.cs#L46)).

R4.1 bu tekniği **aynen** kullanır. Yeni geometri kodu yazılmaz; yalnızca girdi tipi değişir.

### 5.7 Pencere paragraf sınırını aşmaz ama text node sınırını aşabilir

`HardBoundaryOffsets` yalnızca `Paragraph` ve `Document` sınırlarını sert kabul ediyor
([Program.cs:462](../src/EpubFixer.Cli/Program.cs#L462)); `TextNode` sınırı serttir denmiyor. Yani
bir lattice penceresi `...ko<i>li ukta</i>...` gibi bir yapıda **iki text node'a yayılabilir**. Bu
durumda replacement tamamen ilk node'a yazılır ve `<i>` içindeki metin boşalır — biçimlendirme
kayması. Legacy planner'da da aynı davranış var, yani yeni bir risk değil; ama R4.1'in bunun için
bir testi olmalı ve gerçekleşme sayısı R4.2c'de raporlanmalıdır.

### 5.8 `SymSpellChecker` silinecek dosyanın içinde

`SymSpellLexiconMatcher` (Faz 3'ün ürünü, yaşayacak) `SymSpellChecker` sınıfına bağımlı; o sınıf
[SymSpellRegionReconstructor.cs:42](../src/EpubFixer.Cli/OcrReconstruction/SymSpellRegionReconstructor.cs#L42)
içinde tanımlı ve o dosya R4.3'te **silinecek**. Çıkarma işi R4.2b'de yapılır, R4.3'e bırakılmaz.

### 5.9 Morfoloji sayaçları kesin olarak değişecek

`MorphologyCallTraceTests.FullBookFix_MorphologyCallProfileIsRecorded` bugün
`AnalyzeBatchCalls == 4` ve `DistinctWords == 13_215` iddia ediyor
([satır 68-70](../tests/EpubFixer.Tests/MorphologyCallTraceTests.cs#L68)). Bugünkü dört çağrı:
`AnalyzeV2` ×2, `EpubOutputValidator`'ın `AnalyzeV2`'si, `OcrAnalysisService`'in prefill'i.

Lattice motoru `BookKnowledgeBuilder` kullanır; o da **iki ayrı** `oracleBuilder.Build(...)` çağrısı
yapar (detector prefill'i + vocabulary prefill'i,
[BookKnowledgeBuilder.cs:24-29](../src/EpubFixer.Core/Lexicon/BookKnowledgeBuilder.cs#L24)).
R4.2c'de bu sayaçlar **ölçülerek** güncellenecektir. Beklenen yön: `AnalyzeBatchCalls` 4 → 5
(legacy'nin prefill'i gider, lattice'in ikisi gelir) ve `DistinctWords` artar. **Bu bir tahmindir;
teste yazılacak değer koşudan okunur.**

`MorphologyOracleCacheTests`'in "ikinci koşuda `processInvocations == 0`" iddiası korunmalıdır:
`CachingMorphologyOracleBuilder` çağrıları biriktirdiği için
([CachingMorphologyOracleBuilder.cs:19-34](../src/EpubFixer.Cli/Morphology/CachingMorphologyOracleBuilder.cs#L19))
yeni prefill yüzeyi de önbelleğe girer, ama bu **doğrulanmalıdır**.

### 5.10 Ground truth'un OCR kolu ince (D63)

`test-data/odun-kesmek/ground-truth.json` (schemaVersion 2) 160 `knownErrors` içeriyor:

| Sınıf | Kayıt |
|---|---:|
| Hyphenation | 148 |
| Fragmentation | 5 |
| GlyphConfusion | 4 |
| GarbageInsertion | 2 |
| SpuriousSpace | 1 |

Yol haritası R0.2 "sınıf başına ≥ 25 kayıt" istiyordu; gerçekleşen 12 OCR kaydıdır. Ayrıca
9 `protectedOccurrences` kaydı var. **Faz 4'ün kalite iddiası bu 12 kayda dayandırılamaz.** Ölçüm
planı bölüm 11 risk #1'de.

---

## 6. R4.0 — Ölçüm ve emniyet ağı (plana eklendi, D49)

### Amaç
Üretim hattının bugünkü davranışını **sayıya** çevirmek ve benchmark'ı OCR düzeltmelerini de ölçer
hâle getirmek. Bu kalem hiçbir üretim kodunu değiştirmez; yalnızca test ve benchmark yazar. Faz 3'ün
açık kalan üç ölçümü ([phase-3-plan.md](phase-3-plan.md) bölüm 15.1) burada kapanır.

### Dosya haritası
```
tests/EpubFixer.Tests/OcrMutationBaselineTests.cs                             [yeni]
tests/EpubFixer.Tests/PerformanceBudgetTests.cs                               [yeni Fact]
benchmarks/EpubFixer.QualityBenchmarks/QualityBenchmarkRunner.cs              [OCR aşaması]
benchmarks/EpubFixer.QualityBenchmarks/QualityBenchmarkIntegrityEvaluator.cs  [OCR audit aşırı yüklemesi]
benchmarks/EpubFixer.QualityBenchmarks/Models/QualityBenchmarkResult.cs       [OCR alanları]
benchmarks/EpubFixer.QualityBenchmarks/QualityBenchmarkReportWriter.cs        [OCR bölümü]
docs/baselines/odun-kesmek.fix-legacy.json                                    [yeni]
```

### 6.1 Alt adım a — Üretim sayaç kilidi (ayrı commit)

Bugün `fix --apply-ocr-corrections`'ın kaç mutation uyguladığını hiçbir test söylemiyor (5.3).

```csharp
// OcrMutationBaselineTests.cs
[Fact]
[Trait("Category", "Slow")]
public void FullBookFix_LegacyEngineMutationProfileIsPinned()
{
    // ... Fix(input, output, applyOcrCorrections: true)
    var mutation = result.OcrMutation!;
    Assert.Equal(<ÖLÇ>, mutation.PlannedCount);
    Assert.Equal(<ÖLÇ>, mutation.AppliedCount);
    Assert.Equal(<ÖLÇ>, mutation.SingleSourceCount);
    Assert.Equal(<ÖLÇ>, mutation.MultiSourceCount);
    Assert.Equal(<ÖLÇ>, mutation.DocumentsChanged);
    Assert.Empty(mutation.Failures);
    Assert.Equal(0, mutation.UnexpectedTextChanges);
}
```

Ayrıca uygulanan mutation'ların **imza listesi** (document, logicalStart, original → replacement)
`docs/baselines/odun-kesmek.fix-legacy.json` dosyasına yazılır ve testte bu dosyaya karşı
doğrulanır. Böylece R4.2c'de "hangi düzeltme gitti, hangisi geldi" satır satır görülebilir.

`<ÖLÇ>` yer tutucuları **koşularak** doldurulur (kural 3.4). Ölçüm önce, assert sonra.

**Kabul:** test yeşil; baseline dosyası commit'li; golden hash `37b72bc8...` değişmemiş.

### 6.2 Alt adım b — Lattice süre testi (ayrı commit)

Faz 3'ün 28,3 sn'si elle koşulmuş bir sayı, regresyon koruması altında değil
([phase-3-plan.md](phase-3-plan.md) bölüm 15.1 madde 2).

```csharp
// PerformanceBudgetTests.cs
[Fact]
[Trait("Category", "Slow")]
public void FullBookLatticePassCompletesWithinBudget()
{
    // debug-lattice ile aynı kurulum: BookKnowledgeBuilder + SymSpellLexiconMatcher
    //   + WordLatticeBuilder + LatticeDecoder + CorrectionAcceptanceGate
    // Tüm region'lar için Evaluate; süre ve MaxVisitedStates ölçülür.
    Assert.True(elapsed.TotalSeconds <= 30, ...);             // D38 alt bütçesi
    Assert.True(statistics.MaxVisitedStates <= 2_500, ...);   // 1.713 ölçüldü, %45 pay
    Assert.Equal(<ÖLÇ>, statistics.Regions);
    Assert.Equal(<ÖLÇ>, statistics.Applied);
}
```

Bu test `EpubFixer.Cli`'ı kullanır (`SymSpellLexiconMatcher` orada). Tests projesi Cli'ı zaten
referanslıyor. R4.2b'den sonra matcher `EpubFixer.Adapters`'a taşınınca bu testin `using`'i
güncellenir; başka değişiklik gerekmez.

**Kabul:** test yeşil ve 30 sn eşiği gerçek koşuda sağlanıyor. Sağlanmıyorsa **DUR ve bildir** —
eşiği büyütme, bu D38'in bilinçli alt bütçesidir.

### 6.3 Alt adım c — Benchmark'ın OCR kolu (ayrı commit) — bu fazın en kritik ölçüm işi

`QualityBenchmarkRunner.Run`, cross-paragraph aşamasından **sonra** bir OCR aşaması kazanır:

```csharp
var beforeOcr = integrityEvaluator.Capture(package.SpineDocuments);
var ocrStream = LogicalTextStreamBuilder.Build(package.SpineDocuments);
var ocrPlan   = ocrPlanner.CreatePlan(ocrStream, oracleBuilder);   // R4.2a'dan önce: doğrudan legacy dörtlüsü
var ocrResult = new OcrCorrectionMutationApplier().Apply(package, ocrPlan.Plan);
var ocrIntegrity = integrityEvaluator.Audit(beforeOcr, package.SpineDocuments, ocrPlan.Plan.Mutations, "ocr");
```

**Kesin kurallar:**

1. **Aşama sırası üretimle birebir aynı olmalıdır**: V1 inline → V1 cross → (V2 yok, benchmark V1
   hattını koşuyor) → OCR. Benchmark bugün V2'yi koşmuyor; bu bilinen bir farktır ve **bu kalemde
   düzeltilmez** (kapsam disiplini). Bitiş raporunda gözlem olarak yazılır.
2. `IntegrityEvaluator.Audit` bugün `IReadOnlyList<HyphenationCorrectionPlan>` alıyor
   ([QualityBenchmarkIntegrityEvaluator.cs:26](../benchmarks/EpubFixer.QualityBenchmarks/QualityBenchmarkIntegrityEvaluator.cs#L26)).
   OCR aşaması için `IReadOnlyList<OcrCorrectionMutation>` alan bir **aşırı yükleme** eklenir;
   mevcut aşırı yükleme ve `AllowedMutationSet` mantığı **değiştirilmez** (OCP). İzin verilen
   değişim kümesi mutation'ın `SourceSpans`'inden kurulur.
3. `UnexpectedTextChanges` ve `NonTextChanges` toplamına OCR aşamasının sayıları **eklenir**. Kapı
   bu ikisinin sıfır olmasını zaten istiyor
   ([QualityBenchmarkGateEvaluator.cs:22-23](../benchmarks/EpubFixer.QualityBenchmarks/QualityBenchmarkGateEvaluator.cs#L22)).
4. `CorrectlyFixed` / `WronglyFixed` / `ProtectedChanged` **kendiliğinden** doğru sayılır:
   tracker'lar canlı DOM range'leri üzerinde (5.4). Bu üç sayacın hesabına dokunulmaz.
5. Rapor yazıcısı yeni bir **"OCR Stage"** bölümü basar: planlanan / uygulanan mutation sayısı,
   başarısızlıklar, motor adı.

**Kabul:**
- Benchmark OCR kolu açıkken koşar ve kapı sonucu **bu kalemden önce ne ise o kalır** (legacy
  motorla ölçülüyor; kapı kırılıyorsa bu bir keşiftir, DUR ve bildir).
- `ProtectedChanged == 0` ve `UnexpectedTextChanges == 0` legacy motorla ölçülmüştür. **D48'in
  borcu budur** ve mekanizması buradan sonra lattice için de hazırdır (D51).
- Ölçülen precision / recall / sınıf kırılımı `docs/baselines/odun-kesmek.fix-legacy.json`'a
  eklenir.

### 6.4 Alt adım d — Gate profiline OCR notu (ayrı commit)

`docs/baselines/quality-gate.json`'a `current` bloğunun yanına ölçüm bağlamı yazılır: hangi motorla,
hangi tarihte, kaç OCR sınıfı kaydıyla ölçüldüğü. Eşik **değiştirilmez** — bu kalem ölçüm bağlamını
kayda geçirir, kapıyı gevşetmez.

### 6.5 Kapsam dışı
Motor portu (R4.2a), bölge geometrisi (R4.1), lattice'in üretime bağlanması (R4.2c), ground truth
genişletmesi.

---

## 7. R4.1 — `RegionMutationPlanner`

### Amaç
Bölge tabanlı bir düzeltmeyi mevcut `OcrCorrectionMutationApplier`'ın anladığı plana çevirmek. Bu
kalem hiçbir yere bağlanmaz; yalnızca tip ve test üretir.

### Dosya haritası
```
src/EpubFixer.Core/Mutation/RegionMutationPlanner.cs              [yeni]
src/EpubFixer.Core/Mutation/Models/RegionCorrection.cs            [yeni]
src/EpubFixer.Core/Mutation/Models/RegionMutationPlanResult.cs    [yeni]
src/EpubFixer.Core/Mutation/Models/OcrMutationProvenance.cs       [yeni]
src/EpubFixer.Core/Mutation/Models/OcrCorrectionMutation.cs       [Decision → Provenance]
src/EpubFixer.Core/Mutation/OcrCorrectionMutationPlanner.cs       [provenance üretimi]
tests/EpubFixer.Tests/RegionMutationPlannerTests.cs               [yeni]
```

### 7.1 Sözleşme

```csharp
namespace EpubFixer.Core.Mutation.Models;

public sealed record RegionCorrection(
    int LogicalStart,
    int LogicalEndExclusive,
    string Replacement,
    AcceptanceResult Acceptance);

public enum RegionCorrectionSkipReason
{
    OutOfRange,          // aralık stream dışında veya boş
    NoChange,            // replacement, orijinal metnin aynısı
    Overlapping,         // daha önce kabul edilmiş bir düzeltmeyle kesişiyor
    CrossesDocument,     // aralık iki farklı dokümana yayılıyor
    EmptyReplacement
}

public sealed record SkippedRegionCorrection(
    RegionCorrection Correction,
    RegionCorrectionSkipReason Reason);

public sealed record RegionMutationPlanResult(
    OcrCorrectionMutationPlan Plan,
    IReadOnlyList<SkippedRegionCorrection> Skipped);
```

```csharp
namespace EpubFixer.Core.Mutation;

public sealed class RegionMutationPlanner
{
    public RegionMutationPlanResult Create(
        IReadOnlyList<RegionCorrection> corrections,
        LogicalTextStream stream);
}
```

**Yol haritasından sapma (D56):** yol haritası `OcrCorrectionMutationPlan Create(...)` diyordu.
Dönüş tipi `RegionMutationPlanResult`'a genişletildi; gerekçe 7.4'te.

### 7.2 Köken kaydı (D54)

```csharp
namespace EpubFixer.Core.Mutation.Models;

public enum OcrCorrectionEngine { Legacy, Lattice }

public sealed record OcrMutationProvenance(
    OcrCorrectionEngine Engine,
    string Rule,
    OcrConfidence? Confidence,
    bool ConsumesMultipleSources);
```

`OcrCorrectionMutation` değişimi:

```csharp
public sealed record OcrCorrectionMutation(
    string DocumentPath, int LogicalStart, int LogicalLength,
    string OriginalSourceText, string ReplacementText,
    OcrMutationProvenance Provenance,                 // ← Decision yerine
    IReadOnlyList<OcrMutationSourceSpan> SourceSpans)
{
    public string DecisionRule       => Provenance.Rule;
    public OcrConfidence? Confidence => Provenance.Confidence;
    public bool IsMultiSource        => Provenance.ConsumesMultipleSources;
}
```

Eşleme kuralları:

| | Legacy | Lattice |
|---|---|---|
| `Engine` | `Legacy` | `Lattice` |
| `Rule` | `Decision.DecisionReasons.FirstOrDefault().ToString()` | `string.Join("; ", Acceptance.Reasons)` |
| `Confidence` | `Decision.SourceOccurrence.Source.Confidence` | `null` |
| `ConsumesMultipleSources` | `Decision.SelectedProposal?.Proposal.ConsumesMultipleOccurrences == true` | orijinal aralık boşluk içeriyorsa `true` |

`OcrCorrectionMutationPlanner` (legacy) bu eşlemeyi yaparak **davranışını korur**; ürettiği
mutation'ların `DecisionRule`, `Confidence`, `IsMultiSource` değerleri bugünküyle birebir aynıdır.
`Program.WriteOcrMutationReport` yalnızca `Confidence` kolonu için `?.ToString() ?? "—"` alır; başka
değişmez.

### 7.3 Geometri kuralları

1. `logicalStart = correction.LogicalStart`, `logicalEnd = correction.LogicalEndExclusive`. Aralık
   `[0, stream.Text.Length]` içinde ve boş olmamalıdır.
2. Span üretimi
   [OcrCorrectionMutationPlanner.cs:55-64](../src/EpubFixer.Core/Mutation/OcrCorrectionMutationPlanner.cs#L55)
   tekniğinin **birebir aynısıdır**: karakter karakter `GetSourceLocationAt`, bitişikleri birleştir,
   `ExpectedText` biriktir. Ortak kod iki planner arasında **paylaşılabilir** (`internal static`
   yardımcı); kopyalanmaz.
3. `DocumentPath` ilk span'in doküman yoludur. Aralık iki dokümana yayılıyorsa `CrossesDocument` ile
   **atlanır** (pratikte olmamalı — pencere doküman sınırını aşmıyor, D42 — ama savunma katmanıdır).
4. Sıralama: `DocumentPath` (Ordinal) → `LogicalStart`. Kesişim kontrolü bu sırayla yürür.

### 7.4 Atlama, başarısızlık değil (D55)

`EpubFixService` planı geçersizse **exception atıyor**
([EpubFixService.cs:111-112](../src/EpubFixer.Core/Fix/EpubFixService.cs#L111)). Yani tek bir
çakışan bölge düzeltmesi, **tüm kitabın düzeltilmemesine** yol açar. Bu kabul edilemez: lattice
motoru istatistikseldir, ara sıra çakışan aralık üretmesi normaldir.

Bu yüzden `RegionMutationPlanner` çakışan / boş / değişmeyen düzeltmeleri `Skipped` listesine koyar
ve plana **hiç almaz**. Üretilen `OcrCorrectionMutationPlan.Failures` **daima boştur**; `IsValid`
daima `true`'dur.

Çakışmada hangisi kalır: `LogicalStart` küçük olan; eşitse `LogicalEndExclusive` küçük olan (dar
olan); yine eşitse `Replacement` Ordinal küçük olan. Deterministik ve testli.

### 7.5 Yazılacak testler (önce kırmızı)

| Test | Ne kanıtlar |
|---|---|
| `Create_SingleNodeRange_ProducesOneSpan` | Temel geometri |
| `Create_RangeAcrossTwoTextNodes_ProducesTwoSpansWithExpectedText` | 5.7'deki durum; `ExpectedText`'ler node içeriğiyle eşleşir |
| `Create_ReplacementGoesToFirstSpan_RestAreEmptied` | `Applier`'ın sözleşmesine uygunluk (uçtan uca applier ile) |
| `Create_JoinCorrection_RemovesInteriorSpace` | `koli ukta` → `koltukta`; uzunluk değişimi |
| `Create_SplitCorrection_AddsSpace` | `birşey` → `bir şey` |
| `Create_OverlappingCorrections_KeepsFirstAndSkipsSecond` | D55; `Skipped` doğru sebeple dolu |
| `Create_NoChangeCorrection_IsSkipped` | `NoChange` |
| `Create_OutOfRangeCorrection_IsSkipped` | `OutOfRange`; exception atmaz |
| `Create_PlanIsAlwaysValid` | `Failures` boş, `IsValid == true` |
| `Create_IsDeterministic` | Aynı girdi, iki koşu, aynı plan |
| `Create_ProvenanceCarriesAcceptanceReasons` | `DecisionRule` beklenen metni taşır |
| `Apply_RoundTripsThroughExistingApplier` | Plan → `OcrCorrectionMutationApplier` → rebuilt text beklenen (bu kalemin **asıl** kabul testi) |
| `LegacyPlanner_ProvenanceMatchesPreviousDecisionFields` | D54 regresyon güvencesi |

### 7.6 Kabul kriteri
- Üretilen plan mevcut `OcrCorrectionMutationApplier` ile **değişiklik gerektirmeden** çalışır.
- `OcrCorrectionMutationPlanner`'ın (legacy) davranışı değişmemiştir; `OcrMutationBaselineTests`
  (R4.0a) sayaçları aynıdır ve golden hash `37b72bc8...` korunur.
- `CoreLayeringTests` yeşil; izin listesine dosya eklenmemiştir.

### 7.7 Kapsam dışı
`EpubFixService` dokunulmaz. `IOcrCorrectionPlanner` yoktur. CLI bayrağı yoktur. Lattice motoruyla
hiçbir bağlantı kurulmaz.

---

## 8. R4.2 — Üretim hattına bağlama

Bu kalem üç alt adıma bölünmüştür (D39 disiplini). **Her alt adım ayrı commit, her commit'te tüm
suite yeşil.** Alt adımlar farklı oturumlara devredilebilir.

### 8.1 R4.2a — Port aç, eski motoru arkasına koy (çıktı değişmez)

**Amaç:** `EpubFixService`'in somut dört sınıfa olan bağımlılığını bir porta çevirmek. Davranış
**bit düzeyinde** korunur.

**Sözleşme:**

```csharp
namespace EpubFixer.Core.Ocr;

public sealed record OcrCorrectionPlanResult(
    OcrCorrectionMutationPlan Plan,
    OcrCorrectionEngine Engine,
    IReadOnlyList<string> Diagnostics);

public interface IOcrCorrectionPlanner
{
    OcrCorrectionPlanResult CreatePlan(LogicalTextStream stream, IMorphologyOracleBuilder oracleBuilder);
}
```

```csharp
public sealed class LegacyOcrCorrectionPlanner : IOcrCorrectionPlanner
{
    public OcrCorrectionPlanResult CreatePlan(LogicalTextStream stream, IMorphologyOracleBuilder oracleBuilder)
    {
        var analysis = new OcrAnalysisService().AnalyzeCorrections(stream, oracleBuilder);
        var report   = new OcrCorrectionDecisionEvaluator().Evaluate(analysis);
        var plan     = new OcrCorrectionMutationPlanner().Create(report.Decisions, stream);
        return new(plan, OcrCorrectionEngine.Legacy, []);
    }
}
```

`EpubFixService` (D53):

```csharp
public EpubFixService(
    IMorphologyOracleBuilder morphologyOracleBuilder,
    IOcrCorrectionPlanner? ocrCorrectionPlanner = null)
```

Varsayılan `new LegacyOcrCorrectionPlanner()`. Böylece bugünkü **altı** çağrı yeri
(`EpubFixServiceTests`, `MorphologyCallTraceTests` ×2, `MorphologyOracleCacheTests` ×2,
`MorphologyPrefillCompletenessTests`, `PerformanceBudgetTests`, `Program.cs`) hiç değişmez.

OCR bloğu:

```csharp
if (applyOcrCorrections)
{
    var planResult = ocrCorrectionPlanner.CreatePlan(finalStream, morphologyOracleBuilder);
    ocrMutation = new OcrCorrectionMutationApplier().Apply(package, planResult.Plan);
    if (!ocrMutation.Succeeded) throw new InvalidDataException(...);
    finalStream = LogicalTextStreamBuilder.Build(package.SpineDocuments);
}
```

**Testler:**
- `Fix_UsesInjectedOcrPlanner` — sahte planner enjekte edilir, planının kullanıldığı doğrulanır.
- `Fix_DefaultsToLegacyPlanner` — parametre verilmezse `Engine == Legacy`.
- Mevcut tüm testler **değişmeden** yeşil.

**Kabul:** golden hash `37b72bc8...` **aynı**; `OcrMutationBaselineTests` sayaçları **aynı**;
`MorphologyCallTraceTests` sayaçları **aynı**. Bu alt adımda değişen tek şey tiplerdir.

---

### 8.2 R4.2b — `EpubFixer.Adapters` + lattice motoru (kapalı gelir)

**Amaç:** Lattice motorunu portun arkasına takmak ve iki composition root'un (Cli, Benchmarks) aynı
detayları paylaşmasını sağlamak. Varsayılan motor hâlâ **legacy**; çıktı hâlâ değişmez.

**Yeni proje (D57):**

```
src/EpubFixer.Adapters/EpubFixer.Adapters.csproj
    → ProjectReference: EpubFixer.Core
    → PackageReference: SymSpell 6.7.3
    → Content: Resources/OcrReconstruction/**   (tr_50k.txt + ATTRIBUTION.md, Cli'dan taşınır)

src/EpubFixer.Adapters/Lexicon/FileTurkishFrequencyListSource.cs   [Cli'dan taşındı]
src/EpubFixer.Adapters/Ocr/Lattice/SymSpellChecker.cs              [SymSpellRegionReconstructor.cs'ten çıkarıldı, 5.8]
src/EpubFixer.Adapters/Ocr/Lattice/SymSpellLexiconMatcher.cs       [Cli'dan taşındı]
src/EpubFixer.Adapters/Ocr/LatticeOcrPlannerFactory.cs             [yeni — kurulum tek yerde]
```

`EpubFixer.Cli` ve `EpubFixer.QualityBenchmarks` Adapters'ı referanslar. Cli'nın SymSpell
`PackageReference`'ı ve `Resources` `Content` bloğu kaldırılır (transitive olarak Adapters'tan
gelir — **çıktı klasöründe `tr_50k.txt` gerçekten var mı, koşarak doğrula**).

**Motor Core'da kalır (D58):**

```csharp
namespace EpubFixer.Core.Ocr;

public sealed class LatticeOcrCorrectionPlanner : IOcrCorrectionPlanner
{
    public LatticeOcrCorrectionPlanner(
        ITurkishFrequencyList frequencyList,
        Func<BookVocabulary, ILexiconMatcher> matcherFactory,
        LatticeOptions? options = null);

    public OcrCorrectionPlanResult CreatePlan(LogicalTextStream stream, IMorphologyOracleBuilder oracleBuilder);
}
```

İç akış (bugünkü `RunDebugLattice` ile birebir aynı,
[Program.cs:353-366](../src/EpubFixer.Cli/Program.cs#L353)):

```
BookKnowledgeBuilder(frequencyList).Build(stream, oracleBuilder)
  → knowledge.Regions
  → LatticeRegionReconstructor(stream.Text,
        new WordLatticeBuilder(matcherFactory(knowledge.Vocabulary), HardBoundaryOffsets(stream)),
        new LatticeDecoder(knowledge.LanguageModel, options),
        new CorrectionAcceptanceGate(knowledge.Vocabulary, options),
        options)
  → her region için Evaluate → Verdict == Apply olanlar → RegionCorrection
  → RegionMutationPlanner.Create(corrections, stream)
  → OcrCorrectionPlanResult(plan, Lattice, diagnostics)
```

`Diagnostics`'e giren asgari bilgi: `LatticeRunStatistics` özeti, `Skipped` sayıları sebep
kırılımıyla, `Review` karar sayısı.

**`Review` kararları uygulanmaz** ([phase-3-plan.md](phase-3-plan.md) bölüm 15.2). Diagnostics'e
yazılır, plana girmez.

**`HardBoundaryOffsets` Core'a taşınır (D59):** `Program.cs:462`'deki yardımcı
`EpubFixer.Core/Epub/LogicalTextStreamBoundaries.HardOffsets(stream)` olur; `Program.cs` ve
`LatticeOcrCorrectionPlanner` aynı yerden çağırır. D42'nin tek doğruluk kaynağı olur.

**CLI:** `fix ... [--ocr-engine lattice|legacy]`. `CliOptions`'a `OcrEngine` alanı; varsayılan
**`legacy`** (bu alt adımda). `CliOptionsTests`'e ayrıştırma testleri.

**Yeni mimari testi:**
```csharp
[Fact] public void CoreDoesNotReferenceAdapters()   // Core.csproj'da Adapters referansı yok
```

**Testler:**
- `LatticeOcrCorrectionPlanner_ProducesPlanForKnownRegion` — sahte matcher + küçük stream.
- `LatticeOcrCorrectionPlanner_DoesNotIncludeReviewVerdicts`.
- `LatticeOcrCorrectionPlanner_IsDeterministic`.
- `SymSpellLexiconMatcherTests` — yalnızca namespace güncellenir, assert'ler **değişmez**.
- `Fix_WithLatticeEngine_ProducesValidOutput` — `[Trait("Category","Slow")]`, çıktı
  `EpubOutputValidator`'dan geçer. **Bu alt adımda golden hash iddiası yok** (varsayılan hâlâ
  legacy).

**Kabul:** varsayılan koşuda golden hash `37b72bc8...` **aynı**; `--ocr-engine lattice` ile koşu
geçerli bir EPUB üretiyor; tüm suite yeşil.

---

### 8.3 R4.2c — Anahtarı çevir ve yeniden ölç

**Amaç:** `fix --apply-ocr-corrections` varsayılan olarak lattice motorunu kullansın (D60). Bu,
Faz 4'ün **tek kasıtlı davranış değişimi**dir.

**Yapılacaklar, bu sırayla:**

1. Varsayılan motoru `lattice` yap (`CliOptions` ve `EpubFixService`'in varsayılan planner'ı).
   `--ocr-engine legacy` R4.3'e kadar seçilebilir kalır.
2. **Ölç, sonra yaz.** Şu değerler koşularak yeniden ölçülür ve testlere/baseline'lara yazılır:
   - `MorphologyCallTraceTests`: `AnalyzeBatchCalls`, `CacheStatistics.BatchRequests`,
     `DistinctWords` (5.9)
   - `MorphologyCallTraceTests.FullBookFix_ProducesGoldenLogicalText`: **yeni** SHA-256
   - `OcrMutationBaselineTests`: lattice sayaçları → `docs/baselines/odun-kesmek.fix-lattice.json`
   - `PerformanceBudgetTests`: toplam süre (D61 sınırı içinde mi)
   - `measure` çıktısı → `docs/baselines/odun-kesmek.book-health.json` güncellenir
   - Benchmark full koşusu → precision / recall / sınıf kırılımı / `ProtectedChanged`
3. **Fark raporu üret ve elle incele.** `odun-kesmek.fix-legacy.json` ile
   `odun-kesmek.fix-lattice.json` karşılaştırılır:
   - lattice'te olup legacy'de olmayan düzeltmeler (**kazanç**)
   - legacy'de olup lattice'te olmayan düzeltmeler (**kayıp**)
   - ikisinde de olup **farklı** replacement üretenler (**çatışma** — en tehlikeli sınıf)

   Üç listenin **tamamı** elle incelenir ve bitiş raporunda satır satır yazılır. Sonuç
   `docs/baselines/odun-kesmek.engine-diff.json` olarak commit edilir.
4. Yanlış düzeltme bulunursa: **eşik oynatarak kapatma.** `LatticeOptions` bu fazda kalibre edilmez
   (bölüm 1). Yanlış düzeltme varsa DUR, bulguyu bildir; karar (kapatmak mı, R5.4'e devretmek mi)
   kayıt altına alınır.

**Kabul:**
1. Tüm suite yeşil; değişen her beklenen değer **ölçülmüş** ve bitiş raporunda gerekçelendirilmiş.
2. Benchmark full koşusu OCR kolu açık ve **lattice motoruyla** R0.4 kapısından geçer.
3. `ProtectedViolated == 0`, `ProtectedChanged == 0`, `UnexpectedTextChanges == 0`,
   `NonTextChanges == 0`. **D48'in kapanışı budur.**
4. `measure` metriği legacy çıktısına göre kötüleşmemiştir.
5. Süre: ≤ 120 sn ve legacy toplamının en fazla 35 sn üstünde (D61).
6. Uygulanan her düzeltme elle incelenmiş, yanlış düzeltme sayısı **sıfır**.
7. `docs/baselines/README.md` yeni baseline'ları listeler.

**Kabul edilmezse:** varsayılan motor `legacy`'ye geri alınır (tek satır), bulgular raporlanır ve
R4.2c tekrar denenir. Çıktı EPUB'a yanlış düzeltme yazan bir sürüm commit edilmez.

---

## 9. R4.3 — Eski yolların kaldırılması (KOŞULLU)

> **Bu kalem koşulludur (D62).** Ön koşulu sağlanmazsa **yapılmaz**; iki motor bayrağın arkasında
> yaşamaya devam eder ve silme Faz 5'e (R5.4 kalibrasyonundan sonra) ertelenir. Bir agent bu kalemi
> "hazır görünüyor" diye başlatmaz.

### 9.1 Ön koşul: alt küme kanıtı

Yol haritası şunu istiyor: *"generator'ın AutoFix ürettiği her vakada lattice de aynı sonucu
vermeli."*

Ölçüm: `docs/baselines/odun-kesmek.engine-diff.json` (R4.2c'nin 3. adımı) üzerinde **kayıp listesi
boş** olmalıdır. Bugünkü bilgiyle bunun sağlanma olasılığı **düşüktür**: kapının
`OriginalTokenIsValid` kuralı 563 region'ın 418'ini kapatıyor ve D45'in dört ek kuralı dar. Yani
beklenen sonuç "R4.3 ertelenir"dir.

**Ek ön koşul:** R4.2c'den sonra **iki sürüm boyunca stabil koşu** (yol haritası). Pratikte: lattice
varsayılan haldeyken en az iki ayrı commit'te full suite + benchmark yeşil.

### 9.2 Kaldırılacaklar ve tespit edilmiş yan etkileri

| Silinecek | Yan etkisi |
|---|---|
| `EpubFixer.Cli/OcrReconstruction/NoisyChannelRegionReconstructor.cs` | `NoisyChannelV2Tests`, `NoisyChannelDiagnosticTests`, `PerformanceBudgetTests`'in üç testi, `FullBookReaderPreview`, `OcrReconstructionComparison` |
| `.../SymSpellRegionReconstructor.cs` | `SymSpellChecker` **zaten R4.2b'de taşındı**; kalan sınıf yalnızca `OcrReconstructionComparison`'da |
| `.../CurrentRegionReconstructor.cs` | yalnızca `OcrReconstructionComparison` |
| `.../DeterministicOcrCandidateReranker.cs` | `OcrCandidateRerankerTests`, `FullBookReaderPreview`, `OcrReconstructionComparison` |
| `.../BookContextIndex.cs` | `FullBookReaderPreview`, `OcrReconstructionComparison` (Faz 2 D13: taşınmadı, silinecek) |
| `.../OcrReconstructionComparison.cs` + `debug-ocr-reconstruction` komutu | `Program.cs` |
| `.../FullBookReaderPreview.cs` + `reader-preview`, `inspect-reader-preview-prewarm` | `Program.cs`. **DİKKAT:** R5.3 (diff raporu) bu dosyayı başlangıç noktası sayıyor. Silmeden önce R5.3'ün ne kadarını devraldığına karar verilmelidir. |
| `EpubFixer.Core/Ocr/OcrCorrectionCandidateGenerator.cs` | `OcrAnalysisService.AnalyzeCorrections`, `OcrCorrectionCandidateGeneratorTests`, `OcrAnalysisReportingTests`, `MorphologyPrefillEnumerationTests` |
| `EpubFixer.Core/Decision/OcrCorrectionDecisionEvaluator.cs` | `LegacyOcrCorrectionPlanner`, `OcrDecisionReporting`, `Program.cs` `--ocr-decision-report`, üç test dosyası |
| `EpubFixer.Core/Ocr/OcrAnalysisService.cs` (`AnalyzeCorrections` kolu) | `OcrCorrectionV11RealRegressionTests` (**893 candidate / 642 proposal iddialarının tamamı**) |
| `LegacyOcrCorrectionPlanner` + `--ocr-engine` bayrağı | `CliOptions`, `CliOptionsTests` |

**En büyük tek kayıp `OcrCorrectionV11RealRegressionTests`'tir**: 46.927 token, 893 candidate,
137/80/676 güven dağılımı ve on beş hedef kelime iddiası. Bu test silinirse eski motorun kalitesi
hakkındaki tek ayrıntılı ölçüm gider. **Karar gerekir:** silinmeden önce lattice için eşdeğer bir
hedef testi (aynı on beş kelime, lattice motoruyla) yazılmalıdır. Bu, R4.3'ün ilk işi olmalıdır.

### 9.3 Sıra

1. Alt küme kanıtı (9.1). Sağlanmıyorsa **DUR**, raporla, kalemi kapat.
2. Lattice için hedef kelime testi (9.2 son paragraf).
3. `--ocr-engine` bayrağı ve `LegacyOcrCorrectionPlanner` kaldırılır.
4. Core'daki eski üretim zinciri kaldırılır (generator, evaluator, `AnalyzeCorrections`).
5. Cli'daki deneysel reconstructor'lar ve komutları kaldırılır.
6. Yetim kalan testler kaldırılır; **hiçbiri "yeşile boyamak için" değiştirilmez** — ölçtüğü şey
   artık yoksa silinir, varsa yeni motora uyarlanır ve bu bitiş raporunda tek tek gerekçelenir.

### 9.4 Kabul
- Kalite kapısı hâlâ yeşil; golden hash R4.2c'deki değerde **kalır** (silme davranış değiştirmez).
- Ölü kod kalmaz; `dotnet build` uyarısız.
- `README.md` / `README.tr.md` kaldırılan komutları artık anlatmaz.

---

## 10. Kararlar (bu planla birlikte verildi)

Numaralandırma Faz 3'ün D48'inden devam eder.

| # | Karar | Gerekçe | Nereye işlendi |
|---|---|---|---|
| D49 | Yol haritasında olmayan **R4.0** eklendi ve kritik yolun başına kondu. | Faz 4 üretim çıktısını değiştiriyor; değişimden önce bugünkü çıktının sayıyla kilitlenmesi ve benchmark'ın OCR'ı ölçer hâle gelmesi gerekir. Faz 0/1/2/3 planlarının hepsi aynı deseni izledi. | Bölüm 4, 6 |
| D50 | Faz 3 §15.1 madde 1 (**`Fix_OutputIsUnchanged` eksik**) **yanlış tespitti**; test `MorphologyCallTraceTests.FullBookFix_ProducesGoldenLogicalText` olarak zaten var. Gerçek eksik, mutation **sayaçlarıdır**. | Golden SHA-256 `applyOcrCorrections: true` ile ölçülüyor. Hash "bir şey değişti" der, "ne değişti" demez; R4.2c'nin fark analizi sayaç ister. | Bölüm 5.3, 6.1 |
| D51 | `protectedViolated` (D48) borcu `debug-lattice`'te span kesişimi sayarak değil, **benchmark'ın OCR kolunda gerçek mutation'lar üzerinden** (`ProtectedChanged`, `ProtectedViolated`) kapatılır. | Tracker'lar canlı DOM range'leri; hangi motor yazarsa yazsın doğru sayıyor. İkinci bir ölçüm mekanizması yazmak, iki ölçümün çelişmesi riskini doğurur. | Bölüm 5.4, 6.3, 8.3 |
| D52 | İki motor **birbirini dışlar**; planları hiçbir zaman birleştirilmez. | Birleştirme, iki farklı gerekçe modelinden gelen çakışan mutation'ları çözmeyi gerektirir — Faz 4'ün riskini gereksiz yere ikiye katlar. A/B için bayrak yeterli. | Bölüm 8.2 |
| D53 | `IOcrCorrectionPlanner` portu; `EpubFixService` bunu **opsiyonel ctor parametresi** olarak alır, varsayılanı `LegacyOcrCorrectionPlanner`. | Altı mevcut çağrı yeri değişmeden kalır; R4.2a'nın "hiçbir davranış değişmedi" iddiası test diff'i olmadan görünür olur. Composition root (Cli, Benchmarks) açıkça enjekte eder. | Bölüm 8.1 |
| D54 | `OcrCorrectionMutation.Decision` → `OcrMutationProvenance`. `OcrCorrectionMutationPlan`, `OcrMutationSourceSpan`, `Applier` **değişmez**. | Mutation'ın somut `OcrCorrectionDecision`'a bağlı olması yol haritası risk #2'nin kökü. Türetilmiş üç alanı okuyan yalnızca iki yer var ve ikisi de bir köken kaydıyla beslenebilir. | Bölüm 5.5, 7.2 |
| D55 | Çakışan / boş / değişmeyen bölge düzeltmeleri **atlanır**, plan başarısız edilmez. Üretilen plan daima `IsValid`. | `EpubFixService` geçersiz planda exception atıyor: tek bir çakışma tüm kitabın düzeltilmemesi demek. "Emin değilsen dokunma" ilkesi region düzeyinde uygulanır, kitap düzeyinde değil. | Bölüm 3.5, 7.4 |
| D56 | `RegionMutationPlanner.Create` yol haritasındaki `OcrCorrectionMutationPlan` yerine **`RegionMutationPlanResult`** döner. | D55'in atlananları görünür olmalı; sessizce düşen düzeltme, ölçülemeyen recall kaybıdır. `Diagnostics` üzerinden rapora ve baseline'a çıkar. | Bölüm 7.1 |
| D57 | Yeni **`src/EpubFixer.Adapters`** projesi; `SymSpellChecker`, `SymSpellLexiconMatcher`, `FileTurkishFrequencyListSource` ve `tr_50k` kaynağı oraya taşınır. | İki composition root (Cli, Benchmarks) aynı detaya ihtiyaç duyuyor; benchmark'ın Cli'ı referanslaması bir exe'yi kütüphane gibi kullanmak olurdu. Ayrıca `SymSpellChecker` R4.3'te silinecek dosyanın içinde — çıkarma işi zaten yapılmalı. | Bölüm 5.8, 8.2 |
| D58 | `LatticeOcrCorrectionPlanner` **Core'da** kalır; matcher `Func<BookVocabulary, ILexiconMatcher>` olarak enjekte edilir. | D27 korunur: SymSpell bir detaydır, Core onu görmez. Kurulum sırası (knowledge → matcher → lattice → gate → planner) politikadır ve tek yerde yaşamalıdır. | Bölüm 8.2 |
| D59 | `HardBoundaryOffsets` Cli'dan Core'a taşınır (`LogicalTextStreamBoundaries.HardOffsets`). | D42'nin tek doğruluk kaynağı olmalı; iki composition root aynı sınır tanımını kullanacak. `LogicalTextStream` zaten Core tipi, yardımcı orada olmalıydı. | Bölüm 8.2 |
| D60 | Varsayılan motor **R4.2c'de** lattice olur; `--ocr-engine legacy` R4.3'e kadar yaşar. | Varsayılanın erken çevrilmesi, adapter taşımasının hatalarıyla motor değişiminin etkilerini aynı commit'te karıştırır. Anahtar en son çevrilir, tek başına. | Bölüm 8.3 |
| D61 | Süre bütçesi: `fix --apply-ocr-corrections` ≤ 120 sn (mevcut hard gate) **ve** legacy toplamının ≤ +35 sn üstü. | Lattice pass'i tek başına ≤ 30 sn (D38); +35 sn `BookKnowledgeBuilder`'ın iki ek prefill'ine pay bırakır. Salt 120 sn yetmez: legacy'nin bugünkü süresi bilinmiyorsa regresyon 120'nin altında saklanabilir. | Bölüm 1, 8.3 |
| D62 | **R4.3 koşulludur.** Alt küme kanıtı sağlanmazsa silme yapılmaz, iki motor bayrağın arkasında yaşar ve kalem Faz 5'e ertelenir. | Bugünkü kapı 563 region'ın 418'ini `OriginalTokenIsValid` ile kapatıyor; lattice'in legacy'nin üst kümesi olması beklenmiyor. Ölçmeden silmek, sessiz recall kaybıdır. | Bölüm 9 |
| D63 | Faz 4 kalite kapısının **eşikleri yükseltilmez**. Ground truth'un OCR kolu 12 kayıttır; kanıt yükü elle inceleme ve sıfır-regresyon ölçütlerindedir. | 12 kayıt üzerinde ölçülen bir precision/recall istatistiksel olarak anlamlı değil. Gate'i o sayıya göre sıkmak yanlış güven, gevşetmek regresyon gizler. | Bölüm 5.10, 11 |

Yeni bir karar ihtiyacı doğarsa agent kendi başına karara varmaz; gerekçeyi bildirip bekler ve karar
bu tabloya eklenir.

---

## 11. Risk kaydı (Faz 4'e özgü)

| # | Risk | Etki | Azaltma |
|---|---|---|---|
| 1 | **Ground truth'un OCR kolu ince (12 kayıt):** kapı yeşil olsa da lattice'in gerçek precision'ı ölçülmemiş olur | Yüksek | D63; R4.2c'nin üç listeli fark analizi ve **elle inceleme** zorunlu; `ProtectedChanged == 0` ve `UnexpectedTextChanges == 0` motor-bağımsız güvence olarak kalır |
| 2 | **Geometri uyumsuzluğu** (yol haritası risk #2): logical aralık → source span çevrimi bozulur | Yüksek | R4.1 mevcut tekniği aynen kullanır; `Apply_RoundTripsThroughExistingApplier` uçtan uca test; `Applier`'ın `SourceTextMismatch` kontrolü ikinci savunma |
| 3 | **Tek çakışma tüm düzeltmeyi düşürür** (`EpubFixService` exception atıyor) | Yüksek | D55: çakışan düzeltmeler plana hiç girmez, plan daima geçerli |
| 4 | Lattice üretimde hyphenation sonrası koşuyor; Faz 3'ün 31 Apply'ı üretimde geçerli değil, katma değer beklenenden düşük çıkabilir | Orta | 5.2'de açıkça kayıtlı; R4.2c ölçer. Düşük çıkarsa bu bir başarısızlık değil, R5.4'ün girdisidir |
| 5 | Text node sınırını aşan replacement biçimlendirmeyi bozar (`<i>` içeriği boşalır) | Orta | 5.7; R4.1'in çok-node testi; R4.2c raporunda gerçekleşme sayısı |
| 6 | Morfoloji sayaç testleri (`AnalyzeBatchCalls`, `DistinctWords`) kırılır ve "yeşile boyamak için" güncellenir | Orta | 5.9'da beklenen kırılma önceden yazılı; yeni değerler **ölçülecek** ve bitiş raporunda gerekçelenecek (kural 3.4) |
| 7 | `MorphologyOracleCacheTests`'in "ikinci koşuda 0 process" iddiası düşer | Orta | Yeni prefill yüzeyi `CachingMorphologyOracleBuilder`'ın birikimli sözlüğüne giriyor; R4.2c'de açıkça doğrulanır |
| 8 | Adapters taşıması `tr_50k.txt`'yi çıktı klasöründen düşürür, matcher sessizce boş hazneyle çalışır | Orta | R4.2b kabulünde "çıktı klasöründe dosya var mı" koşarak doğrulanır; `SymSpellLexiconMatcherTests` gerçek hazneyle koşuyor |
| 9 | `OcrCorrectionV11RealRegressionTests`'in silinmesiyle eski motorun tek ayrıntılı ölçümü kaybolur | Orta | R4.3'ün ilk işi lattice için eşdeğer hedef testi (9.2) |
| 10 | Süre 120 sn'yi aşar | Düşük | D61 alt bütçesi + R4.0b'nin 30 sn lattice testi; regresyon motor değil, prefill kaynaklıysa `MorphologyCallTraceTests` gösterir |
| 11 | `EpubOutputValidator`'ın "reload sonrası AutoFixCandidate kalmadı" kontrolü lattice düzeltmesinin yarattığı yeni bir tireleme adayıyla kırılır | Düşük | Kontrol zaten mutation sonrası akışta; R4.2c'nin full koşusu bunu doğrudan sınar |

---

## 12. Devir promptları

### R4.0
```
EpubFixer projesinde docs/phase-4-plan.md'deki R4.0 kalemini uygulayacaksın.

Önce şunları oku:
- docs/phase-4-plan.md — bölüm 2 (ön koşul), 3 (ortak kurallar), 5 (mevcut durum tespiti),
  6 (R4.0), 10 (kararlar D49, D50, D51, D63)
- docs/phase-3-plan.md — bölüm 1b ve 15.1
- src/EpubFixer.Core/Fix/EpubFixService.cs
- tests/EpubFixer.Tests/MorphologyCallTraceTests.cs
- tests/EpubFixer.Tests/PerformanceBudgetTests.cs
- benchmarks/EpubFixer.QualityBenchmarks/QualityBenchmarkRunner.cs
- benchmarks/EpubFixer.QualityBenchmarks/QualityBenchmarkIntegrityEvaluator.cs
- src/EpubFixer.Cli/Program.cs satır 338-470 (RunDebugLattice — OKU, kopyala, DEĞİŞTİRME)

Kurallar:
- ÖN KOŞUL: çalışma ağacı temiz ve suite yeşil olmalı. Değilse DUR ve bildir.
- Bu kalem HİÇBİR üretim kodunu değiştirmez. src/ altında yalnızca okuma yaparsın.
  Üretim kodu değişimi gerekiyorsa DUR ve bildir.
- Dört alt adım, dört ayrı commit: (a) mutation sayaç kilidi, (b) lattice süre testi,
  (c) benchmark OCR kolu, (d) gate profiline ölçüm bağlamı notu.
- Beklenen test değerlerini TAHMİN ETME. Önce koş, çıkan sayıyı oku, sonra teste yaz.
  Bitiş raporunda her sayının nereden geldiğini yaz (kural 3.4 / D41).
- Golden hash 37b72bc8508ceb6a06e9f8a5c7f41f65503e5bb99c9a436b82c37e960b2018be DEĞİŞMEMELİ.
  Değişirse DUR — bir yerde üretim davranışını değiştirdin demektir.
- Lattice süre testi 30 sn'yi aşarsa eşiği büyütme; DUR ve bildir (D38).
- Benchmark OCR kolu eklendikten sonra kapı kırılıyorsa DUR ve bildir; kapıyı gevşetme.
- Kapsam R4.0 ile sınırlı: port yok, RegionMutationPlanner yok, lattice'i fix'e bağlama yok.

Bitirdiğinde: eklenen testler, ölçülen sayılar ve nereden geldikleri, benchmark'ın OCR kolu
açıkken kapı sonucu, planda güncellenmesi gereken bir şey olup olmadığı.
```

### R4.1
```
EpubFixer projesinde docs/phase-4-plan.md'deki R4.1 kalemini uygulayacaksın.

Önce şunları oku:
- docs/phase-4-plan.md — bölüm 3, 5.5, 5.6, 5.7, 7 (R4.1), 10 (D54, D55, D56)
- docs/ocr-correction-roadmap.md — R4.1 maddesi ve risk kaydı
- src/EpubFixer.Core/Mutation/OcrCorrectionMutationPlanner.cs (özellikle satır 55-64)
- src/EpubFixer.Core/Mutation/OcrCorrectionMutationApplier.cs (özellikle satır 46)
- src/EpubFixer.Core/Mutation/Models/*.cs
- src/EpubFixer.Core/Epub/Models/LogicalTextStream.cs
- src/EpubFixer.Core/Ocr/Lattice/Models/AcceptanceResult.cs
- src/EpubFixer.Cli/Program.cs satır 573-600 (WriteOcrMutationReport — Provenance'a uyarlanacak)

Kurallar:
- Test-first: kırmızı test → minimum kod → refactor.
- Geometri kodunu SIFIRDAN YAZMA. OcrCorrectionMutationPlanner:55-64 tekniğini kullan;
  gerekirse internal static bir yardımcıya çıkar ve iki planner da onu çağırsın.
- OcrCorrectionMutationApplier, OcrCorrectionMutationPlan, OcrMutationSourceSpan DEĞİŞMEZ.
- OcrCorrectionMutation.Decision → OcrMutationProvenance (D54). Legacy planner'ın ürettiği
  DecisionRule / Confidence / IsMultiSource değerleri BUGÜNKÜYLE BİREBİR AYNI kalmalı;
  bunu bir testle kanıtla.
- Çakışan/boş/değişmeyen düzeltmeler ATLANIR, plan başarısız EDİLMEZ (D55). Üretilen planın
  Failures listesi daima boş, IsValid daima true.
- EpubFixService'e DOKUNMA. Bu kalem hiçbir yere bağlanmaz.
- Golden hash 37b72bc8... ve R4.0'ın mutation sayaçları DEĞİŞMEMELİ.
- Core'a System.IO girmez; CoreLayeringTests yeşil kalır, izin listesine dosya eklenmez.

Bitirdiğinde: eklenen tipler, test listesi, uçtan uca applier testinin sonucu, golden hash'in
korunduğu, planda güncellenmesi gereken bir şey olup olmadığı.
```

### R4.2a
```
EpubFixer projesinde docs/phase-4-plan.md'deki R4.2a alt adımını uygulayacaksın.

Önce şunları oku:
- docs/phase-4-plan.md — bölüm 3, 5.1, 8.1, 10 (D52, D53)
- src/EpubFixer.Core/Fix/EpubFixService.cs (satır 104-114)
- src/EpubFixer.Core/Ocr/OcrAnalysisService.cs
- src/EpubFixer.Core/Decision/OcrCorrectionDecisionEvaluator.cs
- tests/EpubFixer.Tests/EpubFixServiceTests.cs

Kurallar:
- ÖN KOŞUL: R4.0 ve R4.1 commit'li, suite yeşil. Değilse DUR ve bildir.
- Bu alt adım SAF REFACTOR'dur: hiçbir davranış değişmez. Kanıtı, golden hash
  37b72bc8508ceb6a06e9f8a5c7f41f65503e5bb99c9a436b82c37e960b2018be ile R4.0'ın mutation
  sayaçlarının aynı kalmasıdır. Biri değişirse DUR.
- IOcrCorrectionPlanner portu Core'da; LegacyOcrCorrectionPlanner bugünkü dört satırı aynen sarar.
- EpubFixService'in yeni ctor parametresi OPSİYONEL olacak (D53) ki mevcut altı çağrı yeri
  değişmesin. Mevcut test dosyalarında tek satır değiştirmek zorunda kalıyorsan DUR ve gerekçesini
  bildir.
- Yeni motor YOK, CLI bayrağı YOK, Adapters projesi YOK. Onlar R4.2b.

Bitirdiğinde: eklenen tipler, değişen satır sayısı, golden hash ve sayaçların korunduğu kanıtı.
```

### R4.2b
```
EpubFixer projesinde docs/phase-4-plan.md'deki R4.2b alt adımını uygulayacaksın.

Önce şunları oku:
- docs/phase-4-plan.md — bölüm 3.2, 5.8, 8.2, 10 (D57, D58, D59)
- docs/phase-3-plan.md — bölüm 12 (D27, D42) ve 15.2
- src/EpubFixer.Cli/Program.cs satır 338-470 (RunDebugLattice + HardBoundaryOffsets)
- src/EpubFixer.Cli/Ocr/Lattice/SymSpellLexiconMatcher.cs
- src/EpubFixer.Cli/OcrReconstruction/SymSpellRegionReconstructor.cs satır 42-50 (SymSpellChecker)
- src/EpubFixer.Cli/Lexicon/FileTurkishFrequencyListSource.cs
- src/EpubFixer.Core/Lexicon/BookKnowledgeBuilder.cs
- src/EpubFixer.Core/Ocr/Lattice/LatticeRegionReconstructor.cs
- src/EpubFixer.Core/Mutation/RegionMutationPlanner.cs (R4.1 çıktısı)

Kurallar:
- ÖN KOŞUL: R4.2a commit'li, suite yeşil.
- Yeni proje src/EpubFixer.Adapters; EpubFixer.slnx'e eklenir. Core → Adapters bağımlılığı
  OLMAYACAK ve bunu doğrulayan bir test yazılacak.
- SymSpell PackageReference ve Resources/OcrReconstruction Content bloğu Cli'dan Adapters'a taşınır.
  Taşımadan sonra ÇIKTI KLASÖRÜNDE tr_50k.txt gerçekten var mı, koşarak doğrula (risk #8).
- LatticeOcrCorrectionPlanner CORE'DA kalır; matcher Func<BookVocabulary, ILexiconMatcher>
  olarak enjekte edilir (D58). SymSpell Core'a girmez.
- Kurulum sırası RunDebugLattice ile BİREBİR AYNI olmalı. Farklı bir sıra kurma.
- Review kararları plana GİRMEZ, Diagnostics'e yazılır.
- --ocr-engine bayrağı eklenir ama VARSAYILAN legacy KALIR (D60). Golden hash 37b72bc8...
  bu alt adımda da DEĞİŞMEZ.
- Mevcut SymSpellLexiconMatcherTests'in assert'lerine dokunma; yalnızca namespace güncelle.

Bitirdiğinde: yeni proje yapısı, taşınan dosyalar, mimari testin sonucu, --ocr-engine lattice
ile üretilen çıktının EpubOutputValidator'dan geçtiği, golden hash'in korunduğu.
```

### R4.2c
```
EpubFixer projesinde docs/phase-4-plan.md'deki R4.2c alt adımını uygulayacaksın.
Bu, Faz 4'ün TEK KASITLI DAVRANIŞ DEĞİŞİMİDİR. Yavaş git.

Önce şunları oku:
- docs/phase-4-plan.md — bölüm 1 (kabul), 3.4, 5.2, 5.9, 8.3, 10 (D60, D61, D63), 11 (risk kaydı)
- docs/baselines/odun-kesmek.fix-legacy.json (R4.0 çıktısı)
- docs/baselines/odun-kesmek.lattice.json
- tests/EpubFixer.Tests/MorphologyCallTraceTests.cs
- tests/EpubFixer.Tests/MorphologyOracleCacheTests.cs

Kurallar:
- ÖN KOŞUL: R4.2b commit'li, suite yeşil.
- Varsayılan motoru lattice yap. --ocr-engine legacy seçilebilir kalsın.
- Değişen HER beklenen değeri ÖLÇEREK yaz (golden hash, AnalyzeBatchCalls, DistinctWords,
  mutation sayaçları, süre). Hiçbirini tahmin etme, hiçbirini "makul görünüyor" diye kabul etme.
- Fark raporu üret: legacy'de olup lattice'te olmayan (KAYIP), lattice'te olup legacy'de olmayan
  (KAZANÇ), ikisinde de olup farklı replacement üreten (ÇATIŞMA). ÜÇ LİSTENİN TAMAMINI elle
  incele ve bitiş raporunda satır satır yaz. Sonucu docs/baselines/odun-kesmek.engine-diff.json
  olarak commit et.
- Yanlış düzeltme bulursan LatticeOptions'ı OYNATMA (kalibrasyon R5.4'ün işi). DUR, bildir.
- Benchmark full koşusu OCR kolu açık ve lattice motoruyla kapıdan geçmeli. Geçmezse DUR;
  kapıyı gevşetme, eşik yükseltme (D63).
- ProtectedChanged, ProtectedViolated, UnexpectedTextChanges, NonTextChanges hepsi SIFIR olmalı.
  Biri sıfır değilse DUR — bu, kullanıcının kitabına yanlış yazdığımız anlamına gelir.
- Süre: <= 120 sn VE legacy toplamının en fazla 35 sn üstü (D61).
- Kabul sağlanmazsa varsayılanı legacy'ye geri al ve bulguları raporla. Yanlış düzeltme yazan
  bir sürüm COMMIT ETME.

Bitirdiğinde: yeni golden hash ve nasıl ölçüldüğü, üç fark listesi tam hâliyle, benchmark kapı
sonucu, measure metriğinin legacy'ye göre yönü, süre, ve Faz 4 kabul kriterlerinin madde madde
durumu.
```

### R4.3
```
EpubFixer projesinde docs/phase-4-plan.md'deki R4.3 kalemini DEĞERLENDİRECEKSİN.
Bu kalem KOŞULLUDUR (D62): ön koşul sağlanmıyorsa yapılmaz.

Önce şunları oku:
- docs/phase-4-plan.md — bölüm 9, 10 (D62), 11 risk #9
- docs/ocr-correction-roadmap.md — R4.3 maddesi
- docs/baselines/odun-kesmek.engine-diff.json (R4.2c çıktısı)
- tests/EpubFixer.Tests/OcrCorrectionV11RealRegressionTests.cs

Kurallar:
- İLK İŞ: alt küme kanıtını (bölüm 9.1) doğrula. Kayıp listesi boş DEĞİLSE silme yapma;
  bulguyu raporla, kalemi Faz 5'e ertelenmiş olarak kapat ve DUR.
- Kanıt sağlanıyorsa: önce lattice için hedef kelime testini yaz (bölüm 9.2 son paragraf),
  sonra bölüm 9.3'teki sırayla sil.
- Hiçbir testi "yeşile boyamak için" değiştirme. Ölçtüğü şey artık yoksa sil ve gerekçesini yaz;
  varsa yeni motora uyarla ve gerekçesini yaz.
- FullBookReaderPreview'ı silmeden önce R5.3'ün (diff raporu) onu başlangıç noktası saydığını
  hatırla; ne kadarının devralınacağına karar verilmeden silme.
- Golden hash R4.2c'deki değerde KALMALI — silme davranış değiştirmez.

Bitirdiğinde: alt küme kanıtının sonucu, silinen/uyarlanan her test için gerekçe, kapı durumu.
```

---

## 13. Faz 4'ün Faz 5 ile sözleşmesi

- **`LatticeOptions` tek eşik nesnesidir.** R5.4 yalnızca bu nesneyi süpürür. D45'in kapı içinde
  sabitlenmiş eşikleri (uzunluk oranı, `"ie"` kara listesi, büyük harf kuralı) hâlâ sınıf içinde
  sabittir — **bu borç Faz 4'te de kapanmadı** ve R5.4'ün ilk işidir.
- **`OcrCorrectionPlanResult.Diagnostics`** R5.3'ün diff raporunun ve R6.2'nin "kalan hata"
  raporunun girdisidir: `Review` kararları, `Skipped` sebepleri, `SkippedTooLong` ve
  `BudgetExceeded` sayıları motorun **bilerek dokunmadığı** kütledir.
- **`docs/baselines/odun-kesmek.fix-legacy.json` ve `.fix-lattice.json`** R5.1'in maliyet
  kalibrasyonu ve R5.4'ün eşik süpürmesi için referans noktalarıdır. R5.4 bir eşiği oynattığında bu
  iki dosyaya karşı ölçer.
- **Ground truth'un OCR kolu (12 kayıt) Faz 5'in ilk genişletme adayıdır.** R5.4'ün kalibrasyonu bu
  kadar ince bir ölçüm tabanı üzerinde güvenilir değildir; eşik süpürmesinden önce sınıf başına
  kayıt sayısı artırılmalıdır (yol haritası R0.2'nin gerçekleşmemiş hedefi).
- **R4.3 ertelendiyse** iki motor bayrağın arkasında yaşıyor demektir. Faz 5 boyunca `legacy`
  yalnızca A/B için kullanılır; ona yeni özellik eklenmez, hata düzeltilmez (OCP: ölmekte olan koda
  dokunulmaz).
