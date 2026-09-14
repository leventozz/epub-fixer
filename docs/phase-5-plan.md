# Faz 5 — Kalite turu — Uygulama planı

> Bu belge [ocr-correction-roadmap.md](ocr-correction-roadmap.md) Faz 5'in (R5.1–R5.4) uygulama
> planıdır. Her kalem ayrı bir oturumda ayrı bir agent'a devredilebilir; bölüm 14'te kopyala-yapıştır
> devir promptları vardır. Önceki fazların planları: [phase-0-plan.md](phase-0-plan.md),
> [phase-1-plan.md](phase-1-plan.md), [phase-2-plan.md](phase-2-plan.md),
> [phase-3-plan.md](phase-3-plan.md), [phase-4-plan.md](phase-4-plan.md).
>
> Fazın yönetici ilkesi: **önce teşhis, sonra tedavi.** Faz 5'e "kalite turu" adı verilmişti; Faz 4'ün
> kapanış ölçümleri bunun yanlış ad olduğunu gösterdi. Bugün eksik olan şey ince ayar değil,
> **recall**: lattice motoru üretimde 10 düzeltme uyguluyor, legacy 120. Faz 5 bir eşik süpürmesiyle
> başlayamaz, çünkü süpürülecek eşiğin hangisi olduğunu söyleyen ölçüm henüz yok — ve süpürmenin
> hedef fonksiyonu olacak kalite kapısı şu an OCR motorunu **hiç ölçmüyor**.

---

## 1. Faz 5 neden yol haritasındakinden farklı tanımlanıyor (D68)

Yol haritası Faz 5'i şöyle tarif ediyor: *"Kalibrasyon, weighted matcher, insan incelemesi."* Bu
tarif, Faz 4 başlarken makuldü: lattice motorunun üretimde varsayılan olacağı, geriye yalnızca ince
ayarın kalacağı varsayılıyordu. Faz 4 o varsayımı ölçtü ve **yanlışladı**:

| Ölçüm | Değer | Kaynak |
|---|---:|---|
| Legacy'nin uyguladığı düzeltme | 120 | `odun-kesmek.fix-legacy.json` |
| Lattice'in uyguladığı düzeltme | **10** | `odun-kesmek.fix-lattice.json` |
| KAZANÇ (yalnız lattice düzeltiyor) | 9 | `odun-kesmek.engine-diff.json` |
| **KAYIP** (yalnız legacy düzeltiyor) | **119** | aynı |
| ÇATIŞMA (ikisi de, farklı sonuç) | 1 — ve bu tek yanlış vaka | aynı |
| Kitap sağlığı (çözümlenemeyen / 1000 token) | legacy 57,30 → lattice **58,61** | aynı, `bookHealth` |

Faz 4'ün yedi kabul kriterinden dördü açık kaldı (bkz. [phase-4-plan.md](phase-4-plan.md) bölüm 1b) ve
dördü de aynı kökten besleniyor: **lattice motorunun recall'ü legacy'nin on ikide biri.** Varsayılan
motor bu yüzden çevrilmedi (D65), eski motor bu yüzden silinemedi (D67).

Dolayısıyla Faz 5'in gerçek işi şudur:

> **Lattice motorunun recall'ünü, precision'ı düşürmeden, varsayılan olabileceği noktaya taşımak —
> ve taşınamıyorsa bunu sayıyla gerekçelendirip kaydetmek.**

Yol haritasının R5.1 (maliyet kalibrasyonu), R5.2 (trie matcher) ve R5.4 (eşik ayarı) kalemleri bu
işin **araçlarıdır**, amacı değil. Hangi aracın kullanılacağı ölçümle belirlenir; bu yüzden plan
kritik yolun başına yol haritasında olmayan bir **R5.0 (ölçüm tabanı ve teşhis)** kalemi koyar — Faz
0, 1, 2, 3 ve 4 planlarının hepsinin izlediği desen. Ayrıca Faz 4'ün yarım kalan R4.2c adımını
**R5.5** olarak fazın sonuna alır: anahtarı çevirmek Faz 5'in kapanış eylemidir.

**Kalem ID'leri yol haritasıyla uyumlu kalır.** R5.1–R5.4 yol haritasındaki anlamlarını korur; yeni
kalemler yalnızca uçlara (R5.0, R5.5) eklenir. Hiçbir yol haritası kalemi yeniden numaralandırılmaz.

---

## 2. Faz 5 bittiğinde elde ne olacak

| Çıktı | Dosya | Ne işe yarar |
|---|---|---|
| Benchmark'ın motor anahtarı | `benchmarks/EpubFixer.QualityBenchmarks/QualityBenchmarkApplication.cs` | Kalite kapısı ilk kez lattice ile koşulabilir |
| Sınıf kırılımlı kapı | `QualityBenchmarkGateEvaluator.cs` + `quality-gate.json` | 12 OCR kaydındaki regresyon toplamda saklanamaz |
| Kayıp taksonomisi | `docs/baselines/odun-kesmek.loss-taxonomy.json` | 119 kaybın her birinin **hangi aşamada** düştüğü |
| D66'nın kök nedeni | aynı dosya, `rawVersusProduction` düğümü | Kapının üretimde neden gevşediği — açıklanmadan eşik oynatılmaz |
| Genişletilmiş ground truth | `test-data/odun-kesmek/ground-truth.json` (schemaVersion 3) | Kalibrasyonun ve süpürmenin hedef fonksiyonu |
| Maliyet enjeksiyon dikişi | `OcrConfusionSet` + `CorrectionAcceptanceGate` + `LatticeOcrPlannerFactory` | Öğrenilmiş tablo motora **girebilir** hâle gelir |
| Öğrenilmiş maliyet tablosu | `docs/baselines/odun-kesmek.edit-costs.json` | Karışım maliyetleri elle değil veriden |
| Kapı eşikleri tek nesnede | `LatticeOptions` (genişletilmiş) | D45 borcu kapanır; R5.4 tek nesne süpürür |
| Eşik süpürme eğrisi | `docs/baselines/odun-kesmek.threshold-sweep.json` | Seçilen noktanın gerekçesi |
| Düzeltme inceleme raporu | `src/EpubFixer.Cli/.../CorrectionReviewReport.cs` | 100+ düzeltmenin insan tarafından incelenebilmesi |
| Motor kararı | `docs/baselines/odun-kesmek.engine-diff.json` (güncel) | Varsayılan çevrildi mi, çevrilmediyse neden |

**Faz 5'in kabulü** — Faz 4'ün açık kalan dört kriterinin kapanması veya sayıyla gerekçelenmesi:

1. `dotnet test EpubFixer.slnx` yeşil.
2. Kalite kapısı **lattice motoruyla** koşulabilir ve sınıf kırılımlı gate uygular.
3. Ground truth'un OCR kolu ≥ 60 kayıt (bugün 12) ve her kayıt elle doğrulanmış.
4. `docs/baselines/odun-kesmek.loss-taxonomy.json` 119 kaybın **her birini** tek bir sebebe
   atfeder; atfedilemeyen kayıt sayısı sıfır.
5. KAYIP sayısı 119'dan **ölçülebilir biçimde düşmüştür**; düşmediyse hangi kalemin neden
   işe yaramadığı kayıtlıdır.
6. `measure` metriği lattice çıktısında legacy çıktısına göre **kötüleşmemiştir**.
7. Uygulanan her düzeltme incelenmiştir; yanlış düzeltme sayısı **sıfırdır**.
8. Kriter 5–7 sağlanıyorsa varsayılan motor lattice'tir (R5.5). Sağlanmıyorsa varsayılan legacy
   kalır ve **hangi kriterin hangi sayıyla sağlanmadığı** bu belgenin kapanış tablosuna yazılır.

**Faz 5'in kapsamı dışı:** ikinci geçiş OCR (R6.1), kalan hata raporu (R6.2), eski yolların
kaldırılması (R4.3 — D67 ile Faz 5'e ertelendi ama Faz 5'te de **yapılmaz**; ön koşulu R5.5'in
başarısıdır ve en erken Faz 6'da yeniden değerlendirilir).

---

## 3. Ön koşul

1. **Çalışma ağacı temiz olmalıdır.** Bu plan yazılırken `HEAD = 5da77f6`
   ("R4.2c records: pin lattice engine profile and the legacy/lattice diff") ve ağaç temizdi.
2. **Faz 4 kapanış tablosu okunmuş olmalıdır** ([phase-4-plan.md](phase-4-plan.md) bölüm 1b ve 13).
   Dört açık kabul maddesi unutulmuş iş değil, bilinçli olarak bu faza devredilmiş ölçümlerdir.
3. `dotnet test EpubFixer.slnx` yeşil olmalıdır (Faz 4 kapanışında 467/467, ~2 dk 53 sn).
4. Kararlar **D65, D66, D67** okunmuş olmalıdır. Faz 5'in ilk üç alt adımı doğrudan bu üç kaydın
   bıraktığı boşlukları kapatır.

---

## 4. Her kalem için geçerli ortak kurallar

Faz 0–4 kuralları aynen geçerlidir. Faz 5'e özgü eklemeler:

### 4.1 Test-first
Kırmızı test → minimum kod → refactor. Testi olmayan üretim kodu yazılmaz. Bu fazın ürünü ağırlıklı
olarak **ölçüm ve parametre**dir; ölçüm kodunun da testi olur. "Kalibrasyon aracı test edilmez"
kabul edilmez: öğrenilmiş bir tablo yanlışsa bütün fazı zehirler.

### 4.2 Katmanlar
- `EpubFixer.Core` politika kalır. Öğrenilmiş maliyet tablosunu **diskten okuyan** kod Adapters'a
  gider; Core yalnızca tablonun **tipini** tanır. `CoreLayeringTests`'in izin listesine yeni dosya
  eklenmez.
- Kalibrasyon aracı `benchmarks/` altında yaşar (yol haritası R5.1). Üretim hattına girmez;
  ürünü bir **veri dosyasıdır**, bir kod yolu değil.

### 4.3 Sayı uydurulmaz (D41 / kural 3.4 aynen geçerli)
Beklenen test değeri tahminle yazılmaz: önce koşulur, çıkan sayı okunur, sonra teste yazılır ve o
sayının neden o olduğu bitiş raporunda açıklanır. Ölçülemeyen alan sabitle doldurulmaz;
`status: "aborted"` + `reason` yazılır.

### 4.4 Ölçüm aleti önce doğrulanır (D64'ün genellemesi)
Faz 4 bir kez ölçüm aletinin hasarını kalite sorunu sanmanın bedelini ödedi. Faz 5 aynı tuzağa iki
yerden daha açıktır (bölüm 6.1, 6.2). Kural: **bir sayı beklenmedik çıktığında ilk şüpheli
alettir.** Aletin doğruluğu bağımsız bir kontrolle gösterilmeden motor suçlanmaz — ve tersi de
geçerli: gerçek bir kalite sorunu "alet bozuk" diye geçiştirilmez. Ayırt edici ölçüt Faz 4'tekiyle
aynıdır: kaymış/kırpılmış parça → alet; makul ama yanlış düzeltme → motor.

### 4.5 Eşik gevşeterek yeşile boyama yasağı
Bir kalem kapıyı kırıyorsa çözüm kapıyı indirmek değildir. `quality-gate.json`'daki eşikler bu fazda
**yalnızca yukarı** hareket edebilir. Aşağı hareket gerekiyorsa DUR, gerekçeyi bildir; karar bölüm
12'ye eklenir. Bu, yol haritası risk #5'in doğrudan azaltmasıdır.

### 4.6 Recall için precision feda edilmez
Faz 5'in amacı recall'dür, ama hedef dengesi değişmedi: **precision ≥ %98, recall ≥ %60** ve emin
olunmayan durumda karar "dokunma". Bir parametre değişikliği KAYIP'ı düşürüp yanlış düzeltme sayısını
sıfırın üstüne çıkarıyorsa o değişiklik **reddedilir**, ne kadar recall getirdiğine bakılmaksızın.

### 4.7 Determinizm
Aynı girdi → aynı plan → aynı çıktı EPUB → aynı SHA-256. Öğrenilmiş tablolar da deterministik
üretilir: aynı ground truth → aynı `edit-costs.json`. Sözlük/küme iterasyon sırasına güvenilmez;
her sıralamada açık tie-break.

### 4.8 Komutlar
```bash
dotnet build EpubFixer.slnx
dotnet test EpubFixer.slnx
dotnet test tests/EpubFixer.Tests --filter "Category=Slow"
dotnet run --project benchmarks/EpubFixer.QualityBenchmarks -- test-data/odun-kesmek
dotnet run --project benchmarks/EpubFixer.QualityBenchmarks -- --propose test-data/odun-kesmek
dotnet run --project src/EpubFixer.Cli -- debug-lattice "test-data/odun-kesmek/input.epub" --json docs/baselines/odun-kesmek.lattice.json --report artifacts/debug/lattice.md
dotnet run --project src/EpubFixer.Cli -- fix "test-data/odun-kesmek/input.epub" -o out.epub --apply-ocr-corrections --ocr-engine lattice
dotnet run --project src/EpubFixer.Cli -- measure out.epub
EPUBFIXER_UPDATE_BASELINES=1 dotnet test tests/EpubFixer.Tests --filter "FullyQualifiedName~OcrMutationBaselineTests"
```

### 4.9 Commit disiplini
Kalem başına ayrı commit; ilk satır `R5.x: <ne yapıldı>`. R5.0 dört, R5.4 iki alt adıma bölünmüştür;
her alt adım kendi commit'inde ve **her commit'te tüm suite yeşil**.

### 4.10 Kapsam disiplini
Her agent yalnızca kendi kalemini yapar. Yol boyunca fark edilen sorunlar düzeltilmez, bitiş
raporunda "gözlem" olarak yazılır. Sözleşme değişikliği gerekiyorsa önce gerekçe bildirilir, karar
bölüm 12'ye eklenir.

---

## 5. Sıra ve paralellik

```
R5.0  Ölçüm tabanı ve teşhis
  ├─ a  Benchmark'ın lattice kolu + sınıf kırılımlı kapı
  ├─ b  Kayıp taksonomisi (119 kaybın sebep atfı)          ── a'ya bağlı değil
  ├─ c  D66'nın kök nedeni (ham vs üretim kapı farkı)      ── b'ye bağlı
  └─ d  Ground truth'un OCR kolunu genişlet                ── b'ye bağlı (kaynak listesi)
         │
         ├──────────────┬──────────────┬─────────────────┐
         ▼              ▼              ▼                 ▼
      R5.1           R5.4a          R5.3            (R5.2 KOŞULLU)
   maliyet        kapı eşiklerini   inceleme          trie matcher
   kalibrasyonu   LatticeOptions'a  raporu            ↑ yalnızca R5.0b
   (d gerekir)    çıkar                                 "matcher ıskası" derse
         │              │              │                 │
         └──────────────┴──────────────┴─────────────────┘
                        ▼
                     R5.4b  Eşik ve arama uzayı süpürmesi
                        ▼
                     R5.5  Varsayılanı çevir (veya gerekçeli DUR)
```

**Kritik yol:** R5.0a → R5.0b → R5.0d → R5.1 → R5.4b → R5.5

**Paralel yürütülebilir:** R5.0b ile R5.0a; R5.3 ile R5.1/R5.4a. R5.0d tek başına en büyük alt
adımdır (elle etiketleme) ve ayrı bir agent'a devredilebilir — ama R5.0b'nin ürettiği aday listesi
olmadan başlamamalıdır (D70).

---

## 6. Mevcut durumun tespiti

Bu bölüm Faz 5'in üzerine kurulduğu ölçülmüş gerçeklerdir. Bir kalem agent'ı kendi bölümünden önce
buradaki ilgili maddeleri okur.

### 6.1 Kalite kapısı OCR motorunu ölçmüyor — üç ayrı sebeple

Faz 4 benchmark'a bir OCR kolu ekledi (R4.0c) ve kapı yeşil geçti: `CorrectlyFixed=148`,
`WronglyFixed=0`, `Deferred=12`, precision %100, recall %92,5. Bu sayı **OCR motoru hakkında hiçbir
şey söylemiyor**:

1. **Kesişim yok.** Legacy'nin 120 mutation'ı ile ground truth'un 160 kaydı arasında hiçbir örtüşme
   yok — ne tam eşleşme, ne ≥4 karakterlik substring örtüşmesi (D64). 12 OCR kaydının tamamı
   `Deferred`, yani hiç dokunulmamış. Kapının ölçtüğü 148/160, baştan sona **tireleme**dir.
2. **Lattice ile hiç koşulmadı ve koşulamıyor.** `QualityBenchmarkRunner`'ın ctor'ı
   `IOcrCorrectionPlanner? = null` alıp `LegacyOcrCorrectionPlanner`'a düşüyor
   ([QualityBenchmarkRunner.cs:18](../benchmarks/EpubFixer.QualityBenchmarks/QualityBenchmarkRunner.cs#L18)),
   `QualityBenchmarkApplication` ise onu parametresiz çağırıyor
   ([QualityBenchmarkApplication.cs:26](../benchmarks/EpubFixer.QualityBenchmarks/QualityBenchmarkApplication.cs#L26)).
   **Motor seçmenin bayrağı, ortam değişkeni veya profili yok.** Kod değiştirmeden lattice ile
   benchmark koşmak bugün mümkün değil.
3. **Kapı sınıf kırılımını okumuyor.** `QualityBenchmarkGateEvaluator` yalnızca toplam
   `Precision` / `Recall` üzerinden karar veriyor
   ([QualityBenchmarkGateEvaluator.cs:20-26](../benchmarks/EpubFixer.QualityBenchmarks/QualityBenchmarkGateEvaluator.cs#L20)).
   Sınıf kırılımı hesaplanıyor ve **raporlanıyor** (`QualityBenchmarkClassBreakdown`) ama
   gate'e girmiyor. 12 OCR kaydının tamamı bozulsa toplam recall en fazla 12/160 = 7,5 puan düşer;
   kapı eşiği %92,5 olduğundan bu **fark edilmeden geçebilir**.

### 6.2 Sınıf kırılımının `Correct` sayacı iyimser

`QualityBenchmarkRunner.CreateClassBreakdowns`
([:269](../benchmarks/EpubFixer.QualityBenchmarks/QualityBenchmarkRunner.cs#L269)):

```csharp
correct = group.Count(item => !failureIds.Contains(item.Id));
```

**Tespit edilmemiş bir kayıt da "correct" sayılıyor** — başarısızlık listesinde olmadığı için.
Bugünkü 12 OCR kaydının hepsi `Deferred` (tespit edilmedi), dolayısıyla sınıf kırılımı bu sınıflar
için yanıltıcı biçimde yüksek precision gösterir. R5.0a bu sayacı düzeltmeden sınıf kırılımlı kapı
takılamaz — **alet önce doğrulanır** (kural 4.4).

> **R5.0a güncellemesi (commit `29bc238`):** bu tespit güncel kodda **doğrulanmadı**.
> `QualityBenchmarkCorrectionEvaluator.Evaluate` hem `Deferred` hem `WronglyFixed`
> sınıflandırmalarını `failures` listesine ekliyor; `CreateClassBreakdowns`'ın
> `correct = group.Count(item => !failureIds.Contains(item.Id))` satırı bu yüzden tespit
> edilmemiş kayıtları zaten dışlıyor. Sayaç doğru; iddia bu planın yazıldığı andaki koda değil,
> muhtemelen daha eski bir sürüme aitti. `QualityBenchmarkClassBreakdownTests` bu davranışı
> artık pinliyor.

### 6.3 Kaybın profili: hedeflerin çoğu kafeste üretilebilir durumda

119 kaybın `original` alanları üzerinde ölçüldü:

| Özellik | Sayı | Anlamı |
|---|---:|---|
| Uzunluk > `MaxArcLength` (11) | **22** | doğru cevap kafeste hiç üretilemiyor — D65'in sınıfı |
| Uzunluk ≤ 11 | **97** | hedef **üretilebilir**; kayıp kafeste değil, sonrasında |
| Boşluk içeren (çok-token) | 8 | `OriginalTokenIsValid`'in çok-token yorumu şüphelisi |

Legacy'nin karar kuralı dağılımı: `StructuralLexiconRepair` 94, `DirectStructuralRepair` 14,
`EvidenceOnlyDominantLexicon` 9, `AdjacentCompositeRepair` 2.

**Sonuç: kaybın %82'si arama uzayı sorunu değil.** `MaxArcLength`'i büyütmek (D65'in bilinen
çözümü) en fazla 22 vakaya dokunur. Geri kalan 97 için sebep kapıda, matcher'da veya decoder'da
aranmalıdır — ve hangisi olduğu **ölçülmemiştir**. R5.0b'nin tek işi budur.

### 6.4 Kapının reddetme dağılımı ve baskın kural

Faz 3'ün ham koşusu (`odun-kesmek.lattice.json`, 563 region):

| Sebep | Sayı |
|---|---:|
| `OriginalTokenIsValid` | **418** |
| `TooManyOrdinaryEdits` | 42 |
| `NoChange` | 40 |
| `Accepted` | 31 |
| `UnsafeLengthChange` | 15 |
| `ProperNameRisk` | 10 |
| `MarginTooSmall` | 4 |
| `SuspiciousReplacement` | 2 |
| `NoPath` | 1 |

`OriginalTokenIsValid` tek başına region'ların **%74'ünü** kapatıyor. Kural şudur
([CorrectionAcceptanceGate.cs:31](../src/EpubFixer.Core/Ocr/Lattice/CorrectionAcceptanceGate.cs#L31)):

```csharp
if (ChangedTokens(lattice.Window, changed.Start, changed.End).Any(IsValidOriginalToken))
    return Leave(lattice, "OriginalTokenIsValid", changed);
```

**Sınanması gereken hipotez (R5.0b):** `ChangedTokens`
([:212-234](../src/EpubFixer.Core/Ocr/Lattice/CorrectionAcceptanceGate.cs#L212)) yalnızca
harf/rakam/kesme işareti dizilerini token sayar. Garbage glyph'ler (`^ ; : < > ,`) token sınırıdır.
Yani `;;aşılacak` parçası `aşılacak` token'ını üretir — ki bu **geçerli bir Türkçe kelimedir** ve
`IsValidOriginalToken` true döner, düzeltme reddedilir. Aynı şey `()yuncu` → `yuncu`,
`;;.amanda` → `amanda` için de sorulmalıdır. Doğruysa bu, tek bir kuralın kaybın büyük bir dilimini
tek başına açıklaması demektir.

> Bu bir **hipotezdir, bulgu değildir.** R5.0b'nin işi onu doğrulamak veya çürütmektir; agent
> hipotezi doğru varsayarak koda dokunmaz.

### 6.5 Maliyet modeli tek düz değer kullanıyor, enjeksiyon dikişi kopuk

[OcrEditCostModel.cs](../src/EpubFixer.Core/Ocr/OcrEditCostModel.cs) 12 alanlı bir record;
karışım maliyeti **tek bir sabittir**: `KnownGlyphSubstitution = 0.25`. `WeightedEditAligner`
([:126-135](../src/EpubFixer.Core/Ocr/WeightedEditAligner.cs#L126)) bir substitution için ya
`Keep`, ya `KnownGlyphSubstitution` (çift `OcrConfusionSet`'te varsa), ya da
`OrdinarySubstitution = 1.00` yazar. **Çift başına maliyet yoktur** — `ı↔i` ile `3↔e` aynı 0.25'i
öder. R5.1'in öğreneceği şey tam olarak budur.

Enjeksiyon dikişi üç yerden kopuk:

| Yer | Durum |
|---|---|
| `WeightedEditAligner(OcrEditCostModel?, OcrConfusionSet?)` | ✅ ikisini de ctor'dan alıyor |
| `CorrectionAcceptanceGate` | ❌ `private readonly WeightedEditAligner aligner = new();` ([:10](../src/EpubFixer.Core/Ocr/Lattice/CorrectionAcceptanceGate.cs#L10)) — somut tip, enjeksiyon noktası yok |
| `LatticeOcrPlannerFactory.Create` | ❌ `new SymSpellLexiconMatcher(vocabulary)` ([:22](../src/EpubFixer.Adapters/Ocr/LatticeOcrPlannerFactory.cs#L22)) — aligner parametresi geçilmiyor |
| `OcrConfusionSet` | ❌ private ctor, interface yok, yalnızca `Default` — öğrenilmiş çift kümesi giremez |

Yani **öğrenilmiş bir tablo bugün motora giremez.** R5.1'in ilk işi tabloyu öğrenmek değil, dikişi
açmaktır.

Bugünkü karışım kümesi ([OcrConfusionSet.cs](../src/EpubFixer.Core/Ocr/OcrConfusionSet.cs)):
garbage glyph'ler `^ ; : < > ,`; `1/l/ı/i/I` tam kliği; ve simetrik çiftler
`ı↔ü ı↔ö ı↔o ı↔r l↔b i↔h 0↔o 0↔ö 3↔e ^↔ş ^↔ç c↔e c↔ç s↔ş g↔ğ u↔ü o↔ö`.
Kayıp listesindeki `J3arış→Barış` (`J→B`), `()yuncu→oyuncu` (`(`, `)` garbage değil),
`;iiyledikleri→söyledikleri` gibi vakalar bu kümenin dışında kalıyor.

### 6.6 Kapının dört eşiği `LatticeOptions`'ın dışında (D45 borcu)

Faz 4 §13 bunu R5.4'ün ilk işi saydı; kapanmadı. `CorrectionAcceptanceGate` içinde sabit:

| Kural | Satır | Sabit |
|---|---|---|
| `IsShortOrDisproportionateChange` | [:174-190](../src/EpubFixer.Core/Ocr/Lattice/CorrectionAcceptanceGate.cs#L174) | uzunluk < 3 reddet; oran > 1,5 veya < 0,75 reddet |
| `HasSuspiciousReplacementShape` | [:138-143](../src/EpubFixer.Core/Ocr/Lattice/CorrectionAcceptanceGate.cs#L138) | `"ıe"` / `"ie"` içeren replacement reddet |
| `HasProperNameRisk` | [:192-200](../src/EpubFixer.Core/Ocr/Lattice/CorrectionAcceptanceGate.cs#L192) | kaynakta **herhangi bir** büyük harf, veya iki tarafta kesme işareti |
| `IsValidOriginalToken` | [:205-210](../src/EpubFixer.Core/Ocr/Lattice/CorrectionAcceptanceGate.cs#L205) | `BookCount >= 2` veya kaynak `Frequency`/`Morphology` |

`HasProperNameRisk`'in "herhangi bir büyük harf" kuralı özellikle geniştir: kayıp listesindeki
`Avııstıırya'nın`, `Aııersbergerler'e`, `Se-lı;ıstian`, `Soka-f^ı'nda`, `Viya-ııa'da` gibi vakaların
tamamı bu kuralın kapsamına giriyor (yol haritası risk #4'ün kasıtlı bedeli). R5.4 bu bedeli
ölçmeden değiştirmez.

### 6.7 `LatticeOptions`'da iki ölü alan

- `MaxQueriesPerSpan` (= 24) **hiç okunmuyor**; matcher kendi `SymSpellLexiconMatcher.MaxQueriesPerSpan = 24`
  sabitini kullanıyor.
- `MaxMatchesPerSpan` (= 6) `WordLatticeBuilder.cs:60`'ta uygulanıyor; matcher'ın kendi
  `MaxMatchesPerSpan = 16`'sı bu yolda ölü.

R5.4 bu iki alanı süpürmeye sokarsa **hiçbir şey değişmez** ve bu, süpürme sonucunu okuyan kişiyi
yanıltır. R5.4a bunları ya gerçekten bağlar ya da kaldırır.

### 6.8 `MaxPathCost` ile `MinMargin` farklı büyüklükleri ölçüyor

`DecodedPath.EditCost` yalnızca **Word arc** maliyetlerinin toplamıdır (LM hariç); `DecodedPath.Cost`
ise edit + LM'dir. Kapı `MaxPathCost`'u `EditCost`'a
([:66](../src/EpubFixer.Core/Ocr/Lattice/CorrectionAcceptanceGate.cs#L66)), `MinMargin`'i ise tam
`Cost`'a ([:72](../src/EpubFixer.Core/Ocr/Lattice/CorrectionAcceptanceGate.cs#L72)) uyguluyor. Bu
bilinçli olabilir ama **belgelenmemiştir**. `Lambda` süpürülürken `MinMargin` dolaylı olarak değişir,
`MaxPathCost` değişmez — R5.4 bu bağlaşıklığı hesaba katmalıdır.

### 6.9 `FullBookReaderPreview` commit'li ama yanlış motoru kullanıyor

Yol haritası R5.3 "şu an commit edilmemiş" diyor; **bu bilgi eskimiştir** — dosya `8b0e5e0`'de
girdi ([FullBookReaderPreview.cs](../src/EpubFixer.Cli/OcrReconstruction/FullBookReaderPreview.cs),
479 satır, `reader-preview` komutu). Ama akışı tamamen eski hattır: `OcrRegionDetector` →
`NoisyChannelRegionReconstructor` → `DeterministicOcrCandidateReranker`. **Lattice motorunu hiç
çağırmıyor** ve `Review` kararları bu yolda yok. Ayrıca sildiği liste R4.3'ün kaldırma listesindedir.

Dolayısıyla R5.3 bu dosyanın **genişletilmesi değil**, lattice hattı için yeni bir rapor yazılması ve
eskisinin olduğu yerde bırakılmasıdır (R4.3'te birlikte silinecek).

### 6.10 Ground truth'un OCR kolu kalibrasyon için yetersiz

160 kaydın 148'i `Hyphenation`. OCR kolu: `Fragmentation` 5, `GlyphConfusion` 4,
`GarbageInsertion` 2, `SpuriousSpace` 1 = **12**. `MissingSpace` ve `Mixed` sınıfları enum'da var,
kayıt **sıfır**. Yol haritası R0.2'nin "sınıf başına ≥ 25" hedefi gerçekleşmedi.

12 kayıt üzerinde ne maliyet kalibre edilir ne eşik süpürülür — süpürme doğrudan bu 12 kayda
overfit olur. **R5.1 ve R5.4b'nin ortak ön koşulu R5.0d'dir.**

`--propose` bayrağı ve `GroundTruthProposer` zaten var
([QualityBenchmarkApplication.cs:66-72](../benchmarks/EpubFixer.QualityBenchmarks/QualityBenchmarkApplication.cs#L66));
R5.0d sıfırdan araç yazmaz.

---

## 7. R5.0 — Ölçüm tabanı ve teşhis (plana eklendi, D68)

### Amaç
Faz 5'in geri kalanının hangi kalemi neden yapacağını **sayıyla** belirlemek. Bu kalem hiçbir motor
davranışı değiştirmez; ürünü ölçüm altyapısı, bir taksonomi dosyası ve genişletilmiş ground
truth'tur.

**Bu kalem bittiğinde şu üç soru cevaplanmış olur:**
1. Lattice motoru kalite kapısından geçiyor mu, sınıf bazında nerede duruyor? (a)
2. 119 kaybın her biri hangi aşamada düştü? (b)
3. Kapı ham koşuda reddettiği yolu üretimde neden kabul etti? (c)

### 7.1 Alt adım a — Benchmark'ın motor anahtarı ve sınıf kırılımlı kapı

**Sıra önemlidir:** önce sayaç düzeltilir (6.2), sonra kırılım gate'e takılır, en sonunda lattice ile
koşulur. Ters sırada koşulursa lattice'in sayıları bozuk bir aletle ölçülür.

1. **`CreateClassBreakdowns`'ın `correct` sayacını düzelt** (6.2). Tespit edilmemiş kayıt `correct`
   sayılmaz. Bu bir üretim davranışı değişikliği değil, ölçüm düzeltmesidir — ama **legacy
   koşusundaki sınıf kırılımı sayıları değişecektir**. Değişen her sayı bitiş raporunda
   eski → yeni olarak yazılır.
2. **Motor anahtarı ekle.** `--ocr-engine legacy|lattice` benchmark'ın CLI'ına eklenir ve
   `QualityBenchmarkApplication` uygun planner'ı `QualityBenchmarkRunner`'a enjekte eder.
   `LatticeOcrPlannerFactory.Create()` zaten Adapters'ta ve benchmark Adapters'ı referanslıyor —
   yeni bağımlılık gerekmez. Varsayılan **legacy** kalır.
3. **Sınıf kırılımını kapıya tak.** `QualityBenchmarkGateOptions`'a sınıf başına minimum eşik
   alanı eklenir; profil `quality-gate.json`'dan okunur. **Bu fazda OCR sınıfları için eşik
   konmaz** — kayıt sayısı 12 iken eşik koymak yanlış güvendir (D63 aynen geçerli). Konan tek
   şey **mekanizma** ve `Hyphenation` için bugünkü ölçülmüş değerdir; OCR sınıflarının eşikleri
   R5.0d'den sonra R5.4b'de belirlenir.
4. **Her iki motorla koş, ikisini de kaydet.** `quality-gate.json`'ın `ocrStageMeasurement`
   düğümü artık iki motorun sonucunu birden taşır.

**Kabul:**
- `dotnet run --project benchmarks/EpubFixer.QualityBenchmarks -- --ocr-engine lattice test-data/odun-kesmek`
  çalışır ve sonuç raporlanır.
- Legacy koşusunun **gate sonucu değişmez** (PASS kalır); değişen tek şey sınıf kırılımı
  sayılarıdır ve her değişim gerekçelidir.
- Lattice koşusunun kapı sonucu — geçsin ya da geçmesin — `quality-gate.json`'a işlenir.
  **Kırılırsa kapı gevşetilmez** (kural 4.5); sonuç kaydedilir ve R5.4b'nin girdisi olur.
- `ProtectedViolated == 0`, `ProtectedChanged == 0` her iki motorda da doğrulanır. **D48'in
  kapanışı budur** — Faz 3'ten beri açık.

### 7.2 Alt adım b — Kayıp taksonomisi (fazın en kritik ölçüm işi)

**Amaç:** 119 kaybın her birini **tek bir sebebe** atfetmek. Bu dosya Faz 5'in geri kalanının
önceliklendirmesidir: hangi kalemin kaç vakaya dokunacağını o söyler.

Her kayıp için lattice hattı tek tek koşulur ve düştüğü ilk aşama kaydedilir:

| Sebep kodu | Anlamı | İlgili kalem |
|---|---|---|
| `RegionNotDetected` | `OcrRegionDetector` bu yeri hiç bölge saymadı | kapsam dışı — gözlem olarak yaz |
| `WindowSkippedTooLong` | Pencere `MaxWindowLength`'i aştı | R5.4b |
| `TargetNotInLattice` | Doğru kelime hiçbir arc'ta yok (`MaxArcLength` veya bütçe) | R5.4b / R5.2 |
| `MatcherMissedTarget` | Span kafeste ama matcher hedefi döndürmedi | **R5.2'nin koşulu** |
| `DecoderRankedOther` | Hedef kafeste, ama Top-1 başka yol | R5.1 (maliyet) / R5.4b (lambda) |
| `GateRejected:<Reason>` | Top-1 doğruydu, kapı reddetti — sebep kodu birebir yazılır | R5.4a/b |

**Kurallar:**
- Her kayıt **tam olarak bir** sebep alır: zincirdeki **ilk** düşüş noktası. Bir vaka hem
  `TargetNotInLattice` hem `GateRejected` olamaz.
- Atfedilemeyen kayıt **sıfır** olmalıdır. Atfedilemeyen varsa bu bir bulgudur, gizlenmez.
- `GateRejected` vakaları sebep koduna göre ayrı ayrı sayılır (`OriginalTokenIsValid`,
  `TooManyOrdinaryEdits`, `ProperNameRisk`, `UnsafeLengthChange`, …).
- 6.4'teki `ChangedTokens` hipotezi ayrıca sınanır: `GateRejected:OriginalTokenIsValid` alan her
  vaka için **hangi token'ın geçerli sayıldığı** kaydedilir. Hipotez doğruysa bu token, garbage
  glyph'in soyulmasıyla ortaya çıkan temiz parçadır.

**Çıktı:** `docs/baselines/odun-kesmek.loss-taxonomy.json` — vaka başına
`{original, expected, lossReason, gateReason?, validToken?, targetInLattice, arcLength}` ve bir
histogram.

**Kabul:** 119/119 atfedildi; histogram commit'lendi; bitiş raporu "hangi kalem kaç vakaya dokunur"
tablosunu içerir.

**Kapsam dışı:** hiçbir düzeltme yapılmaz. Bu alt adım bir **teşhis**tir; bulunan sebep ne kadar
bariz görünürse görünsün kod değiştirilmez.

### 7.3 Alt adım c — D66'nın kök nedeni

D66 açık bir soru bırakmıştı: `kol-1 ıı kta` parçası **ham koşuda** `Leave` / `TooManyOrdinaryEdits`
alıyor, **üretim koşusunda** `Apply` / `Accepted` alıyor. `MaxArcLength` (D65) doğru cevabın
üretilememesini açıklar; kapının **neden kabul ettiğini açıklamaz**.

En olası aday (D66'da kayıtlı, **doğrulanmamış**): hazne ve dil modeli üretimde hyphenation sonrası
metinden kuruluyor; seçilen yol ve dolayısıyla `OrdinaryEdits` sayısı değişiyor.

**Yapılacak:** aynı parça için iki koşunun kafesini, Top-1 yolunu, arc kırılımını ve `OrdinaryEdits`
hesabını yan yana koyup farkın **nereden** doğduğunu göstermek.

**Kabul:** fark tek bir mekanizmaya bağlanmış ve `loss-taxonomy.json`'ın `rawVersusProduction`
düğümüne yazılmıştır. Açıklanamıyorsa `status: "unexplained"` yazılır ve **R5.4b'de
`MaxArcLength` büyütülmesi bloke edilir** (D75) — çünkü kapıyı üretimde gevşeten etki yerinde
kalıyorsa, arama uzayını büyütmek yanlış yazma riskini artırır.

### 7.4 Alt adım d — Ground truth'un OCR kolunu genişlet (schemaVersion 3)

**Bu, R5.0'ın en büyük alt adımıdır ve ayrı bir agent'a devredilebilir.**

**Hedef:** OCR kolu 12 → **≥ 60** kayıt; `MissingSpace` ve `Mixed` dahil her sınıfta ≥ 5 kayıt.
(Yol haritası R0.2'nin "sınıf başına ≥ 25" hedefi ideal kalır; 60 bu fazın gerçekçi taahhüdüdür.)

**Örnekleme rastgele değil, karar sınırından yapılır (D70).** Aday havuzu:

| Kaynak | Sayı | Neden |
|---|---:|---|
| `engine-diff.json` KAYIP | 119 | legacy düzeltiyor, lattice bırakıyor — karar sınırının tam üstü |
| `engine-diff.json` KAZANÇ | 9 | lattice düzeltiyor, legacy bırakıyor |
| `engine-diff.json` ÇATIŞMA | 1 | ikisi farklı — en bilgilendirici tek vaka |
| Lattice `Review` kararları | 4 | kapının kararsız kaldığı yerler |
| Lattice `Apply` kararları | 10 | precision'ın ölçüleceği yer |

Gerekçe: bugünkü ground truth ile motorların dokunduğu yerler **hiç kesişmiyor** (6.1). Rastgele
stratified örnekleme bu kesişmezliği sürdürür ve kapı yine boşa ölçer. Motorların gerçekten
dokunduğu bölgelerden örneklemek, ground truth'u **karar sınırının ölçüm aleti** hâline getirir.

**Zorunlu kural — döngüsellik yasağı (D71).** Legacy'nin çıktısı ground truth **değildir**. Her kayıt
bağlamı (çevresindeki cümle) okunarak elle doğrulanır ve `verifiedBy: "manual"` işaretlenir.
Legacy'nin önerisi bir **adaydır**; yanlışsa doğru değer yazılır, ve legacy'nin yanlış olduğu vakalar
ayrıca işaretlenir (`legacyProposal` alanı). Bu alan olmadan Faz 5, lattice'i legacy'nin kopyası
olmaya zorlar ve legacy'nin kendi hatalarını ölçülemez kılar.

**Şema:** `schemaVersion` 2 → 3; yeni alanlar `verifiedBy`, `legacyProposal?`, `source`
(`"engine-diff"` | `"manual"` | `"auto"`). `QualityBenchmarkDatasetLoader` **şema 2'yi okumaya devam
eder** (Faz 0'ın geriye dönük uyumluluk kuralı).

**Kabul:**
- OCR kolu ≥ 60 kayıt; sınıf dağılımı raporlanır.
- Loader hem şema 2 hem şema 3 okur; bunu kanıtlayan test var.
- Her yeni kayıt `verifiedBy: "manual"`.
- Benchmark her iki motorla koşulur ve **yeni** sınıf kırılımı kaydedilir. Bu koşu, gate
  eşiklerinin R5.4b'de belirleneceği tabandır.
- **Kapı bu genişlemeyle kırılabilir** — beklenen budur, çünkü artık motorların dokunduğu yerleri
  ölçüyor. Kırılırsa eşik indirilmez (kural 4.5); ölçülen değer kaydedilir ve R5.4b'nin hedefi olur.

---

## 8. R5.1 — Confusion maliyet kalibrasyonu

**Bağımlılık:** R5.0d (yeterli hizalama çifti), R5.0b (hangi vakaların maliyet kaynaklı olduğu).

### 8.1 Önce dikiş, sonra tablo

6.5'te ölçüldü: öğrenilmiş bir tablo bugün motora **giremez**. Sıra:

1. `OcrConfusionSet`'e bir **port** aç. Bugünkü `Default` davranışı değişmeden bu portun arkasına
   geçer (OCP). Öğrenilmiş küme ikinci bir implementasyondur.
2. `CorrectionAcceptanceGate`'in `aligner`'ını enjekte edilebilir yap (`IWeightedEditAligner`).
   Varsayılan bugünkü davranış.
3. `LatticeOcrPlannerFactory.Create`'e maliyet modeli / karışım kümesi parametresi ekle;
   varsayılan `Default`.
4. **Bu üç adımdan sonra tüm baseline'lar bit düzeyinde aynı kalmalıdır.** Değişirse dikiş
   davranış değiştirmiştir — DUR.

### 8.2 Çift başına maliyet

Bugün tüm bilinen karışımlar tek bir `KnownGlyphSubstitution = 0.25` ödüyor (6.5). Öğrenilmiş tablo
**çift başına** maliyet taşır: `cost(ı→i)` ile `cost(3→e)` farklı olabilmelidir. Sözleşme
genişletilir; çift için kayıt yoksa bugünkü düz değere düşülür (backoff).

### 8.3 Öğrenme

- **Girdi:** ground truth'un (bozuk → doğru) çiftleri, `WeightedEditAligner` ile hizalanır.
- **Sayım:** karakter karışım sayımları → olasılık → `-log` maliyet.
- **Düzleştirme zorunlu:** 60 kayıtlık bir tabandan sıfır frekanslı çiftler çıkacaktır; düzleştirme
  olmadan tablo tek örneklere aşırı uyar. Kullanılan yöntem ve parametresi belgelenir.
- **Held-out zorunlu:** ground truth ikiye bölünür; tablo yalnızca eğitim bölümünden öğrenilir,
  rapor held-out üzerinden verilir. Aynı veriden öğrenip aynı veride ölçmek sayı üretir, bilgi
  üretmez.
- **Çıktı:** `docs/baselines/odun-kesmek.edit-costs.json` — deterministik, aynı girdi aynı dosya.

### 8.4 Kabul
- Dikiş adımlarından sonra baseline'lar değişmedi (8.1 madde 4).
- Öğrenilmiş tablo held-out üzerinde raporlandı.
- **A/B:** öğrenilmiş tablo, elle ayarlanmış tabloya karşı Top-1'i **düşürmez** (yol haritası kabul
  kriteri) ve yanlış düzeltme sayısını **artırmaz** (kural 4.6).
- Tablo varsayılan yapılmaz; bir bayrak/parametre arkasında gelir. Varsayılan olup olmayacağına
  R5.4b'nin süpürmesi karar verir.

**Kapsam dışı:** eşik değiştirmek, `MaxArcLength` büyütmek, trie yazmak.

---

## 9. R5.2 — `TrieLexiconMatcher` (KOŞULLU — D72)

> **Bu kalem koşulludur.** Ön koşulu: R5.0b'nin taksonomisinde `MatcherMissedTarget` payı
> **≥ %20** (119 kaybın ≥ 24'ü). Altındaysa kalem **düşürülür** ve gerekçesi kaydedilir — yol
> haritası risk #6 bu ihtimali zaten öngörüyor ("R5.2 hiç gerekmeyebilir").
>
> Bir agent bu kalemi "mimari olarak daha doğru" diye başlatmaz. SymSpell'in weighted cost
> eksikliği bilinen ve **bilinçli** bir kabuldü (D27/R3.1); onu kapatmanın bedeli L boyutunda bir
> iştir ve yalnızca ölçüm gerektiriyorsa ödenir.

**Kapsam:** Vocabulary bir trie'ye konur; `Match` trie üzerinde yürürken her node'da DP satırını
taşır, `min(row) > budget` olunca alt ağacı budar (Levenshtein-automaton / Schulz–Mihov). Maliyet
fonksiyonu `OcrEditCostModel` (R5.1'in öğrenilmiş tablosu dahil).

**Değişmeyecek sözleşmeler** (envanterde tespit edildi):
- `ILexiconMatcher.Match(ReadOnlySpan<char>, double budget)` aynen kalır.
- Bütçe ölçeği `WeightedEditAligner` ile **aynı** olmalıdır; yoksa `MaxPathCost` / `MinMargin` ve
  bütün pinlenmiş baseline'lar kayar.
- Giriş normalize, çıkış **küçük harf normalize yüzey**; büyük harf restorasyonu matcher'ın değil
  `WordLatticeBuilder.RestoreCase`'in işidir.
- Sıralama `Cost` sonra `Word` ordinal; determinizm buna bağlı.
- Trie **Adapters'ta** yaşar. Diskten tablo okuyan hiçbir şey Core'a giremez (`CoreLayeringTests`).

**Kabul:** Aynı sözleşme; A/B'de precision artar **veya** süre düşer, ikisi birden kötüleşmez;
`PerformanceBudgetTests`'in 30 sn / 2.500 state sınırları korunur; üstteki hiçbir katman değişmez.

---

## 10. R5.3 — Düzeltme inceleme raporu

**Amaç:** İnsan doğrulama döngüsü. Faz 4'ün 7. kabul kriteri ("uygulanan her düzeltme elle
incelenmiş") 10 düzeltmede elle yapılabildi; recall arttıkça 100+ düzeltmede yapılamaz.

**6.9'daki tespit belirleyicidir:** mevcut `FullBookReaderPreview` lattice motorunu hiç çağırmıyor ve
R4.3'ün silme listesinde. Bu yüzden R5.3 o dosyayı **genişletmez**; lattice hattı için ayrı bir rapor
yazar ve eskisine dokunmaz (ölmekte olan koda dokunulmaz — Faz 4 §13).

**Kapsam:**
- Uygulanan her düzeltmeyi **bağlamıyla** (öncesi/sonrası cümle) yan yana gösteren Markdown diff.
- `Review` kararları ayrı bölümde — bunlar uygulanmıyor ama insanın görmesi gereken kütle.
- `Leave` kararlarının sebep histogramı ve en sık sebebin örnekleri.
- Motorun **bilerek dokunmadığı** kütle: `SkippedTooLong`, `BudgetExceeded`, atlanan bölge
  düzeltmeleri (`RegionMutationPlanResult.Diagnostics`).
- İki motorun farkı raporda görünür (KAZANÇ / KAYIP / ÇATIŞMA) — `engine-diff.json`'ı elle
  üretmek yerine komutla üretilebilir hâle gelir.

**Kabul:** Full koşunun çıktısı **tek dosyada** gözden geçirilebilir; ÇATIŞMA bölümü en üstte
(en tehlikeli sınıf); rapor `artifacts/` altına yazılır (gitignore'lu) ve özeti `docs/baselines/`'e.

**Bağımlılık:** R5.0a. **Boyut:** M.

---

## 11. R5.4 — Eşik ve arama uzayı ayarı

### 11.1 R5.4a — Kapı eşiklerini `LatticeOptions`'a çıkar (D74)

D45'in borcu; Faz 4 §13 bunu R5.4'ün ilk işi saydı. **Süpürmeden önce yapılmalıdır** — süpürülemeyen
bir eşik, süpürme sonucunu okuyan kişiyi yanıltır.

1. 6.6'daki dört sabit `LatticeOptions`'a alan olarak çıkar. **Varsayılanlar bugünkü değerler**;
   davranış değişmez ve bunun kanıtı baseline'ların aynı kalmasıdır.
2. 6.7'deki iki ölü alan (`MaxQueriesPerSpan`, `MaxMatchesPerSpan`) ya gerçekten bağlanır ya
   kaldırılır. Ölü kalırlarsa süpürmede "etkisi yok" sonucu üretirler ki bu yanlış bilgidir.
3. 6.8'deki `MaxPathCost` (EditCost) / `MinMargin` (Cost) asimetrisi ya düzeltilir ya **kasıtlı
   olduğu belgelenir**. Karar gerekirse bölüm 12'ye eklenir.

**Kabul:** Tüm baseline'lar bit düzeyinde aynı; `LatticeOptions` kapının tek eşik nesnesi; hiçbir
eşik sınıf içinde sabit kalmadı.

### 11.2 R5.4b — Süpürme

**Bağımlılık:** R5.0d (hedef fonksiyon), R5.4a (süpürülebilir nesne), R5.1 (maliyet tablosu varsa).

**Süpürülecekler** — R5.0b'nin taksonomisi hangilerinin kaç vakaya dokunduğunu söyler; süpürme
listesi o tablodan **türetilir**, buradan kopyalanmaz. Beklenen adaylar:
`Lambda`, `MaxPathCost`, `MinMargin`, `MaxOrdinarySubstitutions`, `MaxArcLength`, `BudgetCap`,
ve R5.4a'nın çıkardığı dört kapı eşiği.

**Kurallar:**
- **Hedef fonksiyon tektir ve önceden yazılır:** genişletilmiş ground truth üzerinde
  precision ≥ %98 kısıtı altında recall maksimizasyonu. Süpürmeden sonra "şu metrik daha iyi
  görünüyordu" denmez.
- **`MaxArcLength` D75'e bağlıdır:** R5.0c açıklanamadıysa büyütülmez.
- **Süre bedeli her noktada ölçülür.** `MaxArcLength` ve `BudgetCap` arama uzayını büyütür;
  `PerformanceBudgetTests`'in 30 sn / 2.500 state sınırları ve D61'in 120 sn / legacy+35 sn
  bütçesi kısıttır, süpürmenin çıktısı değil.
- **Overfit koruması:** seçilen nokta held-out bölümde de doğrulanır.
- **Çıktı:** `docs/baselines/odun-kesmek.threshold-sweep.json` — taranan noktalar, her noktada
  (precision, recall, yanlış düzeltme, süre, KAYIP) ve seçilen noktanın **gerekçesi**.

**Kabul:** Seçilen nokta R0.4 kapısını sağlar; yanlış düzeltme sayısı sıfır; KAYIP ölçülmüş biçimde
düşmüş; süre bütçesi içinde; gerekçe belgelenmiş.

---

## 12. R5.5 — Varsayılanı çevir (R4.2c'nin devri, D73)

**Bu, Faz 5'in kapanış eylemidir ve Faz 4'ün yarım kalan R4.2c adımıdır.** Adımların tamamı
[phase-4-plan.md](phase-4-plan.md) bölüm 8.3'te yazılıdır ve **aynen geçerlidir**; burada yalnızca
Faz 5'e özgü kapı tekrarlanır.

**Ön koşul — hepsi sağlanmalı:**

| # | Koşul | Bugünkü değer |
|---|---|---|
| 1 | Yanlış düzeltme sayısı sıfır (elle inceleme, R5.3 raporu üzerinden) | **1** |
| 2 | `measure` metriği legacy'ye göre kötüleşmiyor | **kötüleşiyor** (57,30 → 58,61) |
| 3 | Benchmark lattice ile kapıdan geçiyor (sınıf kırılımı dahil) | ölçülmedi |
| 4 | `ProtectedViolated == 0`, `ProtectedChanged == 0`, `UnexpectedTextChanges == 0`, `NonTextChanges == 0` | lattice için ölçülmedi |
| 5 | Süre ≤ 120 sn **ve** legacy + 35 sn (D61) | ölçülmedi |

**Ölç, sonra yaz** (Faz 4 §8.3 adım 2 aynen): `MorphologyCallTraceTests`'in golden SHA-256'sı
**kasten değişir** ve yeni değeri ölçülerek yazılır; `OcrMutationBaselineTests`, `PerformanceBudgetTests`,
`measure` baseline'ı ve benchmark sayıları yeniden ölçülür.

**Kabul edilmezse:** varsayılan `legacy`'ye geri alınır (tek satır), bulgular raporlanır ve **bu
belgenin kapanış tablosuna hangi koşulun hangi sayıyla sağlanmadığı yazılır.** Çıktı EPUB'a yanlış
düzeltme yazan bir sürüm commit edilmez. Faz 5'in "başarısız" sayılması için sebep yok: ölçülmüş bir
"henüz değil", ölçülmemiş bir "tamam"dan iyidir.

**R4.3 (eski yolların kaldırılması) bu fazda yapılmaz.** D67 onu Faz 5'e ertelemişti; ön koşulu
(KAYIP listesinin boşalması) R5.5 başarılı olsa bile sağlanmayacaktır. En erken Faz 6'da yeniden
değerlendirilir.

---

## 13. Kararlar (bu planla birlikte verildi)

Numaralandırma Faz 4'ün D67'sinden devam eder.

| # | Karar | Gerekçe | Nereye işlendi |
|---|---|---|---|
| D68 | Yol haritasında olmayan **R5.0** eklendi ve kritik yolun başına kondu; Faz 5'in hedefi "kalite turu"ndan **"recall kurtarma ve varsayılanı çevirme"**ye yeniden tanımlandı. | Faz 4'ün ölçümü yol haritasının varsayımını yanlışladı: KAYIP 119 ve kitap sağlığı lattice'te kötüleşiyor. Bir eşik süpürmesi, süpürülecek eşiği gösteren teşhis olmadan yön duygusu olmadan yürümektir. Faz 0/1/2/3/4 planlarının hepsi aynı deseni izledi. | Bölüm 1, 5, 7 |
| D69 | Kalem ID'leri yol haritasıyla uyumlu kalır; R5.1–R5.4 anlamlarını korur, yeni kalemler yalnızca uçlara (R5.0, R5.5) eklenir. | Faz 4 aynı disiplini izledi (R4.0 eklendi, R4.1–R4.3 korundu). Yeniden numaralandırma iki belge arasındaki atıfları kırar ve devir promptlarını okunamaz hâle getirir. | Bölüm 1 |
| D70 | Ground truth genişletmesi rastgele/stratified örneklemeyle değil, **motorların gerçekten dokunduğu bölgelerden** yapılır (KAYIP 119 + KAZANÇ 9 + ÇATIŞMA 1 + Review 4 + Apply 10). | Bugünkü 160 kayıt ile üretimdeki 120 mutation arasında **hiçbir kesişim yok** (D64). Rastgele örnekleme bu kesişmezliği sürdürür; kapı yine tirelemeyi ölçer ve OCR motoru hakkında bilgi vermez. Karar sınırından örneklemek ground truth'u motorun ölçüm aleti yapar. | Bölüm 6.10, 7.4 |
| D71 | **Legacy'nin çıktısı ground truth sayılmaz.** Her yeni kayıt bağlamıyla elle doğrulanır (`verifiedBy: "manual"`) ve legacy'nin önerisi ayrı alanda (`legacyProposal`) tutulur. | Aksi hâlde Faz 5 lattice'i legacy'nin kopyası olmaya zorlar: legacy'nin kendi yanlışları "doğru cevap" olarak sabitlenir ve ölçülemez hâle gelir. Yol haritası risk #1'in (kirli hazne) ölçüm tarafındaki karşılığı. | Bölüm 7.4 |
| D72 | **R5.2 koşulludur.** R5.0b taksonomisinde `MatcherMissedTarget` payı < %20 ise kalem düşürülür. | Yol haritası risk #6 bunu zaten öngörüyor. Trie L boyutunda bir iştir; SymSpell'in weighted cost eksikliği bilinçli bir kabuldü (D27). Ölçüm göstermeden ödenmez. | Bölüm 9 |
| D73 | **R5.5 eklendi**: Faz 4'ün yarım kalan R4.2c adımı Faz 5'in kapanış kalemidir. R4.3 Faz 5'te de **yapılmaz**. | D65 R4.2c'yi ertelerken kök nedeni R5.4'ün kapsamına yazdı; anahtarın çevrilmesi doğal olarak o kapsamın sonuna düşer. R4.3'ün ön koşulu (KAYIP boş) R5.5 başarılı olsa bile sağlanmayacaktır (D67). | Bölüm 12 |
| D74 | D45'in kapı içindeki dört sabit eşiği **R5.4b'den önce, ayrı bir alt adımda (R5.4a)** `LatticeOptions`'a çıkarılır; `LatticeOptions`'ın iki ölü alanı aynı adımda bağlanır veya kaldırılır. | Faz 4 §13 bunu "R5.4'ün ilk işi" saydı ama alt adım yapmadı ve borç kapanmadı. Süpürülemeyen bir eşik süpürme raporunu yanıltır; ölü bir alan "etkisi yok" sonucu üretir ki bu yanlış bilgidir. | Bölüm 6.6, 6.7, 11.1 |
| D75 | **`MaxArcLength` büyütülmesi R5.0c'ye bağlıdır.** D66'nın açıklanmamış etkisi (kapının üretimde ham koşudan farklı karar vermesi) çözülmeden arama uzayı büyütülmez. | D66 zaten kayıtlı: yanlış yazma için **iki** koşul birlikte gerekti ve yalnızca birincisi (`MaxArcLength`) açıklandı. Yalnızca birinciyi düzeltmek, kapıyı gevşeten ikinci etkiyi açıklanmamış olarak yerinde bırakır ve başka bir parçada aynı biçimde yanlış yazabilir. | Bölüm 7.3, 11.2 |
| D76 | Kalite kapısının **sınıf kırılımlı** hâle getirilmesi R5.0a'nın işidir, ama **OCR sınıflarına bu adımda eşik konmaz**; eşikler R5.0d'den sonra R5.4b'de belirlenir. | D63 aynen geçerli: 12 kayıt üzerinde eşik koymak yanlış güven, koymamak regresyon gizler. Çözüm eşiği ertelemek değil, **tabanı büyütmek**tir (R5.0d). Mekanizma önce, sayı sonra. | Bölüm 7.1 |
| D77 | `quality-gate.json` eşikleri bu fazda **yalnızca yukarı** hareket edebilir. Aşağı hareket karar gerektirir. | Yol haritası risk #5: kapının gevşetilmesi regresyonu gizler. Faz 5 recall peşindedir ve recall için kapıyı indirmek en kolay yoldur — bu yüzden açıkça yasaklanır. | Bölüm 4.5 |
| D78 | R5.3 mevcut `FullBookReaderPreview`'u **genişletmez**; lattice hattı için ayrı rapor yazar. | Dosya eski motor zincirine bağlı ve R4.3'ün silme listesinde. Ölmekte olan koda özellik eklenmez (Faz 4 §13'ün OCP kuralı). Yol haritasının "şu an commit edilmemiş" bilgisi de eskimiştir (`8b0e5e0`). | Bölüm 6.9, 10 |

Yeni bir karar ihtiyacı doğarsa agent kendi başına karara varmaz; gerekçeyi bildirip bekler ve karar
bu tabloya eklenir.

---

## 14. Risk kaydı (Faz 5'e özgü)

| # | Risk | Etki | Azaltma |
|---|---|---|---|
| 1 | **Döngüsellik:** legacy'nin çıktısı ground truth sayılır; lattice legacy'nin kopyası olmaya zorlanır ve legacy'nin hataları ölçülemez hâle gelir | Yüksek | D71: her kayıt elle doğrulanır, `legacyProposal` ayrı alanda; legacy'nin yanlış olduğu vakalar özellikle işaretlenir |
| 2 | **Etiketleme hatası kalibrasyonu zehirler:** yanlış bir ground truth kaydı hem R5.1'in tablosunu hem R5.4b'nin hedef fonksiyonunu birlikte bozar | Yüksek | Elle doğrulama bağlamla yapılır; R5.1 held-out ile ölçer; R5.4b'nin seçtiği nokta held-out'ta da doğrulanır |
| 3 | **Recall için precision feda edilir:** maliyetleri düşürmek / eşikleri gevşetmek KAYIP'ı düşürür ama yanlış düzeltme üretir | Yüksek | Kural 4.6 + D77; yanlış düzeltme sayısı sıfırın üstüne çıkan hiçbir nokta seçilmez, ne kadar recall getirdiğine bakılmaksızın |
| 4 | **Ölçüm aleti hasarı kalite sorunu sanılır** (D64'ün tekrarı): sınıf kırılımı sayacı (6.2) ve yeni motor anahtarı iki yeni hasar yüzeyi | Yüksek | Kural 4.4; R5.0a'nın sırası zorunlu: önce sayaç düzeltilir, sonra kırılım takılır, en son lattice koşulur. Legacy koşusunun gate sonucu değişmemelidir — bu, aletin kendi testidir |
| 5 | **12 kayda overfit:** R5.4b genişletilmiş ground truth gelmeden koşulursa seçilen eşik bu kitabın 12 örneğine uyar | Yüksek | R5.4b'nin ön koşulu R5.0d; süpürme held-out bölümle doğrulanır |
| 6 | **`MaxArcLength` süre bütçesini patlatır:** arama uzayı arc uzunluğuyla süper-lineer büyür | Orta | D61 alt bütçesi + `PerformanceBudgetTests`'in 30 sn / 2.500 state sınırları kısıt olarak süpürmeye girer; her nokta süre ile birlikte kaydedilir |
| 7 | **D66'nın açıklanmamış etkisi yerinde kalır:** `MaxArcLength` büyütülüp tek vaka kapanır, kapıyı gevşeten ikinci etki başka bir parçada yanlış yazar | Yüksek | D75: R5.0c açıklanmadan `MaxArcLength` büyütülmez |
| 8 | **`OriginalTokenIsValid` gevşetilirse precision çöker:** 563 region'ın 418'ini kapatan kural, yol haritası risk #4'ün (özel isim) de ana savunması | Yüksek | 6.4'teki hipotez **ölçülür** (R5.0b), gevşetilmez; değişiklik gerekiyorsa R5.4b'de eşik olarak süpürülür ve yanlış düzeltme sıfır kısıtı altında değerlendirilir |
| 9 | **R5.1'in dikişi sessizce davranış değiştirir:** enjeksiyon noktaları açılırken varsayılan yol kayar | Orta | 8.1 madde 4: dikişten sonra tüm baseline'lar bit düzeyinde aynı olmalı; değişirse DUR |
| 10 | **Faz "başarısız" sayılıp eşikler zorlanır:** R5.5'in ön koşulları sağlanmayınca varsayılanı yine de çevirme baskısı | Orta | Bölüm 12: ölçülmüş bir "henüz değil" kabul edilebilir sonuçtur; kapanış tablosuna hangi koşulun hangi sayıyla sağlanmadığı yazılır |
| 11 | **R5.2 gereksiz yere yapılır:** L boyutunda bir iş ölçüm göstermeden başlar | Orta | D72'nin %20 eşiği; R5.0b tamamlanmadan R5.2 başlatılmaz |
| 12 | **Ground truth genişlemesi kapıyı kırar ve "regresyon" sanılır** | Orta | 7.4 kabulünde önceden yazılı: kırılması **beklenen** sonuçtur çünkü kapı ilk kez motorun dokunduğu yeri ölçüyor. Eşik indirilmez, ölçülen değer R5.4b'nin hedefi olur |

---

## 15. Devir promptları

### R5.0a
```
EpubFixer projesinde docs/phase-5-plan.md'deki R5.0 alt adım a kalemini uygulayacaksın.

Önce şunları oku:
- docs/phase-5-plan.md — bölüm 1, 3 (ön koşul), 4 (ortak kurallar, özellikle 4.4 ve 4.5),
  6.1, 6.2, 7.1, 13 (D68, D76, D77), 14 (risk #4)
- docs/phase-4-plan.md — bölüm 1b, 5.4, 5.4b, 10 (D63, D64)
- benchmarks/EpubFixer.QualityBenchmarks/QualityBenchmarkApplication.cs
- benchmarks/EpubFixer.QualityBenchmarks/QualityBenchmarkRunner.cs (özellikle satır 18-23 ve
  CreateClassBreakdowns, satır 252-280)
- benchmarks/EpubFixer.QualityBenchmarks/QualityBenchmarkGateEvaluator.cs
- benchmarks/EpubFixer.QualityBenchmarks/Models/QualityBenchmarkGateOptions.cs
- src/EpubFixer.Adapters/Ocr/LatticeOcrPlannerFactory.cs
- docs/baselines/quality-gate.json

Kurallar:
- ÖN KOŞUL: çalışma ağacı temiz ve suite yeşil olmalı. Değilse DUR ve bildir.
- Bu kalem src/ altındaki üretim kodunu DEĞİŞTİRMEZ. Değişim gerekiyorsa DUR ve bildir.
- SIRA ZORUNLU: (1) CreateClassBreakdowns'ın correct sayacını düzelt, (2) --ocr-engine
  bayrağını benchmark'a ekle, (3) sınıf kırılımını gate mekanizmasına tak, (4) iki motorla koş.
  Ters sırada koşarsan lattice'i bozuk aletle ölçersin.
- OCR sınıflarına eşik KOYMA (D76). Bu adımda yalnızca mekanizma ve Hyphenation'ın bugünkü
  ölçülmüş değeri girer.
- Legacy koşusunun gate sonucu PASS kalmalı. Değişen tek şey sınıf kırılımı sayıları olmalı ve
  her değişimi eski → yeni olarak raporla. Gate sonucu değişirse DUR.
- Lattice koşusu kapıyı kırarsa KAPIYI GEVŞETME (kural 4.5 / D77). Ölçülen değeri kaydet.
- Beklenen değerleri TAHMİN ETME. Önce koş, çıkan sayıyı oku, sonra yaz (kural 4.3).
- Kapsam R5.0a ile sınırlı: taksonomi yok, ground truth genişletme yok, eşik ayarı yok.

Bitirdiğinde: eklenen testler, legacy sınıf kırılımının eski/yeni sayıları, lattice koşusunun
tam sonucu (precision/recall/sınıf kırılımı/ProtectedChanged/ProtectedViolated), planda
güncellenmesi gereken bir şey olup olmadığı.
```

### R5.0b
```
EpubFixer projesinde docs/phase-5-plan.md'deki R5.0 alt adım b kalemini uygulayacaksın.
Bu, Faz 5'in en kritik ölçüm işidir: 119 kaybın her birinin hangi aşamada düştüğünü bulmak.

Önce şunları oku:
- docs/phase-5-plan.md — bölüm 1, 4, 6.3, 6.4, 6.5, 7.2, 13 (D68, D72), 14 (risk #8)
- docs/baselines/odun-kesmek.engine-diff.json (loss listesi — 119 kayıt)
- docs/baselines/odun-kesmek.lattice.json (ham koşu, reason histogram)
- src/EpubFixer.Core/Ocr/Lattice/CorrectionAcceptanceGate.cs (özellikle satır 31 ve 212-234)
- src/EpubFixer.Core/Ocr/Lattice/WordLatticeBuilder.cs
- src/EpubFixer.Core/Ocr/Lattice/LatticeDecoder.cs
- src/EpubFixer.Adapters/Ocr/Lattice/SymSpellLexiconMatcher.cs
- src/EpubFixer.Core/Ocr/OcrConfusionSet.cs

Kurallar:
- Bu kalem HİÇBİR ŞEYİ DÜZELTMEZ. Bulduğun sebep ne kadar bariz görünürse görünsün koda
  dokunma. Ürünü tek bir teşhis dosyasıdır.
- Her kayıp TAM OLARAK BİR sebep alır: zincirdeki İLK düşüş noktası. Sebep kodları 7.2'de.
- Atfedilemeyen kayıt SIFIR olmalı. Atfedemiyorsan bu bir bulgudur, gizleme — dosyaya yaz.
- 6.4'teki ChangedTokens hipotezini SINA: GateRejected:OriginalTokenIsValid alan her vaka için
  hangi token'ın geçerli sayıldığını kaydet. Hipotez doğru VARSAYARAK hareket etme; doğrula
  veya çürüt.
- Çıktı: docs/baselines/odun-kesmek.loss-taxonomy.json (vaka başına kayıt + histogram).
- Kapsam R5.0b ile sınırlı: ground truth genişletme yok, eşik değişikliği yok, D66 kök neden
  analizi yok (o R5.0c).

Bitirdiğinde: 119'un sebep histogramı, "hangi Faz 5 kalemi kaç vakaya dokunur" tablosu,
ChangedTokens hipotezinin sonucu, ve MatcherMissedTarget payının %20 eşiğinin (D72) üstünde
olup olmadığı.
```

### R5.0c
```
EpubFixer projesinde docs/phase-5-plan.md'deki R5.0 alt adım c kalemini uygulayacaksın.
Tek işin D66'nın açık bıraktığı soruyu cevaplamak.

Soru: "kol-1 ıı kta" parçası ham koşuda Leave/TooManyOrdinaryEdits, üretim koşusunda
Apply/Accepted alıyor. MaxArcLength (D65) doğru cevabın üretilememesini açıklıyor ama kapının
neden KABUL ettiğini açıklamıyor.

Önce şunları oku:
- docs/phase-5-plan.md — bölüm 4, 6.8, 7.3, 13 (D75), 14 (risk #7)
- docs/phase-4-plan.md — bölüm 10, D65 ve D66
- docs/baselines/odun-kesmek.engine-diff.json — rawVersusProduction düğümü
- docs/baselines/odun-kesmek.lattice.md — ilgili region kaydı
- src/EpubFixer.Core/Ocr/Lattice/CorrectionAcceptanceGate.cs (OrdinaryEdits, satır 80-95)
- src/EpubFixer.Core/Ocr/LatticeOcrCorrectionPlanner.cs

Kurallar:
- Bu kalem hiçbir davranış düzeltmez. Ürünü bir açıklamadır.
- İki koşunun kafesini, Top-1 yolunu, arc kırılımını ve OrdinaryEdits hesabını YAN YANA koy.
  Farkın nereden doğduğunu mekanizma düzeyinde göster.
- D66'daki "hazne/LM hyphenation sonrası kuruluyor" adayı bir HİPOTEZDİR. Doğrula veya çürüt.
- Açıklayamazsan uydurma: loss-taxonomy.json'a status "unexplained" yaz. Bu durumda R5.4b'de
  MaxArcLength büyütülmesi BLOKE olur (D75) ve bunu bitiş raporunda açıkça belirt.

Bitirdiğinde: farkın mekanizması, kanıtı, ve MaxArcLength'in R5.4b'de büyütülmesinin serbest
olup olmadığı.
```

### R5.0d
```
EpubFixer projesinde docs/phase-5-plan.md'deki R5.0 alt adım d kalemini uygulayacaksın:
ground truth'un OCR kolunu genişletmek. Bu fazın en emek yoğun kalemidir.

ÖN KOŞUL: R5.0b tamamlanmış ve docs/baselines/odun-kesmek.loss-taxonomy.json üretilmiş
olmalıdır. Aday listesi oradan gelir.

Önce şunları oku:
- docs/phase-5-plan.md — bölüm 4, 6.1, 6.10, 7.4, 13 (D70, D71, D76), 14 (risk #1, #2, #12)
- docs/phase-4-plan.md — bölüm 5.10, 10 (D63, D64)
- docs/baselines/odun-kesmek.engine-diff.json (KAYIP 119 / KAZANÇ 9 / ÇATIŞMA 1)
- docs/baselines/odun-kesmek.loss-taxonomy.json
- test-data/odun-kesmek/ground-truth.json (schemaVersion 2, 160 kayıt)
- benchmarks/EpubFixer.QualityBenchmarks/QualityBenchmarkDatasetLoader.cs
- benchmarks/EpubFixer.QualityBenchmarks/GroundTruthProposer.cs (--propose zaten var)
- benchmarks/EpubFixer.QualityBenchmarks/Models/GroundTruthDocument.cs, Models/OcrErrorClass.cs

Kurallar:
- Örnekleme RASTGELE DEĞİL: adaylar motorların gerçekten dokunduğu bölgelerden gelir (D70).
  Kaynak listesi 7.4'teki tabloda.
- LEGACY'NİN ÇIKTISI GROUND TRUTH DEĞİLDİR (D71). Her kaydı bağlamıyla (çevresindeki cümle)
  oku ve elle doğrula. Legacy'nin önerisi legacyProposal alanında ayrı durur; yanlışsa doğru
  değeri sen yaz ve yanlış olduğunu işaretle.
- Hedef: OCR kolu >= 60 kayıt, her sınıfta >= 5 (MissingSpace ve Mixed dahil — bugün sıfırlar).
- schemaVersion 2 -> 3. Loader şema 2'yi okumaya DEVAM ETMELİ; bunu kanıtlayan test yaz.
- Genişletmeden sonra benchmark'ı HER İKİ motorla koş ve yeni sınıf kırılımını kaydet.
- KAPI KIRILABİLİR — beklenen budur (risk #12). Eşiği İNDİRME (D77). Ölçülen değeri kaydet;
  o değer R5.4b'nin hedefidir.
- Kapsam R5.0d ile sınırlı: eşik ayarı yok, maliyet kalibrasyonu yok, motor değişikliği yok.

Bitirdiğinde: eklenen kayıt sayısı ve sınıf dağılımı, legacy'nin yanlış çıktığı vaka sayısı,
her iki motorun yeni sınıf kırılımı, kapı sonucu, ve şema uyumluluk testinin durumu.
```

### R5.1
```
EpubFixer projesinde docs/phase-5-plan.md'deki R5.1 kalemini uygulayacaksın:
confusion maliyet kalibrasyonu.

ÖN KOŞUL: R5.0d tamamlanmış (genişletilmiş ground truth) ve R5.0b'nin taksonomisi okunmuş
olmalı. Taksonomi "maliyet kaynaklı" (DecoderRankedOther) vaka göstermiyorsa DUR ve bildir.

Önce şunları oku:
- docs/phase-5-plan.md — bölüm 4, 6.5, 8, 13 (D68), 14 (risk #2, #3, #9)
- docs/ocr-correction-roadmap.md — R5.1 maddesi
- src/EpubFixer.Core/Ocr/OcrEditCostModel.cs
- src/EpubFixer.Core/Ocr/OcrConfusionSet.cs
- src/EpubFixer.Core/Ocr/WeightedEditAligner.cs (özellikle satır 126-172)
- src/EpubFixer.Core/Ocr/Lattice/CorrectionAcceptanceGate.cs (satır 10 — enjekte edilemeyen aligner)
- src/EpubFixer.Adapters/Ocr/LatticeOcrPlannerFactory.cs (satır 22)
- tests/EpubFixer.Tests/CoreLayeringTests.cs

Kurallar:
- ÖNCE DİKİŞ, SONRA TABLO (8.1). Dikiş adımlarından sonra TÜM baseline'lar bit düzeyinde aynı
  kalmalı. Değişirse dikiş davranış değiştirmiştir — DUR.
- Öğrenilmiş tablo ÇİFT BAŞINA maliyet taşır; bugün tek düz KnownGlyphSubstitution=0.25 var.
- Düzleştirme ZORUNLU ve yöntemi belgelenir. 60 kayıtlık tabandan sıfır frekanslı çiftler çıkar.
- HELD-OUT ZORUNLU: tablo yalnızca eğitim bölümünden öğrenilir, rapor held-out üzerinden verilir.
- Diskten tablo okuyan kod Adapters'a gider; Core yalnızca tipi tanır (CoreLayeringTests).
- Tabloyu VARSAYILAN YAPMA. Bayrak/parametre arkasında gelir; varsayılan kararı R5.4b'nin.
- A/B: Top-1'i düşürmez VE yanlış düzeltme sayısını artırmaz (kural 4.6). İkisinden biri
  bozuluyorsa tablo reddedilir.
- Kapsam R5.1 ile sınırlı: eşik değiştirme yok, MaxArcLength yok, trie yok.

Bitirdiğinde: dikişin baseline'ları değiştirmediğinin kanıtı, öğrenilmiş tablonun held-out
sonucu, A/B karşılaştırması, ve tablonun varsayılan yapılmaya hazır olup olmadığı.
```

### R5.3
```
EpubFixer projesinde docs/phase-5-plan.md'deki R5.3 kalemini uygulayacaksın:
lattice hattı için düzeltme inceleme raporu.

Önce şunları oku:
- docs/phase-5-plan.md — bölüm 4, 6.9, 10, 13 (D78)
- docs/phase-4-plan.md — bölüm 13 (Diagnostics sözleşmesi), 9.2 (R4.3 silme listesi)
- src/EpubFixer.Cli/OcrReconstruction/FullBookReaderPreview.cs (BAŞLANGIÇ NOKTASI DEĞİL —
  neden olmadığını 6.9 açıklıyor; oku ama genişletme)
- src/EpubFixer.Core/Ocr/IOcrCorrectionPlanner.cs (OcrCorrectionPlanResult.Diagnostics)
- src/EpubFixer.Core/Ocr/LatticeOcrCorrectionPlanner.cs
- src/EpubFixer.Core/Mutation/RegionMutationPlanner.cs (RegionMutationPlanResult)
- src/EpubFixer.Cli/Program.cs (debug-lattice raporu, satır 342-464)

Kurallar:
- FullBookReaderPreview.cs'e DOKUNMA (D78). O dosya eski motor zincirine bağlı ve R4.3'ün
  silme listesinde; ölmekte olan koda özellik eklenmez.
- Rapor ÇATIŞMA bölümüyle başlar (en tehlikeli sınıf), sonra KAZANÇ/KAYIP, sonra Apply'lar
  bağlamıyla, sonra Review, sonra Leave histogramı ve bilerek dokunulmayan kütle.
- Rapor artifacts/ altına yazılır (gitignore'lu); yalnızca özeti docs/baselines/'e gider.
- Kapsam R5.3 ile sınırlı: motor davranışı değişmez, eşik değişmez.

Bitirdiğinde: raporun hangi bölümleri içerdiği, full koşuda kaç düzeltme/Review/Leave
gösterdiği, ve engine-diff.json'ı komutla üretebilir hâle gelip gelmediği.
```

### R5.4a
```
EpubFixer projesinde docs/phase-5-plan.md'deki R5.4 alt adım a kalemini uygulayacaksın:
kapı eşiklerini LatticeOptions'a çıkarmak. Bu, D45'in Faz 3'ten beri açık olan borcudur.

Önce şunları oku:
- docs/phase-5-plan.md — bölüm 4, 6.6, 6.7, 6.8, 11.1, 13 (D74)
- docs/phase-4-plan.md — bölüm 13
- src/EpubFixer.Core/Ocr/Lattice/LatticeOptions.cs
- src/EpubFixer.Core/Ocr/Lattice/CorrectionAcceptanceGate.cs (tamamı)
- src/EpubFixer.Core/Ocr/Lattice/WordLatticeBuilder.cs (satır 60 ve 88-89)
- src/EpubFixer.Adapters/Ocr/Lattice/SymSpellLexiconMatcher.cs (satır 9-11)
- src/EpubFixer.Core/Ocr/Lattice/Models/DecodedPath.cs (EditCost vs Cost)

Kurallar:
- VARSAYILANLAR BUGÜNKÜ DEĞERLER. Bu adım davranış değiştirmez ve kanıtı tüm baseline'ların
  bit düzeyinde aynı kalmasıdır. Değişirse DUR.
- 6.6'daki dört sabit LatticeOptions'a çıkar: uzunluk oranı kuralları, "ıe"/"ie" kara listesi,
  ProperNameRisk'in büyük harf kuralı, IsValidOriginalToken'ın BookCount eşiği.
- 6.7'deki iki ölü alanı (MaxQueriesPerSpan, MaxMatchesPerSpan) ya gerçekten bağla ya kaldır.
  Ölü bırakırsan R5.4b'nin süpürmesi "etkisi yok" diye yanlış bilgi üretir.
- 6.8'deki MaxPathCost(EditCost) / MinMargin(Cost) asimetrisini ya düzelt ya KASITLI olduğunu
  belgele. Düzeltmek davranış değiştirir — o durumda DUR ve önce bildir.
- Kapsam R5.4a ile sınırlı: hiçbir eşik DEĞERİ değişmez, yalnızca yeri değişir.

Bitirdiğinde: çıkarılan eşiklerin listesi, baseline'ların değişmediğinin kanıtı, ölü alanlar
için verilen karar, ve asimetri hakkındaki bulgu.
```

### R5.4b
```
EpubFixer projesinde docs/phase-5-plan.md'deki R5.4 alt adım b kalemini uygulayacaksın:
eşik ve arama uzayı süpürmesi.

ÖN KOŞUL — hepsi gerekli:
- R5.0d tamamlanmış (genişletilmiş ground truth = hedef fonksiyonun tabanı)
- R5.4a tamamlanmış (süpürülebilir tek nesne)
- R5.0c'nin sonucu okunmuş: "unexplained" ise MaxArcLength BÜYÜTÜLEMEZ (D75)
Biri eksikse DUR ve bildir.

Önce şunları oku:
- docs/phase-5-plan.md — bölüm 4 (özellikle 4.5 ve 4.6), 6.3, 6.4, 6.6, 6.8, 11.2,
  13 (D75, D77), 14 (risk #3, #5, #6, #7, #8)
- docs/baselines/odun-kesmek.loss-taxonomy.json (süpürme listesi BURADAN türetilir)
- docs/baselines/odun-kesmek.engine-diff.json
- tests/EpubFixer.Tests/PerformanceBudgetTests.cs (satır 75, 114-115 — kısıtlar)

Kurallar:
- HEDEF FONKSİYONU ÖNCE YAZ: genişletilmiş ground truth üzerinde precision >= %98 kısıtı
  altında recall maksimizasyonu. Süpürmeden SONRA "şu metrik daha iyi görünüyordu" deme.
- Yanlış düzeltme sayısını sıfırın üstüne çıkaran hiçbir nokta seçilmez — ne kadar recall
  getirdiğine bakılmaksızın (kural 4.6).
- Kapıyı GEVŞETME yoluyla recall kazanma (D77). quality-gate.json eşikleri yalnızca yukarı.
- Süre her noktada ölçülür; 30 sn / 2.500 state ve 120 sn / legacy+35 sn KISITTIR, çıktı değil.
- Seçilen nokta held-out bölümde de doğrulanır (overfit koruması).
- Çıktı: docs/baselines/odun-kesmek.threshold-sweep.json — taranan noktalar, her noktada
  (precision, recall, yanlış düzeltme, süre, KAYIP) ve seçimin gerekçesi.
- Beklenen değerleri TAHMİN ETME (kural 4.3).

Bitirdiğinde: süpürülen parametreler ve aralıkları, eğri, seçilen nokta ve gerekçesi, yeni
KAYIP sayısı, ve R5.5'in beş ön koşulundan hangilerinin artık sağlandığı.
```

### R5.5
```
EpubFixer projesinde docs/phase-5-plan.md'deki R5.5 kalemini uygulayacaksın:
varsayılan motoru lattice'e çevirmek. Bu, Faz 4'ün yarım kalan R4.2c adımıdır.

ÖN KOŞUL — bölüm 12'deki beş koşulun HEPSİ sağlanmalı. Biri sağlanmıyorsa varsayılanı
ÇEVİRME; bulguyu raporla ve kapanış tablosunu doldur. Ölçülmüş bir "henüz değil" kabul
edilebilir sonuçtur.

Önce şunları oku:
- docs/phase-5-plan.md — bölüm 2 (kabul), 4, 12, 13 (D73), 14 (risk #10)
- docs/phase-4-plan.md — bölüm 8.3 (R4.2c'nin adımları AYNEN geçerli), 10 (D60, D61, D65, D66)
- docs/baselines/odun-kesmek.threshold-sweep.json
- docs/baselines/odun-kesmek.engine-diff.json
- tests/EpubFixer.Tests/MorphologyCallTraceTests.cs (golden SHA-256 — kasten değişecek)
- tests/EpubFixer.Tests/OcrMutationBaselineTests.cs
- src/EpubFixer.Cli/Program.cs (satır 64-67, 978-982 — --ocr-engine)

Kurallar:
- ÖLÇ, SONRA YAZ. Golden hash kasten değişir; yeni değeri ÖLÇEREK yaz, tahmin etme.
  Değişen her beklenen değeri bitiş raporunda gerekçelendir (kural 4.3).
- Fark raporunu üret ve ÜÇ LİSTENİN TAMAMINI elle incele (KAZANÇ / KAYIP / ÇATIŞMA).
  ÇATIŞMA en tehlikeli sınıftır, önce onu incele.
- Yanlış düzeltme bulursan: eşik oynatarak kapatma. DUR, bildir, varsayılanı legacy'ye al.
- --ocr-engine legacy seçilebilir kalır. R4.3 BU FAZDA YAPILMAZ (D73).
- Çıktı EPUB'a yanlış düzeltme yazan bir sürüm commit edilmez.

Bitirdiğinde: beş ön koşulun her birinin ölçülmüş değeri, varsayılanın çevrilip çevrilmediği,
çevrilmediyse hangi koşulun hangi sayıyla sağlanmadığı, ve güncellenen baseline'ların listesi.
```

---

## 16. Faz 5'in Faz 6 ile sözleşmesi

- **`odun-kesmek.loss-taxonomy.json` R6.2'nin ("kalan hata raporu") doğrudan girdisidir.** Faz 5
  sonunda hâlâ `RegionNotDetected` olarak atfedilen vakalar, motorun değil **dedektörün** sınırıdır
  ve Faz 5'in kapsamı dışındadır — R6.2 bu kütleyi sınıflandırır.
- **R4.3 (eski yolların kaldırılması) Faz 6'ya devredilir.** Ön koşulu (KAYIP listesinin boşalması)
  ve "iki sürüm boyunca stabil koşu" şartı en erken R5.5 başarılı olduktan sonra değerlendirilebilir.
  O zamana kadar `legacy` yalnızca A/B için yaşar; ona yeni özellik eklenmez, hata düzeltilmez.
- **`odun-kesmek.threshold-sweep.json` eğrisi R6.1'in (second-pass OCR) fayda tahminidir.** Eğrinin
  precision ≥ %98 kısıtı altında ulaştığı recall tavanı, post-correction'ın bu kitapta
  ulaşabileceği sınırdır; R6.1'in gerekçesi o tavanın yetersizliğidir, öznel bir kalite hissi değil.
- **Genişletilmiş ground truth (schemaVersion 3) ikinci bir kitaba geçmenin ön koşuludur.** Bugünkü
  bütün ölçüm `odun-kesmek` üzerinedir; tek kitaba overfit riski Faz 5'in kapsamında ele alınmadı ve
  açık bir borçtur.
- **`OcrEditCostModel`'in öğrenilmiş tablosu kitaba özeldir.** Başka bir kitapta yeniden öğrenilmeli
  veya genel bir tabana düşülmelidir; hangisi olduğuna karar verilmedi.
