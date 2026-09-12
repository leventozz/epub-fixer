# Faz 1 — Morfolojiyi sıcak döngüden çıkar — Uygulama planı

> Bu belge [ocr-correction-roadmap.md](ocr-correction-roadmap.md) Faz 1'in (R1.1–R1.3) uygulama
> planıdır. Her kalem ayrı bir oturumda ayrı bir agent'a devredilebilir; bölüm 11'de kopyala-yapıştır
> devir promptları vardır. Faz 0'ın planı için: [phase-0-plan.md](phase-0-plan.md).
>
> Faz 1'in tek işi vardır: **düzeltme davranışını bir karakter değiştirmeden**, TRmorph'u runtime
> sıcak yolundan çıkarmak. Bu faz hiçbir hatayı daha iyi düzeltmez, hiçbir yeni hata sınıfı yakalamaz.
> Fazın sonunda kalite sayıları **birebir aynı** kalmalıdır; değişen tek şey nasıl elde edildikleridir.

---

## 1. Faz 1 bittiğinde elde ne olacak

| Çıktı | Dosya | Ne işe yarar |
|---|---|---|
| Saf morfoloji port'u | `EpubFixer.Core/Morphology/IMorphologyOracle.cs` | Politika katmanında I/O yapmayan tek doğruluk kaynağı |
| Ön-doldurma (prefill) hattı | Bileşenlerin kendi `EnumerateMorphologyQueries` metotları | Sorgu kümesi çalıştırmadan **önce** bilinir |
| Oracle disk cache'i | `EpubFixer.Cli/Morphology/MorphologyOracleCache.cs` | İkinci koşuda flookup hiç başlamaz |
| Çağrı sayacı baseline'ı | `docs/baselines/odun-kesmek.morphology.json` | "Sıcak döngüden çıktı mı?" sorusunun sayısal cevabı |
| Eksiksizlik testi | `tests/.../MorphologyPrefillCompletenessTests.cs` | Ön-doldurma açığı sessiz kalite kaybı değil, kırmızı test olur |

**Faz 1'in kabulü:**

1. `dotnet test EpubFixer.slnx` yeşil — **hiçbir mevcut assert değeri değişmeden**
   (özellikle `OcrCorrectionV111RealRegressionTests` ve `EpubFixServiceTests`).
2. Full `fix --apply-ocr-corrections` koşusunda flookup **tek process**, `BatchRequests` sabit ve
   kitap boyutundan bağımsız (R1.0'da ölçülen değerde kilitli).
3. Aynı kitapta ikinci koşuda flookup process'i **hiç başlamaz**.
4. `docs/baselines/odun-kesmek.morphology.json` ve güncellenmiş `odun-kesmek.performance.json`
   commit edilmiştir.

---

## 2. Her kalem için geçerli ortak kurallar

Faz 0'ın [bölüm 2](phase-0-plan.md) kuralları aynen geçerlidir. Faz 1'e özgü eklemeler:

### 2.1 Test-first
Kırmızı test → minimum kod → refactor. Testi olmayan üretim kodu yazılmaz. Bu faz bir **refactor**
fazıdır: her adımda "davranış değişmedi" iddiasını taşıyan test, yeni davranışı tarif eden testten
daha önemlidir.

### 2.2 Katmanlar
- `EpubFixer.Core` politika: `IMorphologyOracle`, `MorphologyOracle`, prefill sorgu üreticileri.
  **Ne `System.IO`, ne `System.Diagnostics.Process`, ne `EpubFixer.TrMorph` import edilir.**
- `EpubFixer.Cli` adapter: disk cache, dosya yolu, `FomaTurkishMorphologyAnalyzer` sahipliği.
- `EpubFixer.TrMorph` sürücü: bugünkü haliyle kalır, **bu fazda değiştirilmez**.
- Yeni istisna tipi Core'da tanımlanır (`MorphologyOracleException`); `TurkishMorphologyException`
  `EpubFixer.TrMorph` içindedir ve Core'dan kullanılamaz — bağımlılık yönü buna izin vermez.

### 2.3 Davranış korunumu bu fazın birinci kuralıdır
Mevcut hiçbir test beklenen değeri **değiştirilerek** yeşile döndürülmez. Bir test kırılıyorsa
sebebi refactor'un davranışı değiştirmiş olmasıdır; değer güncellenmez, sebep bulunur. Zorunlu
bir davranış değişikliği gerekiyorsa agent durur, gerekçeyi bildirir, karar bölüm 9'a eklenir.

### 2.4 Anahtar normalizasyonu (sık yapılan hata)
Oracle anahtarı = `value.Normalize(NormalizationForm.FormC)`, karşılaştırma `StringComparer.Ordinal`,
**büyük/küçük harf korunur**. `FomaTurkishMorphologyAnalyzer.AnalyzeBatch` bugün tam olarak bunu
yapıyor ([FomaTurkishMorphologyAnalyzer.cs:88](../src/EpubFixer.TrMorph/FomaTurkishMorphologyAnalyzer.cs#L88));
oracle aynı kuralı kullanmazsa ön-doldurulan kelime sorguda bulunamaz.

`TurkishWordNormalizer.Normalize` **kullanılmaz** — o küçük harfe çeviriyor. Morfoloji için
`Auersberger` ile `auersberger` aynı kelime değildir; özel isim davranışı buna bağlıdır.

### 2.5 Determinizm
Batch'e giden kelime listesi daima `Distinct(StringComparer.Ordinal).OrderBy(w => w, StringComparer.Ordinal)`
sonrası gönderilir. Cache dosyası da aynı sırayla yazılır. Aksi halde cache dosyası koşudan koşuya
değişir ve diff'i okunamaz hale gelir.

### 2.6 Komutlar
```bash
dotnet build EpubFixer.slnx
dotnet test EpubFixer.slnx
dotnet test tests/EpubFixer.Tests --filter "FullyQualifiedName~Morphology"
dotnet test tests/EpubFixer.Tests --filter "Category=Slow"
dotnet run --project src/EpubFixer.Cli -- fix "test-data/odun-kesmek/input.epub" --output out.epub --apply-ocr-corrections
dotnet run --project benchmarks/EpubFixer.QualityBenchmarks -- test-data/odun-kesmek
```

### 2.7 Commit disiplini
Kalem başına ayrı commit; ilk satır `R1.x: <ne yapıldı>`. R1.3 dört alt adıma bölünmüştür
(bölüm 8.1–8.4); her alt adım kendi commit'inde ve her commit'te **tüm suite yeşil**.

### 2.8 Kapsam disiplini
Her agent yalnızca kendi kalemini yapar. Yol boyunca fark edilen sorunlar düzeltilmez, bitiş
raporunda "gözlem" olarak yazılır.

---

## 3. Sıra ve paralellik

```
R1.0  (ölçüm, önce)  ─────────────────────────► baseline + golden değerler üretir
        │
        ▼
R1.1  (port + in-memory oracle)  ──┬──► R1.2  (disk cache)      [paralel, kısa]
                                   └──► R1.3  (çağrı yerleri)   [paralel, uzun]
```

- **R1.0 ilk başlar ve tek başına biter.** Ondan çıkan sayılar R1.3'ün kabul kriteridir; ölçüm
  yoksa R1.3'ün işe yarayıp yaramadığı tartışılamaz.
- **R1.1 kritik yolun başıdır**; R1.2 ve R1.3 ondan sonra paralel yürüyebilir, ortak dosyaya
  dokunmazlar (R1.2 yalnızca Cli, R1.3 Core + çağıran uçlar).
- R1.2 ve R1.3 aynı anda yürürse `Program.cs` wiring'i **bir kez** birleştirilir; bu birleştirme
  R1.3'ün son alt adımına aittir.

---

## 4. Mevcut durumun tespiti

Bu bölüm okunan koddan çıkarılmıştır; agent doğrulamadan kabul etmesin ama araştırmayı sıfırdan
yapması da gerekmez.

### 4.1 Morfoloji sorgu noktaları

| # | Çağrı yeri | Sorulan form | Sorgu kümesi çalıştırmadan bilinebilir mi? | Faz 1 kapsamında mı? |
|---|---|---|---|---|
| 1 | `HyphenationMorphologyAnalyzer:27` | `LeftPart + RightPart` | **Evet** — evidence listesi morfolojisiz üretiliyor | Evet |
| 2 | `OcrAnomalyDetector:94` | `candidate.Text` | **Evet** — aday span'leri morfolojisiz toplanıyor | Evet |
| 3 | `OcrRegionDetector:21,76,78,79` | `TrimBoundaryPunctuation(fragment)` | **Evet** — tüm fragment'lerin trim'li hali üst küme | Evet |
| 4 | `OcrCorrectionCandidateGenerator:87,90,155` | seed'ler, lexicon anahtarları, composite'ler | **Evet, ama aşamalı** — `OcrAnalysisReport` gerekiyor | Evet |
| 5 | `CleanTurkishLexicon.Load` | tr_50k satırları | Evet | **Hayır** (R2.3'ün işi) |
| 6 | `NoisyChannelRegionReconstructor:110`, `CurrentRegionReconstructor:35`, `DeterministicOcrCandidateReranker` | arama sırasında üretilen adaylar | Hayır | **Hayır** (R4.3'te siliniyor) |
| 7 | `FullBookReaderPreview`, `OcrReconstructionComparison`, `TrMorphReport` | karışık | — | **Hayır** (deneysel/rapor) |

### 4.2 Üretim hattındaki aşama noktaları

`EpubFixService.Fix` metnin **değiştiği** bir hat üzerinde morfolojiyi birden fazla kez soruyor
([EpubFixService.cs:84-104](../src/EpubFixer.Core/Fix/EpubFixService.cs#L84)). Tireleme birleştirmesi
yeni yüzey formlar üretir, dolayısıyla sorgu kümesi her aşamada büyüyebilir:

```
1. AnalyzeV2(afterV1Stream)               → tireleme birleşik formları
2. AnalyzeV2(afterV2Inline)               → mutasyondan sonraki yeni formlar
3. OcrAnomalyDetector(finalStream)        → aday span metinleri
4. OcrCorrectionCandidateGenerator(...)   → 3'ün çıktısına bağlı (aşamalı)
5. EpubOutputValidator → AnalyzeV2(output) → çıktı EPUB'ın formları
```

Bu yüzden "tek `Build` çağrısı" **mümkün değildir**; bkz. karar **D5**.

### 4.3 `ProcessInvocations` bir şey ölçmüyor

Yol haritası R1.3'ün kabulünü `TurkishMorphologyCacheStatistics.ProcessInvocations ≤ 1` diye
yazıyor. Ancak bu alan constructor'da `1` atanıyor ve bir daha **hiç artmıyor**
([FomaTurkishMorphologyAnalyzer.cs:64](../src/EpubFixer.TrMorph/FomaTurkishMorphologyAnalyzer.cs#L64)) —
yani bugün de `1`. Gerçek ölçüt `BatchRequests`'tir: eksik kelime içeren her stdin gidiş-dönüşünde
artar. Bkz. karar **D6**.

### 4.4 Bugünkü performans baseline'ı

`docs/baselines/odun-kesmek.performance.json`: full `fix --apply-ocr-corrections` = **24.166 sn**
(bütçe 120 sn). Morfolojinin bu sürenin ne kadarı olduğu **bilinmiyor** — R1.0'ın işi bunu ölçmektir.

---

## 5. R1.0 — Morfoloji çağrı sayacı ve golden değerler

> Yol haritasında yok; Faz 0'ın "önce ölç" kuralının Faz 1'e uygulanmasıdır (karar **D11**).
> Boyut: **S**. Bu kalem üretim kodu yazmaz, yalnızca ölçer ve kilitler.

### Amaç
İki soru: (a) bugün kaç morfoloji sorgusu, kaç batch, ne kadar süre? (b) R1.3'ten sonra çıktı
birebir aynı mı? İkincisi için refactor'dan **önce** bir golden değer kümesi kaydedilir.

### Dosya haritası
Yeni:
```
tests/EpubFixer.Tests/CountingMorphologyAnalyzer.cs     (ITurkishMorphologyAnalyzer + IBatchTurkishMorphologicalParser dekoratörü)
tests/EpubFixer.Tests/MorphologyCallTraceTests.cs
docs/baselines/odun-kesmek.morphology.json
```
Değişen: `docs/baselines/README.md` (yeni baseline dosyasının tanımı ve nasıl üretildiği).

### Ölçülecekler
`CountingMorphologyAnalyzer` gerçek `FomaTurkishMorphologyAnalyzer`'ı sarar ve sayar:
`IsValidWordCalls`, `AnalyzeCalls`, `AnalyzeBatchCalls`, `DistinctWords`, `MorphologyElapsedMs`
(yalnızca dekore edilen çağrılarda geçen süre). İç istatistikler (`CacheStatistics`) olduğu gibi
geçirilir ve rapora yazılır.

Koşulacak senaryo:
`new EpubFixService(analyzer).Fix("test-data/odun-kesmek/input.epub", …, applyOcrCorrections: true)`.

### Golden değerler
Aynı koşudan `EpubFixResult` sayaçları ve **çıktı paketinin logical text SHA-256'sı** alınır,
`MorphologyCallTraceTests` içinde sabit olarak assert edilir. R1.3 bu testi değiştirmeden geçirmek
zorundadır.

> EPUB **dosyasının** SHA'sı kullanılmaz — zip metadata'sı koşudan koşuya değişebilir.
> Logical text, `LogicalTextStreamBuilder.Build(package.SpineDocuments).Text` üzerinden hesaplanır.

### Yazılacak testler (önce kırmızı)
1. `CountingAnalyzer_DelegatesEveryCallAndCountsIt` — dekoratörün kendisi doğru mu (sentetik).
2. `CountingAnalyzer_ForwardsCacheStatistics`
3. `FullBookFix_MorphologyCallProfileIsRecorded` `[Trait("Category","Slow")]` — ölçülen değerleri
   basar, üst sınır assert'i koyar (bugünkü değerin ~%10 üstü).
4. `FullBookFix_ProducesGoldenLogicalText` `[Trait("Category","Slow")]` — çıktı logical text
   SHA-256 sabitine eşit.

### Baseline dosya şeması
```json
{
  "dataset": "odun-kesmek",
  "measuredOn": "<yyyy-MM-dd>",
  "commit": "<sha>",
  "machine": "<kısa tanım>",
  "scenario": "fix --apply-ocr-corrections",
  "isValidWordCalls": 0,
  "analyzeBatchCalls": 0,
  "batchRequests": 0,
  "distinctWords": 0,
  "morphologyElapsedSeconds": 0.0,
  "totalElapsedSeconds": 0.0,
  "goldenLogicalTextSha256": "<hex>"
}
```

### Kabul kriteri
- [ ] `docs/baselines/odun-kesmek.morphology.json` ölçülen değerlerle commit edilmiştir.
- [ ] Golden logical text SHA-256 testte sabit olarak yazılıdır.
- [ ] Morfolojinin 24.166 sn içindeki payı raporda sayıyla belirtilmiştir.
- [ ] Hiçbir üretim kaynak dosyası değişmemiştir (`git diff --stat src/ benchmarks/` boş).

### Kapsam dışı
Herhangi bir üretim kodu değişikliği. Optimizasyon. `EpubFixer.TrMorph` içine sayaç eklemek
(dekoratör testte kalır).

---

## 6. R1.1 — `IMorphologyOracle` port'u

### Amaç
B4'ün temel taşı: politika katmanının konuştuğu, **asla I/O yapmayan**, önceden doldurulmuş,
değişmez bir morfoloji kaynağı.

### Dosya haritası
Yeni:
```
src/EpubFixer.Core/Morphology/IMorphologyOracle.cs
src/EpubFixer.Core/Morphology/IMorphologyOracleBuilder.cs
src/EpubFixer.Core/Morphology/MorphologyOracle.cs
src/EpubFixer.Core/Morphology/MorphologyOracleException.cs
src/EpubFixer.Cli/Morphology/FomaMorphologyOracleBuilder.cs
tests/EpubFixer.Tests/FakeMorphologyOracle.cs            (test yardımcısı; R1.3 kullanacak)
tests/EpubFixer.Tests/MorphologyOracleTests.cs
tests/EpubFixer.Tests/FomaMorphologyOracleBuilderTests.cs
```
Bu kalemde **hiçbir mevcut dosya değişmez.** Yeni tipler kullanılmadan durur; çağrı yerleri R1.3'ün işidir.

### Sözleşme

```csharp
namespace EpubFixer.Core.Morphology;

/// <summary>Pre-resolved morphology answers. Never performs I/O.</summary>
public interface IMorphologyOracle
{
    /// <summary>Whether the word has at least one parse. Throws when the word was not pre-filled.</summary>
    bool IsValid(string word);

    /// <summary>Every parse for the word. Throws when the word was not pre-filled.</summary>
    IReadOnlyList<TurkishMorphologicalAnalysis> Analyze(string word);

    /// <summary>Whether the oracle holds an answer for the word. Never throws for a valid word.</summary>
    bool IsKnown(string word);
}

/// <summary>
/// Resolves a vocabulary into an oracle. Implemented at the adapter edge — this is where I/O lives.
/// Monotone: may be called more than once; every returned oracle answers everything this builder
/// has resolved so far (see D5).
/// </summary>
public interface IMorphologyOracleBuilder
{
    IMorphologyOracle Build(IEnumerable<string> vocabulary);
}

public sealed class MorphologyOracleException : Exception;
```

`MorphologyOracle` — `IReadOnlyDictionary<string, IReadOnlyList<TurkishMorphologicalAnalysis>>`
üstünde değişmez bir sarmalayıcı; constructor sözlüğü kopyalar.

### Kesin davranış kuralları

| Girdi | `IsValid` / `Analyze` | `IsKnown` |
|---|---|---|
| Ön-doldurulmuş, parse'ı var | `true` / dolu liste | `true` |
| Ön-doldurulmuş, parse'ı yok | `false` / boş liste | `true` |
| Ön-doldurulmamış | **`MorphologyOracleException`** | `false` |
| `null` | `ArgumentNullException` | `ArgumentNullException` |
| boş / whitespace | `ArgumentException` | `ArgumentException` |

- Anahtar kuralı: bölüm 2.4 — NFC, Ordinal, case korunur.
- İstisna mesajı sorulan kelimeyi **ve** oracle'ın boyutunu içerir; ön-doldurma açığını teşhis
  edilebilir kılan budur:
  `Morphology oracle was not pre-filled for 'koltukta' (oracle holds 41873 words).`
- `MorphologyOracle` değişmezdir; kilit kullanmaz, paralel okunabilir.
- "Parse'ı yok" ile "bilinmiyor" **farklı** durumlardır. Bugünkü `IsValidWord` ikisini de `false`
  olarak veriyor; bu ayrımı kurmak bu kalemin asıl değeridir.

### `FomaMorphologyOracleBuilder` (Cli)
- `FomaTurkishMorphologyAnalyzer`'ı **tembel** oluşturur: ilk eksik kelimeye kadar process başlamaz.
  R1.2'nin "ikinci koşuda process başlamaz" kabulü buna bağlıdır.
- `Build`: gelen kelimeler NFC → distinct(Ordinal) → sıralı; yalnızca eksikler için tek
  `AnalyzeBatch`; sonuç iç sözlüğe eklenir; anlık görüntüden yeni bir `MorphologyOracle` döner.
- `IDisposable` — analyzer'ı sahiplenir; oluşturulmadıysa `Dispose` hiçbir şey yapmaz.

### Yazılacak testler (önce kırmızı)

`MorphologyOracleTests`
1. `IsValid_ReturnsTrueForPrefilledWordWithAnalyses`
2. `IsValid_ReturnsFalseForPrefilledWordWithoutAnalyses`
3. `IsValid_ThrowsForUnknownWord`
4. `Analyze_ThrowsForUnknownWord`
5. `IsKnown_ReturnsFalseForUnknownWordAndDoesNotThrow`
6. `Exception_MessageNamesTheMissingWord`
7. `Lookup_UsesNfcNormalizedKey` — dekompoze `ı`/`ş` içeren form NFC anahtarıyla bulunur
8. `Lookup_IsCaseSensitive` — `Auersberger` bilinir, `auersberger` bilinmez
9. `Constructor_CopiesVocabularySoLaterMutationIsNotVisible`
10. `IsValid_ThrowsArgumentExceptionForWhitespace`

`FomaMorphologyOracleBuilderTests` (gerçek TRmorph, küçük kelime kümesi)
11. `Build_ResolvesEveryRequestedWord`
12. `Build_SecondCallReturnsOracleContainingBothVocabularies` — monotonluk (D5)
13. `Build_DoesNotStartProcessWhenVocabularyIsEmpty`
14. `Build_SendsEachDistinctWordOnce` — aynı kelime iki kez verilince `BatchedWords` bir kez artar
15. `Build_IsDeterministicInBatchOrder` — farklı sırayla verilen aynı küme aynı sonucu verir

### Kabul kriteri
- [ ] `EpubFixer.Core/Morphology` altında `System.IO`, `System.Diagnostics` veya `EpubFixer.TrMorph` importu yok.
- [ ] Ön-doldurulmamış kelime **sessizce `false` dönmüyor**, istisna atıyor ve mesaj kelimeyi içeriyor.
- [ ] İki `Build` çağrısı tek process kullanıyor; ikincisi yalnızca farkı batch'liyor.
- [ ] Mevcut testlerin hiçbiri değişmedi (bu kalem hiçbir çağrı yerine dokunmaz).

### Kapsam dışı
Çağrı yerlerini taşımak (R1.3), disk cache (R1.2), `ITurkishMorphologyAnalyzer`'ı silmek.

---

## 7. R1.2 — Oracle disk cache'i

### Amaç
Aynı kitapta tekrar koşularda TRmorph'u **hiç başlatmamak**. Geliştirme döngüsünün ve benchmark
koşularının maliyetini düşürür.

### Dosya haritası
Yeni:
```
src/EpubFixer.Cli/Morphology/MorphologyOracleCache.cs
src/EpubFixer.Cli/Morphology/MorphologyCacheDocument.cs
src/EpubFixer.Cli/Morphology/CachingMorphologyOracleBuilder.cs
tests/EpubFixer.Tests/MorphologyOracleCacheTests.cs
```
Değişen: `src/EpubFixer.Cli/Program.cs` (yalnızca wiring; R1.3 paralel yürüyorsa birleştirme
R1.3'ün son alt adımına bırakılır).

### Sözleşme

```csharp
namespace EpubFixer.Cli.Morphology;

/// <summary>Decorates a builder with an on-disk snapshot of resolved analyses.</summary>
public sealed class CachingMorphologyOracleBuilder : IMorphologyOracleBuilder, IDisposable
{
    public CachingMorphologyOracleBuilder(IMorphologyOracleBuilder inner, MorphologyOracleCache cache);
    public IMorphologyOracle Build(IEnumerable<string> vocabulary);
    public void Flush();       // writes the snapshot when it changed; Dispose also flushes
}

public sealed class MorphologyOracleCache
{
    public MorphologyOracleCache(string sourceEpubPath, string? transducerPath = null, string? directory = null);
    public static string DefaultDirectory { get; }   // %LOCALAPPDATA%/EpubFixer/morphology
    public string CacheKey { get; }                  // sha256(epub) + "-" + sha256(trmorph.fst)[..16]
}
```

### Kesin davranış kuralları
- **Anahtar** = kaynak EPUB SHA-256 + `trmorph.fst` SHA-256. (fst 2.1 MB; hash maliyeti ihmal
  edilebilir. Ölçülen maliyet 250 ms'yi aşarsa dur, bildir — dosya boyutu + mtime'a düşmek bir
  seçenek ama karar tablosuna yazılmalı.)
- **Kısmi isabet normaldir.** Kod değişince sorgu kümesi büyür. `Build`, cache'te olmayanları inner
  builder'dan çözer ve dosyayı yeniden yazar. Cache asla "eksik olduğu için geçersiz" sayılmaz.
- **Bozuk / okunamayan / şema-uyumsuz cache sessizce yeniden üretilir.** Cache bir doğruluk kaynağı
  değil, hızlandırmadır: silinmesi hiçbir sonucu değiştirmemelidir. Bunu bir test kilitler.
- **Yazma atomiktir**: geçici dosya + `File.Move(overwrite: true)`. Yarıda kesilen koşu bozuk cache
  bırakmaz.
- **Serileştirme deterministiktir**: kelimeler Ordinal sıralı, tag'ler Ordinal sıralı, indent'li JSON,
  `schemaVersion` alanı var. Uyuşmazlıkta dosya yok sayılır.
- **Flush zamanlaması**: koşu sonunda açıkça `Flush()`; `Dispose` da flush eder. Her `Build`'de
  yazmak 40k+ kelimelik dosyayı gereksiz yere birkaç kez diske yazar.

### Yazılacak testler (önce kırmızı)
1. `Build_WritesSnapshotAfterResolvingUnknownWords`
2. `Build_SecondRunAnswersFromDiskWithoutCallingInner` — sahte inner builder, çağrı sayısı 0
3. `Build_PartialHitResolvesOnlyTheDelta` — inner'a yalnızca eksik kelimeler gider
4. `Build_CorruptCacheFileIsRegeneratedSilently`
5. `Build_SchemaVersionMismatchIsIgnoredSilently`
6. `Build_CacheKeyChangesWhenTransducerChanges`
7. `Build_CacheKeyChangesWhenSourceEpubChanges`
8. `Cache_FileIsDeterministicAcrossRuns` — aynı girdi, byte-eşit dosya
9. `Cache_WriteIsAtomic` — yarıda kesilen yazma sonrası eski dosya bozulmamış
10. `Build_MissingCacheDirectoryIsCreated`
11. `FullRun_SecondInvocationStartsNoFlookupProcess` `[Trait("Category","Slow")]` — gerçek kitap, iki
    ardışık koşu; ikincisinde `FomaTurkishMorphologyAnalyzer` **hiç oluşturulmaz** (tembel oluşturmayı
    gözleyen bir fabrika ile doğrulanır)

### Kabul kriteri
- [ ] İkinci koşuda flookup process'i başlamıyor (test 11 bunu kanıtlıyor).
- [ ] Cache dosyasını silmek hiçbir sonucu değiştirmiyor, yalnızca ilk koşuyu yavaşlatıyor.
- [ ] Cache dosyası deterministik ve diff alınabilir.
- [ ] `EpubFixer.Core` içinde cache'e dair tek satır yok.

### Kapsam dışı
Çağrı yerleri (R1.3), cache'i çok-kitaplı/paylaşımlı hale getirmek, sıkıştırma, cache temizleme
komutu.

---

## 8. R1.3 — Çağrı yerlerini oracle'a taşı

> Bu fazın kalbi ve tek büyük kalemi. Yol haritası **M** diyor; okunan koda göre gerçekçi boyut **L**.
> Dört alt adıma bölünmüştür; her adım kendi commit'inde ve tüm suite yeşil bırakır.

### Amaç
`ITurkishMorphologyAnalyzer` bağımlılığını üretim ve benchmark sıcak yollarından kaldırmak.
Sonuç: sıcak döngüde **hiç** process çağrısı; tüm sorgular ön-doldurulmuş sözlükten.

### Temel fikir: sorgu kümesi bileşenin **kendisinden** gelir

Ön-doldurma listesini ayrı bir "planlayıcı" sınıfta yeniden yazmak, bu kalemi kesin olarak
başarısız kılacak yaklaşımdır: kural kopyalanır, zamanla kayar, ön-doldurma açığı üretir.
Bunun yerine **her bileşen kendi sorgu kümesini açığa çıkarır** ve bu küme, çağrı yapan kodun
kullandığı yolun **aynısından** üretilir:

```csharp
// OcrAnomalyDetector — bugün zaten adayları morfolojisiz topluyor; yalnızca ikiye ayrılıyor
internal IReadOnlyList<OcrCandidateSpan> CollectCandidates(LogicalTextStream stream);       // saf
public IEnumerable<string> EnumerateMorphologyQueries(LogicalTextStream stream);            // = CollectCandidates(...).Select(span => span.Text)
public OcrAnalysisReport Analyze(LogicalTextStream stream, IMorphologyOracle oracle);       // CollectCandidates'i çağırır
```

Aynı desen dört bileşende de uygulanır:

| Bileşen | `EnumerateMorphologyQueries` girdisi | Ürettiği küme |
|---|---|---|
| `HyphenationMorphologyAnalyzer` | `IReadOnlyList<HyphenationEvidence>` | `LeftPart + RightPart` |
| `OcrAnomalyDetector` | `LogicalTextStream` | aday span metinleri |
| `OcrRegionDetector` | `string text` | tüm fragment'lerin `TrimBoundaryPunctuation`'lı hali |
| `OcrCorrectionCandidateGenerator` | `OcrAnalysisReport` + `LogicalTextStream` + `BookLexicon` | structural seed'ler ∪ lexicon anahtarları ∪ composite'ler |

Generator'ın kümesi bir **üst-yaklaşımdır** (gereğinden fazla kelime doldurabilir). Bu kabul
edilebilir; eksik doldurmak kabul edilemez.

### Emniyet ağı: eksiksizlik testi

Üst-yaklaşımın yeterli olduğu iddiası test edilmeden kabul edilmez.
`RecordingMorphologyOracle`, bilinmeyen kelimede istisna atmak yerine kelimeyi **kaydeder** ve
gerçek analyzer'a sorar. Full kitap koşusundan sonra kaydedilen kümenin **boş** olduğu assert edilir.
Bu test, gelecekte bir bileşen yeni bir form sormaya başladığında derlemeyi değil **testi** kırar —
ve hangi kelimede kırıldığını söyler.

### 8.1 Alt adım A — desen kurulumu: `HyphenationMorphologyAnalyzer`
En küçük ve en yalın çağrı yeri; deseni kurar.
- `Analyze(evidence, ITurkishMorphologyAnalyzer)` → `Analyze(evidence, IMorphologyOracle)`.
- `EnumerateMorphologyQueries(evidence)` eklenir.
- `HyphenationPipeline.AnalyzeV2` ve `OcrAnalysisService.AnalyzeHyphenationV2` ön-doldurma yapar.
- `EpubFixService` ve `EpubOutputValidator` `IMorphologyOracleBuilder` alır (D7).
- Bu adımda `Program.cs` geçici olarak `FomaMorphologyOracleBuilder` ile wire edilir.

### 8.2 Alt adım B — `OcrAnomalyDetector`
- Aday toplama saf metoda ayrılır; `Analyze` onu kullanır. **Aday kümesi birebir aynı kalmalı.**
- `OcrAnalysisService.AnalyzeCorrections` önce ön-doldurur, sonra `Analyze` çağırır.
- Koruyucu test: `OcrCorrectionV111RealRegressionTests` değişmeden geçer
  (`TotalExamined = 46_927`, `Candidates.Count = 893` vb.).

### 8.3 Alt adım C — `OcrCorrectionCandidateGenerator`
- En zor adım: sorgular **üretilmiş** formlar üzerinde.
- `StructuralSeeds` ve composite üretimi saf, yeniden kullanılabilir metotlara çıkarılır;
  `EnumerateMorphologyQueries` bunları **aynı** metotlardan üretir.
- Ön-doldurma alt adım B'nin çıktısına bağlı olduğu için ikinci bir `Build` çağrısı gerekir (D5).
- `lexicon.Entries` anahtarlarının tamamı doldurulur — bu, bugünkü davranışın üst kümesidir ve
  kitap boyutunda sabittir.

### 8.4 Alt adım D — `OcrRegionDetector` + wiring + temizlik
- `Detect(text, ITurkishMorphologyAnalyzer)` → `Detect(text, IMorphologyOracle)`.
- `Program.cs`: `debug-ocr-region`, `fix` ve (varsa) R1.2'nin cache wiring'i birleştirilir.
- Benchmark (`QualityBenchmarkRunner`, `OcrDetectionSource`) oracle'a geçirilir.
- `ITurkishMorphologyAnalyzer` **silinmez** — `CleanTurkishLexicon`, NoisyChannel hattı ve raporlama
  araçları onu kullanmaya devam eder (D7). Bu tipin kaderi R4.3'ün işidir.

### Dosya haritası (birleşik)
Değişen:
```
src/EpubFixer.Core/Morphology/HyphenationMorphologyAnalyzer.cs
src/EpubFixer.Core/Ocr/OcrAnomalyDetector.cs
src/EpubFixer.Core/Ocr/OcrCorrectionCandidateGenerator.cs
src/EpubFixer.Core/Ocr/OcrRegionDetector.cs
src/EpubFixer.Core/Ocr/OcrAnalysisService.cs
src/EpubFixer.Core/Fix/EpubFixService.cs
src/EpubFixer.Core/Fix/HyphenationPipeline.cs
src/EpubFixer.Core/Fix/EpubOutputValidator.cs
src/EpubFixer.Cli/Program.cs
benchmarks/EpubFixer.QualityBenchmarks/QualityBenchmarkRunner.cs
benchmarks/EpubFixer.QualityBenchmarks/OcrDetectionSource.cs
tests/… (fake analyzer'lar fake oracle'a çevrilir)
```
Yeni:
```
src/EpubFixer.Core/Morphology/MorphologyPrefill.cs        (sorgu kümelerini birleştiren ince yardımcı)
tests/EpubFixer.Tests/RecordingMorphologyOracle.cs
tests/EpubFixer.Tests/MorphologyPrefillCompletenessTests.cs
```

### Yazılacak testler (önce kırmızı)
1. `HyphenationMorphologyAnalyzer_EnumeratesExactlyTheWordsItQueries` — kayıt tutan oracle ile
   sorulan küme, enumerate edilen kümeye eşit
2. `OcrAnomalyDetector_EnumeratesExactlyTheWordsItQueries`
3. `OcrRegionDetector_EnumerationCoversEveryQueriedForm` (üst küme yeterli)
4. `OcrCorrectionCandidateGenerator_EnumerationCoversEveryQueriedForm`
5. `OcrAnomalyDetector_CandidateCollectionIsIndependentOfMorphology` — aday span kümesi oracle'dan
   bağımsız
6. `Analyze_ThrowsWhenOracleWasNotPrefilled` — ön-doldurma atlanırsa **gürültülü** hata
7. `MorphologyPrefillCompletenessTests.FullBookFix_RecordsNoUnknownQueries` `[Trait("Category","Slow")]`
8. `FullBookFix_BatchRequestCountIsBounded` `[Trait("Category","Slow")]` — R1.0'da ölçülen değerde
   kilitli; kitap boyutundan bağımsız
9. `FullBookFix_ProducesGoldenLogicalText` — R1.0'ın golden testi **değiştirilmeden** yeşil
10. Mevcut fake analyzer kullanan tüm testler fake oracle'a çevrilir; **beklenen değerleri değişmez**
    (`OcrRegionDetectorTests`, `OcrAnomalyDetectorTests`, `OcrCorrectionCandidateGeneratorTests`,
    `HyphenationMorphologyAnalyzerTests`, `OcrAnalysisReportingTests`)

### Kabul kriteri
- [ ] `dotnet test EpubFixer.slnx` yeşil; hiçbir mevcut assert **değeri** değişmedi.
- [ ] `OcrCorrectionV111RealRegressionTests` ve `EpubFixServiceTests` dokunulmadan geçiyor.
- [ ] Full koşuda `BatchRequests` R1.0 ölçümüne göre sabit ve kitap boyutundan bağımsız; sıcak
      döngüde hiç morfoloji I/O'su yok (oracle istisna atmıyor = kanıt).
- [ ] `MorphologyPrefillCompletenessTests`'te kaydedilen bilinmeyen kelime sayısı **0**.
- [ ] `docs/baselines/odun-kesmek.performance.json` ve `.morphology.json` yeni değerlerle güncellendi.
- [ ] Üretim ve benchmark yollarında `ITurkishMorphologyAnalyzer` kullanan tek satır kalmadı
      (deneysel Cli hattı, `CleanTurkishLexicon` ve raporlama araçları hariç — D7).

### Kapsam dışı
- Dedektörleri iyileştirmek, aday kümesini değiştirmek, eşik ayarlamak.
- `CleanTurkishLexicon` / `BookContextIndex` / `OcrEditCostModel` taşımak (R2.3).
- NoisyChannel hattını oracle'a çevirmek (R4.3'te siliniyor).
- `ITurkishMorphologyAnalyzer`'ı silmek.

---

## 9. Kararlar (bu planla birlikte verildi)

| # | Karar | Gerekçe | Nereye işlendi |
|---|---|---|---|
| D5 | `IMorphologyOracleBuilder.Build` **birden fazla kez** çağrılabilir ve monotondur: dönen her oracle, o ana kadar çözülmüş her şeyi bilir. Yol haritasının "TEK batch" ifadesi "kelime başına değil, **aşama başına** tek batch" olarak okunur. | `EpubFixService.Fix` metni değiştirerek beş ayrı aşamada morfoloji soruyor (bölüm 4.2); tek `Build` fiziksel olarak mümkün değil. İmza değişmiyor, yalnızca semantik netleşiyor. | Bölüm 6 |
| D6 | R1.3'ün kabul ölçütü `ProcessInvocations ≤ 1` yerine **`BatchRequests` = R1.0'da ölçülen sabit değer, kitap boyutundan bağımsız**. `ProcessInvocations == 1` ayrıca korunur. | `ProcessInvocations` constructor'da 1 atanıp hiç artmıyor; bugün de 1, yani hiçbir şey ölçmüyor (bölüm 4.3). | Bölüm 5, 8 |
| D7 | Orkestratörler (`EpubFixService`, `OcrAnalysisService`, `EpubOutputValidator`) `IMorphologyOracleBuilder` alır; yaprak politika bileşenleri (`OcrAnomalyDetector`, `OcrRegionDetector`, `OcrCorrectionCandidateGenerator`, `HyphenationMorphologyAnalyzer`) saf `IMorphologyOracle` alır. | Bağımlılık kuralı: I/O yapabilen port yalnızca akışı yöneten kenarda durur; karar veren kod saf kalır. | Bölüm 8 |
| D8 | Ön-doldurma sorgu kümeleri ayrı bir planlayıcıda **yeniden yazılmaz**; her bileşen kendi kümesini, çağrı yaptığı kodun aynısından üretir. Üstüne eksiksizlik testi konur. | Kural kopyalama bu kalemi sessizce başarısız kılar; ön-doldurma açığı R1.1 kuralı gereği çalışma zamanı istisnasıdır. | Bölüm 8 |
| D9 | Oracle anahtarı NFC + Ordinal + **case-sensitive**. `TurkishWordNormalizer.Normalize` kullanılmaz. | O metot küçük harfe çeviriyor; morfolojide `Auersberger` ≠ `auersberger` ve özel isim davranışı buna bağlı. | Bölüm 2.4 |
| D10 | Yeni `MorphologyOracleException` Core'da tanımlanır. | `TurkishMorphologyException` `EpubFixer.TrMorph` içinde; Core'un onu kullanması bağımlılık kuralını ihlal eder. | Bölüm 6 |
| D11 | Yol haritasında olmayan **R1.0 (ölçüm)** kalemi eklendi ve önce koşuyor. | Faz 0'ın kuralı: ölçüm olmadan optimizasyon yön duygusu olmadan yürümektir. Ayrıca R1.3'ün "davranış değişmedi" iddiası, refactor öncesi golden değer olmadan doğrulanamaz. | Bölüm 5 |

Yeni bir karar ihtiyacı doğarsa agent kendi başına karara varmaz; gerekçeyi bildirip bekler ve
karar bu tabloya eklenir.

---

## 10. Risk kaydı (Faz 1'e özgü)

| # | Risk | Etki | Azaltma |
|---|---|---|---|
| 1 | Ön-doldurma açığı → koşu ortasında istisna | Orta (gürültülü, sessiz değil) | D8 + eksiksizlik testi; üst-yaklaşım serbest |
| 2 | Refactor davranışı sessizce değiştirir | **Yüksek** | R1.0 golden değerleri; mevcut assert değerlerini değiştirmek yasak (2.3) |
| 3 | Anahtar normalizasyonu uyuşmazlığı → her kelime "bilinmiyor" | Yüksek | D9 + `Lookup_UsesNfcNormalizedKey` / `Lookup_IsCaseSensitive` testleri |
| 4 | Generator'ın sorgu kümesi pratikte çok büyük (tüm lexicon) → batch şişer | Düşük | Lexicon zaten kitap boyutunda; R1.0 gerçek boyutu verir. 100k kelimeyi aşarsa dur ve raporla |
| 5 | R1.2 ve R1.3 paralel yürürken `Program.cs` çakışması | Düşük | Wiring birleştirmesi R1.3 alt adım D'ye ait |
| 6 | Faz 1 kaliteyi iyileştirmediği için "boşa iş" görünür | Düşük | Kabul kriteri kaliteyi **sabit tutmak**; kazanç süre ve Faz 3'ün mümkün hale gelmesi |

---

## 11. Devir promptları

### R1.0
```
EpubFixer projesinde docs/phase-1-plan.md'deki R1.0 kalemini uygulayacaksın.

Önce şunları oku:
- docs/phase-1-plan.md — bölüm 2 (ortak kurallar), bölüm 4 (mevcut durum), bölüm 5 (R1.0)
- docs/ocr-correction-roadmap.md — B4 tespiti ve Faz 1
- src/EpubFixer.Core/Fix/EpubFixService.cs
- src/EpubFixer.TrMorph/FomaTurkishMorphologyAnalyzer.cs
- tests/EpubFixer.Tests/PerformanceBudgetTests.cs (yavaş test deseni, repo dosyası bulma)
- docs/baselines/README.md ve docs/baselines/odun-kesmek.performance.json

Kurallar:
- Bu kalem ÜRETİM KODU DEĞİŞTİRMEZ. Yalnızca test yardımcısı, ölçüm ve baseline dosyası yazar.
  Bitirdiğinde `git diff --stat src/ benchmarks/` boş olmalı.
- Test-first: kırmızı test → minimum kod → refactor.
- Ölçülen sayılar gerçek koşudan gelmeli, tahminden değil.
- Golden SHA-256'yı EPUB DOSYASININ değil, çıktı paketinin logical text'inin üzerinden hesapla
  (zip metadata'sı koşudan koşuya değişir).
- Kapsam R1.0 ile sınırlı. Optimizasyon yapma, EpubFixer.TrMorph'a sayaç ekleme.

Bitirdiğinde: ölçülen çağrı sayıları (IsValidWord, AnalyzeBatch, BatchRequests, distinct kelime),
morfolojinin toplam süre içindeki payı, golden değerler, eklenen testler, kabul kriterinin durumu.
```

### R1.1
```
EpubFixer projesinde docs/phase-1-plan.md'deki R1.1 kalemini uygulayacaksın.

Önce şunları oku:
- docs/phase-1-plan.md — bölüm 2, bölüm 6 (R1.1), bölüm 9 (kararlar D5, D9, D10)
- docs/ocr-correction-roadmap.md — R1.1 maddesi ve B4 tespiti
- src/EpubFixer.Core/Morphology/ (dört dosyanın tamamı)
- src/EpubFixer.TrMorph/FomaTurkishMorphologyAnalyzer.cs — özellikle AnalyzeBatch ve normalizasyon
- src/EpubFixer.Cli/Quality/FrequencyMorphologyWordRecognizer.cs (tek-batch doldurma deseni)

Kurallar:
- Test-first: kırmızı test → minimum kod → refactor.
- Bağımlılıklar içeri doğru: EpubFixer.Core/Morphology altına System.IO, System.Diagnostics veya
  EpubFixer.TrMorph importu GİRMEZ. Yeni istisna tipi Core'da tanımlanır.
- Plandaki sözleşmeyi (IMorphologyOracle, IMorphologyOracleBuilder, MorphologyOracle) aynen kullan;
  değiştirmen gerekirse önce gerekçesini söyle.
- Ön-doldurulmamış kelimede IsValid/Analyze İSTİSNA ATAR, sessizce false dönmez. Mesaj kelimeyi
  içermeli. "Parse'ı yok" (false) ile "bilinmiyor" (istisna) farklı durumlardır.
- Anahtar kuralı: NFC, StringComparer.Ordinal, büyük/küçük harf KORUNUR.
  TurkishWordNormalizer.Normalize kullanma — o küçük harfe çeviriyor.
- FomaMorphologyOracleBuilder analyzer'ı TEMBEL oluşturmalı: ilk eksik kelimeye kadar process
  başlamaz (R1.2 buna bağlı).
- Bu kalem HİÇBİR mevcut dosyayı değiştirmez; yeni tipler kullanılmadan durur.
- Kapsam R1.1 ile sınırlı. Çağrı yerlerine (R1.3) ve cache'e (R1.2) dokunma.

Bitirdiğinde: eklenen testler, kabul kriterinin durumu, planda güncellenmesi gereken bir şey olup
olmadığı.
```

### R1.2
```
EpubFixer projesinde docs/phase-1-plan.md'deki R1.2 kalemini uygulayacaksın.
R1.1 bitmiş olmalı; bitmediyse başlamadan bildir.

Önce şunları oku:
- docs/phase-1-plan.md — bölüm 2, bölüm 7 (R1.2), bölüm 9 (karar D5)
- docs/ocr-correction-roadmap.md — R1.2 maddesi
- src/EpubFixer.Core/Morphology/IMorphologyOracle.cs, IMorphologyOracleBuilder.cs (R1.1 çıktısı)
- src/EpubFixer.Cli/Morphology/FomaMorphologyOracleBuilder.cs (R1.1 çıktısı)
- src/EpubFixer.Cli/Program.cs (wiring deseni)

Kurallar:
- Test-first.
- Cache bir HIZLANDIRMADIR, doğruluk kaynağı değil. Cache dosyasını silmek hiçbir sonucu
  değiştirmemeli; bunu bir testle kilitle.
- Kısmi isabet normaldir: eksikler inner builder'dan çözülür ve dosya yeniden yazılır.
  "Eksik cache = geçersiz cache" DEĞİLDİR.
- Bozuk / şema-uyumsuz dosya sessizce yeniden üretilir.
- Yazma atomik (geçici dosya + move), serileştirme deterministik (Ordinal sıralı) olmalı.
- İkinci koşuda flookup process'i HİÇ başlamamalı — kalemin en önemli kabul kriteri budur;
  process oluşturmayı gözleyen bir fabrika ile test et.
- Cache kodu yalnızca EpubFixer.Cli içinde; EpubFixer.Core'a tek satır girmez.
- Kapsam R1.2 ile sınırlı. Çağrı yerlerine (R1.3) dokunma. R1.3 paralel yürüyorsa Program.cs
  çakışmasını ona bırak ve raporunda belirt.

Bitirdiğinde: ölçülen ikinci-koşu süresi, cache dosyasının boyutu, eklenen testler, kabul
kriterinin durumu.
```

### R1.3
```
EpubFixer projesinde docs/phase-1-plan.md'deki R1.3 kalemini uygulayacaksın.
R1.0 ve R1.1 bitmiş olmalı; bitmediyse başlamadan bildir.

Önce şunları oku:
- docs/phase-1-plan.md — bölüm 2, bölüm 4 (sorgu noktaları ve aşama noktaları tabloları),
  bölüm 8 (R1.3), bölüm 9 (kararlar D5, D7, D8)
- docs/ocr-correction-roadmap.md — R1.3 maddesi, B4 ve B5 tespitleri
- src/EpubFixer.Core/Morphology/HyphenationMorphologyAnalyzer.cs
- src/EpubFixer.Core/Ocr/OcrAnomalyDetector.cs, OcrRegionDetector.cs,
  OcrCorrectionCandidateGenerator.cs, OcrAnalysisService.cs
- src/EpubFixer.Core/Fix/EpubFixService.cs, HyphenationPipeline.cs, EpubOutputValidator.cs
- tests/EpubFixer.Tests/OcrCorrectionV11RealRegressionTests.cs (davranış korunumunun kanıtı)

Kurallar:
- Test-first.
- DAVRANIŞ KORUNUMU birinci kuraldır: mevcut hiçbir testin beklenen DEĞERİ değiştirilmez.
  Bir test kırılıyorsa refactor davranışı değiştirmiştir; değeri güncelleme, sebebi bul.
  Zorunlu bir değişiklik gerekiyorsa DUR ve gerekçesiyle bildir.
- Ön-doldurma sorgu kümesini ayrı bir planlayıcıda YENİDEN YAZMA. Her bileşen kendi kümesini,
  çağrı yaptığı kodun aynısından üretsin (bölüm 8, D8).
- Orkestratörler IMorphologyOracleBuilder alır; yaprak bileşenler saf IMorphologyOracle alır (D7).
- Dört alt adımı (A: HyphenationMorphologyAnalyzer, B: OcrAnomalyDetector,
  C: OcrCorrectionCandidateGenerator, D: OcrRegionDetector + wiring) AYRI COMMIT'lerde yap;
  her commit'te tüm suite yeşil kalsın.
- MorphologyPrefillCompletenessTests'i erken yaz; kaydedilen bilinmeyen kelime sayısı 0 olmalı.
- ITurkishMorphologyAnalyzer'ı SİLME — CleanTurkishLexicon, NoisyChannel hattı ve raporlama
  araçları onu kullanmaya devam ediyor. Onların kaderi R4.3'ün işi.
- Kapsam R1.3 ile sınırlı. Dedektörleri iyileştirme, aday kümesini değiştirme, Cli→Core taşıma
  (R2.3) yapma.

Bitirdiğinde: alt adım başına ne değişti, ölçülen BatchRequests ve süre (R1.0 baseline'ına karşı),
eksiksizlik testinin sonucu, golden değerlerin korunup korunmadığı, eklenen/çevrilen testler,
kabul kriterinin durumu, planda güncellenmesi gereken bir şey olup olmadığı.
```

---

## 12. Faz 1'in Faz 2+ ile sözleşmesi

- `IMorphologyOracle` R2.1'de `BookVocabulary`'nin **girdisidir**: "morfolojik olarak geçerli formlar"
  kümesi oracle'dan gelir. Oracle'ın arayüzü R2.1'de değişmez.
- `IMorphologyOracleBuilder`'ın monoton semantiği (D5) R3.x'te de geçerlidir: lattice motoru
  vocabulary'yi build aşamasında kurar, decode sırasında oracle'a hiç sormaz.
- R1.0'ın golden logical text SHA-256'sı, R4.2'de üretim hattı değiştiğinde **kasıtlı olarak**
  değişecektir; o noktada yeni golden değer, yeni motorun gerekçesiyle birlikte kaydedilir.
- `ITurkishMorphologyAnalyzer`'ın silinmesi R4.3'e aittir; Faz 1 onu yalnızca sıcak yoldan çıkarır.
- R1.2'nin cache anahtarı (EPUB SHA + fst SHA) R2.1'de `BookVocabulary` cache'i eklenirse aynı
  desenle genişletilir.
