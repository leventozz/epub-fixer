# Faz 0 — Ölçüm ve emniyet ağı — Uygulama planı

> Bu belge [ocr-correction-roadmap.md](ocr-correction-roadmap.md) Faz 0'ın (R0.1–R0.4) uygulama
> planıdır. Her kalem ayrı bir oturumda ayrı bir agent'a devredilebilir; bölüm 8'de kopyala-yapıştır
> devir promptları vardır.
>
> Faz 0'ın tek işi vardır: **yönü gösteren sayıları üretmek.** Hiçbir kalem düzeltme kalitesini
> iyileştirmez, hiçbir kalem B1–B5'i çözmez. Bu fazda ölçüm aracı yazılır, motor yazılmaz.

---

## 1. Faz 0 bittiğinde elde ne olacak

| Çıktı | Dosya | Ne işe yarar |
|---|---|---|
| Kitap sağlık metriği | `EpubFixer.Core/Quality/` + `epubfixer measure` | Kuzey yıldızı: tek sayı, kaynak vs çıktı |
| Baseline kayıtları | `docs/baselines/*.json` | Her fazın sonunda "iyileşti mi?" sorusunun cevabı |
| Sınıflandırılmış ground truth | `test-data/odun-kesmek/ground-truth.json` v2 | Hangi hata sınıfında ne kadar iyiyiz |
| Performans bütçesi | `tests/.../PerformanceBudgetTests.cs` | 120 sn ve state sayısı regresyonu sessizce dönemez |
| Gerçekçi kalite kapısı | `QualityBenchmarkGateEvaluator` + gate profili | İstatistiksel motorla kullanılabilir kapı |

**Faz 0'ın kabulü:** `dotnet test` yeşil; `epubfixer measure` hem kaynak hem çıktı EPUB için
çalışıyor; `docs/baselines/` altında bugünün sayıları commit edilmiş; kalite kapısı ölçülmüş
eşiklerle çalışıyor ve eşiklerin nereden geldiği belgede yazılı.

---

## 2. Her kalem için geçerli ortak kurallar

### 2.1 Test-first
Kırmızı test → minimum kod → refactor. Testi olmayan üretim kodu yazılmaz. Testler davranışı
tarif eder, implementasyonu değil. Bu fazın ürünü *ölçüm* olduğu için testlerin çoğu "şu girdi için
şu sayı çıkar" biçimindedir — sayıyı önce teste yazın, sonra kodu yazın.

### 2.2 Katmanlar
- `EpubFixer.Core` **politika**dır: ne dosya sistemi, ne process, ne ağ. Ölçüm kuralları buraya yazılır.
- `EpubFixer.Cli` **adapter**dır: dosya okuma, `tr_50k.txt` yükleme, TRmorph process'i, konsol çıktısı.
- `benchmarks/EpubFixer.QualityBenchmarks` ground truth ve kapı işinin yeridir.
- Faz 0 hiçbir dosyayı Cli'dan Core'a **taşımaz** — o R2.3'ün işi. Yeni yazılan ölçüm kodu doğrudan
  doğru katmana yazılır.

### 2.3 Determinizm
Aynı EPUB için aynı sayı çıkmalı. Sözlük/küme iterasyonuna güvenmeyin; her sıralamayı açıkça
belirtin (`OrderByDescending(...).ThenBy(x => x, StringComparer.Ordinal)`). `HashSet` üzerinde
`First()` yok.

### 2.4 Türkçe tuzakları
- Normalizasyon **tek yerde**: `NFC` + `ToLower(new CultureInfo("tr-TR"))`. (`CleanTurkishLexicon.Normalize`
  bugün bunu yapıyor; Core tarafında aynı davranışı veren `TurkishWordNormalizer` yazılır, R2.3'te
  `CleanTurkishLexicon` buna bağlanacak.)
- Normalize **edildikten sonra** karşılaştırma daima `StringComparer.Ordinal`.
  `OrdinalIgnoreCase` kullanmayın — `I/ı/İ/i` yanlış eşleşir.
- `ı i l 1 I İ` karışması ölçülecek olgunun kendisidir; onu normalizasyonla yok etmeyin.

### 2.5 TRmorph sıcak döngüde olmaz
Ölçüm kodu kelime başına `IsValidWord` çağırmaz. Tüm unique token'lar tek `AnalyzeBatch`
(`IBatchTurkishMorphologicalParser`) çağrısıyla toplanır, sonuç sözlüğe yazılır, sorgular sözlükten
cevaplanır. Bu Faz 1'in (R1.1) provası gibidir ama Faz 1'in sözleşmesini **uygulamaz** —
`IMorphologyOracle` R1.1'in işidir, Faz 0 kendi küçük adapter'ını yazar.

### 2.6 Komutlar
```bash
dotnet build EpubFixer.slnx
dotnet test EpubFixer.slnx
dotnet test tests/EpubFixer.Tests --filter "FullyQualifiedName~BookHealth"
dotnet run --project src/EpubFixer.Cli -- measure "test-data/odun-kesmek/input.epub"
dotnet run --project benchmarks/EpubFixer.QualityBenchmarks -- test-data/odun-kesmek
```

### 2.7 Commit disiplini
Kalem başına ayrı commit; mesajın ilk satırı `R0.x: <ne yapıldı>`. Bir kalemin içindeki
"emniyet düzeltmesi" (örn. §5 acil ara çözüm) kendi commit'inde ve raporda ayrıca belirtilir.

### 2.8 Kapsam disiplini
Her agent yalnızca kendi kalemini yapar. Yol boyunca fark edilen sorunlar **düzeltilmez**,
bitiş raporunda "gözlem" olarak yazılır. Sözleşme değiştirme ihtiyacı doğarsa önce gerekçe
bildirilir, onay alınmadan sözleşme değiştirilmez.

---

## 3. Sıra ve paralellik

```
R0.1  (bağımsız)  ─────────────► baseline üretir
R0.3  (bağımsız; §5 acil fix dahil)
R0.2  (bağımsız, emek yoğun) ──┬─► R0.4
                               └─► R0.5
```

- **R0.1 ve R0.3 aynı anda** başlatılabilir; ortak dosyaya dokunmazlar.
- **R0.2 kritik yolun başıdır** ve en uzun kalemdir; ilk başlatılması önerilir.
- **R0.4, R0.2 bitmeden başlamamalıdır** (sınıf kırılımı olmadan kapı yarım kalır).
- **R0.5, R0.2'den sonra**; R0.4 ile paralel yürüyebilir ama R0.4'ün `current` profili
  R0.5 bittikten sonra yeniden ölçülmelidir (dedektörler bağlanınca recall değişir).

---

## 4. R0.1 — Kitap sağlık metriği

### Amaç
"Hiyeroglif gibi görünmesin"i tek sayıya indirgemek. Bu sayı her fazın sonunda tekrar ölçülecek;
fazın işe yarayıp yaramadığı bu sayıyla tartışılacak.

### Dosya haritası

Yeni:
```
src/EpubFixer.Core/Quality/BookHealth.cs
src/EpubFixer.Core/Quality/IBookHealthMeter.cs
src/EpubFixer.Core/Quality/BookHealthMeter.cs
src/EpubFixer.Core/Quality/IWordRecognizer.cs
src/EpubFixer.Core/Quality/SuspiciousTokenRules.cs
src/EpubFixer.Core/Lexicon/TurkishWordNormalizer.cs
src/EpubFixer.Cli/Quality/MeasureCommand.cs
src/EpubFixer.Cli/Quality/FrequencyMorphologyWordRecognizer.cs
tests/EpubFixer.Tests/BookHealthMeterTests.cs
tests/EpubFixer.Tests/SuspiciousTokenRulesTests.cs
tests/EpubFixer.Tests/MeasureCommandTests.cs
docs/baselines/README.md
docs/baselines/odun-kesmek.book-health.json
```
Değişen: `src/EpubFixer.Cli/Program.cs` (yalnızca komut dağıtımı + usage satırı).

### Sözleşme

```csharp
namespace EpubFixer.Core.Quality;

public sealed record BookHealth(
    int TotalTokens,
    int UnresolvableTokens,      // ne vocabulary'de ne morfolojik olarak geçerli
    int SuspiciousTokens,        // garbage glyph / gömülü rakam / izole ı,i,l,1
    double UnresolvableRate,     // 1000 token'da
    IReadOnlyList<string> WorstExamples);

public interface IBookHealthMeter
{
    BookHealth Measure(LogicalTextStream stream);
}

// Faz 0'ın kendi küçük port'u. R2.1'deki BookVocabulary'nin yerine geçmez; onun gelmesiyle
// FrequencyMorphologyWordRecognizer değişir, bu arayüz ve BookHealthMeter değişmez.
public interface IWordRecognizer
{
    bool IsRecognized(string normalizedWord);
}

public sealed class BookHealthMeter : IBookHealthMeter
{
    public BookHealthMeter(IWordRecognizer recognizer, int worstExampleCount = 20);
}
```

### Ölçüm kurallarının kesin tanımı

Bu tanımlar dosya başındaki yorumda **ve** `docs/baselines/README.md` içinde yazılı olmalı.
Tanım değişirse baseline geçersizdir; yeni baseline yeni tanımla birlikte commit edilir.

**TotalTokens** = `new WordTokenizer().Tokenize(stream).Count`.

**Normalizasyon** = `TurkishWordNormalizer.Normalize(value)` = `value.Normalize(NormalizationForm.FormC).ToLower(tr-TR)`.

**UnresolvableTokens** — bir token şu iki denemenin ikisi de başarısız olursa sayılır:
1. `recognizer.IsRecognized(Normalize(token))`
2. Token kesme işareti içeriyorsa, ilk kesme işaretinden önceki parça için aynı sorgu
   (`Ankara'da` → `ankara`). Bu kural Türkçe özel isim çekimini yanlış pozitiften korur.

**IWordRecognizer'ın tanımı (kritik karar):** tanınırlık kümesi = `tr_50k` frekans listesi
**∪** TRmorph'un geçerli saydığı formlar. **Kitabın kendi token'ları bu kümeye dahil edilmez.**
Gerekçe: bozuk token'lar da kitabın token'larıdır; onları tanınır saymak metriği kör eder
(`:,ohbet` kitapta geçtiği için "sağlıklı" görünür). Bu, R2.1'deki `BookVocabulary`'den kasıtlı
olarak farklıdır — orada amaç *kapsama*, burada amaç *teşhis*.

**SuspiciousTokens** — kelime token'ları üzerinde değil, **ham kelimeler** üzerinde sayılır:
metindeki maksimal boşluksuz dizilerden en az bir harf içerenler. Kural kümesi
(`SuspiciousTokenRules`, saf, morfoloji kullanmaz):

| # | Kural | Örnek |
|---|---|---|
| S1 | Hem harf hem rakam içerir | `ge1en`, `1 ı iç` |
| S2 | Şu glif'lerden biri var: `< > ^ ~ \| \ _ §` | `ge-^:cn` |
| S3 | İçinde (sonda değil) iki ardışık noktalama var: `. , : ; ' - ^` | `:,ohbet` |
| S4 | Ham kelime tek karakterli ve `ı i l I İ 1` kümesinden, **ve** komşu ham kelimelerden biri de tek karakterli | `ı ıç` |

Bir ham kelime birden fazla kurala uysa da **bir kez** sayılır.

**UnresolvableRate** = `TotalTokens == 0 ? 0 : UnresolvableTokens * 1000.0 / TotalTokens`.

**WorstExamples** = çözülemeyen token'ların **yüzey biçimleri** (orijinal büyük/küçük harf),
oluş sayısına göre azalan, eşitlikte `StringComparer.Ordinal` artan; ilk `worstExampleCount` tanesi.

### CLI sözleşmesi

```
epubfixer measure <book.epub> [--json <out.json>] [--baseline <baseline.json>]
```
- `--json` verilirse `BookHealth` JSON olarak yazılır (aşağıdaki baseline şeması).
- `--baseline` verilirse baseline okunur ve fark tablosu basılır
  (`UnresolvableRate: 42.10 → 38.70  (-3.40)`), çıkış kodu yine 0 — `measure` bir kapı değildir.
- Hatalı kullanım → 1, I/O hatası → 2 (mevcut `Program.cs` sözleşmesiyle aynı).

`MeasureCommand` `Program.cs`'in içine gömülmez; `QualityBenchmarkApplication` deseninde
test edilebilir bir sınıf olur:
```csharp
public sealed class MeasureCommand
{
    public int Run(string[] arguments, TextWriter output, TextWriter error);
}
```
`Program.Run` içinde `debug-ocr-region` ile aynı yerde dağıtılır; `CliOptions` kaydına
**dokunulmaz** (mevcut `CliOptionsTests` kırılmasın).

### Yazılacak testler (önce kırmızı)

`SuspiciousTokenRulesTests`
1. `S1_EmbeddedDigitIsSuspicious`
2. `S2_GarbageGlyphIsSuspicious`
3. `S3_InnerDoublePunctuationIsSuspicious`
4. `S3_TrailingPunctuationIsNotSuspicious` — `sohbet.` temiz sayılmalı
5. `S4_IsolatedSingleLetterNextToSingleLetterIsSuspicious`
6. `S4_SingleLetterWordAloneIsNotSuspicious` — `o adam` temiz sayılmalı
7. `MultipleRulesCountTokenOnce`

`BookHealthMeterTests` (stream'ler `TemporaryEpub` + `EpubPackageReader` ile kurulur, fake recognizer ile)
8. `Measure_CountsEveryWordToken`
9. `Measure_UnresolvableCountsOnlyUnrecognizedTokens`
10. `Measure_ApostropheSuffixFallsBackToStem` — `Ankara'da` tanınır sayılır
11. `Measure_RateIsPerThousandTokens`
12. `Measure_EmptyStreamHasZeroRateAndNoExamples`
13. `Measure_WorstExamplesAreOrderedByFrequencyThenOrdinal`
14. `Measure_IsDeterministicAcrossRuns` — aynı stream iki kez ölçülür, `BookHealth` eşit

`MeasureCommandTests`
15. `Run_MissingArgumentsPrintsUsageAndReturnsOne`
16. `Run_WritesJsonWhenRequested`
17. `Run_PrintsDeltaAgainstBaseline`

### Kabul kriteri
- [ ] `epubfixer measure test-data/odun-kesmek/input.epub` çalışır ve sayıları basar.
- [ ] Aynı komut `fix --apply-ocr-corrections` çıktısı EPUB için de çalışır (iki ölçüm yan yana raporlanır).
- [ ] `docs/baselines/odun-kesmek.book-health.json` bugünkü kaynak ve çıktı değerleriyle commit edilmiştir.
- [ ] `EpubFixer.Core/Quality` altında hiçbir dosya `System.IO`, `System.Diagnostics.Process` veya `EpubFixer.TrMorph` import etmez.
- [ ] TRmorph tek `AnalyzeBatch` çağrısıyla doldurulur (koşu sırasında birden fazla process başlatılmaz).

### Kapsam dışı
Metriğin iyileştirilmesi, `BookVocabulary` (R2.1), `IMorphologyOracle` (R1.1), Cli→Core taşıma (R2.3).

---

## 5. R0.2 — Ground truth'u zor sınıflarla genişlet

### Amaç
Mevcut 148 kaydın ezici çoğunluğu tireleme; yani bugünkü "%100 recall" **çözülmüş kolay sınıfın**
recall'u. Zor sınıflar ölçülmediği sürece motor iyileşse de bilinemez.

### Bu kalemin gerçek riski
Yanlış ground truth, küçük ground truth'tan **daha kötüdür** — yanlış yöne optimize ettirir.
Bu yüzden kural: **emin olunmayan kayıt ground truth'a girmez.** Emin olunmayanlar
`test-data/odun-kesmek/ground-truth-review.json` dosyasına yazılır ve raporda sayısı belirtilir.
Sınıf başına ≥ 25 hedefine ulaşılamıyorsa uydurulmaz; eksik, gerekçesiyle raporlanır.

### Dosya haritası

Yeni:
```
benchmarks/EpubFixer.QualityBenchmarks/GroundTruthProposer.cs
benchmarks/EpubFixer.QualityBenchmarks/Models/OcrErrorClass.cs
tests/EpubFixer.Tests/GroundTruthProposerTests.cs
test-data/odun-kesmek/ground-truth-review.json      (etiketlenemeyenler)
```
Değişen:
```
benchmarks/.../Models/GroundTruthDocument.cs          (errorClass alanı)
benchmarks/.../QualityBenchmarkDatasetLoader.cs       (şema 1 ve 2 birlikte)
benchmarks/.../QualityBenchmarkReportWriter.cs        (sınıf kırılımlı rapor)
test-data/odun-kesmek/ground-truth.json               (v2 + yeni kayıtlar)
tests/EpubFixer.Tests/QualityBenchmarkTests.cs        (v1/v2 uyumluluk testleri)
```

### Şema değişikliği

```csharp
public enum OcrErrorClass
{
    Unclassified = 0,   // v1 dosyalarından gelen kayıtlar
    Hyphenation,        // Auers-berger
    GlyphConfusion,     // ı/i/l/1 karışması
    SpuriousSpace,      // "koli ukta" → "koltukta"
    MissingSpace,       // "birşey" → "bir şey"
    GarbageInsertion,   // ":,ohbet", "ge-^:cn"
    Fragmentation,      // "1 ı iç" → "üç"
    Mixed               // birden fazla sınıf aynı span'de
}

public sealed record KnownErrorOccurrence(
    string Id,
    string DocumentPath,
    string Original,
    string Expected,
    IReadOnlyList<GroundTruthSourceSpan> SourceSpans)
{
    public OcrErrorClass ErrorClass { get; init; } = OcrErrorClass.Unclassified;
}
```

Loader:
```csharp
public const int MinimumSchemaVersion = 1;
public const int CurrentSchemaVersion = 2;
```
- v1 dosyası okunmaya devam eder; `errorClass` yoksa `Unclassified`.
- v2'de `errorClass` **zorunludur** ve `Unclassified` olamaz → doğrulama hatası.
- Diğer tüm mevcut doğrulamalar (id tekliği, span geçerliliği, çakışan oluşum yasağı) aynen korunur.

### Proposer (etiketleme aracı)

Elle 175+ kayıt çıkarmak hem yavaş hem hataya açık. Araç adayları **üretir**, span'leri
**hesaplar**, sınıfı **önerir**; `expected` alanını dolduran ve kaydı onaylayan agent'tır.

```csharp
public sealed record GroundTruthProposal(
    string Id,
    string DocumentPath,
    string Original,
    OcrErrorClass ProposedClass,
    IReadOnlyList<GroundTruthSourceSpan> SourceSpans,
    string ContextBefore,
    string ContextAfter);

public sealed class GroundTruthProposer
{
    public IReadOnlyList<GroundTruthProposal> Propose(
        LogicalTextStream stream,
        int perClassTarget = 40);
}
```
- Aday kaynağı: mevcut `OcrAnomalyDetector` ve `OcrRegionDetector` çıktıları (yeni dedektör yazılmaz).
- Sınıf önerisi R0.1'in `SuspiciousTokenRules` kurallarının aynısıyla yapılır — iki yerde iki tanım olmasın.
- Span'ler `stream.GetSourceLocationAt` ile karakter karakter üretilir; çıktı
  `QualityBenchmarkGroundTruthValidator`'dan **değişiklik yapılmadan** geçmelidir.
- Sınıf başına hedefin üstünde (40) aday üretilir; etiketleme sırasında eleme payı kalsın.
- Örnekleme belirlenimci olmalı (sabit seed veya belge sırası) — iki koşuda aynı adaylar çıkmalı.

### Etiketleme protokolü
1. `Propose` çalıştırılır, adaylar bağlamıyla birlikte listelenir.
2. Her aday için `expected` yazılır. Ölçüt: bağlam okunduğunda doğru okuma **tek** ve tartışmasız mı?
   Tartışmalıysa → `ground-truth-review.json`.
3. `Original` metni, span'lerden okunan metinle **birebir** aynı olmalı (validator bunu zorunlu kılar).
4. Aynı span'i iki kez eklemeyin (loader bunu reddeder).
5. Kayıt id'leri `odun-kesmek-<class>-<nnnn>` biçiminde (örn. `odun-kesmek-glyph-0007`).
6. Mevcut 148 kayıt silinmez; `errorClass` alanı eklenerek v2'ye taşınır (çoğu `Hyphenation`).

### Rapor değişikliği
`QualityBenchmarkReportWriter` sınıf kırılımı basar:

```
Per-class breakdown:
  class            known  detected  correct  wrong  precision  recall
  Hyphenation        148       148      148      0    100.00%  100.00%
  GlyphConfusion      27         0        0      0        N/A     0.00%
  ...
```
`N/A`, payda sıfırken basılır (mevcut `FormatRate` davranışı korunur).

### Yazılacak testler
1. `Loader_ReadsSchemaVersionOne_DefaultsErrorClassToUnclassified`
2. `Loader_ReadsSchemaVersionTwo`
3. `Loader_RejectsSchemaVersionTwoWithoutErrorClass`
4. `Loader_RejectsUnknownSchemaVersion`
5. `Proposer_ProducesSpansThatPassGroundTruthValidator`
6. `Proposer_IsDeterministic`
7. `Proposer_ClassifiesEachKnownPatternCorrectly` (sınıf başına en az bir örnek)
8. `ReportWriter_PrintsPerClassPrecisionAndRecall`
9. `ReportWriter_PrintsNotAvailableWhenClassHasNoDetections`
10. `Dataset_OdunKesmekGroundTruthLoadsAndValidates` — gerçek dosya üstünde duman testi

### Kabul kriteri
- [ ] `ground-truth.json` `schemaVersion: 2`, tüm kayıtlarda `errorClass` dolu.
- [ ] Sınıf başına ≥ 25 kayıt **veya** eksik kalan sınıf için gerekçeli rapor.
- [ ] Eski v1 dosyası (test fixture olarak saklanan bir kopya) hâlâ okunabiliyor.
- [ ] Benchmark koşusu sınıf kırılımlı precision/recall basıyor.
- [ ] Validator gerçek dosyayı hatasız doğruluyor.

### Kapsam dışı
Kapının eşiklerinin değiştirilmesi (R0.4), dedektörlerin iyileştirilmesi, düzeltme motoru.

### Beklenen ve **doğru** olan sonuç
Genişletmeden sonra yeni sınıflarda recall ≈ 0 çıkacaktır. Bu bir hata değil, Faz 0'ın üretmesi
istenen sinyalin ta kendisidir: benchmark koşusu bugün yalnızca tireleme hattını çalıştırıyor
(`QualityBenchmarkRunner`, OCR düzeltme hattına hiç girmiyor — B3). Bu gözlem raporda açıkça
yazılmalı; bu boşluğu kapatan kalem bölüm 8'deki R0.5'tir.

---

## 6. R0.3 — Performans bütçesi testi

### Amaç
120 sn'lik hard gate'in ve algoritmik patlamanın teste bağlanması. Regresyon saatlerce
beklemeden yakalanmalı.

### Dosya haritası
```
tests/EpubFixer.Tests/PerformanceBudgetTests.cs        (yeni)
src/EpubFixer.Core/Ocr/SearchBudget.cs                 (yeni)
src/EpubFixer.Cli/OcrReconstruction/NoisyChannelRegionReconstructor.cs  (yalnızca sayaç enjeksiyonu)
docs/baselines/odun-kesmek.performance.json            (yeni)
```

### İki ayrı test, iki ayrı amaç

**T1 — Duvar saati bütçesi (üretim hattı).**
`EpubFixService.Fix(input, output, applyOcrCorrections: true)` gerçek `odun-kesmek` üzerinde
çalıştırılır; süre 120 sn'yi aşarsa test kırmızı. Bu test **üretim hattını** ölçer; deneysel
NoisyChannel hattını değil (o hat bugün EPUB'a dokunmuyor — B3).

Kurallar:
- Çıktı geçici klasöre yazılır ve test sonunda silinir (`TemporaryEpub` deseni).
- Ölçüm `Stopwatch` ile, tek koşu; ısınma koşusu yok (gerçek kullanım tek koşudur).
- Süre ölçümü makineye bağlıdır: bütçe 120 sn'dir, "bugünkü süre + %10" değil. Ölçülen gerçek
  süre `docs/baselines/odun-kesmek.performance.json` içine yazılır ki trend izlenebilsin.
- Test bir `[Trait("Category", "Slow")]` ile işaretlenir; `dotnet test` içinde kalır,
  filtrelenebilir olur.

**T2 — Arama uzayı tavanı (algoritmik regresyon).**
```csharp
namespace EpubFixer.Core.Ocr;

public sealed class SearchBudget
{
    public SearchBudget(long maxStates);
    public long Visited { get; }
    public long MaxStates { get; }
    public bool Exceeded { get; }
    public void Visit();          // sayar; tavanı aşınca Exceeded true olur, exception atmaz
}
```
- `NoisyChannelRegionReconstructor` isteğe bağlı bir `SearchBudget?` alır; her *expanded state*
  için `Visit()` çağırır. Bütçe `null` ise davranış bugünküyle **birebir aynıdır**
  (davranış değişikliği yok, yalnızca gözlem).
- Test, sabit bir fixture region için (örn. `"koli ukta"`, `"ı ızellikle"`) ziyaret edilen state
  sayısının belirlenen tavanın altında kalmasını assert eder.
- Tavan, ölçülen değerden değil **makul üst sınırdan** seçilir ve teste sabit yazılır. Öneri:
  region başına ≤ 2.000.000 state.

**§5 acil ara çözümü (D1 onaylandı).**
T2'nin tavanının anlamlı olması için, [NoisyChannelRegionReconstructor.cs:66](../src/EpubFixer.Cli/OcrReconstruction/NoisyChannelRegionReconstructor.cs#L66)
içindeki `RetentionKey` gövdesinde yer alan iç `Expand(text, state)` çağrısı (one-step lookahead)
kaldırılır. Kurallar:
- **Ayrı commit**: `R0.3: remove one-step lookahead from NoisyChannel retention key`.
- Öncesi/sonrası ölçüm raporda yan yana verilir (fixture region için state sayısı ve süre).
- Bu bir **hız** düzeltmesidir, kalite düzeltmesi değil (B2 yerinde duruyor). Top-1 sonuçlarının
  değiştiği gözlenirse durup bildirin — beklenen, aday sıralamasının bozulmadan kalmasıdır;
  bozuluyorsa bu bilgi R3.5'in A/B'si için kıymetlidir.
- `NoisyChannelV2Tests` / `NoisyChannelDiagnosticTests` yeşil kalmalı.

### Yazılacak testler
1. `SearchBudget_CountsVisitsAndFlagsExceeded` (saf birim test)
2. `SearchBudget_NullBudgetDoesNotChangeReconstructionOutput` — aynı region, bütçeli ve bütçesiz
   koşuda aynı aday listesi
3. `FullBookFixCompletesWithinBudget` (T1)
4. `RegionSearchStaysWithinStateCeiling` (T2)

### Kabul kriteri
- [ ] `dotnet test` içinde T1 gerçek kitapla koşuyor ve 120 sn bütçesini assert ediyor.
- [ ] T2 region başına state tavanını assert ediyor.
- [ ] Bütçe enjeksiyonu mevcut testlerin hiçbirini kırmıyor; reconstructor'ın davranışı değişmiyor.
- [ ] `docs/baselines/odun-kesmek.performance.json` bugünkü süreleri içeriyor.

### Kapsam dışı
Performansın **iyileştirilmesi** — §5 acil ara çözümü tek istisnadır. B1'in asıl çözümü
(arama uzayının yeniden tanımlanması) R3.2'nin işidir; bu kalemde algoritma değiştirilmez.

---

## 7. R0.4 — Kalite kapısını gerçekçi hale getir

### Amaç
`QualityBenchmarkGateEvaluator` bugün `DetectionRecall`, `AutoFixCoverage`, `ProtectionRate` için
**tam %100** ve altı sayaç için **tam 0** talep ediyor. İstatistiksel bir motorla bu kapı
kullanılamaz: ilk gerçek koşuda kırmızı olur ve kapatılır — yani kapı olmaktan çıkar.

### Dosya haritası
```
benchmarks/.../Models/QualityBenchmarkGateOptions.cs       (yeni)
benchmarks/.../Models/QualityBenchmarkResult.cs            (Precision/Recall + sınıf kırılımı)
benchmarks/.../QualityBenchmarkGateEvaluator.cs            (değişen)
benchmarks/.../QualityBenchmarkApplication.cs              (profil yükleme)
benchmarks/.../QualityBenchmarkReportWriter.cs             (yeni metrikler)
docs/baselines/quality-gate.json                           (yeni — kapı profili)
tests/EpubFixer.Tests/QualityBenchmarkGateTests.cs         (yeniden yazılır)
```

### Metrik tanımları (rapora da yazılır)
```
Precision = CorrectlyFixed / (CorrectlyFixed + WronglyFixed)      // payda 0 → N/A, kapı geçer
Recall    = CorrectlyFixed / KnownErrors                           // payda 0 → N/A, kapı geçer
```
`DetectionRecall`, `AutoFixCoverage`, `ProtectionRate` kapıdan çıkarılır, **raporda kalır**
(teşhis için değerli, kapı için değil).

### Kapı sözleşmesi
```csharp
public sealed record QualityBenchmarkGateOptions(
    double MinimumPrecision = 0.98,
    double MinimumRecall = 0.60)
{
    // Sıfır toleranslı sayaçlar — bunlar gevşetilmez.
    public int MaximumProtectedViolated { get; init; }      // 0
    public int MaximumProtectedChanged { get; init; }       // 0
    public int MaximumUnexpectedTextChanges { get; init; }  // 0
    public int MaximumNonTextChanges { get; init; }         // 0
}

public sealed class QualityBenchmarkGateEvaluator
{
    public QualityBenchmarkGateEvaluator(QualityBenchmarkGateOptions? options = null);
    public QualityBenchmarkGateResult Evaluate(QualityBenchmarkResult result);
}
```
`Missed`, `KnownDeferred`, `Deferred` sayaçları kapıdan çıkar — bunlar recall'un tamamlayanıdır,
ayrıca kapı olmaları çifte sayımdır. Raporda kalırlar.

### Profil dosyası ve cırcır (ratchet) kuralı
Yol haritasının hedefi `precision ≥ 0.98`, `recall ≥ 0.60`. Bugünün motoru (yalnızca tireleme
hattı) R0.2 sonrası bu recall'u **karşılamayacaktır**. Kapı bugün hedefe ayarlanırsa sürekli
kırmızı olur ve anlamını yitirir. Çözüm iki profil:

```json
{
  "target":  { "minimumPrecision": 0.98, "minimumRecall": 0.60 },
  "current": { "minimumPrecision": 0.98, "minimumRecall": 0.00, "measuredOn": "2026-09-12",
               "note": "R0.2 sonrası ölçülen değer; yalnızca yukarı çekilir." }
}
```
- Testler ve CI `current` profiliyle koşar.
- `current` değerleri yalnızca **yukarı** güncellenir; bir fazın kabulü "bu fazdan sonra ölçülen
  değer `current`'a yazıldı" demektir. Düşürme isteği açık bir karar gerektirir.
- R4.2'nin kabulü `current == target` olduğunda sağlanmış sayılır.

### Yazılacak testler
1. `Evaluate_DefaultOptionsUseRoadmapThresholds` — varsayılanlar 0.98 / 0.60
2. `Evaluate_PrecisionBelowThresholdFails`
3. `Evaluate_PrecisionAtThresholdPasses` (sınır değeri dahil)
4. `Evaluate_RecallBelowThresholdFails`
5. `Evaluate_NullPrecisionDoesNotFail` — hiç düzeltme yapılmamışsa kapı geçer
6. `Evaluate_ProtectedViolatedAlwaysFails` (opsiyonlardan bağımsız, sıfır toleranslı)
7. `Evaluate_UnexpectedTextChangesAlwaysFails`
8. `Evaluate_DetectionRateImperfectDoesNotFail` — eski `AddPerfectRateFailure` davranışının
   gittiğini kilitleyen test
9. `GateOptions_LoadedFromProfileFile`
10. `ReportWriter_PrintsPrecisionAndRecall`
11. Mevcut `Evaluate_ImperfectRateFails` testleri **silinir** (davranış kasıtlı olarak değişti;
    yerine 8 numara gelir).

### Kabul kriteri
- [ ] Eşikler yapılandırılabilir ve testlerde açıkça belirtilmiş.
- [ ] `ProtectedViolated`, `ProtectedChanged`, `UnexpectedTextChanges`, `NonTextChanges` hâlâ
      sıfır toleranslı ve opsiyonlarla gevşetilemiyor.
- [ ] Precision sınıf kırılımlı raporlanıyor (R0.2'nin `errorClass` alanı üzerinden).
- [ ] `docs/baselines/quality-gate.json` commit edilmiş ve `current` değerleri gerçek koşudan geliyor.
- [ ] `dotnet run --project benchmarks/EpubFixer.QualityBenchmarks -- test-data/odun-kesmek` çıkış kodu 0.

### Kapsam dışı
Motorun recall'unu yükseltmek; dedektörleri benchmark'a bağlamak (R0.5).

---

## 8. R0.5 — OCR dedektörlerini benchmark'a bağla

> Yol haritasına Faz 0'ın beşinci kalemi olarak eklendi (D3 onaylandı).

### Amaç
`QualityBenchmarkRunner` bugün yalnızca tireleme hattını koşuyor: `HyphenationDetector` →
`HyphenationEvidenceEvaluator` → `HyphenationDecisionEvaluator`. R0.2 ile eklenen
`GlyphConfusion`, `SpuriousSpace`, `GarbageInsertion`, `Fragmentation` kayıtları için
"detected" sayısı **yapısal olarak** 0 kalır — hiçbir dedektör bu kayıtlara bakmıyor.
O halde sınıf kırılımı yalnızca "OCR hattı benchmark'a bağlı değil" bilgisini tekrar eder;
motor iyileştikçe sayının hareket etmesi için hattın bağlı olması gerekir.

### Dosya haritası
```
benchmarks/.../OcrDetectionSource.cs                  (yeni)
benchmarks/.../QualityBenchmarkMatcher.cs             (ikinci kaynak eklenir)
benchmarks/.../QualityBenchmarkRunner.cs              (OCR dedektörleri koşuya girer)
tests/EpubFixer.Tests/QualityBenchmarkMatcherTests.cs (genişletilir)
```

### Kural
Bir known error şu iki koşuldan **biri** sağlanırsa "detected" sayılır:
1. Mevcut tireleme eşleştirmesi (`QualityBenchmarkMatcher.IsMatch`) — **davranışı değişmez**.
2. `OcrAnomalyDetector` veya `OcrRegionDetector` çıktısındaki bir span, kaydın logical aralığını
   kapsıyor (kesişme değil, **kapsama**: `span.Start <= error.Start && span.End >= error.End`).

`Detected` sayacı böylece "bir dedektör bu hatayı gördü mü" sorusunu cevaplar; "doğru düzeltildi mi"
sorusu `CorrectlyFixed`'in işidir ve karışmaz.

### Yazılacak testler
1. `Matcher_HyphenationMatchingIsUnchanged` (regresyon kilidi)
2. `Matcher_OcrSpanCoveringKnownErrorCountsAsDetected`
3. `Matcher_OcrSpanOverlappingButNotCoveringIsNotDetected`
4. `Matcher_ErrorDetectedByBothSourcesIsCountedOnce`
5. `Runner_ReportsNonZeroDetectionForOcrClasses` (gerçek veri kümesiyle duman testi)

### Kabul kriteri
- [ ] `Hyphenation` sınıfının detection sayıları R0.5 öncesiyle **birebir aynı**.
- [ ] En az bir OCR sınıfında `detected > 0`.
- [ ] TRmorph dedektör koşusunda batch kullanılır; benchmark süresi 120 sn bütçesini aşmaz.
- [ ] R0.4'ün `current` profili bu kalemden **sonra** yeniden ölçülüp güncellenir.

### Kapsam dışı
Dedektörlerin iyileştirilmesi, OCR düzeltme hattının benchmark'ta **uygulanması** (o R4.2'dir —
burada yalnızca *tespit* ölçülür, mutasyon değil).

---

## 9. Baseline dosya formatları

`docs/baselines/README.md` şunları yazmalı: her dosyanın ne zaman, hangi commit'te, hangi komutla
üretildiği; metrik tanımının değişmesi halinde baseline'ın geçersiz olduğu.

`docs/baselines/odun-kesmek.book-health.json`
```json
{
  "dataset": "odun-kesmek",
  "measuredOn": "2026-09-12",
  "commit": "<sha>",
  "definitionVersion": 1,
  "source":  { "totalTokens": 0, "unresolvableTokens": 0, "suspiciousTokens": 0,
               "unresolvableRate": 0.0, "worstExamples": [] },
  "output":  { "totalTokens": 0, "unresolvableTokens": 0, "suspiciousTokens": 0,
               "unresolvableRate": 0.0, "worstExamples": [] }
}
```

`docs/baselines/odun-kesmek.performance.json`
```json
{
  "dataset": "odun-kesmek",
  "measuredOn": "2026-09-12",
  "commit": "<sha>",
  "machine": "<kısa tanım>",
  "fixWithOcrCorrectionsSeconds": 0.0,
  "budgetSeconds": 120,
  "maxStatesPerRegion": 0
}
```

---

## 10. Kararlar (2026-09-12'de verildi)

| # | Karar | Sonuç | Nereye işlendi |
|---|---|---|---|
| D1 | §5 acil ara çözümü (RetentionKey lookahead kaldırma) R0.3 kapsamında, ayrı commit'te | **Onaylandı** | Bölüm 6 |
| D2 | Kapı `current` / `target` profili + yalnızca-yukarı cırcır kuralı | **Onaylandı** | Bölüm 7 |
| D3 | R0.5 (OCR dedektörlerini benchmark'a bağlama) Faz 0'a eklensin | **Onaylandı** | Bölüm 8 + yol haritası |
| D4 | R0.2'de yalnızca tartışmasız kayıtlar ground truth'a; tereddütlüler `ground-truth-review.json`'a | **Onaylandı** | Bölüm 5 |

Yeni bir karar ihtiyacı doğarsa agent kendi başına karara varmaz; gerekçeyi bildirip bekler ve
karar bu tabloya eklenir.

---

## 11. Devir promptları

### R0.1
```
EpubFixer projesinde docs/ocr-correction-roadmap.md yol haritasındaki R0.1 kalemini uygulayacaksın.

Önce şunları oku:
- docs/phase-0-plan.md — bölüm 2 (ortak kurallar) ve bölüm 4 (R0.1). Ölçüm kurallarının kesin
  tanımı oradadır, onları birebir uygula.
- docs/ocr-correction-roadmap.md — R0.1 maddesi ve "Hedef mimari"
- src/EpubFixer.Core/Tokenization/WordTokenizer.cs
- src/EpubFixer.Core/Epub/Models/LogicalTextStream.cs
- src/EpubFixer.Cli/OcrReconstruction/CleanTurkishLexicon.cs (normalizasyon ve tr_50k yüklemesi)
- src/EpubFixer.Cli/Program.cs (komut dağıtımı deseni)
- benchmarks/EpubFixer.QualityBenchmarks/QualityBenchmarkApplication.cs (test edilebilir komut deseni)
- tests/EpubFixer.Tests/TemporaryEpub.cs (sentetik stream kurma yolu)

Kurallar:
- Test-first: kırmızı test → minimum kod → refactor. Testi olmayan üretim kodu yazma.
- Bağımlılıklar içeri doğru: EpubFixer.Core/Quality altına System.IO, Process veya TRmorph girmez.
- Plandaki sözleşmeleri (BookHealth, IBookHealthMeter, IWordRecognizer) aynen kullan; değiştirmen
  gerekirse önce gerekçesini söyle.
- TRmorph'u kelime başına çağırma; tek AnalyzeBatch ile doldur.
- CliOptions kaydına dokunma; measure komutunu debug-ocr-region gibi ayrı dağıt.
- Kapsam R0.1 ile sınırlı. Metriği iyileştirme, başka kalemin işini yapma; fark ettiğin sorunları
  rapor et.

Bitirdiğinde: eklenen testler, kabul kriterinin karşılanıp karşılanmadığı, ölçülen baseline
değerleri ve yol haritasında güncellenmesi gereken bir şey olup olmadığı.
```

### R0.2
```
EpubFixer projesinde docs/ocr-correction-roadmap.md yol haritasındaki R0.2 kalemini uygulayacaksın.

Önce şunları oku:
- docs/phase-0-plan.md — bölüm 2 ve bölüm 5 (R0.2). Etiketleme protokolü ve "yanlış ground truth
  küçük ground truth'tan kötüdür" kuralı oradadır.
- docs/ocr-correction-roadmap.md — R0.2 maddesi ve "Risk kaydı" (özellikle risk 3)
- benchmarks/EpubFixer.QualityBenchmarks/QualityBenchmarkDatasetLoader.cs
- benchmarks/EpubFixer.QualityBenchmarks/QualityBenchmarkGroundTruthValidator.cs
- benchmarks/EpubFixer.QualityBenchmarks/Models/GroundTruthDocument.cs
- benchmarks/EpubFixer.QualityBenchmarks/QualityBenchmarkReportWriter.cs
- src/EpubFixer.Core/Ocr/OcrAnomalyDetector.cs ve OcrRegionDetector.cs
- test-data/odun-kesmek/ground-truth.json

Kurallar:
- Test-first. Şema değişikliği önce testle tanımlanır.
- Loader hem v1 hem v2 okumalı; mevcut doğrulamaların hiçbiri gevşetilmez.
- Ürettiğin her kayıt QualityBenchmarkGroundTruthValidator'dan değişiklik yapılmadan geçmeli.
- Emin olmadığın kaydı ground truth'a koyma; ground-truth-review.json'a yaz ve sayısını raporla.
- Sınıf başına 25 hedefine ulaşamazsan uydurma; eksiği gerekçesiyle raporla.
- Kapsam R0.2 ile sınırlı. Kapı eşiklerine (R0.4) ve dedektörlere dokunma.

Bitirdiğinde: sınıf başına kayıt sayıları, review'a düşen kayıt sayısı, eklenen testler, kabul
kriterinin durumu ve yol haritasında güncellenmesi gereken bir şey olup olmadığı.
```

### R0.3
```
EpubFixer projesinde docs/ocr-correction-roadmap.md yol haritasındaki R0.3 kalemini uygulayacaksın.

Önce şunları oku:
- docs/phase-0-plan.md — bölüm 2 ve bölüm 6 (R0.3). İki testin neyi ölçtüğü ve D1 kararı oradadır.
- docs/ocr-correction-roadmap.md — R0.3 maddesi, B1 tespiti ve bölüm 5 (acil ara çözüm)
- src/EpubFixer.Core/Fix/EpubFixService.cs
- src/EpubFixer.Cli/OcrReconstruction/NoisyChannelRegionReconstructor.cs
- tests/EpubFixer.Tests/TemporaryEpub.cs

Kurallar:
- Test-first.
- SearchBudget enjeksiyonu davranışı değiştirmemeli: bütçe null iken çıktı bugünküyle birebir aynı.
  Bunu bir testle kilitle.
- T1 üretim hattını (EpubFixService.Fix) ölçer, deneysel NoisyChannel hattını değil.
- §5 acil ara çözümü (RetentionKey içindeki iç Expand çağrısının kaldırılması) bu kaleme dahildir;
  ayrı commit'te yap ve öncesi/sonrası ölçümü raporla. Top-1 sonuçları değişirse dur ve bildir.
- Kapsam R0.3 ile sınırlı. §5 dışında performansa dokunma, algoritmayı değiştirme.

Bitirdiğinde: ölçülen gerçek süreler (§5 öncesi/sonrası), seçilen state tavanı ve gerekçesi,
eklenen testler, kabul kriterinin durumu.
```

### R0.5
```
EpubFixer projesinde docs/ocr-correction-roadmap.md yol haritasındaki R0.5 kalemini uygulayacaksın.
R0.2 bitmiş olmalı; bitmediyse başlamadan bildir.

Önce şunları oku:
- docs/phase-0-plan.md — bölüm 2 ve bölüm 8 (R0.5). Kapsama kuralı (kesişme değil kapsama) oradadır.
- docs/ocr-correction-roadmap.md — R0.5 maddesi ve B3 tespiti
- benchmarks/EpubFixer.QualityBenchmarks/QualityBenchmarkMatcher.cs
- benchmarks/EpubFixer.QualityBenchmarks/QualityBenchmarkRunner.cs
- src/EpubFixer.Core/Ocr/OcrAnomalyDetector.cs ve OcrRegionDetector.cs

Kurallar:
- Test-first.
- Mevcut tireleme eşleştirmesinin davranışı değişmemeli; bunu bir regresyon testiyle kilitle.
- "Detected" tespit demektir, düzeltme değil; CorrectlyFixed semantiğine dokunma.
- TRmorph'u batch kullan; benchmark koşusu 120 sn bütçesini aşmamalı.
- Kapsam R0.5 ile sınırlı. Dedektörleri iyileştirme, OCR mutasyonunu benchmark'a sokma (o R4.2).

Bitirdiğinde: sınıf başına detection sayıları (öncesi/sonrası), eklenen testler, kabul kriterinin
durumu ve R0.4'ün current profilinin yeniden ölçülmesi gerekip gerekmediği.
```

### R0.4
```
EpubFixer projesinde docs/ocr-correction-roadmap.md yol haritasındaki R0.4 kalemini uygulayacaksın.
R0.2 bitmiş olmalı; bitmediyse başlamadan bildir.

Önce şunları oku:
- docs/phase-0-plan.md — bölüm 2 ve bölüm 7 (R0.4). Metrik tanımları ve cırcır (ratchet) kuralı oradadır.
- docs/ocr-correction-roadmap.md — R0.4 maddesi ve "Risk kaydı" (özellikle risk 5)
- benchmarks/EpubFixer.QualityBenchmarks/QualityBenchmarkGateEvaluator.cs
- benchmarks/EpubFixer.QualityBenchmarks/Models/QualityBenchmarkResult.cs
- benchmarks/EpubFixer.QualityBenchmarks/QualityBenchmarkApplication.cs
- tests/EpubFixer.Tests/QualityBenchmarkGateTests.cs

Kurallar:
- Test-first.
- ProtectedViolated, ProtectedChanged, UnexpectedTextChanges, NonTextChanges sıfır toleranslı kalır
  ve opsiyonlarla gevşetilemez. Bunu bir testle kilitle.
- Eski AddPerfectRateFailure davranışı kasıtlı olarak kaldırılıyor; ona bağlı testleri sil ve
  yerine yeni davranışı kilitleyen test yaz.
- current profil değerleri gerçek koşudan gelmeli, tahminden değil. R0.5 senden sonra biterse
  current profili onun sonunda yeniden ölçülecek; bunu raporunda not düş.
- Kapsam R0.4 ile sınırlı. Motoru iyileştirme, dedektör bağlama (R0.5) işine girme.

Bitirdiğinde: ölçülen precision/recall değerleri, current profile yazılan eşikler, eklenen ve
silinen testler, kabul kriterinin durumu.
```

---

## 12. Faz 0'ın Faz 1+ ile sözleşmesi

- `IWordRecognizer` (R0.1) R2.1'de `BookVocabulary` tabanlı bir implementasyonla değiştirilecek;
  `BookHealthMeter` ve `BookHealth` **değişmeyecek**. Metrik tanımı değişirse baseline'lar
  geçersizdir — `definitionVersion` artırılır.
- `SearchBudget` (R0.3) R3.2/R3.3'te yeni lattice motorunun bütçe mekanizması olarak yeniden
  kullanılacak; `MaxWindowLength` aşımıyla birlikte `SkippedTooLong` raporlaması oraya bağlanacak.
- `errorClass` (R0.2) R5.1'in confusion maliyet kalibrasyonunun girdisidir — hizalamalar sınıf
  bazlı yapılacak.
- Kapı profili (R0.4) her fazın kabulünde güncellenir; `current == target` olduğunda R4.2 kabulü
  sağlanmış sayılır.
