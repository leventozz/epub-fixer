# Faz 2 — Kelime haznesi ve dil modeli — Uygulama planı

> Bu belge [ocr-correction-roadmap.md](ocr-correction-roadmap.md) Faz 2'nin (R2.1–R2.3) uygulama
> planıdır. Her kalem ayrı bir oturumda ayrı bir agent'a devredilebilir; bölüm 11'de kopyala-yapıştır
> devir promptları vardır. Önceki fazların planları: [phase-0-plan.md](phase-0-plan.md),
> [phase-1-plan.md](phase-1-plan.md).
>
> Faz 2'nin tek işi vardır: **Faz 3'ün decoder'ının soracağı iki soruya cevap verebilen bilgi
> tabanını kurmak.** Sorular şunlardır: *"Bu dizi bir kelime mi ve ne kadar olağan?"* (R2.1) ve
> *"Bu kelime bu bağlamda ne kadar olağan?"* (R2.2). Faz 2 hiçbir EPUB'ı değiştirmez, hiçbir
> düzeltme uygulamaz, hiçbir kalite sayısını kıpırdatmaz. Fazın sonunda `measure` çıktısı
> **birebir aynı** olmalıdır (bkz. karar **D21**).

---

## 1. Faz 2 bittiğinde elde ne olacak

| Çıktı | Dosya | Ne işe yarar |
|---|---|---|
| Frekans listesi port'u | `EpubFixer.Core/Lexicon/ITurkishFrequencyList.cs` + Cli adapter | `tr_50k` politikadan görünür, I/O kenarda kalır |
| Maliyet modeli Core'da | `EpubFixer.Core/Ocr/OcrEditCostModel.cs` | R3.2'nin lattice arc maliyeti Core'dan gelir |
| Temiz token kaynağı | `EpubFixer.Core/Lexicon/CleanTokenSequence.cs` | Vocabulary ve LM **aynı** token akışını görür |
| Kelime haznesi | `EpubFixer.Core/Lexicon/BookVocabulary.cs` (+ builder, options) | R3.1'in sorgulayacağı sözlük; OOV cezası |
| Kapsama/kontaminasyon raporu | `EpubFixer.Core/Lexicon/VocabularyCoverageReport.cs` | "Hedef kelime haznede mi?" sorusunun sayısı |
| Dil modeli | `EpubFixer.Core/Lexicon/BookLanguageModel.cs` | R3.3'ün Viterbi skorundaki bağlam terimi |
| Bilgi tabanı orkestratörü | `EpubFixer.Core/Lexicon/BookKnowledgeBuilder.cs` | Faz 3'ün tek giriş noktası |
| Ölçüm komutu ve baseline'lar | `debug-vocabulary` + `docs/baselines/odun-kesmek.vocabulary.json`, `.language-model.json` | Faz 3 öncesi kilitlenen sayılar |

**Faz 2'nin kabulü:**

1. `dotnet test EpubFixer.slnx` yeşil — **hiçbir mevcut assert değeri değişmeden**
   (özellikle `BookHealthMeterTests`, `MorphologyCallTraceTests` golden SHA-256'sı,
   `OcrCorrectionV11RealRegressionTests`).
2. `measure test-data/odun-kesmek/input.epub` çıktısı `docs/baselines/odun-kesmek.book-health.json`
   ile **birebir** aynı (D21).
3. `debug-vocabulary` gerçek kitapta koşuyor; kapsama, kontaminasyon ve LM sayıları baseline
   dosyalarına yazılmış.
4. Held-out kapsama ≥ %95 ve ground-truth hedef kapsaması ≥ %98 (bölüm 6.5).
5. `EpubFixer.Core` içinde **yeni** hiçbir dosya sistemi/process API'si yok; mimari testi
   izin listesiyle yeşil (D14).
6. Bilgi tabanının tamamının kurulması (region tespiti dahil) ≤ 3 sn (D24).

---

## 2. Her kalem için geçerli ortak kurallar

Faz 0 [bölüm 2](phase-0-plan.md) ve Faz 1 [bölüm 2](phase-1-plan.md) kuralları aynen geçerlidir.
Faz 2'ye özgü eklemeler:

### 2.1 Test-first
Kırmızı test → minimum kod → refactor. Testi olmayan üretim kodu yazılmaz. Bu fazın ürünü bir
**bilgi tabanı**dır; testlerin çoğu "şu korpus verildiğinde şu kelime haznede / şu skor çıkar"
biçimindedir. Sayıyı önce teste yazın.

### 2.2 Katmanlar
- `EpubFixer.Core` politika: tokenizasyon, filtreler, sayımlar, olasılık formülleri, kapsama metriği.
  **`System.IO` yok** (istisna listesi D14'te).
- `EpubFixer.Cli` adapter: `tr_50k.txt` dosyasını okuyan kaynak, `debug-vocabulary` komutu, JSON yazımı.
- `EpubFixer.TrMorph` bu fazda **hiç değişmez**. Morfoloji yalnızca `IMorphologyOracle` üzerinden görülür.

### 2.3 İki ayrı normalizasyon uzayı vardır — karıştırmayın (D17)

| Uzay | Kural | Kim kullanır |
|---|---|---|
| Morfoloji | NFC, **büyük/küçük harf korunur**, `StringComparer.Ordinal` | `IMorphologyOracle` (Faz 1, D9) |
| Hazne / LM | NFC + `ToLower(tr-TR)` = `TurkishWordNormalizer.Normalize`, `StringComparer.Ordinal` | `BookVocabulary`, `BookLanguageModel` |

`Auersberger` oracle'a **böyle** sorulur, hazneye `auersberger` olarak girer ve hazne onun
baskın yüzey biçimini (`Auersberger`) ayrıca saklar — decoder EPUB'a yazacağı metni oradan alır.
`OrdinalIgnoreCase` hiçbir yerde kullanılmaz (`I/ı/İ/i` yanlış eşleşir).

### 2.4 Determinizm
Aynı EPUB → byte-eşit rapor. Sözlük/küme iterasyon sırasına güvenilmez; her sıralama açıkça
`OrderBy(..., StringComparer.Ordinal)` ile belirtilir. Kayan noktalı sayılar raporlarda sabit
formatla (`"0.000"`, `InvariantCulture`) yazılır.

### 2.5 Kontaminasyon birinci düşmandır
Bozuk bir token hazneye girerse motor o hatayı "doğru" sayar ve **düzeltmeyi bırakır** — hatta
doğru kelimeyi bozuk olana doğru çekebilir. Bu yüzden hazneye girişte şüphede karar
**"alma"**dır (bölüm 6.3). Kapsama uğruna filtre gevşetilmez; gevşetme önerisi ancak ölçümle gelir.

### 2.6 Komutlar
```bash
dotnet build EpubFixer.slnx
dotnet test EpubFixer.slnx
dotnet test tests/EpubFixer.Tests --filter "FullyQualifiedName~Vocabulary"
dotnet test tests/EpubFixer.Tests --filter "FullyQualifiedName~LanguageModel"
dotnet test tests/EpubFixer.Tests --filter "Category=Slow"
dotnet run --project src/EpubFixer.Cli -- measure "test-data/odun-kesmek/input.epub"
dotnet run --project src/EpubFixer.Cli -- debug-vocabulary "test-data/odun-kesmek/input.epub" --json docs/baselines/odun-kesmek.vocabulary.json
```

### 2.7 Commit disiplini
Kalem başına ayrı commit; ilk satır `R2.x: <ne yapıldı>`. R2.3 üç alt adıma bölünmüştür
(bölüm 5.2–5.4); her alt adım kendi commit'inde ve her commit'te **tüm suite yeşil**.

### 2.8 Kapsam disiplini
Her agent yalnızca kendi kalemini yapar. Yol boyunca fark edilen sorunlar düzeltilmez, bitiş
raporunda "gözlem" olarak yazılır. Sözleşme değişikliği gerekiyorsa önce gerekçe bildirilir.

---

## 3. Sıra ve paralellik

```
R2.3  (katman düzeltmesi, ÖNCE)  ──►  R2.1  (BookVocabulary)  ──►  R2.2  (BookLanguageModel)
                                          │                              │
                                          └──────►  R2.4  (bilgi tabanı + ölçüm)  ◄──────┘
```

- **R2.3 kritik yolun başındadır** (D12). Yol haritası onu "bağımsız" diye işaretliyor ama R2.1
  frekans listesine Core'dan erişmek zorunda; port önce kurulmazsa ya politika Cli'da kalır ya da
  üçüncü bir `tr_50k` yükleyicisi doğar (bugün zaten **iki** tane var, bölüm 4.1).
- R2.1 ve R2.2 sıralıdır: LM'in unigram backoff'u hazneden gelir.
- R2.4 son kalemdir; ikisini de gerçek kitapta koşturup baseline'a yazar.
- Paralellik penceresi dardır; iki agent aynı anda çalışacaksa R2.3 bitmeden başlamasınlar.

---

## 4. Mevcut durumun tespiti

Okunan koddan çıkarılmıştır; agent doğrulamadan kabul etmesin ama araştırmayı sıfırdan yapmasın.

### 4.1 `tr_50k` bugün iki ayrı yerden, iki ayrı kuralla yükleniyor

| Yükleyici | Normalizasyon | "Lexical" kuralı | Çıktı |
|---|---|---|---|
| [CleanTurkishLexicon.cs:19](../src/EpubFixer.Cli/OcrReconstruction/CleanTurkishLexicon.cs#L19) | NFC + tr-TR lower | Harf **veya en fazla bir** kesme; harf zorunlu **değil** | `word → frequency` |
| [FrequencyMorphologyWordRecognizer.cs:44](../src/EpubFixer.Cli/Quality/FrequencyMorphologyWordRecognizer.cs#L44) | `TurkishWordNormalizer` (aynı sonuç) | Harf zorunlu, kesme sayısı **sınırsız** | `HashSet<string>` |

İki kural aynı dosyadan farklı kümeler üretebilir. R2.3 bunları tek porta indirir ve **bugünkü
anahtar kümesinin değişmediğini** testle kanıtlar (bölüm 5, test 7).

Ayrıca `CleanTurkishLexicon.Load(path, analyzer)` aldığı `analyzer` parametresini **hiç
kullanmıyor** (satır 29–33'teki yorum nedenini açıklıyor). Taşınırken düşürülür (D15).

### 4.2 `EpubFixer.Core` bugün dosya sistemine dokunuyor

Yol haritası R2.3'ün kabulünü "Core hiçbir dosya sistemi/process API'si import etmez" diye
yazıyor. Bugün Core'da dört dosya `System.IO` kullanıyor:

```
src/EpubFixer.Core/Epub/EpubPackageReader.cs
src/EpubFixer.Core/Epub/EpubPackageWriter.cs
src/EpubFixer.Core/Fix/EpubFixService.cs
src/EpubFixer.Core/Fix/EpubOutputValidator.cs
```

Bunları kenara taşımak Faz 2'nin işi değildir (EPUB okuma/yazmanın tamamını port arkasına almak
demektir). Mimari testi bu dördünü **açık izin listesi** olarak taşır ve beşincisinin eklenmesini
engeller — bkz. karar **D14**.

### 4.3 Kitabın sağlık sayıları (Faz 0 baseline'ı)

`docs/baselines/odun-kesmek.book-health.json`: kaynakta **46.973** token, **2.832**'si
çözümlenemiyor (‰60,3). En sık çözümlenemeyenler neredeyse tamamen özel isim:
`Burg`, `Auersberger`, `Joana'nın`, `Gentz`, `Ekdal`, `Billroth`, `Graben'de`, `Kilb'de`…

Bu, R2.1'in var oluş gerekçesidir: `tr_50k` + morfoloji bu kitabın kendi isimlerini bilmiyor.
Hazne onları **kitaptan** öğrenecek. Aynı yol en büyük riski de doğuruyor: bozuk token'lar da
aynı kapıdan girebilir (bölüm 2.5).

Faz 1 ölçümü: kitapta **13.215** distinct morfoloji sorgusu var; yani kitap ölçeğindeki bir
prefill batch'i ~13k kelimedir. `tr_50k` bunun üstüne **50 bin** kelime daha eklerdi — eklemiyoruz
(D16).

### 4.4 Ground truth hâlâ tireleme ağırlıklı — gözlem

`test-data/odun-kesmek/ground-truth.json` (schemaVersion 2) 160 `expected` kaydı içeriyor:
`Hyphenation` 148, `Fragmentation` 5, `GlyphConfusion` 4, `GarbageInsertion` 2, `SpuriousSpace` 1.
R0.2'nin "sınıf başına ≥ 25 kayıt" hedefi karşılanmamış. Faz 2 bunu **düzeltmez**; ama hedef
kapsaması metriği (bölüm 6.5) bu çarpıklık bilinerek okunmalıdır — ve held-out ölçümü tam bu
yüzden ikinci, bağımsız bir sayı olarak zorunludur (D19).

### 4.5 Bağlamın kitapta gerçekten var olduğu doğrulandı

`berjer koltukta` kalıbı kitapta düzinelerce kez geçiyor (`…diye düşündüm berjer koltukta…`).
R2.2'nin kabul testi (`P(koltukta | berjer) > P(koltukta)`) bu veriyle gerçekten karşılanabilir;
uydurma bir iddia değildir. Ayrıca `Berjer koltukta` (cümle başı, büyük harf) de geçiyor —
2.3'teki normalizasyon kuralının neden gerekli olduğunun canlı örneği.

---

## 5. R2.3 — Katman düzeltmesi: Cli → Core

> **İlk kalem** (D12). Boyut: **M**. Bu kalem davranış değiştirmez; taşır ve tek doğruluk
> kaynağına indirir.

### Amaç
B5. Politika Core'a, I/O Cli'da. R2.1 ve R2.2 doğru katmana yazılabilsin diye önce zemin kurulur.

### Kapsam kararı: ne taşınır, ne taşınmaz

| Tip | Karar | Gerekçe |
|---|---|---|
| `OcrEditCostModel` | **Core'a taşınır** | Saf politika (sabitlerden ibaret record); R3.2 arc maliyetini buradan alacak |
| `CleanTurkishLexicon` | **Bölünerek taşınır** | Politika (ayrıştırma/filtre/sayım) Core'a, dosya okuma Cli'da kalır |
| `BookContextIndex` | **Cli'da kalır** (D13) | Yalnızca R4.3'te silinecek deneysel reranker hattına hizmet ediyor; bağlamın decoder'a taşınması R2.2'nin işi |

### Dosya haritası

Yeni:
```
src/EpubFixer.Core/Lexicon/ITurkishFrequencyList.cs
src/EpubFixer.Core/Lexicon/TurkishFrequencyList.cs
src/EpubFixer.Core/Ocr/OcrEditCostModel.cs
src/EpubFixer.Cli/Lexicon/FileTurkishFrequencyListSource.cs
tests/EpubFixer.Tests/TurkishFrequencyListTests.cs
tests/EpubFixer.Tests/CoreLayeringTests.cs
```
Silinen: `src/EpubFixer.Cli/OcrReconstruction/CleanTurkishLexicon.cs`,
`src/EpubFixer.Cli/OcrReconstruction/OcrEditCostModel.cs`.

Değişen: `NoisyChannelRegionReconstructor`, `SymSpellRegionReconstructor`,
`OcrReconstructionComparison`, `FullBookReaderPreview`, `FrequencyMorphologyWordRecognizer`,
`Program.cs` ve `CleanTurkishLexicon` kullanan testler
(`NoisyChannelDiagnosticTests`, `NoisyChannelV2Tests`, `PerformanceBudgetTests`).

### Sözleşme

```csharp
namespace EpubFixer.Core.Lexicon;

/// <summary>A frequency-ranked word list. Pure policy: holds no path, opens no file.</summary>
public interface ITurkishFrequencyList
{
    bool Contains(string normalizedWord);
    long GetFrequency(string normalizedWord);
    long TotalFrequency { get; }
    IReadOnlyCollection<string> Words { get; }
}

/// <summary>Immutable in-memory frequency list. Keys are TurkishWordNormalizer-normalized.</summary>
public sealed class TurkishFrequencyList : ITurkishFrequencyList
{
    public static TurkishFrequencyList FromLines(IEnumerable<string> lines);
}
```

Cli tarafı yalnızca satırları verir:

```csharp
namespace EpubFixer.Cli.Lexicon;

public static class FileTurkishFrequencyListSource
{
    public static string DefaultPath { get; }                       // Resources/OcrReconstruction/tr_50k.txt
    public static TurkishFrequencyList Load(string? path = null);   // File.ReadLines → FromLines
}
```

### Kesin davranış kuralları
- Satır biçimi bugünkü gibi: boşlukla ayrılmış alanlar, **son** alan frekans; `frequency <= 0`
  veya alan sayısı < 2 olan satır atlanır.
- Anahtar: `TurkishWordNormalizer.Normalize` (NFC + tr-TR lower).
- Lexical filtresi **tek** kurala iner: en az bir harf; harf dışında yalnızca kesme işareti
  (`'`, `’`). Kesme sayısı sınırlanmaz.
- Aynı anahtara düşen satırların frekansları toplanır (bugünkü davranış).
- `TurkishFrequencyList` değişmezdir, kilit kullanmaz, paralel okunabilir.

### Yazılacak testler (önce kırmızı)
1. `FromLines_SumsFrequenciesOfDuplicateKeys`
2. `FromLines_SkipsMalformedAndNonPositiveLines`
3. `FromLines_NormalizesKeysToLowercaseNfc` — `Auersberger` → `auersberger`
4. `FromLines_RejectsEntriesWithDigitsOrPunctuation`
5. `FromLines_AcceptsApostropheForms`
6. `TotalFrequency_IsTheSumOfAllEntries`
7. `RealResource_KeySetMatchesTodaysLoaders` `[Trait("Category","Slow")]` — gerçek `tr_50k.txt`;
   yüklenen anahtar sayısı ve sıralı anahtar listesinin SHA-256'sı teste sabit olarak yazılır.
   **Bu test bugünkü davranışın korunduğunun kanıtıdır**: değer, taşımadan **önce** iki eski
   yükleyiciyle ölçülür; iki yükleyici birbirinden farklı sonuç veriyorsa agent durur ve bildirir.
8. `CoreLayeringTests.CoreHasNoNewFileSystemDependencies` — `EpubFixer.Core` altındaki tüm `.cs`
   dosyaları taranır; `System.IO`, `System.Diagnostics.Process`, `File.`, `Directory.`,
   `ZipArchive` geçen dosyalar bölüm 4.2'deki **dört dosyalık izin listesiyle** karşılaştırılır.
   Liste dışı bir dosya çıkarsa test kırmızı (D14).
9. `OcrEditCostModelTests.DefaultsAreUnchanged` — taşıma sonrası sabitler birebir aynı.

### Kabul kriteri
- [ ] `CleanTurkishLexicon` ve Cli'daki `OcrEditCostModel` dosyaları silinmiş; tüm çağrı yerleri
      yeni tiplere bağlı.
- [ ] `tr_50k.txt` için yüklenen anahtar kümesi taşımadan önce ve sonra **birebir aynı** (test 7).
- [ ] `measure` çıktısı baseline ile birebir aynı (D21).
- [ ] `EpubFixer.Core` içinde yeni I/O yok; `CoreLayeringTests` yeşil.
- [ ] `dotnet test EpubFixer.slnx` yeşil, hiçbir mevcut assert değeri değişmedi.

### Alt adımlar
- **5.2 Alt adım A** — `ITurkishFrequencyList` + `TurkishFrequencyList` + Cli kaynağı; iki eski
  yükleyicinin ikisi de yeni porta bağlanır (`CleanTurkishLexicon` geçici olarak ince bir
  sarmalayıcıya düşer). Test 7 burada yazılır ve yeşilleşir.
- **5.3 Alt adım B** — `CleanTurkishLexicon` silinir; `NoisyChannel*`, `SymSpell*`,
  `OcrReconstructionComparison`, `FullBookReaderPreview` ve testler `ITurkishFrequencyList` alır.
- **5.4 Alt adım C** — `OcrEditCostModel` Core'a taşınır; `CoreLayeringTests` eklenir.

### Kapsam dışı
`BookContextIndex` taşımak (D13), EPUB I/O'sunu porta almak (bölüm 4.2), `ITurkishMorphologyAnalyzer`'ı
silmek (R4.3), deneysel reconstructor'ların mantığına dokunmak.

---

## 6. R2.1 — `BookVocabulary`

> Boyut: **L** (yol haritası M diyor; kapsama/kontaminasyon ölçümü ve held-out harness'ı bu kalemde).
> Bağımlılık: R2.3, R1.1.

### Amaç
Decoder'ın "bu bir kelime mi, ne kadar olağan?" sorusuna **bu kitap için** cevap veren tek kaynak.
`tr_50k` tek başına yetmiyor: kitabın en sık çözümlenemeyen token'ları onun özel isimleri
(bölüm 4.3).

### Dosya haritası
Yeni:
```
src/EpubFixer.Core/Lexicon/CleanTokenSequence.cs
src/EpubFixer.Core/Lexicon/BookVocabulary.cs
src/EpubFixer.Core/Lexicon/BookVocabularyBuilder.cs
src/EpubFixer.Core/Lexicon/BookVocabularyOptions.cs
src/EpubFixer.Core/Lexicon/Models/VocabularyEntry.cs
src/EpubFixer.Core/Lexicon/Models/VocabularySource.cs
src/EpubFixer.Core/Lexicon/VocabularyCoverageReport.cs
src/EpubFixer.Core/Lexicon/VocabularyCoverageMeter.cs
tests/EpubFixer.Tests/CleanTokenSequenceTests.cs
tests/EpubFixer.Tests/BookVocabularyBuilderTests.cs
tests/EpubFixer.Tests/BookVocabularyCoverageTests.cs
```
Bu kalem mevcut hiçbir üretim dosyasını değiştirmez; wiring R2.4'ün işidir.

### 6.1 Sözleşme

```csharp
namespace EpubFixer.Core.Lexicon;

public enum VocabularySource { Book, Frequency, Morphology }

public sealed record VocabularyEntry(
    string Normalized,        // NFC + tr-TR lower
    string PreferredSurface,  // kitapta en sık görülen yüzey biçimi; listeden gelende = Normalized
    int BookCount,            // kitabın temiz bölgelerindeki sayım (liste-only kelimede 0)
    VocabularySource Source);

public sealed class BookVocabulary
{
    public int Count { get; }
    public IReadOnlyCollection<string> Words { get; }            // normalize anahtarlar, Ordinal sıralı
    public bool Contains(string word);
    public double UnigramLogProbability(string word);            // backoff dahil; OOV'de sabit ceza
    public VocabularySource SourceOf(string word);               // yoksa VocabularyLookupException
    public VocabularyEntry? Find(string word);                   // yoksa null
}

public sealed record BookVocabularyOptions(
    int MinBookCount = 2,              // temiz token sayımı bu eşikte ise → Book
    int MinUnverifiedBookCount = 3,    // ne morfolojik geçerli ne listede → bu eşiğin altı girmez
    double AddK = 0.5,                 // kitap unigram'ı için add-k
    double FrequencyListDiscount = 0.1,
    double MorphologyOnlyLogProbability = -14.0,
    double UnknownLogProbability = -18.0);

public sealed class BookVocabularyBuilder
{
    public BookVocabularyBuilder(ITurkishFrequencyList frequencyList, BookVocabularyOptions? options = null);

    /// <summary>D8 deseni: sorgu kümesi çalıştırmadan önce bilinir.</summary>
    public IEnumerable<string> EnumerateMorphologyQueries(
        LogicalTextStream stream, IReadOnlyList<CorruptedTextRegion> regions);

    public BookVocabulary Build(
        LogicalTextStream stream, IReadOnlyList<CorruptedTextRegion> regions, IMorphologyOracle oracle);
}
```

> **Sözleşme genişletmesi (D18):** yol haritası `Contains` / `UnigramLogProbability` / `SourceOf`
> diyordu. `Words` R3.1'in matcher'ını kurmak için, `PreferredSurface` decoder'ın EPUB'a yazacağı
> metni bilmek için, `Find` ikisini tek aramada vermek için eklendi. Kaldırma yok, yalnızca ekleme.

### 6.2 `CleanTokenSequence` — tek token akışı

Vocabulary ve LM aynı token dizisini görmek zorundadır; iki ayrı tokenizasyon zamanla kayar
(D8'in aynı gerekçesi). Bu yüzden ortak, saf bir kaynak:

```csharp
public sealed record CleanToken(string Normalized, string Surface, int LogicalStart, bool StartsSegment);

public static class CleanTokenSequence
{
    public static IReadOnlyList<CleanToken> Build(
        LogicalTextStream stream, IReadOnlyList<CorruptedTextRegion> regions);
}
```

- Tokenizasyon `WordTokenizer` ile yapılır; **yeni tokenizer yazılmaz**.
- Bozuk region ile **kesişen** her token atlanır ve akışta bir kesinti bırakır: atlanan token'ın
  ardından gelen token `StartsSegment = true` alır. LM böylece bozuk bölgenin iki yakasını
  yanlışlıkla bigram yapmaz.
- Paragraf/doküman sınırındaki ilk token da `StartsSegment = true`.
- `Normalized = TurkishWordNormalizer.Normalize(Surface)`.

### 6.3 Hazneye giriş kuralları (kontaminasyon savunması)

Bir kitap token'ı hazneye ancak **hepsi** sağlanırsa girer:

1. Lexical: en az bir harf; harf dışında yalnızca kesme işareti.
2. `SuspiciousTokenRules.IsSuspicious(surface, previous, next)` **false** olacak
   (gömülü rakam, garbage glyph, iç/ön çift noktalama, izole `ı/i/l/1`).
3. Bozuk region ile kesişmeyecek (6.2).
4. Tek karakterli token yalnızca frekans listesinde varsa girer — `GlyphConfusion` sınıfının
   gürültüsü tam olarak buradan sızar.
5. Sayım eşiği:
   - `BookCount ≥ MinBookCount` → `Source = Book`
   - `BookCount < MinBookCount` ama `oracle.IsValid(surface)` → `Source = Morphology`
   - morfolojik olarak geçersiz **ve** frekans listesinde yok ise ek olarak
     `BookCount ≥ MinUnverifiedBookCount` aranır. Özel isimler bu kapıdan girer
     (`Auersberger` kitapta 50+ kez geçiyor; tek seferlik OCR artığı geçemiyor).

Frekans listesinin tamamı ayrıca hazneye girer (`Source = Frequency`), **morfoloji sorgusu
yapılmadan** (D16). Kaynak önceliği: `Book` > `Frequency` > `Morphology`.

`EnumerateMorphologyQueries` yalnızca **kitap** token'larının yüzey biçimlerini döndürür
(kesme kökleri dahil); liste kelimeleri sorulmaz.

### 6.4 Unigram olasılığı

`N` = hazneye giren kitap token'larının toplam sayımı, `V` = `Count`.

| Katman | Koşul | Değer |
|---|---|---|
| 1 | `BookCount ≥ 1` | `log((BookCount + AddK) / (N + AddK·V))` |
| 2 | Kitapta yok, listede var | `log(FrequencyListDiscount · freq(w)/TotalFrequency)` |
| 3 | Yalnızca morfolojik geçerli | `MorphologyOnlyLogProbability` |
| 4 | Haznede yok | `UnknownLogProbability` |

**Değişmez (test edilir):** katman numarası büyüdükçe değer artmaz; yani katman 2'nin döndürdüğü
değer, kitapta geçen **en seyrek** kelimenin değerinin üstüne çıkamaz (gerekirse tavanla kırpılır).
Aksi halde decoder, kitapta hiç geçmeyen bir liste kelimesini kitabın kendi kelimesine tercih eder.

### 6.5 Kapsama ve kontaminasyon ölçümü (D19)

"Kitabın temiz token'larının ≥ %95'i haznede" ölçütü **olduğu gibi kullanılamaz**: hazne zaten o
token'lardan kuruluyor, sayı yapısal olarak ~%100 çıkar. Üç ayrı sayı ölçülür:

```csharp
public sealed record VocabularyCoverageReport(
    int VocabularySize,
    int BookSourced, int FrequencySourced, int MorphologySourced,
    double HeldOutCoverage,        // 1
    double TargetCoverage,         // 2
    int SuspiciousEntries,         // 3
    IReadOnlyList<string> MissingTargets,
    IReadOnlyList<string> WorstMissingHeldOut);
```

1. **Held-out kapsama** — spine dokümanlarından indeksi 5'in katı olanlar dışarıda bırakılarak
   hazne kurulur; dışarıda kalan dokümanların temiz token'larının ne kadarının haznede olduğu
   ölçülür. **Kabul: ≥ %95.**
2. **Hedef kapsaması** — `ground-truth.json`'daki 160 `expected` formunun (normalize edilmiş)
   ne kadarı haznede. Hedef kelime haznede yoksa Faz 3 onu **asla** öneremez; bu yüzden en kritik
   sayıdır. **Kabul: ≥ %98.** Eksikler `MissingTargets` olarak raporlanır (bölüm 4.4'teki sınıf
   çarpıklığı akılda tutularak okunur).
3. **Kontaminasyon** — `SuspiciousTokenRules` ile şüpheli sayılan hazne girdisi sayısı.
   **Kabul: 0.** Ek olarak `Book` kaynaklı girdilerden Ordinal sıralı 100 örnek rapora yazılır;
   bu bir test değil, R2.4'ün insan gözüne sunduğu kanıttır.

### 6.6 Yazılacak testler (önce kırmızı)

`CleanTokenSequenceTests`
1. `Build_SkipsTokensOverlappingCorruptedRegions`
2. `Build_MarksTokenAfterSkippedRegionAsSegmentStart`
3. `Build_MarksFirstTokenOfParagraphAsSegmentStart`
4. `Build_NormalizesWithTurkishLowercase` — `Berjer` → `berjer`, `I` → `ı`

`BookVocabularyBuilderTests` (sentetik korpus + `FakeMorphologyOracle`)
5. `Build_IncludesTokenSeenTwice`
6. `Build_ExcludesSingleOccurrenceUnverifiedToken`
7. `Build_IncludesSingleOccurrenceMorphologicallyValidToken`
8. `Build_IncludesProperNameAboveUnverifiedThreshold`
9. `Build_ExcludesTokensInsideCorruptedRegions`
10. `Build_ExcludesSuspiciousTokens` — `:,ohbet`, `ge-^:cn`, gömülü rakam
11. `Build_ExcludesIsolatedSingleGlyphNotInFrequencyList` — `ı`, `l`, `1`
12. `Build_IncludesEveryFrequencyListWord`
13. `Build_PrefersBookSourceOverFrequency`
14. `Build_KeepsMostFrequentSurfaceFormAsPreferred` — `Berjer` 3, `berjer` 40 → `berjer`;
    yalnızca büyük harfle geçen `Auersberger` → `Auersberger`
15. `EnumerateMorphologyQueries_CoversEveryFormTheBuilderAsks` — `RecordingMorphologyOracle` ile
    kaydedilen bilinmeyen sorgu sayısı **0** (Faz 1'in eksiksizlik deseni)
16. `EnumerateMorphologyQueries_DoesNotIncludeFrequencyListWords` (D16)
17. `Build_IsDeterministic` — aynı girdi iki kez → aynı `Words` sırası, aynı olasılıklar
18. `UnigramLogProbability_IsHigherForFrequentBookWord`
19. `UnigramLogProbability_FrequencyOnlyWordNeverOutranksRarestBookWord` (6.4 değişmezi)
20. `UnigramLogProbability_ReturnsUnknownPenaltyForOov`
21. `SourceOf_ThrowsForUnknownWord`

`BookVocabularyCoverageTests` `[Trait("Category","Slow")]` — gerçek kitap
22. `HeldOutCoverage_IsAtLeastNinetyFivePercent`
23. `TargetCoverage_IsAtLeastNinetyEightPercent`
24. `SuspiciousEntries_IsZero`
25. `Build_CompletesWithinBudget` — hazne kurulumu ≤ 2 sn (D24)

### 6.7 Kabul kriteri
- [ ] Held-out kapsama ≥ %95, hedef kapsaması ≥ %98, şüpheli girdi sayısı 0.
  2026-09-13 ölçümü: token kütlesi tabanlı bitişik %20 held-out bölmesinde held-out kapsama
  **%90,64**; hedef kapsaması **%100**; şüpheli girdi **0**. Eşik düşürülmedi; karar D25'e
  işlendi.
- [ ] `EnumerateMorphologyQueries` eksiksiz (kayıt tutan oracle ile 0 bilinmeyen).
- [ ] Frekans listesi için **hiç** morfoloji sorgusu yapılmıyor.
- [ ] `BookVocabulary` değişmez ve deterministik; `EpubFixer.Core` içinde I/O yok.
- [ ] Mevcut hiçbir test değişmedi; `measure` çıktısı baseline ile aynı (D21).

### 6.8 Kapsam dışı
LM (R2.2), `BookHealthMeter`'ın tanıyıcısını değiştirmek (D21), disk cache (D24), matcher/lattice
(Faz 3), eşik kalibrasyonu (R5.4). Kapsama hedefine ulaşmak için 6.3'teki filtreleri gevşetmek
**yasaktır**; gerekiyorsa agent durur ve ölçümü bildirir.

---

## 7. R2.2 — `BookLanguageModel`

> Boyut: **M**. Bağımlılık: R2.1.

### Amaç
B2'nin ikinci yarısı: bağlamı reranker'dan çıkarıp decoder'ın skoruna sokmak.
`P(koltukta | berjer)` bu kitapta çok yüksektir (bölüm 4.5); kararı **verirken** kullanmak,
verdikten sonra düzeltmeye çalışmaktan hem doğru hem ucuzdur.

### Dosya haritası
Yeni:
```
src/EpubFixer.Core/Lexicon/ILanguageModel.cs
src/EpubFixer.Core/Lexicon/BookLanguageModel.cs
src/EpubFixer.Core/Lexicon/BookLanguageModelBuilder.cs
src/EpubFixer.Core/Lexicon/LanguageModelStatistics.cs
tests/EpubFixer.Tests/BookLanguageModelTests.cs
```

### Sözleşme

```csharp
namespace EpubFixer.Core.Lexicon;

public interface ILanguageModel
{
    /// <summary>Stupid-backoff SCORE in log space — not a normalized probability (D22).
    /// A null previousWord means segment start.</summary>
    double LogProbability(string word, string? previousWord);
}

public sealed class BookLanguageModel : ILanguageModel
{
    public LanguageModelStatistics Statistics { get; }   // unigram/bigram tür ve token sayıları
}

public sealed class BookLanguageModelBuilder
{
    public BookLanguageModelBuilder(BookVocabulary vocabulary, double backoffAlpha = 0.4);
    public BookLanguageModel Build(LogicalTextStream stream, IReadOnlyList<CorruptedTextRegion> regions);
}
```

### Kesin davranış kuralları
- Sayımlar `CleanTokenSequence` üzerinden yapılır — vocabulary ile **aynı** akış (6.2).
- `StartsSegment = true` olan token'ın öncülü `<s>`'tir; bozuk region'ın iki yakası ve paragraf
  sınırının iki yakası bigram yapılmaz.
- Haznede olmayan token bigram sayımına **girmez** (kontaminasyon savunmasının devamı).
- Stupid backoff: `c(p,w) > 0` ise `log(c(p,w)/c(p))`, değilse
  `log(alpha) + vocabulary.UnigramLogProbability(w)`.
- `previousWord` haznede yoksa doğrudan backoff katmanına düşülür.
- `previousWord = null` → `vocabulary.UnigramLogProbability(w)`; `<s>` bigramları ayrıca sayılır
  ve `previousWord = "<s>"` ile sorgulanabilir.
- Model değişmezdir, deterministiktir, kilit kullanmaz.

### Yazılacak testler (önce kırmızı)
1. `LogProbability_UsesBigramWhenSeen`
2. `LogProbability_BacksOffToUnigramWithAlphaWhenBigramUnseen`
3. `LogProbability_ReturnsUnknownPenaltyForOovWord`
4. `LogProbability_TreatsNullPreviousWordAsUnigram`
5. `LogProbability_DoesNotBridgeAcrossCorruptedRegion`
6. `LogProbability_DoesNotBridgeAcrossParagraphBoundary`
7. `Build_IgnoresTokensOutsideVocabulary`
8. `Build_IsDeterministic`
9. `Statistics_ReportTypeAndTokenCounts`
10. `RealBook_ContextualScoreBeatsContextFree` `[Trait("Category","Slow")]`
    — `LogProbability("koltukta", "berjer") > LogProbability("koltukta", null)`
11. `RealBook_ContextualScoreBeatsUnrelatedContext` `[Trait("Category","Slow")]`
    — `LogProbability("koltukta", "berjer") > LogProbability("koltukta", "graben")`
12. `RealBook_HeldOutBigramHitRateIsReported` `[Trait("Category","Slow")]` — held-out
    dokümanlardaki bigram'ların ne kadarının modelde bulunduğu ölçülür ve baseline'a yazılır
    (perplexity **kullanılmaz**, D22)

### Kabul kriteri
- [ ] 10 ve 11 numaralı iddialar gerçek kitapta doğrulanmış.
- [ ] Bilinmeyen bigram'da unigram'a, bilinmeyen unigram'da sabit cezaya düşüyor.
- [ ] Bozuk region ve paragraf sınırı bigram köprüsü kurmuyor.
- [ ] Model kurulumu ≤ 1 sn; `EpubFixer.Core` içinde I/O yok.

### Kapsam dışı
Trigram, interpolasyon, Kneser–Ney, `lambda` kalibrasyonu (R5.4), lattice'e bağlamak (R3.3),
`BookContextIndex`'i silmek (R4.3).

---

## 8. R2.4 — Bilgi tabanı orkestratörü ve ölçüm

> Yol haritasında yok; Faz 1'in R1.0'ına karşılık gelen "ölç ve kilitle" kalemidir (D23).
> Boyut: **S**. Bağımlılık: R2.1, R2.2.

### Amaç
İki bilgi tabanını Faz 3'ün kullanacağı tek giriş noktasında birleştirmek ve gerçek kitaptaki
sayıları baseline'a yazmak. Koşturulmayan kod çürür; bu kalem Faz 2'yi çalışır halde bırakır.

### Sözleşme

```csharp
namespace EpubFixer.Core.Lexicon;

public sealed record BookKnowledge(
    BookVocabulary Vocabulary,
    ILanguageModel LanguageModel,
    IReadOnlyList<CorruptedTextRegion> Regions,
    VocabularyCoverageReport Coverage);

public sealed class BookKnowledgeBuilder
{
    public BookKnowledgeBuilder(ITurkishFrequencyList frequencyList, BookVocabularyOptions? options = null);

    /// <summary>Region tespiti + hazne + LM. Oracle prefill'i burada, aşama aşama yapılır (D5).</summary>
    public BookKnowledge Build(LogicalTextStream stream, IMorphologyOracleBuilder oracleBuilder);
}
```

Sıra: `OcrRegionDetector.EnumerateMorphologyQueries` → oracle → `Detect` → hazne sorguları →
oracle (monoton, ikinci batch) → `BookVocabulary` → `BookLanguageModel`.

### Kapsam
- `src/EpubFixer.Core/Lexicon/BookKnowledgeBuilder.cs`
- `src/EpubFixer.Cli/Quality/VocabularyReportCommand.cs` — `debug-vocabulary <epub> [--json <path>]`
- `Program.cs` wiring (`CachingMorphologyOracleBuilder` + `FileTurkishFrequencyListSource`)
- `docs/baselines/odun-kesmek.vocabulary.json`, `docs/baselines/odun-kesmek.language-model.json`
- `docs/baselines/README.md` — yeni baseline'ların tanımı ve nasıl üretildiği

### Baseline şeması (vocabulary)
```json
{
  "dataset": "odun-kesmek",
  "measuredOn": "<yyyy-MM-dd>",
  "commit": "<sha>",
  "vocabularySize": 0,
  "bookSourced": 0,
  "frequencySourced": 0,
  "morphologySourced": 0,
  "heldOutCoverage": 0.0,
  "targetCoverage": 0.0,
  "missingTargets": [],
  "suspiciousEntries": 0,
  "sampledBookEntries": [],
  "buildSeconds": 0.0
}
```

### Yazılacak testler
1. `BookKnowledgeBuilder_PrefillsOracleForEveryQuery` — `RecordingMorphologyOracle`, bilinmeyen 0
2. `BookKnowledgeBuilder_IsDeterministic`
3. `VocabularyReportCommand_WritesJsonAndHumanOutput` (sahte builder ile)
4. `VocabularyReportCommand_FailsCleanlyOnMissingEpub`
5. `FullBook_KnowledgeBuildStaysWithinBudget` `[Trait("Category","Slow")]` — ≤ 3 sn (D24)

### Kabul kriteri
- [ ] `debug-vocabulary` gerçek kitapta koşuyor, JSON ve insan-okur çıktı üretiyor.
- [ ] İki baseline dosyası ölçülen değerlerle commit edilmiş; `README.md` tanımları yazılmış.
- [ ] Bilgi tabanı kurulumu ≤ 3 sn; ikinci koşuda morfoloji cache'i sayesinde flookup başlamıyor.
- [ ] `measure` çıktısı hâlâ baseline ile birebir aynı (D21).

---

## 9. Kararlar (bu planla birlikte verildi)

| # | Karar | Gerekçe | Nereye işlendi |
|---|---|---|---|
| D12 | **Sıra değişti: R2.3 → R2.1 → R2.2.** Yol haritası R2.3'ü "bağımsız" sayıyordu. | R2.1 frekans listesine Core'dan erişmek zorunda; port önce kurulmazsa ya politika Cli'da kalır ya da üçüncü bir `tr_50k` yükleyicisi doğar. | Bölüm 3, 5 |
| D13 | **`BookContextIndex` Core'a taşınmaz**, Cli'da kalır. R2.3'ün kapsamı `CleanTurkishLexicon` + `OcrEditCostModel`. | Yalnızca R4.3'te silinecek deneysel reranker hattına hizmet ediyor; işlevinin decoder'daki karşılığı R2.2'dir. Ölmekte olan kodu Core'a taşımak churn'dür. *Aksi tercih edilirse taşıma mekaniktir ve R2.3'e eklenebilir.* | Bölüm 5 |
| D14 | Mimari testi "Core'da hiç I/O yok" yerine **izin listeli** yazılır: `EpubPackageReader`, `EpubPackageWriter`, `EpubFixService`, `EpubOutputValidator`. | Bu dört dosya bugün zaten `System.IO` kullanıyor; hepsini porta almak Faz 2'nin işi değil. Test yine de beşinci ihlali engeller. | Bölüm 4.2, 5 |
| D15 | `CleanTurkishLexicon.Load`'un kullanılmayan `analyzer` parametresi taşınırken **düşürülür**; lexical filtresi tek kurala iner. | Parametre hiç kullanılmıyor (dosyadaki yorum nedenini açıklıyor); iki farklı filtre aynı dosyadan iki farklı küme üretebiliyor. Anahtar kümesinin değişmediği testle kanıtlanır. | Bölüm 4.1, 5 |
| D16 | Frekans listesi kelimeleri **morfoloji oracle'ına sorulmaz**. | Küratörlü bir liste; 50k kelimelik batch kitap ölçeğindeki 13k'lık prefill'i dörde katlar ve hiçbir kararı değiştirmez. | Bölüm 6.3 |
| D17 | İki normalizasyon uzayı: morfoloji NFC + case-sensitive (D9), hazne/LM NFC + tr-TR lower. Hazne baskın **yüzey biçimini** ayrıca saklar. | Morfolojide `Auersberger ≠ auersberger`; haznede `Berjer` ile `berjer` aynı kelime. Decoder EPUB'a yazarken doğru yüzey biçimine ihtiyaç duyar. | Bölüm 2.3, 6.1 |
| D18 | `BookVocabulary` sözleşmesi `Words`, `Count`, `Find`, `PreferredSurface` ile **genişletilir**. | R3.1 matcher'ı kelime listesi olmadan kurulamaz; decoder yüzey biçimi olmadan metin yazamaz. Yol haritasındaki üç üye korunur. | Bölüm 6.1 |
| D19 | Kapsama **iki bağımsız sayıyla** ölçülür: held-out (≥ %95) ve ground-truth hedef kapsaması (≥ %98). | Yol haritasının "kitabın temiz token'larının %95'i haznede" ölçütü döngüseldir: hazne o token'lardan kuruluyor, sayı yapısal olarak ~%100 çıkar. | Bölüm 6.5 |
| D20 | Hazneye giriş filtreleri (şüpheli token, tek karakter, doğrulanmamış özel isim eşiği) **kapsama uğruna gevşetilmez**. | Kontaminasyon, motorun hatayı "doğru" sayması demektir; bu, kaçırılmış bir düzeltmeden pahalıdır (yol haritası risk #1). | Bölüm 2.5, 6.3 |
| D21 | `BookHealthMeter`'ın tanıyıcısı Faz 2'de **değiştirilmez**; `measure` çıktısı birebir aynı kalır. | Kuzey yıldızı metriği motorun kendi bilgi tabanına bağlanırsa kendini onaylar: kitapta iki kez geçen bir OCR artığı "çözümlendi" sayılır ve sayı gerçek olmayan bir iyileşme gösterir. | Bölüm 1, 6.8 |
| D22 | LM stupid backoff'tur ve **normalize olasılık değil skor** döndürür; kalite ölçütü perplexity değil **held-out bigram isabet oranı**dır. | Stupid backoff normalize değildir; perplexity raporlamak yanlış bir kesinlik iddiasıdır. | Bölüm 7 |
| D23 | Yol haritasında olmayan **R2.4** eklendi (orkestratör + ölçüm komutu + baseline). | Faz 2'nin ürünü Faz 4'e kadar üretim hattına bağlanmıyor; ölçülmeyen ve koşturulmayan kod çürür. R1.0 ile aynı gerekçe. | Bölüm 8 |
| D24 | Hazne/LM için **disk cache yok**; bütçe ≤ 3 sn ölçülür. | Sayım işi kitap ölçeğinde ucuzdur; pahalı olan morfolojiydi ve cache'i R1.2'de var. Ölçüm 3 sn'yi aşarsa agent durur ve bildirir. | Bölüm 1, 8 |
| D25 | Held-out kapsama eşiği **düşürülmedi**; token kütlesi tabanlı gerçek ölçüm %90,64 olarak baseline'a yazıldı. | Kaçanlar held-out diliminde eğitim diliminde görülmeyen/az görülen yüzey biçimleri ve türetimlerdir. Filtre gevşetmek kontaminasyon riskini artırır; morfolojik türetim/lemma genellemesi R5.2'nin işidir. | Bölüm 6.7, 8 |

Yeni bir karar ihtiyacı doğarsa agent kendi başına karara varmaz; gerekçeyi bildirip bekler ve
karar bu tabloya eklenir.

---

## 10. Risk kaydı (Faz 2'ye özgü)

| # | Risk | Etki | Azaltma |
|---|---|---|---|
| 1 | Bozuk token hazneye sızar — özellikle **sistematik** OCR hatası ikiden fazla tekrar eder | **Yüksek** | 6.3 filtreleri; `SuspiciousEntries == 0` kabulü; `Book` girdilerinden 100'lük örneğin raporda insan gözüne sunulması |
| 2 | Kapsama hedefi tutmayınca filtrelerin gevşetilmesi | **Yüksek** | D20 açık yasak; gevşetme yalnızca ölçümle ve karar tablosuna yazılarak |
| 3 | Hazne kendi kendini ölçer (döngüsel kapsama) | Orta | D19 held-out + hedef kapsaması |
| 4 | Kuzey yıldızı metriğinin hazneye bağlanması sahte iyileşme gösterir | Orta | D21; `measure` çıktısının birebir aynı kaldığı kabul maddesi |
| 5 | İki normalizasyon uzayının karışması → hazne aramaları sessizce boş döner | Orta | D17 tablosu; yüzey biçimi ve normalizasyon testleri |
| 6 | R2.3 taşıması `tr_50k` anahtar kümesini sessizce değiştirir → tüm aşağı akış kayar | Orta | Test 7 (gerçek kaynak, sıralı anahtar SHA-256'sı); `measure` baseline'ı |
| 7 | Listede olan ama kitapta olmayan kelime, kitabın kelimesini yener | Orta | 6.4 değişmezi ve `UnigramLogProbability_FrequencyOnlyWordNeverOutranksRarestBookWord` |
| 8 | Ground truth'un tireleme ağırlığı (148/160) hedef kapsamasını kolay gösterir | Düşük | Bölüm 4.4 gözlemi; held-out sayısı bağımsız kanıt |
| 9 | Faz 2 kaliteyi iyileştirmediği için "boşa iş" görünür | Düşük | Kabul kriteri kaliteyi **sabit tutmak**; kazanç Faz 3'ün mümkün hale gelmesi |

---

## 11. Devir promptları

### R2.3
```
EpubFixer projesinde docs/phase-2-plan.md'deki R2.3 kalemini uygulayacaksın.

Önce şunları oku:
- docs/phase-2-plan.md — bölüm 2 (ortak kurallar), bölüm 4 (mevcut durum), bölüm 5 (R2.3),
  bölüm 9 (kararlar D12, D13, D14, D15)
- docs/ocr-correction-roadmap.md — R2.3 maddesi ve B5 tespiti
- src/EpubFixer.Cli/OcrReconstruction/CleanTurkishLexicon.cs, OcrEditCostModel.cs
- src/EpubFixer.Cli/Quality/FrequencyMorphologyWordRecognizer.cs
- src/EpubFixer.Core/Lexicon/TurkishWordNormalizer.cs
- CleanTurkishLexicon'u kullanan tüm çağrı yerleri (Cli + testler)

Kurallar:
- Test-first: kırmızı test → minimum kod → refactor.
- DAVRANIŞ KORUNUMU birinci kuraldır. tr_50k.txt için yüklenen anahtar kümesi taşımadan önce ve
  sonra BİREBİR AYNI olmalı; bunu gerçek kaynakla, sıralı anahtar listesinin SHA-256'sı üzerinden
  bir testle kilitle. Fark çıkarsa DUR ve bildir — testi değiştirme.
- `measure test-data/odun-kesmek/input.epub` çıktısı docs/baselines/odun-kesmek.book-health.json
  ile birebir aynı kalmalı.
- Politika Core'a, dosya okuma Cli'da. Core'a System.IO GİRMEZ; mimari testini bölüm 4.2'deki
  dört dosyalık izin listesiyle yaz (D14).
- BookContextIndex'i TAŞIMA (D13). CleanTurkishLexicon.Load'un kullanılmayan analyzer
  parametresini düşür (D15).
- Üç alt adımı (A: port + iki yükleyicinin birleştirilmesi, B: CleanTurkishLexicon'un silinmesi,
  C: OcrEditCostModel + mimari testi) AYRI COMMIT'lerde yap; her commit'te tüm suite yeşil.
- Kapsam R2.3 ile sınırlı. Hazne (R2.1), LM (R2.2), deneysel reconstructor'ların mantığı yok.

Bitirdiğinde: taşınan tipler, anahtar kümesinin aynı kaldığının kanıtı, eklenen testler,
kabul kriterinin durumu, planda güncellenmesi gereken bir şey olup olmadığı.
```

### R2.1
```
EpubFixer projesinde docs/phase-2-plan.md'deki R2.1 kalemini uygulayacaksın.
R2.3 bitmiş olmalı; bitmediyse başlamadan bildir.

Önce şunları oku:
- docs/phase-2-plan.md — bölüm 2, bölüm 4 (özellikle 4.3 ve 4.4), bölüm 6 (R2.1),
  bölüm 9 (kararlar D16, D17, D18, D19, D20, D21)
- docs/ocr-correction-roadmap.md — R2.1 maddesi, "Hedef mimari" ve risk #1
- docs/phase-1-plan.md — bölüm 8 (EnumerateMorphologyQueries deseni, karar D8)
- src/EpubFixer.Core/Lexicon/ (BookLexiconBuilder, TurkishWordNormalizer, R2.3'ün port'u)
- src/EpubFixer.Core/Tokenization/WordTokenizer.cs
- src/EpubFixer.Core/Quality/SuspiciousTokenRules.cs
- src/EpubFixer.Core/Ocr/OcrRegionDetector.cs ve Models/CorruptedTextRegion.cs
- docs/baselines/odun-kesmek.book-health.json (en sık çözümlenemeyen token'lar)

Kurallar:
- Test-first: kırmızı test → minimum kod → refactor.
- KONTAMİNASYON BİRİNCİ DÜŞMANDIR. Bölüm 6.3'teki giriş filtrelerini kapsama hedefini tutturmak
  için GEVŞETME (D20). Hedef tutmuyorsa DUR, ölçümü ve gerekçeni bildir.
- Kapsama iki bağımsız sayıyla ölçülür (D19): held-out (≥ %95) ve ground-truth hedef kapsaması
  (≥ %98). "Kitabın token'larının %95'i haznede" tek başına döngüseldir, onu tek kanıt sayma.
- İki normalizasyon uzayını karıştırma (D17): oracle NFC + case-sensitive; hazne NFC + tr-TR lower.
  Hazne baskın yüzey biçimini ayrıca saklar.
- Frekans listesi kelimelerini morfoloji oracle'ına SORMA (D16).
- EnumerateMorphologyQueries desenini Faz 1'deki gibi uygula: sorgu kümesi, çağrı yapan kodun
  AYNISINDAN üretilsin; RecordingMorphologyOracle ile eksiksizliğini test et.
- BookHealthMeter'ın tanıyıcısına DOKUNMA (D21); `measure` çıktısı baseline ile birebir aynı kalmalı.
- Core'a I/O girmez. Hazne değişmez ve deterministik olmalı.
- Kapsam R2.1 ile sınırlı. LM (R2.2), orkestratör/CLI komutu (R2.4), matcher/lattice (Faz 3) yok.

Bitirdiğinde: ölçülen held-out kapsama, hedef kapsaması, eksik hedefler listesi, şüpheli girdi
sayısı, kaynak kırılımı (Book/Frequency/Morphology), kurulum süresi, eklenen testler, kabul
kriterinin durumu.
```

### R2.2
```
EpubFixer projesinde docs/phase-2-plan.md'deki R2.2 kalemini uygulayacaksın.
R2.1 bitmiş olmalı; bitmediyse başlamadan bildir.

Önce şunları oku:
- docs/phase-2-plan.md — bölüm 2, bölüm 4.5, bölüm 7 (R2.2), bölüm 9 (kararlar D17, D22)
- docs/ocr-correction-roadmap.md — R2.2 maddesi ve B2 tespiti ("bağlam neden decoder'da")
- R2.1 çıktısı: src/EpubFixer.Core/Lexicon/BookVocabulary.cs, CleanTokenSequence.cs

Kurallar:
- Test-first.
- LM sayımları CleanTokenSequence üzerinden yapılır — vocabulary ile AYNI token akışı.
  İkinci bir tokenizasyon yazma.
- Bozuk region'ın iki yakası ve paragraf sınırı bigram KÖPRÜSÜ KURMAZ; bunu testle kilitle.
- Haznede olmayan token bigram sayımına girmez.
- Stupid backoff normalize olasılık değil SKOR döndürür (D22); bunu isimlendirmede ve XML
  yorumunda açıkça yaz. Perplexity raporlama; held-out bigram isabet oranı kullan.
- Gerçek kitap testleri: P(koltukta | berjer) hem bağlamsızdan hem de ilgisiz bağlamdan büyük
  olmalı. Bu kalıbın kitapta düzinelerce kez geçtiği doğrulandı (bölüm 4.5).
- Core'a I/O girmez. Model değişmez ve deterministik olmalı.
- Kapsam R2.2 ile sınırlı. Trigram, interpolasyon, lambda kalibrasyonu (R5.4), lattice'e bağlama
  (R3.3), BookContextIndex'i silme (R4.3) yok.

Bitirdiğinde: unigram/bigram tür sayıları, held-out bigram isabet oranı, kurulum süresi,
eklenen testler, kabul kriterinin durumu.
```

### R2.4
```
EpubFixer projesinde docs/phase-2-plan.md'deki R2.4 kalemini uygulayacaksın.
R2.1 ve R2.2 bitmiş olmalı; bitmediyse başlamadan bildir.

Önce şunları oku:
- docs/phase-2-plan.md — bölüm 2, bölüm 8 (R2.4), bölüm 9 (D23, D24)
- docs/phase-1-plan.md — bölüm 9 karar D5 (builder monotondur, aşama başına batch)
- src/EpubFixer.Cli/Program.cs (komut wiring deseni), src/EpubFixer.Cli/Quality/MeasureCommand.cs
- docs/baselines/README.md ve mevcut baseline dosyaları

Kurallar:
- Test-first.
- Oracle prefill'i aşama aşama yapılır: önce region detector'ın sorguları, sonra hazne sorguları.
  RecordingMorphologyOracle ile bilinmeyen sorgu sayısının 0 olduğunu testle kanıtla.
- Ölçülen sayılar GERÇEK KOŞUDAN gelir, tahminden değil.
- Baseline dosyaları deterministik olmalı (Ordinal sıralı listeler, sabit sayı formatı).
- Bilgi tabanı kurulumu ≤ 3 sn; aşarsa DUR ve bildir (D24). Disk cache EKLEME.
- `measure` çıktısı docs/baselines/odun-kesmek.book-health.json ile birebir aynı kalmalı (D21).
- Kapsam R2.4 ile sınırlı. Faz 3'ün matcher/lattice tiplerini yazma; üretim hattına bağlama yok.

Bitirdiğinde: baseline'a yazılan sayılar, kurulum süresi, ikinci koşuda flookup'ın başlayıp
başlamadığı, eklenen testler, kabul kriterinin durumu, Faz 3 için gözlemler.
```

---

## 12. Faz 2'nin Faz 3 ile sözleşmesi

- `BookVocabulary.Words` R3.1'in `ILexiconMatcher` implementasyonunun (SymSpell veya trie)
  **girdisidir**. Matcher hazneyi build aşamasında alır; `Match` sırasında morfolojiye veya
  I/O'ya hiç gitmez.
- `VocabularyEntry.PreferredSurface` R3.2'nin arc'ına yazılacak metni belirler; EPUB'a giden
  string buradan gelir, normalize anahtardan değil.
- `BookVocabulary.UnigramLogProbability`'nin OOV cezası (`UnknownLogProbability`) R3.4'ün
  "orijinal token zaten geçerli mi" kuralındaki eşiğin tabanıdır.
- `ILanguageModel.LogProbability` R3.3'ün Viterbi skorundaki `lambda · -LogProbability(...)`
  terimidir; `lambda` R3.3'te `LatticeOptions`'a girer, R5.4'te kalibre edilir.
- `OcrEditCostModel` (artık Core'da) R3.2'nin arc maliyet fonksiyonudur; split/join'in tek
  uzayda çözülmesini sağlayan boşluk silme/ekleme maliyetleri oradan gelir.
- `BookKnowledgeBuilder` Faz 3'ün ve Faz 4'ün tek giriş noktasıdır: R4.2'de `EpubFixService`
  bilgi tabanını kitap başına **bir kez** kurar, region başına değil.
- Faz 2 hiçbir kalite sayısını değiştirmediği için R1.0'ın golden logical text SHA-256'sı
  **korunur**; ilk kasıtlı değişim R4.2'de olacaktır.
