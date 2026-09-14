# Faz 5 — İş kuyruğu

> Bu belge [phase-5-plan.md](phase-5-plan.md)'in **operasyonel karşılığıdır**. Plan "neden ve ne"
> sorularını cevaplar; bu belge "sırada ne var, kim neyi devralır" sorusunu cevaplar.
>
> **Neden ayrı bir belge:** Planın R5.1 / R5.3 / R5.4 / R5.5 kalemleri, tek bir oturumda
> bitirilemeyecek kadar büyük yazılmıştı (D79). Her biri burada tek commit'lik kalemlere bölündü.
> Planın kalem bölümleri (8, 10, 11, 12) **sözleşme ve kabul** tanımı olarak geçerliliğini korur;
> bir kalemi devralan agent önce plandaki ilgili bölümü, sonra buradaki satırını okur.

---

## 1. İş boyutu bütçesi (D79)

Bir kalem şu dördünü birden sağlamalıdır. Sağlamıyorsa **bölünmemiştir**:

| # | Kural | Nasıl kontrol edilir |
|---|---|---|
| 1 | **Tek commit** | Kalem bittiğinde tek bir commit çıkar |
| 2 | **En fazla bir full-book koşusu** | İkiden fazla gerekiyorsa ya kalem büyüktür ya altyapı eksiktir |
| 3 | **Tek dosya ailesi** | Bir port + implementasyonu + testi = bir aile. İki katmanı aynı anda değiştiren kalem bölünür |
| 4 | **Tek doğrulanabilir çıktı** | "Baseline değişmedi" **veya** "şu dosya üretildi" **veya** "şu sayı ölçüldü" — üçünden biri |

**DUR kuralı:** Bir agent kalemin bu bütçeyi aşacağını anlarsa **işi yarım bırakmaz ve büyütmez** —
durur, ne bulduğunu ve kalemin nasıl bölünmesi gerektiğini bildirir. Yarım kalmış büyük bir kalem,
hiç başlanmamış iki küçük kalemden pahalıdır.

**Neden full-book koşusu birim:** lattice pass'i ~28 sn, tam suite ~3 dk. Bir kalem iki koşuya
sığıyorsa bir oturuma da sığar. Sığmıyorsa ölçüm altyapısı eksiktir ve **önce o yazılır**
(R5.4b-0'ın varlık sebebi budur).

### 1b. Elle etiketleme kalemleri parti hâlinde bölünür (D79'un eki)

Yukarıdaki dört ölçüt **kod kalemleri** için yeterlidir; **elle etiketleme** kalemleri için değildir.
Etiketleme tek commit'e, tek dosyaya ve tek çıktıya sığar ama süresi kayıt sayısıyla doğrusal artar.

R5.0d bunu ölçtü: 129 kaydın elle doğrulanması **kesintisiz ~2 saat** sürdü ve oturum dışarıdan
durduruldu. İş tamamlanmıştı, ama bu şans eseriydi — 15 dakika erken durdurulsaydı yarım kalmış,
commit'lenmemiş ve devredilemez bir ground truth kalırdı.

**Kural:** Elle etiketleme kalemi **≤ 25 kayıtlık partilere** bölünür. Her parti kendi commit'i,
kendi ölçümü. Parti sınırı keyfi değil: doğrulama hızı ~1 kayıt/dk civarında ve 25 kayıt bir
oturumun etiketleme payına sığıyor.

**Neden önemli:** etiketleme işi geri alınamaz emek harcar. Kod kalemi yarım kalırsa `git checkout`
ile atılır ve hiçbir şey kaybolmaz; yarım kalan etiketleme, insanın okuduğu 60 cümlenin çöpe
gitmesidir.

---

## 2. Kuyruk

Durum kodları: ✅ bitti · 🔄 devam ediyor · ⬜ hazır · 🔒 ön koşulu bekliyor · ⛔ düşürüldü

| # | Kalem | Durum | Ön koşul | Çıktı |
|---|---|---|---|---|
| | **R5.0 — Ölçüm tabanı ve teşhis** | | | |
| 1 | R5.0a — benchmark motor anahtarı + sınıf kırılımlı kapı | ✅ `73b1618` | — | iki motorun OCR-stage ölçümü |
| 2 | R5.0b — 119 kaybın sebep taksonomisi | ✅ `d8d8f24` | — | `loss-taxonomy.json` |
| 3 | R5.0c — D66'nın kök nedeni | ✅ `d512427` | R5.0b | `rawVersusProduction: explained` |
| 4 | R5.0d — ground truth schemaVersion 3 | ✅ `33d4b87` | R5.0b | 288 kayıt, OCR kolu 112; iki motor yeniden ölçüldü |
| 5 | R5.0e — `MissingSpace` / `SpuriousSpace` boşluğunu karara bağla | ⬜ | R5.0d | karar: doldur, ertele veya kapsam dışı yaz |
| 5b | R5.0f — kapı profilini yeni ölçüm tabanına taşı | ⬜ | R5.0d + **D80 onayı** | karşılaştırılabilir kapı |
| | **R5.1 — Maliyet kalibrasyonu** | | | |
| 6 | R5.1a — `IOcrConfusionSet` portu | ⬜ | — | baseline değişmedi |
| 7 | R5.1b — aligner enjeksiyon dikişi | 🔒 | R5.1a | baseline değişmedi |
| 8 | R5.1c — çift-başına maliyet sözleşmesi | 🔒 | R5.1b | baseline değişmedi |
| 9 | R5.1d — kalibrasyon aracı (motora bağlanmaz) | 🔒 | R5.0d, R5.1c | `edit-costs.json` |
| 10 | R5.1e — tabloyu bayrak arkasında bağla + A/B | 🔒 | R5.1d | A/B ölçümü |
| | **R5.2** | ⛔ düşürüldü (D72, `b438025`) | — | `MatcherMissedTarget` %8,4 |
| | **R5.3 — İnceleme raporu** | | | |
| 11 | R5.3a — rapor iskeleti + Apply bölümü | ⬜ | R5.0a | rapor üretildi |
| 12 | R5.3b — Review + Leave + dokunulmayan kütle | 🔒 | R5.3a | rapor bölümleri |
| 13 | R5.3c — iki motor farkı komutla üretilir | 🔒 | R5.3a | `engine-diff.json` komutla |
| | **R5.4a — Eşikleri tek nesneye çıkar** | | | |
| 14 | R5.4a-1 — dört kapı eşiği → `LatticeOptions` | ⬜ | — | baseline değişmedi |
| 15 | R5.4a-2 — iki ölü alan: bağla veya kaldır | 🔒 | R5.4a-1 | baseline değişmedi |
| 16 | R5.4a-3 — `MaxPathCost`/`MinMargin` asimetrisi | 🔒 | R5.4a-1 | belgelendi veya karar |
| | **R5.4b — Süpürme** | | | |
| 17 | R5.4b-0 — süpürme koşum altyapısı | 🔒 | R5.4a-1 | tek nokta ölçülebiliyor |
| 18 | R5.4b-1 — `OriginalTokenIsValid` ekseni (24 vaka) | 🔒 | R5.4b-0 | eksen eğrisi |
| 19 | R5.4b-2 — arama uzayı ekseni (28 vaka) | 🔒 | R5.4b-0 | eksen eğrisi |
| 20 | R5.4b-3 — skor ekseni (25 vaka) | 🔒 | R5.4b-0 | eksen eğrisi |
| 21 | R5.4b-4 — birleşik nokta seçimi + held-out | 🔒 | 18, 19, 20 | `threshold-sweep.json` |
| | **R5.5 — Varsayılanı çevir** | | | |
| 22 | R5.5a — beş ön koşulu ölç (çevirme) | 🔒 | R5.4b-4 | karar verisi |
| 23 | R5.5b — anahtarı çevir + yeniden ölç | 🔒 | R5.5a hepsi yeşil | yeni baseline'lar |

**Paralel yürüyebilenler:** (6) R5.1a ile (14) R5.4a-1 ile (11) R5.3a — üçü ayrı dosya ailelerine
dokunur. (5) R5.0e ve (5b) R5.0f de bunlardan bağımsızdır.

**Kritik yol:** R5.0f → R5.1a → R5.1b → R5.1c → R5.1d → R5.4b-0 → eksenler → R5.4b-4 → R5.5a → R5.5b

---

## 2b. R5.0d ne ölçtü — Faz 5'in başlangıç noktası

R5.0d ground truth'u 160 → 288 kayda çıkardı (176 `Hyphenation` + **112 OCR kolu**, eskiden 12) ve
iki motoru da bu yeni taban üzerinde yeniden ölçtü. Sonuç, D68/D69'un dayandığı varsayımı **ilk kez
doğrudan** doğruluyor:

| | Legacy | Lattice |
|---|---:|---:|
| Precision | %98,85 | %98,25 |
| **Recall** | **%89,93** | **%58,33** |
| Doğru düzeltilen | 259 | 168 |
| Yanlış düzeltilen | 3 | 3 |
| Ertelenen | 26 | **117** |
| `ProtectedChanged` / `ProtectedViolated` | 0 / 0 | 0 / 0 |

**Recall farkı 31,6 puan.** R5.4b'nin kapatmaya çalışacağı mesafe budur ve artık tek bir sayıdır.

Üç bulgu:

1. **Eski kapı hiçbir şey ölçmüyormuş.** 12 kayıtlık tabanda lattice **geçiyordu** (recall %93,12);
   112 kayıtlık tabanda %58,33'e düşüyor. Motor değişmedi — ölçüm aleti değişti. D64'ün tespiti
   sayıya dönüştü.
2. **Legacy de altın standart değil (D71 doğrulandı).** Yeni tabanda legacy'nin **3 yanlış
   düzeltmesi** görünür oldu; en çarpıcısı `olın` → `olan` (doğrusu **`John`**). D71'in "legacy'nin
   çıktısı ground truth sayılmaz" kuralı olmasaydı bu üç hata doğru cevap olarak sabitlenecekti.
3. **Kapı artık her iki motorda da kırmızı.** Bu beklenen sonuçtu (plan risk #12) ama bir yan etkisi
   var: eşikler eski tabana göre ayarlandığı için **legacy bile geçemiyor**. Bu, D80'i gerektiriyor
   (bkz. bölüm 2c).

### 2c. Çözülmesi gereken gerilim: D77 kapıyı kilitledi (D80 önerisi)

Kapının bugünkü eşikleri: precision ≥ %98, recall ≥ %92,5, `ClassRecall:Hyphenation` = %100.
Bunlar **160 kayıtlık eski taban** üzerinde ölçülmüştü. Yeni tabanda legacy %89,93, lattice %58,33
— yani **hiçbir motor geçemiyor ve geçemeyecek.**

D77 diyor ki: "eşikler yalnızca yukarı hareket edebilir." Harfiyen uygulanırsa kapı kalıcı olarak
kırmızıdır ve R5.4b'nin yeşil bir hedefi yoktur.

**Gerilimin kaynağı:** eşik, *aynı ölçümün* tekrarları hakkında bir iddiadır. Ground truth değişince
ölçüm değişti; %92,5 artık daha sıkı bir çıta değil, **başka bir çıta**. D77 gevşetmeyi yasaklamak
için kondu ve o amacı hâlâ geçerli — ama taban değişimini gevşetme saymak, doğru ölçüme geçmeyi
cezalandırır.

**D80 önerisi:** Ölçüm tabanı değiştiğinde eşikler **yeni taban üzerinde yeniden kurulur**; eski
profil tabanıyla birlikte dosyada arşivlenir. Üç koruma:

1. Yeni eşik **seçilmez, ölçülür**: iyi olan motorun (bugün legacy) ölçülmüş değeri alınır.
   Aşağı yuvarlanmaz.
2. Eski profil `supersededProfiles` altında **tabanıyla** saklanır — değişim denetlenebilir kalır.
3. D77 **taban içinde aynen geçerlidir**: yeniden kurulduktan sonra eşikler yalnızca yukarı.

Bu, lattice için kapıyı "legacy'ye yetiş" koşuluna çevirir — Faz 5'in hedefinin tam olarak kendisi.

> **D80 onay bekliyor.** Bu, plan bölüm 13'ün "agent kendi başına karara varmaz" kuralına giren bir
> karardır: kapı eşiğine dokunuyor. Onaylanana kadar R5.0f başlamaz ve R5.4b'nin hedefi tanımsızdır.

---

## 3. Taksonomi → kalem eşlemesi

R5.0b'nin ölçtüğü 119 kaybın hangi kaleme düştüğü. **Süpürme eksenlerinin boyutu buradan gelir:**

| Sebep | Vaka | Hangi kalem | Not |
|---|---:|---|---|
| `RegionNotDetected` | 32 | **kapsam dışı** | Dedektörün sınırı, motorun değil → R6.2 |
| `TargetNotInLattice` | 27 | R5.4b-2 | `MaxArcLength` / `BudgetCap` |
| `GateRejected:OriginalTokenIsValid` | 24 | R5.4b-1 | Hipotez 24/24 doğrulandı |
| `DecoderRankedOther` | 23 | R5.1e, R5.4b-3 | maliyet tablosu + `Lambda` |
| `MatcherMissedTarget` | 10 | ⛔ | 4'ü algoritma ıskası, 6'sı OOV — D72 |
| `GateRejected:TooManyOrdinaryEdits` | 2 | R5.4b-3 | `MaxOrdinarySubstitutions` |
| `WindowSkippedTooLong` | 1 | R5.4b-2 | `MaxWindowLength` |

**Ulaşılabilir tavan:** 32 vaka kapsam dışı, 10 vaka düşürüldü → Faz 5'in dokunabileceği en fazla
**77 vaka**. KAYIP 119 → 42'nin altına inemez. Bu sayı R5.5a'nın 2. koşulunu (kitap sağlığı)
değerlendirirken hatırlanmalıdır: lattice'in legacy'yi geçmesi bu fazda **beklenmemektedir**.

**R5.0d'nin ölçümüyle birlikte okunduğunda:** lattice recall %58,33, legacy %89,93 (bölüm 2b).
77 vakanın tamamı kurtarılsa bile lattice legacy'ye *yaklaşır*, geçmez. R5.4b'nin başarı ölçütü
"legacy'yi geç" değil, **"aradaki 31,6 puanın ölçülebilir bir kısmını kapat ve precision'ı
%98'in üstünde tut"**tur.

---

## 4. Devir promptu

Her kalem için **ortak gövde + kalem deltası** kullanılır. Ortak gövde bir kez yazılır; kalem
promptu yalnızca farkı taşır.

### 4.1 Ortak gövde (her devirde aynen kullanılır)

```
EpubFixer projesinde docs/phase-5-tasks.md'deki <KALEM> kalemini uygulayacaksın.

Önce şunları oku:
- docs/phase-5-tasks.md — bölüm 1 (iş boyutu bütçesi), 2 (kuyruk), 3 (taksonomi eşlemesi)
- docs/phase-5-plan.md — bölüm 4 (ortak kurallar) ve <PLAN BÖLÜMÜ>
- <KALEME ÖZEL DOSYALAR>

Her kalemde geçerli kurallar:
- ÖN KOŞUL: çalışma ağacı temiz ve suite yeşil olmalı. Değilse DUR ve bildir.
- Test-first: kırmızı test → minimum kod → refactor. Testi olmayan üretim kodu yazma.
- Beklenen değerleri TAHMİN ETME. Önce koş, çıkan sayıyı oku, sonra yaz (plan kural 4.3).
- Kapıyı GEVŞETME, eşik İNDİRME (plan kural 4.5 / D77).
- Yanlış düzeltme sayısını artıran hiçbir değişiklik kabul edilmez (plan kural 4.6).
- İŞ BOYUTU: tek commit, en fazla bir full-book koşusu, tek dosya ailesi, tek çıktı.
  Bu bütçeyi aşacağını anlarsan DUR — işi büyütme, kalemin nasıl bölüneceğini bildir.
- Kapsam yalnızca bu kalem. Fark ettiğin başka sorunları düzeltme, bitiş raporunda
  "gözlem" olarak yaz.

Bitirdiğinde: ne değişti, hangi testler eklendi, <KALEME ÖZEL ÇIKTI>, ve planda/kuyrukta
güncellenmesi gereken bir şey olup olmadığı.
```

### 4.2 Kalem deltaları

---

#### R5.0e — `MissingSpace` / `SpuriousSpace` boşluğunu karara bağla
- **PLAN BÖLÜMÜ:** 7.4, 13 (D70, D71)
- **DOSYALAR:** `test-data/odun-kesmek/ground-truth.json`, `docs/baselines/odun-kesmek.loss-taxonomy.json`
- **BAĞLAM:** R5.0d bu iki sınıfı dolduramadı ve sebebini yazdı: **D70'in aday havuzunda yoklar.**
  Havuz motorların dokunduğu yerlerden oluşuyor; hiçbir motor `MissingSpace`'e dokunmadığı için
  o vakalar diff'e hiç girmiyor. Bu bir ihmal değil, D70'in doğrudan sonucu.
- **YAP:** Üç seçenekten birini **gerekçeyle** seç:
  (a) havuz dışına çıkıp elle bul — D70'den sapma olur, gerekçesi yazılır;
  (b) Faz 6'ya ertele — ölçüm boşluğu olarak kaydedilir;
  (c) kapsam dışı yaz — kitapta bu sınıf gerçekten yoksa.
  Önce **(c)'yi sına**: kitapta `MissingSpace` örneği var mı? Yoksa karar kendiliğinden verilir.
- **DUR:** Kayıt eklemeye karar verirsen ≤25 kayıtlık parti (bölüm 1b) ve her kayıt
  `verifiedBy: "manual"` (D71).
- **ÇIKTI:** verilen karar ve gerekçesi; kayıt eklendiyse yeni sınıf dağılımı.

---

#### R5.0f — Kapı profilini yeni ölçüm tabanına taşı
- **PLAN BÖLÜMÜ:** 13 (D77); **kuyruk bölüm 2c (D80)**
- **DOSYALAR:** `docs/baselines/quality-gate.json`,
  `benchmarks/EpubFixer.QualityBenchmarks/QualityBenchmarkGateEvaluator.cs`
- **ÖN KOŞUL:** **D80 onaylanmış olmalı.** Onaylanmadıysa bu kalem BAŞLAMAZ — kapı eşiğine
  dokunmak agent'ın tek başına vereceği bir karar değildir (plan bölüm 13).
- **BAĞLAM:** Eşikler 160 kayıtlık eski taban üzerinde ölçülmüştü; 288 kayıtlık yeni tabanda
  **legacy bile geçemiyor** (recall %89,93 < %92,5). Kapı kalıcı kırmızı ve R5.4b'nin yeşil
  hedefi yok.
- **YAP:** Eşikleri yeni taban üzerinde yeniden kur. Üç koruma (bölüm 2c): yeni eşik **ölçülür,
  seçilmez** (legacy'nin değeri, aşağı yuvarlanmadan); eski profil `supersededProfiles` altında
  **tabanıyla** arşivlenir; D77 taban içinde aynen geçerli kalır.
- **DUR:** Eşiği legacy'nin ölçülmüş değerinin **altına** koyma. Lattice'in geçmesi için indirme —
  lattice'in geçmesi R5.4b'nin işi, kapının değil.
- **ÇIKTI:** yeni profil, arşivlenen eski profil, ve legacy'nin kapıdan geçtiğinin doğrulanması.

---

#### R5.1a — `IOcrConfusionSet` portu
- **PLAN BÖLÜMÜ:** 6.5, 8.1 madde 1
- **DOSYALAR:** `src/EpubFixer.Core/Ocr/OcrConfusionSet.cs`, `WeightedEditAligner.cs`,
  `tests/EpubFixer.Tests/OcrConfusionSetTests.cs`
- **YAP:** `OcrConfusionSet`'in yüzeyini (`IsKnownConfusion`, `Replacements`, `IsGarbageGlyph`)
  bir port arkasına al. Bugünkü `Default` o portun ilk implementasyonu olur. Private ctor
  kısıtı kalkar ama **varsayılan davranış birebir aynı kalır**.
- **DUR:** Herhangi bir baseline değişirse — dikiş davranış değiştirmiş demektir.
- **ÇIKTI:** tüm baseline'ların bit düzeyinde aynı kaldığının kanıtı.

---

#### R5.1b — Aligner enjeksiyon dikişi
- **PLAN BÖLÜMÜ:** 6.5, 8.1 madde 2–3
- **DOSYALAR:** `src/EpubFixer.Core/Ocr/Lattice/CorrectionAcceptanceGate.cs` (satır 10),
  `src/EpubFixer.Adapters/Ocr/LatticeOcrPlannerFactory.cs` (satır 22)
- **YAP:** Kapının `private readonly WeightedEditAligner aligner = new();` satırını enjekte
  edilebilir hâle getir (`IWeightedEditAligner`). Factory'ye maliyet modeli / karışım kümesi
  parametresi ekle; varsayılan bugünkü `Default`.
- **DUR:** Baseline değişirse DUR. Tablo öğrenme bu kalemde YOK.
- **ÇIKTI:** baseline'ların değişmediğinin kanıtı.

---

#### R5.1c — Çift-başına maliyet sözleşmesi
- **PLAN BÖLÜMÜ:** 6.5, 8.2
- **DOSYALAR:** `src/EpubFixer.Core/Ocr/OcrEditCostModel.cs`,
  `src/EpubFixer.Core/Ocr/WeightedEditAligner.cs` (satır 126–172)
- **YAP:** Bugün tüm bilinen karışımlar tek `KnownGlyphSubstitution = 0.25` ödüyor. Sözleşmeyi
  **çift başına** maliyet taşıyacak şekilde genişlet; çift için kayıt yoksa bugünkü düz değere
  düş (backoff). **Boş tablo = bugünkü davranış.**
- **DUR:** Baseline değişirse DUR — boş tabloyla davranış birebir aynı olmalı.
- **ÇIKTI:** boş tabloyla baseline'ın değişmediği + dolu tabloyla farklı maliyet çıktığı testi.

---

#### R5.1d — Kalibrasyon aracı
- **PLAN BÖLÜMÜ:** 8.3
- **DOSYALAR:** `benchmarks/` altında yeni araç, `test-data/odun-kesmek/ground-truth.json`
- **YAP:** Ground truth'un (bozuk → doğru) çiftlerini `WeightedEditAligner` ile hizala, karakter
  karışım sayımlarını çıkar, düzleştir, `-log` maliyete çevir,
  `docs/baselines/odun-kesmek.edit-costs.json` yaz.
- **ZORUNLU:** düzleştirme yöntemi ve parametresi belgelenir; held-out bölme yapılır ve rapor
  held-out üzerinden verilir; aynı girdi → aynı dosya (determinizm).
- **DUR:** Bu kalem tabloyu **motora bağlamaz**. Üretim kodu değişmez.
- **ÇIKTI:** `edit-costs.json` + held-out raporu.

---

#### R5.1e — Tabloyu bayrak arkasında bağla ve A/B ölç
- **PLAN BÖLÜMÜ:** 8.4
- **DOSYALAR:** `LatticeOcrPlannerFactory`, `docs/baselines/odun-kesmek.edit-costs.json`
- **YAP:** Öğrenilmiş tabloyu bir bayrak/parametre arkasında motora bağla ve A/B ölç.
  Taksonomideki `DecoderRankedOther` (23 vaka) bu kalemin hedef kütlesidir.
- **DUR:** Tabloyu VARSAYILAN YAPMA — o kararı R5.4b-4 verir. Top-1 düşerse veya yanlış
  düzeltme artarsa tablo reddedilir (plan 8.4).
- **ÇIKTI:** A/B tablosu: 23 vakanın kaçı kurtarıldı, yanlış düzeltme sayısı, süre.

---

#### R5.3a — Rapor iskeleti ve Apply bölümü
- **PLAN BÖLÜMÜ:** 6.9, 10, 13 (D78)
- **DOSYALAR:** yeni rapor dosyası (Cli), `src/EpubFixer.Core/Ocr/IOcrCorrectionPlanner.cs`
- **YAP:** Lattice hattı için yeni bir Markdown rapor. Bu kalemde yalnızca **Apply** bölümü:
  her uygulanan düzeltme öncesi/sonrası cümle bağlamıyla.
- **DUR:** `FullBookReaderPreview.cs`'e DOKUNMA (D78) — eski motor zincirine bağlı ve R4.3'ün
  silme listesinde.
- **ÇIKTI:** `artifacts/` altına yazılan rapor + kaç Apply gösterdiği.

---

#### R5.3b — Review, Leave ve dokunulmayan kütle
- **PLAN BÖLÜMÜ:** 10
- **DOSYALAR:** R5.3a'nın rapor dosyası, `RegionMutationPlanner` (`RegionMutationPlanResult`)
- **YAP:** Rapora üç bölüm ekle: `Review` kararları, `Leave` sebep histogramı + en sık sebebin
  örnekleri, ve motorun **bilerek dokunmadığı** kütle (`SkippedTooLong`, `BudgetExceeded`,
  atlanan bölge düzeltmeleri).
- **ÇIKTI:** her bölümün full koşudaki sayıları.

---

#### R5.3c — İki motor farkı komutla üretilir
- **PLAN BÖLÜMÜ:** 10
- **DOSYALAR:** R5.3a'nın rapor dosyası, `docs/baselines/odun-kesmek.engine-diff.json`
- **YAP:** KAZANÇ / KAYIP / ÇATIŞMA üçlüsünü komutla üret. `engine-diff.json` elle değil
  komutla üretilebilir hâle gelir. **ÇATIŞMA bölümü raporda en üstte** (en tehlikeli sınıf).
- **ÇIKTI:** komutla üretilen dosyanın mevcut `engine-diff.json` ile tutarlılığı.

---

#### R5.4a-1 — Dört kapı eşiğini `LatticeOptions`'a çıkar
- **PLAN BÖLÜMÜ:** 6.6, 11.1 madde 1, 13 (D74)
- **DOSYALAR:** `src/EpubFixer.Core/Ocr/Lattice/LatticeOptions.cs`,
  `CorrectionAcceptanceGate.cs` (satır 138–143, 174–190, 192–200, 205–210)
- **YAP:** Dört sabiti alan olarak çıkar: uzunluk oranı kuralları (< 3, 1,5 / 0,75),
  `"ıe"`/`"ie"` kara listesi, `ProperNameRisk`'in büyük harf kuralı,
  `IsValidOriginalToken`'ın `BookCount` eşiği.
- **DUR:** **Hiçbir eşik DEĞERİ değişmez, yalnızca yeri değişir.** Baseline değişirse DUR.
- **ÇIKTI:** çıkarılan eşiklerin listesi + baseline'ların değişmediğinin kanıtı.

---

#### R5.4a-2 — İki ölü alanı bağla veya kaldır
- **PLAN BÖLÜMÜ:** 6.7, 11.1 madde 2
- **DOSYALAR:** `LatticeOptions.cs`, `WordLatticeBuilder.cs` (satır 60),
  `src/EpubFixer.Adapters/Ocr/Lattice/SymSpellLexiconMatcher.cs` (satır 9–11)
- **YAP:** `MaxQueriesPerSpan` hiç okunmuyor; `MaxMatchesPerSpan` iki yerde farklı değerlerle
  duruyor (options 6, matcher 16). Ya gerçekten bağla ya kaldır.
- **NEDEN:** Ölü kalırlarsa R5.4b'nin süpürmesi "etkisi yok" sonucu üretir — bu yanlış bilgidir.
- **ÇIKTI:** verilen karar ve gerekçesi.

---

#### R5.4a-3 — `MaxPathCost` / `MinMargin` asimetrisi
- **PLAN BÖLÜMÜ:** 6.8, 11.1 madde 3
- **DOSYALAR:** `CorrectionAcceptanceGate.cs` (satır 66, 72),
  `src/EpubFixer.Core/Ocr/Lattice/Models/DecodedPath.cs`
- **YAP:** `MaxPathCost` `EditCost`'a (yalnız Word arc'ları), `MinMargin` tam `Cost`'a (edit + LM)
  uygulanıyor. Bu asimetriyi **belgele**.
- **DUR:** Düzeltmek davranış değiştirir — düzeltilmesi gerektiğini düşünüyorsan DUR ve önce
  bildir; karar plan bölüm 13'e eklenir.
- **ÇIKTI:** asimetrinin kasıtlı mı yoksa kaza mı olduğu bulgusu.

---

#### R5.4b-0 — Süpürme koşum altyapısı
- **PLAN BÖLÜMÜ:** 11.2
- **DOSYALAR:** `benchmarks/` altında yeni harness
- **YAP:** İki katmanlı ölçüm:
  - **Katman 1 (ucuz, süpürme için):** `BookKnowledge` **bir kez** kurulur, sonra N
    `LatticeOptions` noktası aynı bilgi tabanı üzerinde koşulur. Her nokta için
    R5.0b'nin 119 kayıp vakası + mevcut Apply'lar üzerinden (kurtarılan kayıp, yeni yanlış
    Apply, süre) ölçülür.
  - **Katman 2 (pahalı, yalnız finalistler için):** tam benchmark koşusu.
- **ÖLÇ:** 28,3 sn'nin ne kadarı bilgi tabanı kurulumu, ne kadarı lattice pass'i — bu sayı
  katman 1'in ne kadar kazandırdığını söyler ve bitiş raporuna yazılır.
- **KABUL:** **tek bir nokta** uçtan uca ölçülebiliyor ve sonuç tekrarlanabilir. Bu kalemde
  süpürme YAPILMAZ.
- **ÇIKTI:** harness + tek nokta ölçümü + bilgi tabanı / lattice pass süre ayrışması.

---

#### R5.4b-1 — `OriginalTokenIsValid` ekseni (24 vaka)
- **PLAN BÖLÜMÜ:** 6.4, 11.2; **kuyruk bölüm 3**
- **DOSYALAR:** R5.4b-0'ın harness'ı, `loss-taxonomy.json`
- **YAP:** R5.4a-1'in çıkardığı `BookCount` eşiğini ve kuralın kapsamını tek eksen olarak süpür.
  Hedef kütle: taksonomideki 24 `GateRejected:OriginalTokenIsValid` vakası (hipotez 24/24
  doğrulandı: garbage glyph soyulunca ortaya çıkan temiz token geçerli sayılıyor).
- **DUR:** Bu kural 563 region'ın 418'ini kapatıyor ve yol haritası risk #4'ün (özel isim) ana
  savunması. Yanlış düzeltme sayısını sıfırın üstüne çıkaran hiçbir nokta önerilmez.
- **ÇIKTI:** eksen eğrisi — her nokta için (kurtarılan kayıp, yeni yanlış Apply, süre).

---

#### R5.4b-2 — Arama uzayı ekseni (28 vaka)
- **PLAN BÖLÜMÜ:** 6.3, 11.2, 13 (D75)
- **DOSYALAR:** R5.4b-0'ın harness'ı, `tests/EpubFixer.Tests/PerformanceBudgetTests.cs`
- **YAP:** `MaxArcLength`, `BudgetCap`, `MaxWindowLength` eksenini süpür. Hedef kütle: 27
  `TargetNotInLattice` + 1 `WindowSkippedTooLong`.
- **D75 KONTROLÜ:** R5.0c `rawVersusProduction.status = "explained"` verdi ve
  `maxArcLengthGrowthBlockedByD75 = false` — **`MaxArcLength` büyütülebilir.** Ama R5.0c'nin
  uyarısını oku: kapının kabul/red kararı aday kümesine duyarlı.
- **KISIT:** 30 sn / 2.500 state ve 120 sn / legacy+35 sn **kısıttır, çıktı değil**. Süre her
  noktada ölçülür.
- **ÇIKTI:** eksen eğrisi + her noktanın süre bedeli.

---

#### R5.4b-3 — Skor ekseni (25 vaka)
- **PLAN BÖLÜMÜ:** 6.8, 11.2
- **DOSYALAR:** R5.4b-0'ın harness'ı
- **YAP:** `Lambda`, `MaxPathCost`, `MinMargin`, `MaxOrdinarySubstitutions` eksenini süpür.
  Hedef kütle: 23 `DecoderRankedOther` + 2 `GateRejected:TooManyOrdinaryEdits`.
- **DİKKAT:** 6.8'deki asimetri — `Lambda` süpürülürken `MinMargin` dolaylı değişir,
  `MaxPathCost` değişmez. Bağlaşıklığı hesaba kat.
- **ÇIKTI:** eksen eğrisi.

---

#### R5.4b-4 — Birleşik nokta seçimi ve held-out doğrulama
- **PLAN BÖLÜMÜ:** 11.2
- **DOSYALAR:** üç eksenin eğrileri, `docs/baselines/odun-kesmek.threshold-sweep.json`
- **YAP:** Üç eksenin sonucundan birleşik nokta seç. **Hedef fonksiyon önceden yazılır:**
  genişletilmiş ground truth üzerinde precision ≥ %98 kısıtı altında recall maksimizasyonu.
- **ZORUNLU:** seçilen nokta held-out bölümde de doğrulanır (overfit koruması); finalistler
  katman 2 (tam benchmark) ile ölçülür.
- **DUR:** Süpürmeden SONRA "şu metrik daha iyi görünüyordu" deme.
- **ÇIKTI:** `threshold-sweep.json` + seçilen noktanın gerekçesi + yeni KAYIP sayısı.

---

#### R5.5a — Beş ön koşulu ölç (çevirme)
- **PLAN BÖLÜMÜ:** 12
- **DOSYALAR:** plan bölüm 12'deki tablo, `docs/baselines/`
- **YAP:** R5.5'in beş ön koşulunu tek tek ölç ve tabloyu doldur. **Varsayılanı ÇEVİRME.**
- **HATIRLATMA:** Kuyruk bölüm 3 — ulaşılabilir tavan 77 vaka, yani KAYIP 42'nin altına inemez.
  Lattice'in legacy'yi geçmesi bu fazda beklenmiyor; 2. koşul (kitap sağlığı) bu ışıkta
  değerlendirilir.
- **ÇIKTI:** beş koşulun ölçülmüş değeri ve R5.5b'nin serbest olup olmadığı.

---

#### R5.5b — Anahtarı çevir ve yeniden ölç
- **PLAN BÖLÜMÜ:** 12; [phase-4-plan.md](phase-4-plan.md) bölüm 8.3 (adımlar aynen geçerli)
- **DOSYALAR:** `src/EpubFixer.Cli/Program.cs` (satır 64–67, 978–982),
  `tests/EpubFixer.Tests/MorphologyCallTraceTests.cs`, `OcrMutationBaselineTests.cs`
- **ÖN KOŞUL:** R5.5a'nın beş koşulu **hepsi** yeşil. Biri değilse bu kalem BAŞLAMAZ.
- **YAP:** Varsayılan motoru lattice yap. Golden SHA-256 **kasten değişir** — yeni değeri
  ÖLÇEREK yaz. `OcrMutationBaselineTests`, `PerformanceBudgetTests`, `measure` baseline'ı ve
  benchmark sayılarını yeniden ölç.
- **DUR:** Üç listeyi (KAZANÇ / KAYIP / ÇATIŞMA) elle incele. Yanlış düzeltme bulursan eşik
  oynatarak kapatma — DUR, varsayılanı legacy'ye al, bildir.
- **ÇIKTI:** çevrilip çevrilmediği + güncellenen baseline listesi.

---

## 5. Kuyruğun bakımı

- Bir kalem bittiğinde bölüm 2'deki durumu ve commit hash'i **aynı commit'te** güncellenir.
- Bir kalem bölünürse yeni satırlar buraya eklenir; plan bölüm 13'e karar olarak yazılması
  **gerekmez** — bölme operasyonel bir karardır, mimari bir karar değil.
- Bir kalem düşürülürse (R5.2 gibi) satırı ⛔ olarak kalır ve gerekçesi yazılır. Silinmez:
  düşürülmüş bir kalem, hiç düşünülmemiş bir kalemden farklıdır.
