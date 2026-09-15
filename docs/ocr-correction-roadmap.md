# OCR Düzeltme Motoru — Yol Haritası

> **Sürüm 2 — 2026-09-15.** Sürüm 1 (Faz 0–5 planları) `adc202d`'de arşivlendi ve silindi.
> Neden yeniden yazıldı: sürüm 1'in dayandığı varsayım — *"lattice motoru legacy'nin yerini
> alacak"* — üç kez ölçüldü ve üç kez yanlışlandı. Bkz. bölüm 2.
>
> Kararların gerekçeleri ayrı belgededir: **[decisions.md](decisions.md)**.
> Her iş kalemi ayrı bir oturuma devredilebilir; devir şablonu bölüm 8'dedir.

---

## 1. Hedef

Türkçe OCR ile üretilmiş bir EPUB'ın okunabilir hale gelmesi. **%100 doğruluk hedef değil.**

| | Hedef | Bugün |
|---|---|---|
| Precision (uygulanan düzeltmelerin doğruluğu) | ≥ %98 | %98,85 (legacy) |
| Recall (yakalanan hata oranı) | ≥ %60 | %89,93 (legacy) |
| Full-book çalışma süresi | ≤ 120 sn (hard gate) | ~28 sn (legacy), lattice pass +28 sn |
| Çevrimdışı | Zorunlu — ağ yok, harici servis yok | ✅ |

Emin olunmayan her durumda karar **"dokunma"**dır. Yanlış düzeltme, düzeltilmemiş hatadan pahalıdır.

---

## 2. Nerede olduğumuz

### 2.1 Ne kuruldu (Faz 0–4, tamamlandı)

Sürüm 1'in beş fazı bitti ve **mimari olarak hedefe ulaştı**:

- **Ölçüm ağı** — `BookHealthMetric`, `measure` komutu, performans bütçe testleri, sınıf kırılımlı kalite kapısı
- **Morfoloji sıcak döngüden çıktı** — `IMorphologyOracle` + disk cache; TRmorph runtime'da sıfır çağrı
- **Bilgi tabanı** — `BookVocabulary`, `BookLanguageModel`; politika Core'da, I/O Adapters'da
- **Lattice motoru** — `ILexiconMatcher` → `WordLatticeBuilder` → `LatticeDecoder` → `CorrectionAcceptanceGate`
- **Üretim hattına bağlandı** — `IOcrCorrectionPlanner` portu, `RegionMutationPlanner`, `--ocr-engine legacy|lattice`

Suite: **522/522 yeşil** (~3 dk 33 sn). `EpubFixer.Core` 7.676 satır, testler 12.020 satır.

### 2.2 Ne ölçüldü — ve varsayımı nasıl yanlışladı

288 kayıtlık ground truth üzerinde (R5.0d, `33d4b87`):

| | Legacy | Lattice |
|---|---:|---:|
| Precision | %98,85 | %98,25 |
| **Recall** | **%89,93** | **%58,33** |
| Üretimde uygulanan mutation | 120 | 10 |
| Kitap sağlığı (çözümlenemeyen/1000) | 57,30 | **58,61** (kötüleşiyor) |
| `ProtectedViolated` | 0 | 0 |

**KAYIP = 119** (legacy düzeltiyor, lattice düzeltmiyor). Sebep taksonomisi
(`odun-kesmek.loss-taxonomy.json`, atfedilemeyen kayıt sıfır):

| Sebep | Vaka | Ulaşılabilir mi |
|---|---:|---|
| `RegionNotDetected` | 32 | ❌ dedektörün sınırı, motorun değil |
| `TargetNotInLattice` | 27 | ✅ arama uzayı |
| `GateRejected:OriginalTokenIsValid` | 24 | ✅ kapı eşiği (hipotez 24/24 doğrulandı) |
| `DecoderRankedOther` | 23 | ✅ maliyet tablosu + `Lambda` |
| `MatcherMissedTarget` | 10 | ❌ düşürüldü (D72) |
| diğer | 3 | ✅ |

**Ulaşılabilir tavan 77 vaka** — yani KAYIP en iyi ihtimalle 42'ye iner. Bu, en kusursuz kalibrasyon
turundan sonra bile **lattice'in legacy'yi geçemeyeceği** anlamına gelir. Sürüm 1'in bütün Faz 5
kuyruğu (19 kalem, kritik yol 10 adım) bu duvara koşuyordu.

### 2.3 Gözden kaçan veri

Lattice'in **9 KAZANCI** var — legacy'nin yapamadığı düzeltmeler
(`odun-kesmek.engine-diff.json`):

```
kü-^:ük   → küçük        ıı<ıda    → yılda        entz      → Gentz
ohbet     → sohbet       koli ukta → koltukta     bi-^:imde → biçimde
Strind-berg → Strindberg Gert-rude → Gertrude     Za-al'a   → Zaal'a
```

Sınıf kırılımı da aynı şeyi söylüyor: `SpuriousSpace`'te lattice 1/1, legacy 0/1.

**İki motor birbirinin alternatifi değil, tamamlayıcısı.** Sürüm 1 bunu D52 ile ("iki motor
birbirini dışlar") yapısal olarak görünmez kılmıştı.

---

## 3. Strateji: hibrit hat (D81)

> **Legacy varsayılan kalır ve birincil motordur. Lattice, legacy'nin dokunmadığı yerlerde koşar.**

Bu, sürüm 1'in "yerine geçme" hedefini **terk eder**. Gerekçe bölüm 2.2'dir: yerine geçme
ölçülebilir biçimde ulaşılamaz, tamamlayıcılık ise bugün ölçülmüş 9 vakalık bir kazanç.

Ne değişir:

| | Sürüm 1 | Sürüm 2 |
|---|---|---|
| Lattice'in başarı ölçütü | legacy'nin recall'üne yetişmek | legacy'nin **üstüne** net kazanç koymak |
| KAYIP 119'un anlamı | kapatılması gereken açık | **anlamsız** — legacy zaten düzeltiyor |
| R5.4b süpürmesinin amacı | recall kurtarmak | **precision** korumak |
| R4.3 (legacy'yi silmek) | ertelenmiş borç | **kalıcı olarak düşürüldü** (D84) |
| D52 (motorlar birbirini dışlar) | geçerli | **iptal** (D81) |

### 3.1 Mimari — Composite planner

Port zaten doğru yerde duruyor; hiçbir mevcut planner değişmez (OCP):

```
IOcrCorrectionPlanner.CreatePlan(stream, oracleBuilder) → OcrCorrectionPlanResult

                    CompositeOcrCorrectionPlanner
                     │
      ┌──────────────┴──────────────┐
  LegacyOcrCorrectionPlanner   LatticeOcrCorrectionPlanner
      │  (birincil)                 │  (ikincil)
      └──────────────┬──────────────┘
                     ▼
        birleştirme: aynı snapshot üzerinde
        logical aralık çakışmasında BİRİNCİL kazanır
                     ▼
        OcrCorrectionMutationApplier (değişmez) → EPUB
```

**Birleştirme neden güvenli — üç ölçülmüş gerçek:**

1. **Aynı snapshot.** Her iki planner da `EpubFixService`'in verdiği aynı `finalStream`'i alır ve
   planını onun üzerine kurar. Birleştirme iki *karar modelini* uzlaştırmak değil, iki mutation
   kümesini **geometriyle** ayıklamaktır (D82).
2. **Applier zaten doğruluyor.** `OcrCorrectionMutationApplier` her span için `ExpectedText`
   eşleşmesini kontrol ediyor; çakışmayan düzenlemeler tek geçişte güvenle uygulanır.
3. **A/B ölçümü birebir taşınır.** R4.2c'de her iki motor da aynı `finalStream`'i görmüştü —
   lattice'in gördüğü girdi hibritte **değişmiyor**.

Bundan **doğrulanabilir bir tahmin** çıkar:

> Hibrit **129 mutation** uygulamalı: 120 legacy + 9 lattice KAZANÇ.
> Tek çakışma (`kol-1 ıı kta`) birincile, yani legacy'ye gider.
> **Sayı 129 çıkmazsa birleştirmede hata vardır** — motorda değil.

Bu, H3'ün kabul kriteridir. (Çakışan tek vakada **her iki motor da yanlış**: legacy
`kol-1 ı kta`, lattice `koli nokta`, doğrusu `koltukta`. Birincil kuralı burada kalite değil
öngörülebilirlik seçiyor — D83.)

---

## 4. İş kalemleri

Durum kodları: ✅ bitti · ⬜ hazır · 🔒 ön koşulu bekliyor · ⛔ düşürüldü

### M1 — Kapıyı ölçülebilir yap (ön koşul)

Kapı bugün **her iki motorda da kırmızı**, çünkü eşikler 160 kayıtlık eski taban üzerinde
ölçülmüştü. Kapı kırmızıyken hiçbir kalemin "başarılı" tanımı yoktur — bu yüzden ilk sırada.

| # | Kalem | Durum | Ön koşul | Çıktı |
|---|---|---|---|---|
| G1 | Kapı profilini yeni ölçüm tabanına taşı (D80'i uygula) | ⬜ | — | legacy kapıdan geçiyor |
| G2 | `MissingSpace` / `SpuriousSpace` boşluğunu karara bağla | ⬜ | — | karar + gerekçe |

**G1 — Kapı profili.** Eşikler 288 kayıtlık yeni taban üzerinde yeniden kurulur. Üç koruma (D80):
yeni eşik **ölçülür, seçilmez** (legacy'nin değeri, aşağı yuvarlanmadan); eski profil
`supersededProfiles` altında **tabanıyla** arşivlenir; D77 taban içinde aynen geçerli kalır.
**DUR:** eşiği legacy'nin ölçülmüş değerinin altına koyma.
*Dosyalar:* `docs/baselines/quality-gate.json`, `QualityBenchmarkGateEvaluator.cs`

**G2 — Ölçüm boşluğu.** R5.0d bu iki sınıfı dolduramadı: D70'in aday havuzu motorların dokunduğu
yerlerden oluşuyor, hiçbir motor `MissingSpace`'e dokunmuyor. Üç seçenekten birini **gerekçeyle**
seç: (a) havuz dışına çıkıp elle bul, (b) ertele ve ölçüm boşluğu olarak kaydet, (c) kapsam dışı
yaz. **Önce (c)'yi sına:** kitapta bu sınıf gerçekten var mı?
*Dosyalar:* `test-data/odun-kesmek/ground-truth.json`, `odun-kesmek.loss-taxonomy.json`

### M2 — Hibrit hattı (fazın kalbi)

| # | Kalem | Durum | Ön koşul | Çıktı |
|---|---|---|---|---|
| H1 | `CompositeOcrCorrectionPlanner` + birleştirme kuralı | ⬜ | — | birim testler, üretim değişmedi |
| H2 | `--ocr-engine hybrid` + composition root bağlantısı | 🔒 | H1 | bayrak çalışıyor |
| H3 | Hibriti ölç | 🔒 | H2, G1 | baseline'lar + kapı sonucu |
| H4 | Lattice'in eklediği her mutation elle incelenir | 🔒 | H3, R1 | yanlış düzeltme sayısı |
| H5 | Varsayılanı hibrit yap | 🔒 | H4 temiz | yeni golden'lar |

**H1 — Composite.** Saf bir birleştirme fonksiyonu: iki `OcrCorrectionPlanResult` al, ikincinin
logical aralığı birincininkiyle çakışan mutation'larını **atla**, kalanları birleştir. Atlananlar
`Diagnostics`'e yazılır — sessizce düşen düzeltme ölçülemeyen kayıptır (D56'nın kuralı).
`OcrCorrectionEngine` enum'una `Hybrid` **sona** eklenir (D36).
**DUR:** Mevcut iki planner'ın içine dokunma. Bu kalem üretim davranışını değiştirmez —
varsayılan hâlâ legacy, tüm baseline'lar bit düzeyinde aynı kalmalı.
*Dosyalar:* `src/EpubFixer.Core/Ocr/CompositeOcrCorrectionPlanner.cs`, `OcrMutationProvenance.cs`, testler

**H2 — Bayrak.** `--ocr-engine legacy|lattice|hybrid`. İki composition root (Cli ve Benchmarks)
aynı kuruluma ihtiyaç duyar; üçüncü bir kopya doğmasın.
**DUR:** Varsayılanı ÇEVİRME — o H5'in işi.
*Dosyalar:* `src/EpubFixer.Cli/Program.cs`, `QualityBenchmarkApplication.cs`

**H3 — Ölçüm.** Tek full-book koşusu. Ölçülecekler: mutation sayısı (**beklenen 129**),
benchmark precision/recall + sınıf kırılımı, kapı sonucu, `measure` metriği, süre,
`ProtectedViolated` / `ProtectedChanged` / `UnexpectedTextChanges` / `NonTextChanges`.
**Süre kısıtı:** ≤ 120 sn **ve** legacy + 35 sn (D61).
**DUR:** 129 çıkmazsa sebebi bul ve bildir — sayıyı açıklamadan ilerleme.
*Çıktı:* `docs/baselines/odun-kesmek.fix-hybrid.json`, güncellenmiş `quality-gate.json`

**H4 — İnceleme.** Lattice'in eklediği ~9 mutation'ın **her biri** bağlamıyla elle incelenir.
**DUR:** Bir tane bile yanlış düzeltme bulursan eşik oynatarak kapatma — DUR, bildir.
*Ön koşul:* R1 (inceleme raporu)

**H5 — Anahtarı çevir.** Varsayılan motor `hybrid` olur. Golden SHA-256 **kasten değişir** ve
yeni değeri **ölçülerek** yazılır. `OcrMutationBaselineTests`, `PerformanceBudgetTests`,
`MorphologyCallTraceTests`, `measure` baseline'ı yeniden ölçülür.
*Ön koşul:* H4'te yanlış düzeltme **sıfır**, süre bütçede, kapı yeşil, `ProtectedViolated == 0`

### M3 — İnceleme ve görünürlük

| # | Kalem | Durum | Ön koşul | Çıktı |
|---|---|---|---|---|
| R1 | İnceleme raporu — Apply bölümü | ⬜ | — | rapor üretildi |
| R2 | Review + Leave + dokunulmayan kütle | 🔒 | R1 | rapor bölümleri |
| R3 | Motor kaynağı raporda görünür | 🔒 | R1, H2 | hangi mutation hangi motordan |

**R1.** Lattice hattı için yeni Markdown rapor: her uygulanan düzeltme öncesi/sonrası cümle
bağlamıyla. **DUR:** `FullBookReaderPreview.cs`'e dokunma (D78) — eski zincire bağlı.
**R2.** `Review` kararları, `Leave` sebep histogramı, bilerek dokunulmayan kütle.
**R3.** Hibritte her mutation'ın `Provenance`'ından motoru okunabilir olmalı; atlanan çakışmalar
raporun **en üstünde** (en tehlikeli sınıf).

### M4 — Precision derinleştirme (KOŞULLU)

> **Bu blok yalnızca H3/H4 bir precision sorunu gösterirse açılır.** Hibritte lattice'in payı
> ~9 mutation; 9 vaka için L boyutunda kalibrasyon işi ödemek ölçüm göstermeden yapılamaz.
> Sürüm 1'de bu blok kritik yoldaydı çünkü amacı recall'dü; artık değil.

| # | Kalem | Durum | Ön koşul |
|---|---|---|---|
| P1 | Kapının dört eşiğini `LatticeOptions`'a çıkar (D45 borcu) | ⬜ | — |
| P2 | `LatticeOptions`'ın iki ölü alanı: bağla veya kaldır | 🔒 | P1 |
| P3 | `MaxPathCost` / `MinMargin` asimetrisini belgele | 🔒 | P1 |
| P4 | `IOcrConfusionSet` portu + aligner enjeksiyon dikişi | 🔒 | H4 precision sorunu gösterdi |
| P5 | Maliyet kalibrasyon aracı + A/B | 🔒 | P4 |
| P6 | Eşik süpürme altyapısı + eksenler | 🔒 | P1, H3 |

P1–P3 **koşulsuzdur** ve hemen yapılabilir: eşiklerin dağınık olması D45'ten beri açık bir borç ve
ölü alanlar her ölçümü yanıltır. P4–P6 ölçüme bağlıdır.

### M5 — Tek kitap borcu (D87)

| # | Kalem | Durum | Çıktı |
|---|---|---|---|
| B1 | İkinci kitabı ölçüm tabanına ekle | ⬜ | ikinci `ground-truth.json` |
| B2 | Baseline'ları kitap başına ayır | 🔒 (B1) | kitap-bağımsız kapı |

**Bugünkü bütün ölçüm tek kitaba (`odun-kesmek`) dayanıyor.** Sürüm 1 bunu açık borç olarak
kaydetti ama kalem açmadı. Öğrenilmiş her eşik, her maliyet tablosu ve kapının her sayısı bu
kitaba overfit olabilir — ve bunu söyleyecek ölçüm yok.

**Bu blok M2'den sonra, M4'ten önce gelmelidir:** hibrit hattı ikinci kitapta doğrulanmadan
eşik kalibrasyonuna girmek, tek kitaba iki kat daha fazla overfit etmektir.

### M6 — Kapsam dışı kütle (Faz 6)

| # | Kalem | Durum | Not |
|---|---|---|---|
| X1 | Kalan hata raporu | ⬜ | `RegionNotDetected` 32 vaka — **dedektörün** sınırı |
| X2 | Legacy'nin 3 yanlış düzeltmesi | ⬜ | `olın`→`olan` (doğrusu `John`) dahil |
| X3 | Second-pass OCR (Tesseract) | ⛔ | yalnızca orijinal PDF/görüntü varsa; XL |
| — | R4.3 — eski yolların silinmesi | ⛔ | **kalıcı düşürüldü** (D84) |

**X2 önemli:** legacy artık birincil motor ve kendi 3 yanlış düzeltmesi var. Sürüm 1'de bunlar
"legacy zaten ölecek" diye görmezden gelinebilirdi; hibritte **ölmüyor**, dolayısıyla hataları
artık kalıcı.

---

## 5. Sıra

```
G1 ──┐
G2   ├──► H1 ──► H2 ──► H3 ──► H4 ──► H5
R1 ──┘                   ▲       ▲
                         │       │
P1 ─► P2                 │      R3
  └─► P3                 │
                        B1 ─► B2
```

**Kritik yol:** `G1 → H1 → H2 → H3 → H4 → H5` — altı kalem.

**Paralel yürüyebilenler:** G1 · G2 · H1 · R1 · P1 — beşi ayrı dosya ailelerine dokunur.

Sürüm 1'in kritik yolu 10 adımdı ve sonunda ölçülmüş bir "henüz değil" vardı. Bu altı adımın
sonunda üretimde **129 düzeltme** var.

---

## 6. İş boyutu bütçesi (D79 — aynen geçerli)

Bir kalem şu dördünü birden sağlamalıdır. Sağlamıyorsa **bölünmemiştir**:

| # | Kural | Nasıl kontrol edilir |
|---|---|---|
| 1 | **Tek commit** | Kalem bittiğinde tek bir commit çıkar |
| 2 | **En fazla bir full-book koşusu** | İkiden fazla gerekiyorsa ya kalem büyüktür ya altyapı eksiktir |
| 3 | **Tek dosya ailesi** | Bir port + implementasyonu + testi = bir aile |
| 4 | **Tek doğrulanabilir çıktı** | "Baseline değişmedi" **veya** "şu dosya üretildi" **veya** "şu sayı ölçüldü" |

**DUR kuralı:** Bir agent kalemin bu bütçeyi aşacağını anlarsa **işi yarım bırakmaz ve büyütmez** —
durur, ne bulduğunu ve kalemin nasıl bölünmesi gerektiğini bildirir.

**Elle etiketleme kalemleri ≤ 25 kayıtlık partilere bölünür.** Ölçüldü: 129 kaydın elle
doğrulanması kesintisiz ~2 saat sürdü. Kod kalemi yarım kalırsa `git checkout` ile atılır;
yarım kalan etiketleme, insanın okuduğu 60 cümlenin çöpe gitmesidir.

---

## 7. Risk kaydı

| # | Risk | Etki | Azaltma |
|---|---|---|---|
| 1 | **Hibrit iki motorun hatalarını toplar** — legacy'nin 3 yanlışı + lattice'in eklediği ne varsa | Yüksek | H4 elle inceleme, yanlış düzeltme sıfır kısıtı; X2 legacy'nin kendi hatalarını hedefler |
| 2 | **Birleştirme sessizce mutation düşürür** ve recall kaybı ölçülmez | Yüksek | H1: atlanan her mutation `Diagnostics`'e yazılır; R3 raporda en üstte gösterir |
| 3 | **Süre bütçesi patlar** — hibrit iki motorun maliyetini toplar (~28 + ~28 sn) | Orta | D61 zaten bunu öngörüyor (legacy + 35 sn); H3'te ölçülür, aşarsa DUR |
| 4 | **Tek kitaba overfit** — bütün ölçüm `odun-kesmek` üzerinde | Yüksek | M5 kalem olarak açıldı ve M4'ün önüne kondu |
| 5 | **İki motor birlikte çürür** — R4.3 kalıcı düştüğü için ikisi de yaşayacak | Orta | Legacy'ye yeni özellik eklenmez; X2 dışında yalnızca hata düzeltmesi alır |
| 6 | **Kapı gevşetilerek yeşile boyanır** | Yüksek | D77 (yalnızca yukarı) + D80 (taban değişimi ölçülür, seçilmez) |
| 7 | **Özel isimler "düzeltilir"** (*Auersberger*, *Rennweg*) | Orta | `ProperNameRisk` + `OriginalTokenIsValid` korunur; P1 bunları süpürülebilir yapar ama gevşetmez |
| 8 | **Recall için precision feda edilir** | Yüksek | Yanlış düzeltme sayısını sıfırın üstüne çıkaran hiçbir değişiklik kabul edilmez |

---

## 8. Devir şablonu

```
EpubFixer projesinde docs/ocr-correction-roadmap.md'deki <KALEM> kalemini uygulayacaksın.

Önce şunları oku:
- docs/ocr-correction-roadmap.md — bölüm 3 (strateji), 4 (<KALEMİN BLOĞU>), 6 (iş boyutu), 7 (risk)
- docs/decisions.md — hâlâ bağlayıcı kararlar
- <KALEME ÖZEL DOSYALAR>

Her kalemde geçerli kurallar:
- ÖN KOŞUL: çalışma ağacı temiz ve suite yeşil olmalı. Değilse DUR ve bildir.
- Test-first: kırmızı test → minimum kod → refactor. Testi olmayan üretim kodu yazma.
- Bağımlılıklar içeri doğru: Core'a framework/IO importu girmez.
- Beklenen değerleri TAHMİN ETME. Önce koş, çıkan sayıyı oku, sonra yaz.
- Kapıyı GEVŞETME, eşik İNDİRME (D77 / D80).
- Yanlış düzeltme sayısını artıran hiçbir değişiklik kabul edilmez.
- İŞ BOYUTU: tek commit, en fazla bir full-book koşusu, tek dosya ailesi, tek çıktı.
  Bu bütçeyi aşacağını anlarsan DUR — işi büyütme, kalemin nasıl bölüneceğini bildir.
- Kapsam yalnızca bu kalem. Fark ettiğin başka sorunları düzeltme, bitiş raporunda
  "gözlem" olarak yaz.
- Yeni bir karar gerekiyorsa kendi başına verme: gerekçeyi bildir, karar decisions.md'ye eklensin.

Bitirdiğinde: ne değişti, hangi testler eklendi, <KALEME ÖZEL ÇIKTI>, ve roadmap'te
güncellenmesi gereken bir şey olup olmadığı.
```

---

## 9. Bakım

- Bir kalem bittiğinde bölüm 4'teki durumu ve commit hash'i **aynı commit'te** güncellenir.
- Bir kalem bölünürse yeni satırlar buraya eklenir; karar kaydı **gerekmez** — bölme operasyonel
  bir karardır, mimari bir karar değil.
- Bir kalem düşürülürse satırı ⛔ olarak **kalır** ve gerekçesi yazılır. Silinmez: düşürülmüş bir
  kalem, hiç düşünülmemiş bir kalemden farklıdır.
- Yeni mimari karar `decisions.md`'ye eklenir, buraya değil.
