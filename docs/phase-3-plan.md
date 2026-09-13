# Faz 3 — Yeni düzeltme motoru (Lattice + Viterbi + kabul kapısı) — Uygulama planı

> Bu belge [ocr-correction-roadmap.md](ocr-correction-roadmap.md) Faz 3'ün (R3.1–R3.5) uygulama
> planıdır. Her kalem ayrı bir oturumda ayrı bir agent'a devredilebilir; bölüm 14'te kopyala-yapıştır
> devir promptları vardır. Önceki fazların planları: [phase-0-plan.md](phase-0-plan.md),
> [phase-1-plan.md](phase-1-plan.md), [phase-2-plan.md](phase-2-plan.md).
>
> Faz 3'ün tek işi vardır: **bozuk bir bölgeyi, Faz 2'nin bilgi tabanını sorgulayarak, sınırlı bir
> pencere üzerinde tam (yaklaşımsız) bir en kısa yol aramasıyla çözmek ve sonucu uygulamaya değer
> bulup bulmadığına karar vermek.** Faz 3 **hiçbir EPUB'ı değiştirmez**: üretim hattına bağlama
> Faz 4'ün işidir. Fazın sonunda `fix` komutunun çıktısı **birebir aynıdır**; yeni motor yalnızca
> `debug-ocr-reconstruction` ve `debug-lattice` üzerinden ölçülür.
>
> Mevcut `NoisyChannelRegionReconstructor`, `SymSpellRegionReconstructor`, `CurrentRegionReconstructor`
> ve `DeterministicOcrCandidateReranker` bu fazda **okunur, değiştirilmez**. A/B karşılaştırması için
> yaşarlar; silinmeleri R4.3'tür.

---

## 1. Faz 3 bittiğinde elde ne olacak

| Çıktı | Dosya | Ne işe yarar |
|---|---|---|
| Karışım kümesi | `EpubFixer.Core/Ocr/OcrConfusionSet.cs` | `ı/i/l/1` bilgisi tek yerde, tipli ve test edilmiş |
| Ağırlıklı hizalayıcı | `EpubFixer.Core/Ocr/WeightedEditAligner.cs` | "Bu span'den bu kelimeye maliyet kaç, kaçı olağan ikame?" |
| Sözlük sorgulayıcı | `EpubFixer.Core/Ocr/Lattice/ILexiconMatcher.cs` + `EpubFixer.Cli/Ocr/Lattice/SymSpellLexiconMatcher.cs` | Aday **üretmeyi** bırakıp **sorgulamaya** geçiş |
| Kafes kurucu | `EpubFixer.Core/Ocr/Lattice/WordLatticeBuilder.cs` | Split + join + karakter bozulması tek uzayda |
| Çözücü | `EpubFixer.Core/Ocr/Lattice/LatticeDecoder.cs` | DAG üzerinde tam Viterbi + k-best |
| Kabul kapısı | `EpubFixer.Core/Ocr/Lattice/CorrectionAcceptanceGate.cs` | Precision'ın tek savunma hattı |
| Motor + A/B | `EpubFixer.Core/Ocr/Lattice/LatticeRegionReconstructor.cs`, `OcrReconstructionComparison`'ın 4. sütunu | Eski motorla yan yana ölçüm |
| Baseline | `docs/baselines/odun-kesmek.lattice.json` | Faz 4 öncesi kilitlenen sayılar |

**Faz 3'ün kabulü:**

1. `dotnet test EpubFixer.slnx` yeşil — **hiçbir mevcut assert değeri değişmeden**
   (özellikle `OcrCorrectionV11RealRegressionTests`, `EpubFixServiceTests`, `NoisyChannelV2Tests`,
   `MorphologyCallTraceTests` golden SHA-256'sı, `OcrEditCostModelTests`'in mevcut satırları).
2. `odun-kesmek-region-01` fixture'ında Lattice motoru **Top-1 = 10/10**
   (bugünkü NoisyChannel: Top-1 9/10, Top-5 10/10).
3. Full kitap (459 region) için kafes kurma + çözme + kapı toplam süresi **≤ 30 sn**
   (120 sn'lik hard gate'in içinde Faz 4'e yer bırakacak şekilde; D38).
4. `docs/baselines/odun-kesmek.lattice.json` commit edilmiştir ve karşılaştırma raporu dört motoru
   (Current / SymSpell / NoisyChannel / Lattice) aynı tabloda gösterir.
5. Kapı, ground truth'un 9 `protectedOccurrences` kaydının hiçbirinde `Apply` vermez
   (`ProtectedViolated == 0`).
6. `fix` ve `measure` çıktıları **değişmemiştir** — yeni motor üretim hattına bağlı değildir.

---

## 1b. Faz 3'ün kapanış durumu (2026-09-13, `2ec7252`)

Faz 3 uygulandı ve üç onarım turundan geçti (`170494b`, `1682e4e`, `2ec7252`). Kapanıştaki
gerçek durum aşağıdadır. **Faz 4 agent'ı bu tabloyu okumadan başlamasın:** açık kalan maddeler
unutulmuş iş değil, bilinçli olarak Faz 4'e devredilmiş ölçümlerdir.

| # | Kabul kriteri | Durum | Kanıt |
|---|---|---|---|
| 1 | Suite yeşil, mevcut assert değerleri değişmeden | ✅ | 436/436, ~1 dk 34 sn |
| 2 | Fixture Top-1 10/10 | ⚠️ **8/10** | `Decode_FixtureTopOneWithFixtureLanguageModelIsPinned` (gerçek `tr_50k` haznesi + fixture LM, λ=0,5) |
| 3 | Full kitap ≤ 30 sn | ✅ **28,3 sn** | `odun-kesmek.lattice.json`; **testle korunmuyor** (bkz. bölüm 15 ön koşulları) |
| 4 | Baseline commit'li + dört motorlu karşılaştırma | ✅ | `odun-kesmek.lattice.json`, `.md`, `OcrReconstructionComparison` 4. sütun |
| 5 | `ProtectedViolated == 0` | ❌ **ölçülmedi** | Alan baseline'dan düşürüldü (D48) |
| 6 | `fix` / `measure` çıktıları değişmedi | ⚠️ doğru ama **testsiz** | `Fix_OutputIsUnchanged` yazılmadı |

**Motorun bugünkü davranışı:** 563 region → 31 `Apply`, 4 `Review`, 528 `Leave`.
31 Apply kararının tamamı elle incelendiğinde doğru (ikisi bağlam gerektiriyor: `ıı<ıda`→`yılda`,
`;entz`→`Gentz`). Gözlemlenen precision hedefi karşılıyor; **recall bilinçli olarak düşük**.

Karar dağılımının gerekçeleri (`reasonHistogram`):
`OriginalTokenIsValid` 418, `TooManyOrdinaryEdits` 42, `NoChange` 40, `Accepted` 31,
`UnsafeLengthChange` 15, `ProperNameRisk` 10, `MarginTooSmall` 4, `SuspiciousReplacement` 2,
`NoPath` 1.

**Dikkat — katma değer henüz dar:** 31 kararın 24'ü tireleme birleştirmesidir, yani üretim
hattının zaten çözdüğü sınıf (B7'deki "kolay sınıf"). Lattice'in bugün gerçekten yeni getirdiği
düzeltmeler yedi tanedir: `:,ohbet`, `koli ukta`, `kü-^:ük`, `bi-^:imde`, `ba-^arısız`, `ıı<ıda`,
`;entz`. Recall'ü asıl kısıtlayan `OriginalTokenIsValid` (418 region, %74) ve bölüm 12'deki
D44–D45 kararlarıdır. Bunların gevşetilmesi **ancak ground-truth ölçümüyle** yapılmalıdır (R4.2),
10 satırlık fixture üzerinden değil.

---

## 2. Ön koşul: Faz 2 commit edilmemiş

Bu plan yazılırken çalışma ağacında Faz 2'nin tamamı (`src/EpubFixer.Core/Lexicon/*`,
`src/EpubFixer.Core/Ocr/OcrEditCostModel.cs`, `src/EpubFixer.Cli/Lexicon/`,
`VocabularyReportCommand`, 10+ test dosyası, iki baseline JSON'u) **commit edilmemiş** durumdaydı.

**İlk R3 agent'ı işe başlamadan önce çalışma ağacı temiz olmalıdır.** Faz 2 commit'lenmemişse agent
durur ve bildirir; kendi kalemini yarım kalmış bir ağacın üstüne yazmaz. Faz 3'ün her kalemi
"önceki commit yeşildi" varsayımıyla çalışır.

---

## 3. Her kalem için geçerli ortak kurallar

Faz 0 [bölüm 2](phase-0-plan.md), Faz 1 [bölüm 2](phase-1-plan.md) ve Faz 2
[bölüm 2](phase-2-plan.md) kuralları aynen geçerlidir. Faz 3'e özgü eklemeler:

### 3.1 Test-first
Kırmızı test → minimum kod → refactor. Testi olmayan üretim kodu yazılmaz. Bu fazın ürünü bir
**arama algoritmasıdır**; testlerin çoğu "şu pencere verildiğinde şu arc / şu path / şu karar çıkar"
biçimindedir. Beklenen maliyeti önce teste yazın; kod, maliyeti teste uydurmak için değil, testin
tarif ettiği davranışı üretmek için yazılır.

### 3.2 Katmanlar
- `EpubFixer.Core/Ocr/Lattice/` politika: arayüzler, kafes, çözücü, kapı, maliyet hizalaması.
  **`System.IO` yok, `Process` yok, üçüncü parti arama kütüphanesi yok** (D27).
- `EpubFixer.Cli/Ocr/Lattice/` adapter: `SymSpellLexiconMatcher` (SymSpell paketi yalnızca Cli'da).
- `EpubFixer.TrMorph` bu fazda **hiç değişmez**; morfoloji bu fazda **hiç çağrılmaz** (D32).
- `CoreLayeringTests` yeşil kalır; izin listesine **yeni dosya eklenmez**.

### 3.3 Eski motorlara dokunulmaz (OCP)
`NoisyChannelRegionReconstructor`, `SymSpellRegionReconstructor`, `CurrentRegionReconstructor`,
`DeterministicOcrCandidateReranker`, `BookContextIndex`, `OcrCorrectionCandidateGenerator`,
`OcrCorrectionDecisionEvaluator` — hiçbiri düzenlenmez. Karışım tablosu gibi bilgi **kopyalanır,
ortaklaştırılmaz** (D26). Bu kod R4.3'te silinecektir; ölmekte olan koda refactor yapılmaz.

### 3.4 İki normalizasyon uzayı (Faz 2 D17 aynen geçerli)
Sorgu/eşleşme uzayı `TurkishWordNormalizer.Normalize` (NFC + tr-TR lower). EPUB'a yazılacak metin
**daima** `VocabularyEntry.PreferredSurface`'ten gelir, normalize anahtardan değil. Büyük harf
onarımı (`Cebimde`) kafes kurucunun işidir (D31), matcher'ın değil.

### 3.5 Determinizm
Aynı pencere → byte-eşit kafes; aynı kafes → aynı path sırası. Her sıralamada açık tie-break:
önce `Cost` (küçükten büyüğe), sonra `Word`/`Text` için `StringComparer.Ordinal`, sonra `From`,
sonra `To`. Sözlük/küme iterasyon sırasına **hiçbir yerde** güvenilmez. Kayan noktalı eşitlik
`Math.Abs(a - b) < 1e-9` ile yapılır.

### 3.6 Sınırlar bir özelliktir, kusur değil
Her arama sınırlıdır: `MaxWindowLength` aşılırsa region **atlanır** (`SkippedTooLong`),
`MaxRegionStates` aşılırsa region atlanır (`BudgetExceeded`). Atlanan region sessizce kaybolmaz,
raporda görünür. Emin olunmayan her durumda karar **"dokunma"**dır.

### 3.7 Komutlar
```bash
dotnet build EpubFixer.slnx
dotnet test EpubFixer.slnx
dotnet test tests/EpubFixer.Tests --filter "FullyQualifiedName~Lattice"
dotnet test tests/EpubFixer.Tests --filter "FullyQualifiedName~Aligner"
dotnet test tests/EpubFixer.Tests --filter "Category=Slow"
dotnet run --project src/EpubFixer.Cli -- debug-ocr-reconstruction tests/Fixtures/OcrRegion/odun-kesmek-region-01.txt --expected tests/Fixtures/OcrRegion/odun-kesmek-region-01.expected.json
dotnet run --project src/EpubFixer.Cli -- debug-lattice "test-data/odun-kesmek/input.epub" --json docs/baselines/odun-kesmek.lattice.json
```

### 3.8 Commit disiplini
Kalem başına ayrı commit; ilk satır `R3.x: <ne yapıldı>`. R3.2 üç alt adıma bölünmüştür
(bölüm 8.6); her alt adım kendi commit'inde ve her commit'te **tüm suite yeşil**.

### 3.9 Kapsam disiplini
Her agent yalnızca kendi kalemini yapar. Eşik kalibrasyonu (R5.4), öğrenilmiş maliyet tablosu
(R5.1), trie matcher (R5.2), mutation geometrisi (R4.1) ve `EpubFixService` entegrasyonu (R4.2)
**bu fazın dışındadır**. Yol boyunca fark edilen sorunlar düzeltilmez, bitiş raporunda "gözlem"
olarak yazılır. Sözleşme değişikliği gerekiyorsa önce gerekçe bildirilir, karar bölüm 12'ye eklenir.

---

## 4. Sıra ve paralellik

```
R3.0 (hizalayıcı + karışım kümesi)
   └─► R3.1 (ILexiconMatcher + SymSpell adapter)
          └─► R3.2 (WordLatticeBuilder)
                 └─► R3.3 (LatticeDecoder)
                        └─► R3.4 (CorrectionAcceptanceGate)
                               └─► R3.5 (LatticeRegionReconstructor + A/B + baseline)
```

Faz 3 **seridir**; paralellik penceresi yoktur. Tek istisna: R3.5'in rapor iskeleti (dördüncü sütun,
JSON şeması) R3.4 sürerken hazırlanabilir, ama commit'i R3.4'ten sonradır.

**R3.0 yol haritasında yoktur, bu planla eklenmiştir** (D26). Gerekçesi: R3.1'in maliyeti, R3.2'nin
arc maliyeti ve R3.4'ün "olağan ikame sayısı ≤ 1" kuralı **aynı hesaptır**. Üç yerde ayrı ayrı
yazılırsa üç farklı maliyet doğar ve kapının saydığı ikame ile arc'ın ödediği maliyet tutmaz.

---

## 5. Mevcut durumun tespiti

### 5.1 Faz 2 neyi hazır teslim ediyor

| Ne | Nerede | Faz 3'te nasıl kullanılır |
|---|---|---|
| 53.276 kelimelik hazne | `BookVocabulary.Words` | R3.1 matcher'ının sözlüğü |
| Yüzey biçimi | `VocabularyEntry.PreferredSurface` | R3.2'nin arc metni |
| OOV cezası | `BookVocabularyOptions.UnknownLogProbability = -18.0` | R3.4 kural 4'ün tabanı |
| Bigram + stupid backoff | `ILanguageModel.LogProbability(word, previous)` | R3.3'ün `lambda · −logP` terimi |
| Maliyet modeli | `EpubFixer.Core/Ocr/OcrEditCostModel.cs` | R3.0'ın maliyet fonksiyonu |
| Region listesi | `BookKnowledge.Regions` | R3.2'nin girdisi |
| Tek giriş noktası | `BookKnowledgeBuilder.Build(stream, oracleBuilder, targets)` | R3.5 ve R4.2'nin kurulumu |

Ölçülmüş değerler (`docs/baselines/odun-kesmek.vocabulary.json`, `.language-model.json`):
hazne 53.276 (6.960 kitap / 42.986 frekans / 3.330 morfoloji), held-out kapsama %90,64,
**hedef kapsaması %100 (eksik hedef yok)**, unigram 10.938 tip, bigram 32.346 tip, held-out bigram
isabet oranı %29,47, bilgi tabanı kurulumu 1,3 sn.

**Hedef kapsamasının %100 olması Faz 3 için kritik bir haberdir:** fixture'daki 10 hedefin hepsi
haznededir. R3.1 bir hedefi bulamazsa sebep "kelime yok" değil, **sorgu planı yetersiz**tir.

### 5.2 `OcrEditCostModel` yol haritasının anlattığından farklı

Yol haritası bölüm 3, boşluk maliyetlerini "silme 0.25, ekleme 0.5" diye anlatıyor. Gerçek değerler:

```
Keep 0   LineBreakDeletion 0.15   GarbageDeletion 0.20   KnownGlyphSubstitution 0.25
HyphenDeletion 0.25   SpaceDeletion 0.30   ShortFragmentMerge 0.40   SpaceInsertion 0.70
OrdinarySubstitution 1.00   LocalPairContraction 1.00
```

**Sayılar `OcrEditCostModel`'den alınır, yol haritası düzyazısından değil** (D28). Modelde genel
karakter silme/ekleme maliyeti yoktur; R3.0 iki alan ekler (bölüm 6.1).

### 5.3 Karışım tablosu bugün bir `switch` ifadesinin içinde

[NoisyChannelRegionReconstructor.cs:114](../src/EpubFixer.Cli/OcrReconstruction/NoisyChannelRegionReconstructor.cs#L114):

```csharp
'1' => "liıI", 'l' => "ıi1b", 'ı' => "ilrüöo", 'i' => "ıh", '0' => "oö", '3' => "e", '^' => "şç", 'c' => "e"
```

`IsKnown` ise hedefe hiç bakmıyor, yalnızca **kaynak** karakterin bu listede olmasına bakıyor — yani
`ı → z` de "bilinen karışım" sayılıp 0.25 ödüyor. R3.0 bunu **çift yönlü ve simetrik** bir kümeye
çevirir: `(ı, ü)` bilinen, `(ı, z)` değil. Eski dosya bu değişiklikten etkilenmez, kendi kopyasıyla
yaşamaya devam eder (D26).

### 5.4 SymSpell yalnızca Cli'nın bağımlılığı

`SymSpell 6.7.3` paket referansı `src/EpubFixer.Cli/EpubFixer.Cli.csproj` içindedir. Core'un tek
üçüncü parti bağımlılığı AngleSharp'tır. Faz 3 bu dengeyi bozmaz (D27).

### 5.5 Fixture ve bugünkü skorlar

`tests/Fixtures/OcrRegion/odun-kesmek-region-01.txt` — 21 region, 10'u etiketli:

| Kaynak | Hedef | | Kaynak | Hedef |
|---|---|---|---|---|
| `ı ıç` | `üç` | | `ı ızellikle` | `özellikle` |
| `kendi-ıni` | `kendimi` | | `ı ılduğu` | `olduğu` |
| `1 ı iç` | `hiç` | | `Ce-lıimde` | `Cebimde` |
| `:,ohbet` | `sohbet` | | `ge-^:cn` | `geçen` |
| `koli ukta` | `koltukta` | | `yü-ıiimeye` | `yürümeye` |

Bugünkü NoisyChannel: **Top-1 9/10, Top-5 10/10**, full benchmark ~145 sn. Lattice'in hedefi
Top-1 10/10 ve **çok daha hızlı** olmaktır.

### 5.6 Bu on hedef SymSpell'e ham haliyle sorulursa bulunmaz

Sözlük mesafe 2 ile kurulur (SymSpell'de sorgu mesafesi sözlük mesafesini aşamaz). Ham span'lerin
düzenleme mesafeleri: `ı ıç → üç` 3, `ge-^:cn → geçen` 4, `yü-ıiimeye → yürümeye` 4.
**Sorgu planı olmadan recall yapısal olarak düşer.** Boşluk/tire/garbage temizliği uygulandığında
10 hedefin 9'u mesafe ≤ 2'ye iner; `yü-ıiimeye → yürümeye` yalnızca tek karakterlik karışım ikamesi
eklendiğinde iner. Bölüm 7.3 bunu çözer; bu, R3.1'in gerçek işidir.

---

## 6. R3.0 — Karışım kümesi ve ağırlıklı hizalayıcı (plana eklendi)

**Boyut:** M. **Bağımlılık:** yok (Faz 2 commit'li olmalı).

### Amaç
Tek bir doğruluk kaynağı: "şu span'den şu kelimeye geçmenin ağırlıklı maliyeti nedir ve bu geçişte
kaç tane *olağan* (karışım kümesi dışı) ikame vardır?" Üç kalem bu cevabı kullanır: R3.1 (aday
filtresi), R3.2 (arc maliyeti), R3.4 (kural 5).

### Dosya haritası
```
src/EpubFixer.Core/Ocr/OcrConfusionSet.cs          (yeni)
src/EpubFixer.Core/Ocr/WeightedEditAligner.cs      (yeni)
src/EpubFixer.Core/Ocr/Models/EditAlignment.cs     (yeni)
src/EpubFixer.Core/Ocr/OcrEditCostModel.cs         (iki alan eklenir)
tests/EpubFixer.Tests/OcrConfusionSetTests.cs      (yeni)
tests/EpubFixer.Tests/WeightedEditAlignerTests.cs  (yeni)
tests/EpubFixer.Tests/OcrEditCostModelTests.cs     (mevcut assert'ler korunur, iki satır eklenir)
```

### 6.1 Sözleşme

```csharp
namespace EpubFixer.Core.Ocr;

public sealed class OcrConfusionSet
{
    public static OcrConfusionSet Default { get; }
    public bool IsKnownConfusion(char source, char target);   // simetrik
    public IReadOnlyList<char> Replacements(char source);      // sıralı, deterministik
    public bool IsGarbageGlyph(char value);                    // ^ ; : < > ,
}

public enum EditOperationKind
{
    Keep, KnownGlyphSubstitution, OrdinarySubstitution,
    SpaceDeletion, SpaceInsertion, HyphenDeletion, LineBreakDeletion, GarbageDeletion,
    OrdinaryDeletion, OrdinaryInsertion
}

public readonly record struct EditOperation(EditOperationKind Kind, int SourceIndex, char Source, char Target);

public sealed record EditAlignment(double Cost, IReadOnlyList<EditOperation> Operations)
{
    public int OrdinarySubstitutions { get; }   // Kind == OrdinarySubstitution sayısı
    public int OrdinaryEdits { get; }           // OrdinarySubstitution + OrdinaryDeletion + OrdinaryInsertion
}

public interface IWeightedEditAligner
{
    bool TryAlign(ReadOnlySpan<char> source, ReadOnlySpan<char> target, double budget, out EditAlignment alignment);
}
```

`OcrEditCostModel`'e eklenen iki alan (mevcut alanlar ve değerleri **değişmez**):

```csharp
double OrdinaryDeletion = 1.00,
double OrdinaryInsertion = 1.00
```

### 6.2 Kesin davranış kuralları

1. **Hizalama yönü:** `source` = metindeki bozuk span, `target` = haznedeki kelime. Maliyet
   asimetriktir: `source`tan silinen boşluk `SpaceDeletion` (0.30), `target`ı üretmek için eklenen
   boşluk `SpaceInsertion` (0.70).
2. **Silme maliyeti karaktere bağlıdır:** `' '`/`'\t'` → `SpaceDeletion`; `'\r'`/`'\n'` →
   `LineBreakDeletion`; `'-'`/`'­'` → `HyphenDeletion`; `IsGarbageGlyph` → `GarbageDeletion`;
   diğer her karakter → `OrdinaryDeletion`.
3. **İkame maliyeti karışıma bağlıdır:** `IsKnownConfusion(s, t)` → `KnownGlyphSubstitution` (0.25),
   aksi halde `OrdinarySubstitution` (1.00). Karakterler eşitse `Keep` (0).
4. **Büyük/küçük harf farkı ikame değildir:** karşılaştırma `TurkishWordNormalizer.Normalize`
   uzayında yapılır; `C` ile `c` `Keep`tir.
5. **Bütçe budaması:** DP satırının minimumu `budget`ı aşarsa hesap orada durur ve `TryAlign`
   `false` döner. Bu yalnızca performans değil, **sözleşmenin parçasıdır**: bütçe üstü hizalama
   üretilmez.
6. **Karışım kümesi simetriktir; `Default` tablosu** (eski `switch` tablosunun simetrik kapanışı):
   `{1, l, ı, i, I}`, `{ı, ü}`, `{ı, ö}`, `{ı, o}`, `{ı, r}`, `{l, b}`, `{i, h}`, `{0, o}`,
   `{0, ö}`, `{3, e}`, `{^, ş}`, `{^, ç}`, `{c, e}`, `{c, ç}`, `{s, ş}`, `{g, ğ}`, `{u, ü}`,
   `{o, ö}`. Üyelik `IsKnownConfusion` için çift yönlüdür.
7. **Determinizm:** eşit maliyetli hizalamalarda `Keep > Substitution > Deletion > Insertion`
   sırasıyla tercih edilir; `Operations` daima `SourceIndex` artan sırada döner.

### 6.3 Yazılacak testler (önce kırmızı)

| Test | İddia |
|---|---|
| `Default_IsSymmetric` | `IsKnownConfusion('ı','ü')` ve `IsKnownConfusion('ü','ı')` ikisi de true |
| `Default_RejectsUnrelatedPair` | `IsKnownConfusion('ı','z')` false — bugünkü `IsKnown`'ın açığı |
| `Align_IdenticalIsZero` | `("üç","üç")` → `Cost == 0`, tüm op'lar `Keep` |
| `Align_SpaceDeletionUsesModelValue` | `("ko li","koli")` → `0.30` |
| `Align_KnownGlyphIsCheaperThanOrdinary` | `("ıç","üç") == 0.25`, `("zç","üç") == 1.00` |
| `Align_CountsOrdinarySubstitutions` | `("zç","üç")` → `OrdinarySubstitutions == 1` |
| `Align_CaseDifferenceIsFree` | `("cebimde","Cebimde")` → `0` |
| `Align_GarbagePrefixIsCheap` | `(":,ohbet","sohbet")` → beklenen değeri testte açıkça sabitle |
| `Align_ReturnsFalseAboveBudget` | `("koltukta","merhaba")`, bütçe 1.0 → `false` |
| `Align_TargetFixtureCostsAreUnderBudget` | 10 fixture çiftinin her biri ≤ 3.0 (bütçe tavanı) |
| `Align_IsDeterministic` | Aynı çift 100 kez → aynı `Operations` dizisi |

`Align_TargetFixtureCostsAreUnderBudget` bu kalemin **en önemli testidir**: R3.2'nin bütçesi bu
maliyetlerin üstünde olmazsa doğru aday kafese hiç giremez. Her çiftin gerçek maliyeti bir tabloda
sabitlenir, böylece R5.1 maliyet modelini değiştirdiğinde etkisi görünür olur.

### 6.4 Kabul kriteri
- Yukarıdaki testler yeşil; `OcrEditCostModelTests`'in mevcut assert'leri **değişmemiş**.
- `CoreLayeringTests` yeşil.
- 10 fixture çiftinin maliyet tablosu bir testte sabitlenmiş.

### 6.5 Kapsam dışı
Maliyet öğrenme (R5.1), transpozisyon operatörü, `LocalPairContraction` ve `ShortFragmentMerge`
kullanımı (bunlar eski motorun operatörleridir; kafeste karşılıkları arc uzunluğudur).

---

## 7. R3.1 — `ILexiconMatcher` + SymSpell adapter

**Boyut:** M. **Bağımlılık:** R3.0.

### Amaç
Aday getirme: *üretim* değil *sorgulama*. B1'in kökü buradadır — 29 harfi her pozisyonda denemek
yerine sözlüğe "bu span'e bu bütçe içinde ne benziyor?" diye sorulur.

### Dosya haritası
```
src/EpubFixer.Core/Ocr/Lattice/ILexiconMatcher.cs        (yeni: arayüz + LexiconMatch)
src/EpubFixer.Core/Ocr/Lattice/LexiconQueryPlan.cs       (yeni: sorgu varyantları — saf politika)
src/EpubFixer.Cli/Ocr/Lattice/SymSpellLexiconMatcher.cs  (yeni: adapter)
tests/EpubFixer.Tests/LexiconQueryPlanTests.cs           (yeni)
tests/EpubFixer.Tests/SymSpellLexiconMatcherTests.cs     (yeni)
```

### 7.1 Sözleşme (yol haritasından aynen)

```csharp
namespace EpubFixer.Core.Ocr.Lattice;

public readonly record struct LexiconMatch(string Word, double Cost);

public interface ILexiconMatcher
{
    IReadOnlyList<LexiconMatch> Match(ReadOnlySpan<char> span, double budget);
}
```

`ReadOnlySpan<char>` bir **ref struct**'tır: iterator (`yield`), `async`, lambda ve LINQ ifadelerinin
içine giremez. Implementasyon span'i **ilk satırda** `string`e çevirir ve sonrasını string ile
yürütür. İmzayı bilerek koruyoruz: çağrı yerinde tahsis yok, adapter içinde tek tahsis var.

### 7.2 `LexiconMatch.Word` nedir
`VocabularyEntry.PreferredSurface` — hazne ne gördüyse o (`Auersberger`, `berjer`). **Büyük harf
onarımı burada yapılmaz** (D31); span büyük harfle başlıyorsa düzeltmeyi R3.2 yapar. `Cost`
R3.0 hizalayıcısından gelir, SymSpell'in tamsayı mesafesinden **değil**.

### 7.3 Sorgu planı — bu kalemin gerçek işi (D35)

`LexiconQueryPlan` saf politikadır, Core'da yaşar ve SymSpell'i tanımaz; yalnızca sorgulanacak
string'leri üretir. **İki katmanlıdır:**

**Katman 1 (daima çalışır, 4 sorgu):**
1. `TurkishWordNormalizer.Normalize(span)`
2. (1) + boşluk/tab/tire/yumuşak tire çıkarılmış
3. (1) + garbage glyph (`^ ; : < > ,`) çıkarılmış
4. (1) + her ikisi de çıkarılmış

**Katman 2 (yalnızca katman 1 bütçe içinde hiç eşleşme döndürmezse):**
5. (4)'ün üzerinde, soldan sağa ilk `MaxConfusionPositions = 4` karışabilir karakter için
   `OcrConfusionSet.Replacements` ile **tek** karakterlik ikame varyantları. Toplam sorgu sayısı
   `MaxQueriesPerSpan = 24`'ü aşamaz; aşarsa deterministik olarak (önce pozisyon, sonra replacement
   sırası) kesilir.

Bu tasarımın nedeni ölçülmüştür (bölüm 5.6): 10 hedefin 9'u katman 1 ile mesafe ≤ 2 içine giriyor,
`yü-ıiimeye → yürümeye` yalnızca katman 2 ile giriyor. Tipik span'in maliyeti **4 SymSpell
sorgusudur**; katman 2 nadirdir.

### 7.4 Adapter kuralları (`SymSpellLexiconMatcher`)

1. Sözlük **kurulumda bir kez** `BookVocabulary.Words` üzerinden kurulur
   (`maxDictionaryEditDistance = 2`, `Verbosity.All`). `Match` sırasında sözlük değişmez.
2. Kelime frekansı olarak `VocabularyEntry.BookCount + 1` kullanılır; SymSpell **sıralaması
   kullanılmaz**, yalnızca aday havuzu olarak kullanılır.
3. Her sonucun kelimesi **orijinal span**'e karşı `TryAlign(span, surface, budget)` ile yeniden
   puanlanır; `false` dönenler atılır.
4. Sonuç: kelime bazında `Distinct` (`StringComparer.Ordinal`),
   `OrderBy(Cost).ThenBy(Word, Ordinal)`, `Take(MaxMatchesPerSpan = 16)`.
5. **Cache:** `Match` sonuçları `(normalize edilmiş span, bütçe)` anahtarıyla bellek içi sözlükte
   tutulur. Kafes kurucu aynı span'i defalarca sorar; cache olmadan bütçe testi kırmızıya döner.
6. `Match` **asla** morfolojiye, diske veya ağa gitmez.

### 7.5 Yazılacak testler (önce kırmızı)

| Test | İddia |
|---|---|
| `QueryPlan_StripsSpacesAndGarbage` | `":,ohbet"` planı `"ohbet"` içerir |
| `QueryPlan_TierTwoOnlyOnEmptyTierOne` | Katman 1 eşleşiyorsa plan 4 sorguda biter |
| `QueryPlan_IsBounded` | 48 karakterlik en kötü span → sorgu sayısı ≤ 24 |
| `QueryPlan_IsDeterministic` | Aynı span → aynı sıralı sorgu listesi |
| `Match_FindsFixtureTargets` | **10 fixture span'inin her biri için hedef kelime sonuçta var** |
| `Match_RespectsBudget` | Bütçe 0.5 → `koli ukta` için `koltukta` yok (maliyeti 1.25) |
| `Match_OrdersByWeightedCost` | Sıralama ağırlıklı maliyete göre, SymSpell mesafesine göre değil |
| `Match_ReturnsPreferredSurface` | Özel isim span'i → `Auersberger` (normalize anahtar değil) |
| `Match_IsCached` | Aynı span iki kez → sayaçlı sahte ile SymSpell çağrısı 1 |
| `Match_DoesNotCallMorphology` | Atarsa patlayan sahte oracle ile kurulum → `Match` sorunsuz |

`Match_FindsFixtureTargets` bu kalemin **kabul testidir** ve yol haritasının R3.1 kabul kriterinin
tam karşılığıdır (`koltukta`, `üç`, `hiç`, `sohbet`, `geçen`, `yürümeye` ve diğer dördü).

### 7.6 Kabul kriteri
- 10/10 fixture hedefi ilgili span için dönen listede var.
- `EpubFixer.Core` içinde SymSpell'e referans yok; `CoreLayeringTests` yeşil.
- Gerçek hazne (53k) ile matcher kurulumu ≤ 2 sn (`Category=Slow` testiyle ölç).

### 7.7 Kapsam dışı
Trie matcher (R5.2), kafes, bağlam, kabul kararı. Matcher **tek span** görür; pencere kavramı yok.

---

## 8. R3.2 — `WordLatticeBuilder`

**Boyut:** L — bu fazın kalbi. **Bağımlılık:** R3.1.

### Amaç
Split, join ve karakter bozulmasını tek uzayda toplamak. Boşluk özel bir operatör değil, maliyeti
olan sıradan bir semboldür.

### Dosya haritası
```
src/EpubFixer.Core/Ocr/Lattice/IWordLatticeBuilder.cs   (yeni)
src/EpubFixer.Core/Ocr/Lattice/WordLatticeBuilder.cs    (yeni)
src/EpubFixer.Core/Ocr/Lattice/LatticeOptions.cs        (yeni)
src/EpubFixer.Core/Ocr/Lattice/LatticeWindow.cs         (yeni: pencere seçimi)
src/EpubFixer.Core/Ocr/Lattice/Models/LatticeArc.cs     (yeni)
src/EpubFixer.Core/Ocr/Lattice/Models/WordLattice.cs    (yeni)
tests/EpubFixer.Tests/LatticeWindowTests.cs             (yeni)
tests/EpubFixer.Tests/WordLatticeBuilderTests.cs        (yeni)
```

### 8.1 Sözleşme

```csharp
public enum LatticeArcKind { Word, Identity, Literal }

public sealed record LatticeArc(int From, int To, string Word, double Cost, LatticeArcKind Kind);

public enum LatticeBuildOutcome { Built, SkippedTooLong, BudgetExceeded }

public sealed record WordLattice(
    string Window,
    int WindowOffset,
    IReadOnlyList<LatticeArc> Arcs,
    LatticeBuildOutcome Outcome,
    int VisitedStates);

public interface IWordLatticeBuilder
{
    WordLattice Build(CorruptedTextRegion region, string fullText, LatticeOptions options);
}

public sealed record LatticeOptions(
    int MaxWindowLength = 48,
    int MaxArcLength = 20,
    int ContextTokens = 1,
    double BudgetBase = 1.0,
    double BudgetPerFourChars = 1.0,
    double BudgetCap = 3.0,
    int MaxMatchesPerSpan = 16,
    int MaxQueriesPerSpan = 24,
    int MaxRegionStates = 200_000,
    double Lambda = 0.5,
    double MaxPathCost = 2.5,
    double MinMargin = 0.75,
    int MaxOrdinarySubstitutions = 1);
```

Yol haritasının altı alanı **aynen korunur**; yedi alan eklenir (D30). `Lambda`, `MaxPathCost`,
`MinMargin`, `MaxOrdinarySubstitutions` R3.3 ve R3.4'ün eşikleridir ve **tek bir options nesnesinde**
toplanır ki R5.4 tek yerden süpürsün. `LatticeArc.Kind` yol haritasının dört alanlı record'una
eklenmiştir (D29): çözücünün ve kapının "bu arc bir öneri mi, orijinalin kendisi mi, yoksa aradaki
noktalama mı?" ayrımını yapması gerekir.

### 8.2 Pencere (`LatticeWindow`)

1. Pencere = region + iki yanından `ContextTokens` adet token.
2. Pencere **hard boundary geçmez**: `LogicalTextStream.Boundaries` içindeki `TextNode` dışı her
   sınır pencereyi keser (paragraf/blok sınırı). Ham metinle çalışan yollarda (fixture `.txt`)
   boş satır aynı rolü üstlenir.
3. Pencere uzunluğu `MaxWindowLength`'i aşarsa **region atlanır**: `Outcome = SkippedTooLong`,
   `Arcs` boş. Kısaltarak zorlanmaz.
4. `WindowOffset` = pencerenin `fullText` içindeki başlangıç indeksi; kafesteki her indeks pencereye
   görelidir ve mutlak konum `WindowOffset + i`'dir. **Bu çevrim R4.1'in geometrisinin temelidir;
   testle kilitlenir.**

### 8.3 Düğümler ve arc üretimi

Düğümler pencere içindeki karakter indeksleridir (`0..Window.Length`). Arc üretimi bir
**erişilebilirlik iş listesiyle** yapılır; böylece 48×20 = 960 span'in tamamı sorgulanmaz:

1. Başlangıç düğüm kümesi `S = {0} ∪ {i : Window[i-1] ayırıcı}` (ayırıcı = boşluk, tab, satır sonu,
   tire, yumuşak tire).
2. İş listesinden bir düğüm `i` alınır; `j ∈ (i, min(i + MaxArcLength, N)]` için `Window[i..j]`:
   - boşlukla başlıyor veya bitiyorsa **atlanır**,
   - hiç harf/rakam içermiyorsa **atlanır**,
   - aksi halde `matcher.Match(span, Budget(span))` çağrılır; dönen her eşleşme için
     `LatticeArc(i, j, match.Word, match.Cost, Word)` üretilir ve `j` iş listesine eklenir.
3. `Budget(span) = Math.Min(BudgetCap, BudgetBase + BudgetPerFourChars * (span.Length / 4))`
   (tamsayı bölme). Her `Match` çağrısı ve üretilen her arc `SearchBudget.Visit()` sayılır;
   `MaxRegionStates` aşılırsa `Outcome = BudgetExceeded` ve region atlanır.
4. **Identity arc'ları (D29):** pencerenin her orijinal token'ı için
   `(tokenStart, tokenEnd, orijinal metin, 0, Identity)` arc'ı daima üretilir. Bu, "dokunma" yolunun
   kafeste **maliyetsiz** var olmasını ve her pencerede `0 → N` arasında en az bir tam yol
   bulunmasını garanti eder.
5. **Literal arc'ları:** token dışı her karakter dizisi (boşluk, noktalama) için
   `(start, end, orijinal metin, 0, Literal)` arc'ı üretilir; çıktı yeniden kurulurken noktalama ve
   boşluk aynen korunur.
6. Bir `Word` arc'ı birden fazla iç boşluk kapsıyorsa **join**, bir token'ın ortasında bitiyorsa
   **split** olur. İkisi için de özel kod yoktur; ikisi de (2)'nin doğal sonucudur.

### 8.4 Büyük harf onarımı (D31)
Bir `Word` arc'ının metni şu kurala göre yazılır: span'in ilk harfi büyükse
`char.ToUpper(word[0], tr-TR) + word[1..]`; span tamamen büyükse (≥ 2 harf ve hepsi büyük) kelime de
tamamen büyük harfe çevrilir; aksi halde `PreferredSurface` aynen. Başka hiçbir harf dönüşümü
yapılmaz.

### 8.5 Yazılacak testler (önce kırmızı)

| Test | İddia |
|---|---|
| `Window_IncludesOneContextTokenEachSide` | `berjer koli ukta oturdu` → pencere `berjer`den başlar |
| `Window_StopsAtHardBoundary` | Paragraf sınırı geçilmez |
| `Window_TooLongIsSkipped` | 60 karakterlik region → `SkippedTooLong`, arc yok |
| `Window_OffsetMapsBackToFullText` | `fullText.Substring(WindowOffset, Window.Length) == Window` |
| `Build_JoinArcSpansInnerSpace` | `koli ukta` için `(0,9,"koltukta")` arc'ı var, maliyeti 1.25 |
| `Build_SplitArcEndsMidToken` | `bugünçok` → `(0,5,"bugün")` ve `(5,8,"çok")` arc'ları var |
| `Build_IdentityArcAlwaysExists` | Temiz pencere → her token için maliyeti 0 identity arc'ı |
| `Build_LiteralArcsPreservePunctuation` | `koltukta,` → `,` için literal arc |
| `Build_RestoresLeadingCapital` | `Ce-lıimde` → arc metni `Cebimde` |
| `Build_ArcCountStaysUnderCeiling` | Fixture'ın en kötü penceresi → arc sayısı ≤ **regresyon sabiti** |
| `Build_BudgetExceededIsReported` | `MaxRegionStates = 10` → `BudgetExceeded`, arc yok |
| `Build_AllTenTargetsHaveAnArc` | **10 hedefin her biri için ilgili arc kafeste var** |
| `Build_IsDeterministic` | Aynı region 50 kez → aynı arc dizisi (sıra dahil) |

`Build_AllTenTargetsHaveAnArc` ve `Build_ArcCountStaysUnderCeiling` yol haritasının R3.2 kabul
kriterinin iki yarısıdır; ikisi birlikte yeşil olmadan kalem bitmiş sayılmaz.

### 8.6 Alt adımlar (ayrı commit'ler — D39)
- **A:** `LatticeWindow` + pencere testleri (`SkippedTooLong` dahil). Kafes yok.
- **B:** Arc üretimi: identity + literal + word arc'ları, bütçe sayacı.
- **C:** Büyük harf onarımı + tavan ve determinizm regresyon assert'leri.

### 8.7 Kapsam dışı
Skorlama, LM, en iyi yolun seçimi, kabul kararı. Kafes **sıralanmamış bir olasılıklar kümesidir**;
hangi yolun kazandığı R3.3'ün işidir.

---

## 9. R3.3 — `LatticeDecoder`

**Boyut:** M. **Bağımlılık:** R3.2, R2.2.

### Amaç
DAG üzerinde **tam** en kısa yol. Beam yok, yaklaşım yok, lookahead yok — B1'in tekrar doğmaması
için bu üç kelime sözleşmedir.

### Dosya haritası
```
src/EpubFixer.Core/Ocr/Lattice/ILatticeDecoder.cs     (yeni)
src/EpubFixer.Core/Ocr/Lattice/LatticeDecoder.cs      (yeni)
src/EpubFixer.Core/Ocr/Lattice/Models/DecodedPath.cs  (yeni)
tests/EpubFixer.Tests/LatticeDecoderTests.cs          (yeni)
```

### 9.1 Sözleşme

```csharp
public sealed record DecodedPath(string Text, double Cost, IReadOnlyList<LatticeArc> Arcs);

public interface ILatticeDecoder
{
    IReadOnlyList<DecodedPath> Decode(WordLattice lattice, int kBest = 3);
}
```

`LatticeDecoder(ILanguageModel languageModel, LatticeOptions options, string? previousWord = null)` —
`previousWord` pencereden önceki son temiz token'dır (bigram bağlamının başlangıcı); yoksa `"<s>"`.

### 9.2 Skor

```
best[j] = min over arcs i→j of  best[i] + arc.Cost + lambda * (-LM.LogProbability(arc.Word, previousWordOfPathTo(i)))
```

Kesin kurallar:
1. Düğümler artan indeks sırasında işlenir; kafes tanımı gereği DAG'dır (`From < To`).
2. `Literal` arc'ları **LM terimi almaz** (noktalama kelime değildir), maliyeti 0'dır ve bağlam
   kelimesini değiştirmezler.
3. `Identity` ve `Word` arc'ları LM terimi alır. Bağlam kelimesi yoldaki bir önceki
   `Word`/`Identity` arc'ının metnidir; yoksa constructor'daki `previousWord`.
4. `lambda` `LatticeOptions.Lambda`'dan gelir (başlangıç 0.5, R5.4'te kalibre edilir).
   `lambda = 0` verildiğinde çözücü saf düzenleme maliyetine iner — bu bir testtir.
5. **k-best:** her düğümde en iyi `kBest` kısmi yol tutulur (list Viterbi). Çıktı `Text` bazında
   tekilleştirilir; `kBest` **farklı metin** döndürmeye çalışır, farklı arc dizilimi yeterli
   değildir. R3.4'ün margin hesabının anlamlı olması buna bağlıdır.
6. Tie-break: `(Cost, Text ordinal)`.
7. `Outcome != Built` olan kafes için `Decode` **boş liste** döner; istisna atmaz.
8. Çözücü hiçbir şeyi değiştirmez: `DecodedPath.Text`, arc metinlerinin sırayla birleştirilmesidir
   ve identity+literal yolunda pencerenin **birebir** aynısını üretir. Bu bir testtir.

### 9.3 Yazılacak testler (önce kırmızı)

| Test | İddia |
|---|---|
| `Decode_IdentityPathReproducesWindowExactly` | Temiz pencere → `Text == Window`, `Cost == 0` |
| `Decode_PrefersCheaperEditPath` | `koli ukta` → Top-1 `koltukta` |
| `Decode_ContextChangesWinner` | `berjer` bağlamında `koltukta`, nötr bağlamda başka aday kazanır |
| `Decode_LambdaZeroIsPureEditCost` | `lambda = 0` → sıralama yalnızca maliyete göre |
| `Decode_KBestReturnsDistinctTexts` | `kBest = 3` → 3 farklı metin |
| `Decode_ReturnsEmptyForSkippedLattice` | `SkippedTooLong` → boş liste |
| `Decode_IsExactNotBeam` | Elle kurulmuş kafeste "ucuz başlayıp pahalı biten" yolun kaybettiği, **globalde en iyi** yolun kazandığı örnek |
| `Decode_FixtureTopOneIsTenOfTen` | **10/10 Top-1** (bu kalemin kabul testi) |
| `Decode_IsDeterministic` | Aynı kafes 50 kez → aynı sıralı sonuç |

`Decode_IsExactNotBeam` yazılmadan bu kalem bitmez: beam'e dönüş bu projeyi bir kez zaten
durdurdu (B1).

### 9.4 Kabul kriteri
- Fixture'da Top-1 10/10.
- k-best farklı metinler döndürüyor (R3.4'ün girdisi hazır).
- Pencere başına çözüm süresi ortalaması testte ölçülüp üst sınırla kilitlenmiş.

### 9.5 Kapsam dışı
Eşik, margin, "dokunma" kuralı — hepsi R3.4. Çözücü **karar vermez**, sıralar.

---

## 10. R3.4 — `CorrectionAcceptanceGate`

**Boyut:** M. **Bağımlılık:** R3.3. **Yol haritasının en önemli tek kalemi.**

### Amaç
Precision ≥ %98. Kapı, motorun "bilmiyorum" diyebildiği tek yerdir; yanlış düzeltme,
düzeltilmemiş hatadan pahalıdır.

### Dosya haritası
```
src/EpubFixer.Core/Ocr/Lattice/ICorrectionAcceptanceGate.cs  (yeni)
src/EpubFixer.Core/Ocr/Lattice/CorrectionAcceptanceGate.cs   (yeni)
src/EpubFixer.Core/Ocr/Lattice/Models/AcceptanceResult.cs    (yeni)
tests/EpubFixer.Tests/CorrectionAcceptanceGateTests.cs       (yeni)
tests/EpubFixer.Tests/ProtectedOccurrenceGateTests.cs        (yeni)
```

### 10.1 Sözleşme

```csharp
public enum AcceptanceVerdict { Apply, Review, Leave }

public sealed record AcceptanceResult(
    AcceptanceVerdict Verdict,
    string? Replacement,
    int LogicalStart,           // mutlak, fullText'e göre
    int LogicalEndExclusive,    // mutlak, fullText'e göre
    IReadOnlyList<string> Reasons);

public interface ICorrectionAcceptanceGate
{
    AcceptanceResult Evaluate(CorruptedTextRegion region, WordLattice lattice, IReadOnlyList<DecodedPath> paths);
}
```

Yol haritasının imzasına iki alan ve bir parametre eklendi (D33): **span olmadan R4.1 mutation
geometrisi kuramaz** ve kapının değişen aralığı hesaplaması için kafesin penceresi gerekir.
`Replacement` yalnızca `Verdict == Apply` iken doludur.

`CorrectionAcceptanceGate(BookVocabulary vocabulary, LatticeOptions options)` — başka bağımlılık yok.

### 10.2 Kurallar (hepsi sağlanmalı; biri düşerse `Apply` yok)

| # | Kural | Sağlanmazsa |
|---|---|---|
| 0 | Kafes `Built` ve en az bir path var | `Leave` — `NoPath` |
| 1 | En iyi path identity path'ten **farklı** | `Leave` — `NoChange` |
| 2 | En iyi path'in maliyeti `MaxPathCost`'un altında | `Leave` — `CostAboveThreshold` |
| 3 | İkinci en iyi **farklı metinli** path ile fark ≥ `MinMargin` | `Review` — `MarginTooSmall` |
| 4 | Değiştirilen her orijinal token zaten geçerli **değil** | `Leave` — `OriginalTokenIsValid` |
| 5 | Path'teki `OrdinarySubstitutions` toplamı ≤ `MaxOrdinarySubstitutions` | `Leave` — `TooManyOrdinaryEdits` |

**Kural 4'ün tanımı (D32):** bir orijinal token "zaten geçerli"dir eğer `vocabulary.Find(token)`
null değilse **ve** (`entry.BookCount >= 2` **veya**
`entry.Source is VocabularySource.Frequency or VocabularySource.Morphology`). Morfoloji oracle'ı
**çağrılmaz**: hazne, morfolojik olarak geçerli formları zaten `Source = Morphology` ile içeriyor
(Faz 2). Bu, kapıyı I/O'dan ve prefill eksikliği riskinden tamamen ayırır.

Bu kural `Auersberger`, `Simmeringer`, `Rennweg` gibi özel isimleri korur (yol haritası risk #4) ve
`protectedOccurrences`'ın motor tarafındaki karşılığıdır.

### 10.3 Değişen aralık (`LogicalStart` / `LogicalEndExclusive`)
En iyi path ile identity path karakter karakter karşılaştırılır; **ilk farklı** ve **son farklı**
pozisyonlar bulunur, token sınırlarına genişletilir, `WindowOffset` eklenerek mutlak aralığa
çevrilir. `Replacement` yalnızca bu aralığın yeni metnidir. Dokunulmayan bağlam token'ı çıktıya
**yazılmaz** — R4.1'in overlap tespiti için bu daralma zorunludur.

### 10.4 Başlangıç eşikleri (R5.4'te kalibre edilecek — D34)
`MaxPathCost = 2.5`, `MinMargin = 0.75`, `MaxOrdinarySubstitutions = 1`, `Lambda = 0.5`.
Bunlar **ölçülmüş değil seçilmiş** sayılardır; testlerde açıkça belirtilir ve
`docs/baselines/odun-kesmek.lattice.json`'a yazılır ki R5.4 nereden başladığını bilsin.
Bir testi yeşile döndürmek için eşik oynatılmaz.

### 10.5 Yazılacak testler (önce kırmızı)

| Test | İddia |
|---|---|
| `Gate_AppliesConfidentCorrection` | `koli ukta` → `Apply`, `koltukta`, doğru aralık |
| `Gate_LeavesCleanToken` | Temiz pencere → `Leave`, `NoChange` |
| `Gate_NeverTouchesValidOriginal` | `Auersberger` içeren pencere → `Leave`, `OriginalTokenIsValid` |
| `Gate_ReviewsOnSmallMargin` | İki path 0.1 farkla → `Review` |
| `Gate_RejectsAboveCostThreshold` | Maliyet 3.0 > 2.5 → `Leave` |
| `Gate_RejectsTwoOrdinarySubstitutions` | 2 olağan ikame → `Leave` |
| `Gate_SpanCoversOnlyChangedText` | Değişmemiş bağlam token'ı aralığın dışında |
| `Gate_SpanIsAbsolute` | `fullText.Substring(start, end - start)` orijinal bozuk metni verir |
| `Gate_ReasonsAreAlwaysPopulated` | Her verdict için en az bir gerekçe |
| `Gate_DoesNotCallMorphology` | Atarsa patlayan sahte oracle ile → sorunsuz |
| `ProtectedOccurrences_AreNeverApplied` | **9 `protectedOccurrences` kaydının hiçbirinde `Apply` yok** |

`ProtectedOccurrences_AreNeverApplied` bu kalemin kabul testidir ve yol haritasının
`ProtectedViolated == 0` ölçütünün Faz 3'teki karşılığıdır.

### 10.6 Kabul kriteri
- Yukarıdaki testler yeşil; özellikle protected testi.
- Fixture'ın 10 hedefinde kaç `Apply` / `Review` / `Leave` çıktığı bir testte sabitlenmiş. Bu sayı
  R5.4'ün eğrisinin başlangıç noktasıdır; **10/10 Apply beklenmiyor**, beklenen şey sayının bilinir
  olmasıdır.

### 10.7 Kapsam dışı
Eşik süpürmesi (R5.4), mutation üretimi (R4.1), `Review` kararlarının diff raporu (R5.3).

---

## 11. R3.5 — `LatticeRegionReconstructor` + A/B + baseline

**Boyut:** M. **Bağımlılık:** R3.4.

### Amaç
Yeni motoru mevcut port arkasına koymak, eskiyle yan yana ölçmek ve Faz 4 öncesi sayıları
kilitlemek. R1.0 ve R2.4 ile aynı gerekçe: **ölçülmeyen ve koşturulmayan kod çürür.**

### Dosya haritası
```
src/EpubFixer.Core/Ocr/Lattice/LatticeRegionReconstructor.cs        (yeni)
src/EpubFixer.Core/Ocr/Models/ReconstructionSource.cs               (enum'a Lattice eklenir)
src/EpubFixer.Cli/Ocr/Lattice/LatticeReportCommand.cs               (yeni: debug-lattice)
src/EpubFixer.Cli/OcrReconstruction/OcrReconstructionComparison.cs  (dördüncü sütun)
src/EpubFixer.Cli/Program.cs                                        (komut kaydı + usage)
docs/baselines/odun-kesmek.lattice.json                             (yeni baseline)
docs/baselines/README.md                                            (satır eklenir)
tests/EpubFixer.Tests/LatticeRegionReconstructorTests.cs            (yeni)
tests/EpubFixer.Tests/LatticeFixtureRegressionTests.cs              (yeni)
tests/EpubFixer.Tests/LatticePerformanceBudgetTests.cs              (yeni, Category=Slow)
```

### 11.1 Sözleşme

```csharp
public sealed class LatticeRegionReconstructor : IOcrRegionReconstructor
{
    public LatticeRegionReconstructor(
        string fullText,
        IWordLatticeBuilder builder,
        ILatticeDecoder decoder,
        ICorrectionAcceptanceGate gate,
        LatticeOptions options);

    public IReadOnlyList<ReconstructionCandidate> Reconstruct(CorruptedTextRegion region, int maxCandidates = 5);
    public AcceptanceResult Evaluate(CorruptedTextRegion region);   // Faz 4'ün kullanacağı yüzey
    public LatticeRunStatistics Statistics { get; }
}

public sealed record LatticeRunStatistics(
    int Regions, int Built, int SkippedTooLong, int BudgetExceeded,
    int Applied, int Reviewed, int Left,
    long TotalArcs, long TotalVisitedStates, double TotalMilliseconds);
```

`IOcrRegionReconstructor` **değişmez**; `fullText` constructor'dan gelir (mevcut motorların yaptığı
gibi). `Reconstruct`, k-best path'leri `ReconstructionCandidate`'a çevirir
(`Source = ReconstructionSource.Lattice`, `Evidence` = kapının gerekçeleri + arc özeti).

### 11.2 `ReconstructionSource` enum'una değer eklenmesi
`Lattice` değeri **sona** eklenir; `Current`, `SymSpell`, `NoisyChannel` değerlerinin sırası
değişmez — karşılaştırma kodu bu değerleri dizi indeksi olarak kullanıyor
(`row.Results[(int)ReconstructionSource.NoisyChannel]`), sıra değişirse rapor sessizce bozulur (D36).

### 11.3 `debug-lattice` komutu
```
epubfixer debug-lattice <book.epub> [--json <out.lattice.json>] [--report <out.md>]
```
`BookKnowledgeBuilder` ile bilgi tabanını **bir kez** kurar, tüm region'ları çözer,
`LatticeRunStatistics`'i ve karar dağılımını yazar. `--report` verilirse her `Apply`/`Review`
kararını bağlamıyla listeler (R5.3'ün tohumu; bu fazda sade bir tablo yeterlidir).
Komut deseni için `VocabularyReportCommand` örnek alınır.

### 11.4 Baseline şeması (`docs/baselines/odun-kesmek.lattice.json`)

```json
{
  "dataset": "odun-kesmek",
  "measuredOn": "YYYY-MM-DD",
  "commit": "<sha>",
  "options": { "lambda": 0.5, "maxPathCost": 2.5, "minMargin": 0.75, "maxOrdinarySubstitutions": 1,
               "maxWindowLength": 48, "maxArcLength": 20, "contextTokens": 1, "budgetCap": 3.0 },
  "regions": 459, "built": 0, "skippedTooLong": 0, "budgetExceeded": 0,
  "applied": 0, "reviewed": 0, "left": 0,
  "fixtureTop1": "10/10", "fixtureTop5": "10/10",
  "totalSeconds": 0.0, "averageArcsPerRegion": 0.0, "maxVisitedStates": 0,
  "protectedViolated": 0
}
```

### 11.5 Yazılacak testler

| Test | İddia |
|---|---|
| `Reconstructor_ImplementsPortWithoutChangingIt` | `IOcrRegionReconstructor` üzerinden çağrılabiliyor |
| `Reconstructor_ReportsLatticeSource` | Adayların `Source == ReconstructionSource.Lattice` |
| `Fixture_TopOneIsTenOfTen` | **10/10** (uçtan uca, gerçek hazne ile) |
| `Fixture_ComparisonReportHasFourEngines` | Rapor dört sütunlu |
| `Statistics_CountsSkippedAndExceeded` | Atlanan region'lar sayılıyor |
| `FullBook_StaysWithinTimeBudget` (`Slow`) | 459 region ≤ **30 sn** |
| `FullBook_ProtectedViolatedIsZero` (`Slow`) | Hiçbir `Apply` protected span ile kesişmiyor |
| `FullBook_StateCeiling` (`Slow`) | Region başına `VisitedStates` ≤ regresyon sabiti |
| `Fix_OutputIsUnchanged` | `fix --apply-ocr-corrections` çıktısı Faz 2'deki ile birebir aynı |

`Fix_OutputIsUnchanged` bu fazın **davranış korunumu** kanıtıdır: yeni motor üretim hattına
bağlanmadı. Mevcut `MorphologyCallTraceTests` golden SHA-256'sı da bunu doğrular.

### 11.6 Kabul kriteri
- Karşılaştırma raporu dört motoru Top-1, Top-5, süre ve region başına state sayısıyla gösteriyor.
- `docs/baselines/odun-kesmek.lattice.json` yazılmış ve commit edilmiş; `README.md` satırı eklenmiş.
- Full kitap koşusu ≤ 30 sn, `protectedViolated == 0`.
- `fix` ve `measure` çıktıları değişmemiş.

### 11.7 Kapsam dışı
`EpubFixService` entegrasyonu, `--ocr-engine` bayrağı, mutation planlama — hepsi Faz 4.

---

## 12. Kararlar (bu planla birlikte verildi)

| # | Karar | Gerekçe | Nereye işlendi |
|---|---|---|---|
| D26 | Yol haritasında olmayan **R3.0** eklendi (karışım kümesi + ağırlıklı hizalayıcı), kritik yolun başına kondu. Eski motorun karışım tablosu **kopyalanır, ortaklaştırılmaz**. | R3.1'in maliyeti, R3.2'nin arc maliyeti ve R3.4'ün "olağan ikame ≤ 1" kuralı aynı hesaptır; üç yerde yazılırsa üç farklı maliyet doğar. Eski motor R4.3'te siliniyor; ölmekte olan koda refactor churn'dür. | Bölüm 4, 6 |
| D27 | `SymSpellLexiconMatcher` **Cli'da kalır**; SymSpell paketi Core'a eklenmez. Core yalnızca `ILexiconMatcher`'ı görür. | SymSpell bilinçli bir geçici detaydır (R5.2'de değişecek). Detaylar kenarda, port arkasında durur; Core'un test edilebilirliği sahte matcher ile korunur. | Bölüm 3.2, 7 |
| D28 | Maliyet sayıları **`OcrEditCostModel`'den** alınır; yol haritası düzyazısındaki "0.25 / 0.5" değerleri eskidir (gerçek: 0.30 / 0.70). | Tek doğruluk kaynağı koddur. Yol haritası metni R3.2 bitince düzeltilecek. | Bölüm 5.2 |
| D29 | Kafeste **identity** ve **literal** arc'ları zorunludur; `LatticeArc` bir `Kind` alanı alır. | "Dokunma" yolunun kafeste maliyetsiz var olması gerekir: kapı, en iyi yolu identity yoluyla karşılaştırarak karar verir ve her pencerede tam bir yol garanti edilir. | Bölüm 8.1, 8.3 |
| D30 | `LatticeOptions` yol haritasının altı alanını korur, yedi alan ekler (`MaxMatchesPerSpan`, `MaxQueriesPerSpan`, `MaxRegionStates`, `Lambda`, `MaxPathCost`, `MinMargin`, `MaxOrdinarySubstitutions`). | Tüm eşikler tek nesnede toplanmazsa R5.4'ün süpürmesi üç dosyaya dağılır. | Bölüm 8.1 |
| D31 | Büyük harf onarımı **kafes kurucunun** işidir, matcher'ın değil. | Matcher sözlük sorgulayıcısıdır; metin üretimi pencere bağlamı gerektirir. Hazne yüzey biçimini zaten saklıyor (Faz 2 D17). | Bölüm 3.4, 8.4 |
| D32 | Kapı kural 4 (**"orijinal zaten geçerli"**) morfoloji oracle'ını **çağırmaz**; hazne üzerinden karar verir (`BookCount ≥ 2` veya `Source ∈ {Frequency, Morphology}`). | Hazne morfolojik olarak geçerli formları zaten içeriyor. Oracle çağrısı, Faz 1'in "bilinmeyende exception" kuralı yüzünden yeni bir prefill yüzeyi doğurur; Faz 3'ün buna ihtiyacı yok. | Bölüm 10.2 |
| D33 | `AcceptanceResult` **mutlak aralık** taşır (`LogicalStart`, `LogicalEndExclusive`); `Evaluate` kafesi de parametre alır. | R4.1 mutation geometrisini span olmadan kuramaz; aralık ayrıca "dokunulmayan bağlam yazılmaz" güvencesidir. | Bölüm 10.1, 10.3 |
| D34 | Eşikler (`2.5 / 0.75 / 1 / 0.5`) bu fazda **seçilir, kalibre edilmez**; testlerde ve baseline'da açıkça yazılır. | Kalibrasyon R5.4'ün işi ve R5.1'in maliyet tablosuna bağımlı. Sayının bilinir olması, doğru olmasından önce gelir. | Bölüm 10.4, 11.4 |
| D35 | Sorgu planı iki katmanlıdır; katman 2 yalnızca katman 1 boş dönerse çalışır, `MaxQueriesPerSpan = 24`. | Ölçüm: 10 hedefin 9'u katman 1 ile mesafe ≤ 2'ye giriyor, `yü-ıiimeye` girmiyor. Sınırsız varyant üretimi B1'i geri getirir. | Bölüm 5.6, 7.3 |
| D36 | `ReconstructionSource` enum'una `Lattice` **sona** eklenir. | Mevcut karşılaştırma kodu enum değerlerini dizi indeksi olarak kullanıyor; sıra değişimi raporu sessizce bozar. | Bölüm 11.2 |
| D37 | Faz 3 **üretim hattına dokunmaz**; `fix` ve `measure` çıktıları birebir korunur ve bu bir testtir. | İki hattın veri modeli uyumsuzluğu (risk #2) tek başına ele alınmalı; R4.1 için ayrı bir oturum var. | Bölüm 1, 11.5 |
| D38 | Full kitap bütçesi Faz 3 için **≤ 30 sn** (120 sn'lik hard gate'in alt bütçesi). | 120 sn EPUB okuma/yazma, tespit, morfoloji ve Faz 4'ün mutation'larını da içerir. Alt bütçe olmadan gate son anda patlar. | Bölüm 1, 11.5 |
| D39 | R3.2 üç alt adıma bölünür; her alt adım ayrı commit ve tüm suite yeşil. | L boyutlu tek commit gözden geçirilemez; pencere mantığı ile arc üretimi ayrı ayrı doğrulanabilir. | Bölüm 8.6 |

### Uygulama sırasında verilen kararlar (D40–D48)

Aşağıdakiler plan yazıldıktan **sonra**, uygulama ve üç onarım turu sırasında verildi. Her biri
planın bir maddesini değiştiriyor; Faz 4 ve Faz 5 bu tabloyu esas alır.

| # | Karar | Gerekçe | Nereye işlendi |
|---|---|---|---|
| D40 | Kapı kural 2 **`DecodedPath.EditCost`** üzerinde çalışır: arc maliyetlerinin toplamı, LM terimi hariç. | Planın bölüm 10.2'si "path'in maliyeti" derken hangi maliyet olduğunu söylemiyordu. `DecodedPath.Cost` her kelime arc'ı için `λ·−logP` içeriyor; gerçek haznede kitapta 2 kez geçen bir kelime tek başına ~5,1 ediyor, bağlamlı bir pencere ~8+. `MaxPathCost = 2.5` böylece yapısal olarak geçilemez bir eşikti: ilk full-book koşusu 563 region'da **0 Apply** verdi. | Bölüm 10.2 kural 2 |
| D41 | Bir baseline ölçülemediğinde dosyaya **`status: "aborted"` + `reason` + `measuredFields: null`** yazılır; hiçbir alan tahminle veya sabitle doldurulmaz. | İlk R3.5 baseline'ında `fixtureTop1`, `protectedViolated`, `maxVisitedStates` kodda literal olarak yazılıydı ve ölçüm gibi commit edilmişti. R5.4 eşikleri bu dosyaya bakarak kalibre edecek; oradaki her sahte sayı sonraki fazların kararını bozar. | Bölüm 11.4 |
| D42 | Hard boundary'ler `WordLatticeBuilder`'a **dışarıdan** `hardBoundaryOffsets` olarak verilir (`LogicalTextStream.Boundaries`'ten Cli hesaplar). Core'daki `\n\n` taraması yalnızca ham metin/fixture yolu için fallback olarak kalır. | `LogicalTextStreamBuilder` text node'ları ayraçsız birleştiriyor; `\n\n` bir EPUB'da hiç geçmez. İlk implementasyon yalnızca fixture yolunu kurmuştu, yani pencere paragraf ve doküman sınırlarını serbestçe aşıyordu. | Bölüm 8.2 kural 2 |
| D43 | Çözücü state anahtarı **`(düğüm, PreviousWord)`**; anahtar başına en iyi `kBest` state tutulur, `Text` ve `Arcs` state'te taşınmaz (geri işaretçi + backtrack). | Bigram LM'de gelecek maliyeti yalnızca bu ikiliye bağlıdır, dolayısıyla anahtar başına en ucuzu tutmak Top-1'i **exact** bırakır. İlk iki implementasyon iki uçta hatalıydı: önce düğüm başına top-8 kesme (beam — sözleşme ihlali), sonra `(Text, PreviousWord)` anahtarıyla hiç birleştirmeme (tam yol sayımı; full-book koşusu 7+ dakikada bitmedi). | Bölüm 9.2 kural 5 |
| D44 | Arama sınırları daraltıldı: `MaxArcLength` 20 → **11**, `MaxMatchesPerSpan` 16 → **6**, sorgu başına `MaxSuggestionsPerQuery = 64`. | 30 sn bütçesi için gerekliydi (region başına ortalama 470 arc, en kötü 1.713 state). Bedeli ölçüldü ve kabul edildi: fixture hedeflerinin arc kapsaması 10/10 → **9/10**, ve 11 karakterden uzun tireli token'lar (`Auers-lıerger`) tek arc'a sığmıyor. Geri açılması R5.4'ün süre/kalite eğrisine bağlıdır. | Bölüm 8.1, 8.3 |
| D45 | Kapıya dört yeni kural eklendi: `UnsafeLengthChange` (kaynak < 3 alfanümerik, ya da uzunluk oranı 0,75–1,5 dışında), `ProperNameRisk` (kaynakta büyük harf veya apostrof + ≥1 olağan düzenleme), apostrof öncesi **kök değişimi**, `SuspiciousReplacement` (`"ie"`/`"ıe"` içeren replacement). **Eşikleri şu an sınıf içinde sabit.** | Precision'ı 60 karardan ~15 yanlıştan 31 karardan 0 yanlışa indiren değişiklik budur. Ama üçü gözlemlenen hata listesinden türetilmiş dar kurallardır ve bedeli var: `"ie"` kara listesi `ancakJeannie` gibi doğru düzeltmeleri de engelliyor, uzunluk oranı `ı ıç → üç` sınıfını (3→2 kısalma) yapısal olarak eliyor, büyük harf kuralı cümle başı her hatayı dokunulmaz kılıyor. **Borç:** eşikler D30 gereği `LatticeOptions`'a taşınmalı, aksi halde R5.4 bunları süpüremez. | Bölüm 10.2, 10.4 |
| D46 | `MaxOrdinarySubstitutions` artık yalnızca ikameleri değil **tüm olağan düzenlemeleri** (ikame + ekleme + silme, `EditAlignment.OrdinaryEdits`) sınırlar. Ad değişmedi. | Yalnızca ikame sayılırken `1 → göster` gibi saf eklemeden oluşan uydurmalar kuralı hiç görmeden geçiyordu. Adın anlamıyla uyumsuzluğu bilinçli olarak kabul edildi; yeniden adlandırma R5.4'e bırakıldı. | Bölüm 10.2 kural 5 |
| D47 | Fixture ölçümü **gerçek `tr_50k` haznesi + gerçek LM (λ=0,5)** ile yapılır. λ=0 ile ölçüm geçersizdir. | Identity arc'ları 0 maliyetlidir; λ=0 iken "dokunma" yolu daima en ucuzdur ve Top-1 motorun kalitesinden bağımsız olarak 0/10 çıkar. Ölçümü kelimeye iten tek kuvvet LM terimidir. İlk fixture testleri ise yalnızca 10 doğru cevaptan oluşan bir hazne ve cevabı ödüllendiren bir LM kullanıyordu; o kurulum hiçbir şey ölçmüyordu. | Bölüm 9.3, 10.5 |
| D48 | `protectedViolated` baseline'dan **düşürüldü**; kabul kriteri 5 açık kaldı ve R4.2'den önce ölçülecek. | Ölçülmeyen bir alanı sabit `0` ile yazmak D41'in yasakladığı şeydir. Kriterin kendisi düşmedi, yalnızca ölçümü ertelendi. | Bölüm 1b, 15 |

Yeni bir karar ihtiyacı doğarsa agent kendi başına karara varmaz; gerekçeyi bildirip bekler ve karar
bu tabloya eklenir.

---

## 13. Risk kaydı (Faz 3'e özgü)

| # | Risk | Etki | Azaltma |
|---|---|---|---|
| 1 | **Recall sessizce düşer:** matcher hedefi hiç döndürmez, motor bölgeyi "çözümsüz" sayar | Yüksek | `Match_FindsFixtureTargets`, `Build_AllTenTargetsHaveAnArc`; hedef kapsaması Faz 2'de %100 ölçülmüş — bulunamama daima sorgu planı hatasıdır |
| 2 | **Arama tekrar patlar** (B1'in dönüşü): sınırsız varyant, sınırsız arc, beam/lookahead | Yüksek | `MaxQueriesPerSpan`, `MaxArcLength`, `MaxRegionStates`, arc tavanı regresyon assert'i, `Decode_IsExactNotBeam` |
| 3 | Kapı fazla cömert: precision düşer, yanlış düzeltme EPUB'a girer | Yüksek | Altı kural birlikte; `ProtectedOccurrences_AreNeverApplied`; Faz 3 üretime bağlanmıyor, gerçek kanıt R4.2'de |
| 4 | Kapı fazla katı: recall ~0, motor hiçbir şey uygulamaz | Orta | `Review` verdict'i ile ayrım; karar dağılımı baseline'a yazılıyor; kalibrasyon R5.4 |
| 5 | Pencere geometrisi ile logical stream geometrisi tutmaz, R4.1 çöker | Orta | `Window_OffsetMapsBackToFullText` ve `Gate_SpanIsAbsolute` bu çevrimi Faz 3'te kilitler |
| 6 | LM ağırlığı (`lambda`) bağlamı fazla dinler, nadir ama doğru kelimeyi ezer | Orta | `Decode_LambdaZeroIsPureEditCost`; held-out bigram isabet oranı yalnızca %29,47 — LM zayıf, ağırlığı düşük başlatılıyor |
| 7 | Eski motorların testleri (`NoisyChannelV2Tests`, `PerformanceBudgetTests`) yeni tiplerden etkilenir | Düşük | Eski dosyalara dokunulmuyor; yeni tipler ayrı namespace'te |

---

## 14. Devir promptları

### R3.0
```
EpubFixer projesinde docs/phase-3-plan.md'deki R3.0 kalemini uygulayacaksın.

Önce şunları oku:
- docs/phase-3-plan.md — bölüm 2 (ön koşul), 3 (ortak kurallar), 5 (mevcut durum), 6 (R3.0),
  12 (kararlar D26, D28)
- docs/ocr-correction-roadmap.md — Faz 3 girişi ve "Hedef mimari"
- src/EpubFixer.Core/Ocr/OcrEditCostModel.cs
- src/EpubFixer.Cli/OcrReconstruction/NoisyChannelRegionReconstructor.cs satır 95-115
  (karışım tablosu ve maliyet kullanımı — OKU, DEĞİŞTİRME)
- tests/EpubFixer.Tests/OcrEditCostModelTests.cs

Kurallar:
- ÖN KOŞUL: çalışma ağacı temiz olmalı (Faz 2 commit'li). Değilse DUR ve bildir.
- Test-first: kırmızı test → minimum kod → refactor.
- OcrEditCostModel'e YALNIZCA iki alan eklenir (OrdinaryDeletion, OrdinaryInsertion = 1.00);
  mevcut alanların değerleri ve mevcut assert'ler DEĞİŞMEZ.
- Karışım kümesi SİMETRİKTİR. Bugünkü IsKnown yalnızca kaynak karaktere bakıyor (ı→z de "bilinen"
  sayılıyor); yeni küme bunu düzeltir. Eski dosyayı DÜZELTME (D26).
- Core'a System.IO girmez; CoreLayeringTests yeşil kalır.
- 10 fixture çiftinin (tests/Fixtures/OcrRegion/odun-kesmek-region-01.expected.json) ağırlıklı
  maliyetini bir tabloda sabitleyen test yaz; hepsi ≤ 3.0 olmalı. Olmayan çıkarsa DUR ve bildir —
  bütçeyi kendi başına büyütme.
- Kapsam R3.0 ile sınırlı. Matcher, kafes, çözücü, kapı yok.

Bitirdiğinde: eklenen tipler, 10 çiftin maliyet tablosu, eklenen testler, kabul kriterinin durumu,
planda güncellenmesi gereken bir şey olup olmadığı.
```

### R3.1
```
EpubFixer projesinde docs/phase-3-plan.md'deki R3.1 kalemini uygulayacaksın.

Önce şunları oku:
- docs/phase-3-plan.md — bölüm 3, 5.4, 5.6, 7 (R3.1), 12 (D27, D31, D35)
- docs/ocr-correction-roadmap.md — R3.1 maddesi
- src/EpubFixer.Core/Lexicon/BookVocabulary.cs, Models/VocabularyEntry.cs, TurkishWordNormalizer.cs
- src/EpubFixer.Core/Ocr/WeightedEditAligner.cs, OcrConfusionSet.cs (R3.0 çıktısı)
- src/EpubFixer.Cli/OcrReconstruction/SymSpellRegionReconstructor.cs (SymSpell kullanımı — OKU,
  DEĞİŞTİRME)

Kurallar:
- Test-first: kırmızı test → minimum kod → refactor.
- Sözleşme yol haritasındaki gibi: IReadOnlyList<LexiconMatch> Match(ReadOnlySpan<char>, double).
  Span ref struct'tır; ilk satırda string'e çevir.
- SymSpell paketi CORE'A EKLENMEZ (D27). Arayüz ve sorgu planı Core'da, adapter Cli'da.
- Sıralama ve filtreleme SymSpell mesafesiyle değil, R3.0 ağırlıklı maliyetiyle yapılır.
- Sorgu planı iki katmanlıdır (bölüm 7.3): katman 2 yalnızca katman 1 boş dönerse çalışır,
  toplam sorgu ≤ 24. Sınırsız varyant üretme.
- Word alanı VocabularyEntry.PreferredSurface'tir; büyük harf onarımı BURADA YAPILMAZ (D31).
- Match asla morfolojiye, diske, ağa gitmez. Sonuçlar cache'lenir.
- KABUL TESTİ: 10 fixture span'inin her biri için hedef kelime dönen listede olmalı. Biri
  bulunamıyorsa sorgu planını düzelt; hedef kapsaması Faz 2'de %100 ölçülmüş, kelime haznededir.
- Kapsam R3.1 ile sınırlı. Kafes, pencere, bağlam, karar yok.

Bitirdiğinde: eklenen tipler, 10 hedefin bulunma kanıtı, span başına sorgu sayısı ölçümü,
eklenen testler, kabul kriterinin durumu.
```

### R3.2
```
EpubFixer projesinde docs/phase-3-plan.md'deki R3.2 kalemini uygulayacaksın.
Bu, fazın en büyük kalemi; üç alt adıma bölünmüş ve her biri ayrı commit (D39).

Önce şunları oku:
- docs/phase-3-plan.md — bölüm 3, 5.2, 8 (R3.2), 12 (D28, D29, D30, D31, D39)
- docs/ocr-correction-roadmap.md — R3.2 maddesi ve "Hedef mimari"deki split/join şeması
- src/EpubFixer.Core/Ocr/Models/CorruptedTextRegion.cs
- src/EpubFixer.Core/Ocr/OcrRegionDetector.cs (region geometrisi)
- src/EpubFixer.Core/Epub/Models/LogicalTextStream.cs, TextBoundary.cs (hard boundary)
- src/EpubFixer.Core/Ocr/SearchBudget.cs
- R3.0 ve R3.1 çıktıları

Kurallar:
- Test-first: kırmızı test → minimum kod → refactor.
- Alt adımlar AYRI COMMIT: (A) pencere seçimi, (B) arc üretimi, (C) büyük harf + tavan assert'leri.
- LatticeOptions yol haritasının altı alanını AYNEN korur, bölüm 8.1'deki yedi alanı ekler.
- Identity ve literal arc'ları zorunludur (D29): her pencerede 0→N tam bir yol bulunmalı.
- Maliyet sayıları OcrEditCostModel'den gelir, yol haritası düzyazısından DEĞİL (D28).
- MaxWindowLength aşılırsa region ATLANIR (SkippedTooLong); kısaltarak zorlama.
- Arc sayısı için regresyon tavanı assert'i yaz; B1'in geri dönmesini engelleyen tek şey budur.
- KABUL: 10 hedefin her biri için ilgili arc kafeste olmalı VE arc sayısı tavanın altında kalmalı.
- Kapsam R3.2 ile sınırlı. Skorlama, LM, en iyi yol seçimi, karar yok.

Bitirdiğinde: pencere kuralları, arc üretim stratejisi, 10 hedefin arc kanıtı, arc tavanı sayısı,
eklenen testler, kabul kriterinin durumu.
```

### R3.3
```
EpubFixer projesinde docs/phase-3-plan.md'deki R3.3 kalemini uygulayacaksın.

Önce şunları oku:
- docs/phase-3-plan.md — bölüm 3, 9 (R3.3), 12 (D30)
- docs/ocr-correction-roadmap.md — R3.3 maddesi ve B1 tespiti (neden beam yok)
- src/EpubFixer.Core/Lexicon/BookLanguageModel.cs, ILanguageModel.cs
- docs/baselines/odun-kesmek.language-model.json (held-out bigram isabet oranı %29,47)
- R3.2 çıktısı

Kurallar:
- Test-first: kırmızı test → minimum kod → refactor.
- TAM arama: DAG üzerinde exact shortest path. BEAM YOK, LOOKAHEAD YOK, budama yok.
  Decode_IsExactNotBeam testini yazmadan bu kalemi bitmiş sayma.
- Literal arc'ları LM terimi almaz ve bağlam kelimesini değiştirmez.
- k-best FARKLI METİN döndürür (R3.4'ün margin hesabı buna bağlı).
- Outcome != Built olan kafes için boş liste dön; istisna atma.
- Tie-break: (Cost, Text ordinal). Determinizm testiyle kilitle.
- KABUL: fixture'da Top-1 10/10.
- Kapsam R3.3 ile sınırlı. Eşik, margin, "dokunma" kuralı yok — hepsi R3.4.

Bitirdiğinde: skor formülü, k-best stratejisi, Top-1 sonucu, pencere başına süre ölçümü,
eklenen testler, kabul kriterinin durumu.
```

### R3.4
```
EpubFixer projesinde docs/phase-3-plan.md'deki R3.4 kalemini uygulayacaksın.
Bu, yol haritasının en önemli tek kalemidir: precision'ın tek savunma hattı.

Önce şunları oku:
- docs/phase-3-plan.md — bölüm 3, 10 (R3.4), 12 (D32, D33, D34), 13 (risk 3 ve 4)
- docs/ocr-correction-roadmap.md — R3.4 maddesi ve risk kaydı #4 (özel isimler)
- src/EpubFixer.Core/Lexicon/BookVocabulary.cs, Models/VocabularyEntry.cs, Models/VocabularySource.cs
- test-data/odun-kesmek/ground-truth.json — protectedOccurrences (9 kayıt)
- R3.2 ve R3.3 çıktıları

Kurallar:
- Test-first: kırmızı test → minimum kod → refactor.
- ALTI kural da sağlanmadan Apply yok (bölüm 10.2). Emin değilsen karar "dokunma"dır.
- Kural 4 morfoloji oracle'ını ÇAĞIRMAZ; hazne üzerinden karar verir (D32).
- AcceptanceResult mutlak aralık taşır ve aralık yalnızca DEĞİŞEN metni kapsar (D33);
  dokunulmayan bağlam token'ı replacement'a girmez.
- Eşikler seçilmiştir, kalibre edilmemiştir (D34): 2.5 / 0.75 / 1 / 0.5. Testlerde açıkça yaz.
  Bir testi yeşile döndürmek için eşik OYNATMA — sebebi bul, gerekiyorsa DUR ve bildir.
- KABUL TESTİ: ground-truth'un 9 protectedOccurrences kaydının hiçbirinde Apply çıkmamalı.
- Fixture'ın 10 hedefindeki Apply/Review/Leave dağılımını bir testte SABİTLE. 10/10 Apply
  BEKLENMİYOR; beklenen şey sayının bilinir olması.
- Kapsam R3.4 ile sınırlı. Eşik süpürmesi (R5.4), mutation (R4.1), diff raporu (R5.3) yok.

Bitirdiğinde: altı kuralın implementasyonu, karar dağılımı, protected testinin sonucu,
eklenen testler, kabul kriterinin durumu.
```

### R3.5
```
EpubFixer projesinde docs/phase-3-plan.md'deki R3.5 kalemini uygulayacaksın.

Önce şunları oku:
- docs/phase-3-plan.md — bölüm 1 (kabul), 3, 11 (R3.5), 12 (D36, D37, D38)
- docs/ocr-correction-roadmap.md — R3.5 maddesi
- src/EpubFixer.Cli/OcrReconstruction/OcrReconstructionComparison.cs (dört sütuna çıkacak)
- src/EpubFixer.Cli/Quality/VocabularyReportCommand.cs (komut ve baseline yazma deseni — örnek al)
- src/EpubFixer.Core/Lexicon/BookKnowledgeBuilder.cs
- docs/baselines/README.md
- R3.2, R3.3, R3.4 çıktıları

Kurallar:
- Test-first: kırmızı test → minimum kod → refactor.
- IOcrRegionReconstructor sözleşmesi DEĞİŞMEZ; fullText constructor'dan gelir.
- ReconstructionSource enum'una Lattice SONA eklenir (D36) — mevcut değerlerin sırası değişmez.
- Faz 3 üretim hattına BAĞLANMAZ (D37): fix ve measure çıktıları birebir aynı kalmalı; bunu
  bir testle kanıtla.
- Full kitap bütçesi ≤ 30 sn (D38). Aşılırsa DUR ve bildir; bütçeyi büyütme.
- Bilgi tabanı kitap başına BİR KEZ kurulur (BookKnowledgeBuilder), region başına değil.
- docs/baselines/odun-kesmek.lattice.json'u bölüm 11.4 şemasıyla yaz ve commit et;
  docs/baselines/README.md'ye satırını ekle.
- Kapsam R3.5 ile sınırlı. EpubFixService entegrasyonu ve --ocr-engine bayrağı Faz 4'tür.

Bitirdiğinde: dört motorun karşılaştırma tablosu, baseline sayıları, full kitap süresi,
protectedViolated değeri, eklenen testler, Faz 3 kabul listesinin (bölüm 1) madde madde durumu.
```

---

## 15. Faz 3'ün Faz 4 ile sözleşmesi

### 15.1 R4.2'den önce kapatılacak üç ölçüm

Bunlar Faz 3'ün açık kalan kabul maddeleridir (bölüm 1b). Üçü de küçüktür ve üçü de doğrudan
yol haritası risk #2'yi (iki hattın veri modeli uyumsuzluğu) azaltır. **R4.2 bunlar olmadan
başlamamalıdır**, çünkü üretim hattına bağlandıktan sonra bir regresyonun kaynağını ayırt etmek
çok daha pahalıdır.

1. **`Fix_OutputIsUnchanged`** — `fix --apply-ocr-corrections` çıktısının Faz 2'deki ile birebir
   aynı olduğunu kanıtlayan test. Faz 3'ün "üretime dokunmadım" iddiası şu an testsiz; R4.2'nin
   ilk kasıtlı değişimi bu testin **beklenen değerinin** değişmesiyle görünür olmalı.
2. **Full-book süre testi** (`Category=Slow`) — 28,3 sn elle koşulmuş bir sayıdır, regresyon
   koruması altında değildir. D44'ün daraltılmış sınırları geri açılırken ilk kırılacak şey budur.
3. **`protectedViolated` ölçümü** (D48) — ground truth'un 9 `protectedOccurrences` kaydının
   logical aralıklarıyla hiçbir `Apply` aralığının kesişmediğini `debug-lattice` koşusunda sayan
   ve baseline'a yazan kontrol.

### 15.2 Arayüz sözleşmesi

- `AcceptanceResult.LogicalStart` / `LogicalEndExclusive` **mutlak logical offset**'lerdir ve
  doğrudan `RegionCorrection`'a geçer. R4.1 bu aralığı `stream.GetSourceLocationAt` ile karakter
  karakter source span'lere çevirir — mevcut `OcrCorrectionMutationPlanner`'ın tekniği aynen
  kullanılır, sıfırdan geometri yazılmaz.
- `LatticeRegionReconstructor.Evaluate(region)` R4.2'nin çağıracağı yüzeydir; `Reconstruct`
  yalnızca A/B raporu içindir.
- `AcceptanceVerdict.Review` kararları R4.2'de **uygulanmaz**; R5.3'ün diff raporunun girdisidir.
- `LatticeOptions` tek eşik nesnesidir; R5.4 yalnızca bu nesneyi süpürür, başka dosya açmaz.
- `LatticeRunStatistics` R4.2'nin kalite kapısı raporuna ve R6.2'nin "kalan hata" raporuna girdi
  verir; `SkippedTooLong` + `BudgetExceeded` sayıları motorun **bilerek dokunmadığı** kütledir.
- Faz 3 hiçbir kalite sayısını değiştirmediği için R1.0'ın golden logical text SHA-256'sı
  **korunur**; ilk kasıtlı değişim R4.2'de olacaktır.
