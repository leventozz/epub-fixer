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

Suite: **555/555 yeşil** (~5 dk 31 sn; sürüm 2 başlarken 522'ydi).

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

Kapı, eşikleri 160 kayıtlık eski taban üzerinde ölçüldüğü için **her iki motorda da kırmızıydı**.
Kapı kırmızıyken hiçbir kalemin "başarılı" tanımı yoktur — bu yüzden ilk sırada.
**G1 ile çözüldü:** kapı artık legacy'de yeşil, lattice'te kırmızı; aradaki mesafe Faz 5'in
ölçmek istediği şeydir ve kapı düşürülerek kapatılmadı.

| # | Kalem | Durum | Ön koşul | Çıktı |
|---|---|---|---|---|
| G1 | Kapı profilini yeni ölçüm tabanına taşı (D80'i uygula) | ✅ `cd5b5ef` | — | legacy geçiyor, lattice geçmiyor |
| G2 | `MissingSpace` / `SpuriousSpace` boşluğunu karara bağla | ⬜ | — | karar + gerekçe |
| G3 | Profil bulunamazsa/bozuksa kapı gürültülü patlasın | ✅ `6eb5da0` | — | sessiz zayıflama kapandı |

**G1 — Kapı profili.** ✅ `cd5b5ef`. Eşikler 288 kayıtlık taban üzerinde yeniden kuruldu:
precision `0.9885496183206107`, recall `0.8993055555555556`, `ClassRecall:Hyphenation`
`0.9659090909090909` — üçü de legacy'nin **ölçülmüş, yuvarlanmamış** değerleri. Eski profil
`supersededProfiles[0]` altında tabanıyla (`{160, v2}`) arşivlendi. Üretim kodu değişmedi.
`QualityBenchmarkGateProfilePinTests` D80 kural 1'i pinler: eşikleri ve ölçüm sayımlarını
JSON'dan okur, recall'ü sayımlardan **yeniden hesaplar**, sabit sayı barındırmaz — biri eşiği
legacy'nin ölçtüğünün üstüne çekerse benchmark koşusu gerekmeden kırmızıya döner.
*Gözlem (açık):* pin testi sınıf kırılımını kurarken `detected = known` veriyor; kapı `detected`
okumadığı için bugün etkisiz, ama kapıya `detected` tabanlı bir kural eklenirse test sessizce
yanlış şeyi doğrular.

**G2 — Ölçüm boşluğu.** R5.0d bu iki sınıfı dolduramadı: D70'in aday havuzu motorların dokunduğu
yerlerden oluşuyor, hiçbir motor `MissingSpace`'e dokunmuyor. Üç seçenekten birini **gerekçeyle**
seç: (a) havuz dışına çıkıp elle bul, (b) ertele ve ölçüm boşluğu olarak kaydet, (c) kapsam dışı
yaz. **Önce (c)'yi sına:** kitapta bu sınıf gerçekten var mı?
*Dosyalar:* `test-data/odun-kesmek/ground-truth.json`, `odun-kesmek.loss-taxonomy.json`

**G3 — Sessiz zayıflama.** ✅ Kapatıldı. `LoadCurrent` artık `null` dönmüyor, **`InvalidDataException`
atıyor**; `QualityBenchmarkApplication` bu hatayı taşıyor ve `Run` **hiç iş yapmadan** stderr'e yazıp
`exit 2` veriyor. Anlamsız bir PASS, cevapsızlıktan kötüdür.

Dört sessiz zayıflama yolu kapatıldı — en sinsisi dördüncüsü:

| Durum | Eskiden | Şimdi |
|---|---|---|
| Profil dosyası yok | varsayılanlar, PASS | `exit 2` |
| `current` düğümü yok | varsayılanlar, PASS | `exit 2` |
| Bozuk JSON | **`JsonException` ile çökme** (catch listesinde değildi) | `exit 2`, temiz mesaj |
| `minimumPrecision` **yazım hatası** | sessizce %98/%60, **PASS** | `exit 2` |

Dördüncüsü gerçek dünyada en olası hata: `QualityBenchmarkGateOptions` positional record ve iki
eşik için varsayılan taşıyor, yani **eksik bir alan ile kasıtlı bir değer ayırt edilemiyordu.**
Artık ikisi de **açıkça** bildirilmek zorunda.

Uçtan uca doğrulandı: profile `minimumPrecision` → `minimumPrecission` yazım hatası enjekte edildi;
koşu `exit 2` ve alanı adıyla söyleyen bir hata verdi. Eskiden recall barı %92,71'den %60'a düşer
ve koşu **PASS** raporlardı.

### M2 — Hibrit hattı (fazın kalbi)

| # | Kalem | Durum | Ön koşul | Çıktı |
|---|---|---|---|---|
| H1 | `CompositeOcrCorrectionPlanner` + birleştirme kuralı | ✅ `7ed1e03` | — | 7 birim test, üretim değişmedi |
| H2 | `--ocr-engine hybrid` + composition root bağlantısı | ✅ `42699f4` | H1 | bayrak çalışıyor |
| H3 | Hibriti ölç | ✅ `9c3224e` | H2, G1 | baseline'lar + kapı sonucu |
| H4 | Lattice'in eklediği her mutation elle incelenir | ✅ `28e623e` | H3 | **1 yeni yanlış** |
| H4b-1 | Çöp tutmanın bedelini modele koy + silme arc'ı | ✅ `7a52ba9` | H4 | **kapı YEŞİL** |
| H4b-2 | ~~Ayrı ölçüm kalemi~~ | ⛔ | — | H4b-1 kendi ölçümünü taşıdı |
| H5 | Varsayılanı hibrit yap | ✅ `68eefb7` | H4b-1 | **hibrit varsayılan, kapı hibride çekildi** |

**H1 — Composite.** ✅ `7ed1e03`. İki planner aynı `stream`/`oracleBuilder` üzerinde koşar;
ikincinin mutation'ı birincininkiyle `DocumentPath` + `[LogicalStart, LogicalStart+LogicalLength)`
kesişiyorsa **atlanır** ve `Diagnostics`'e yazılır (D83 + D56). Çıktı `(DocumentPath, LogicalStart)`
sırasıyla deterministik. `OcrCorrectionEngine.Hybrid` enum'un **sonuna** eklendi (D36).
Composite hiçbir yere bağlanmadı, mevcut iki planner'a dokunulmadı — varsayılan hâlâ legacy.
**D88** verildi: plan geçerliliği yalnızca birincilin `Failures`'ına bağlıdır.

*Gözlem (açık):* `OverlapsAny` adayı yalnızca **birincilin** mutation'larına karşı sınıyor.
Bir planner'ın kendi planı içinde çakışma üretmediği **varsayılıyor ama doğrulanmıyor** — bugün
doğru (lattice D55, legacy kendi çakışma tespiti) ama applier bu ihlali yakalayamaz: her span
`ExpectedText`'i *orijinal* node verisine karşı doğrular, aynı aralığa iki düzenleme de geçer ve
ikisi birden uygulanır. Yeni bir ikincil planner eklenirse **önce bu değişmez assert edilmelidir.**
*Dosyalar:* `src/EpubFixer.Core/Ocr/CompositeOcrCorrectionPlanner.cs`, `OcrMutationProvenance.cs`, testler

**H2 — Bayrak.** ✅ `42699f4`. `--ocr-engine legacy|lattice|hybrid` her iki composition root'ta
çalışıyor. Motor adı → planner çözümlemesi tek yerde toplandı: `OcrPlannerFactory` (Adapters),
tanınmayan adda **exception atar**, sessizce legacy'ye düşmez — **D89**. İki root çözümleme,
arg doğrulama ve kullanım metni için aynı factory'yi çağırıyor; motor adı listesi artık tek
kaynakta. Varsayılan değişmedi (`OcrEngine ?? "legacy"`).
*Dosyalar:* `src/EpubFixer.Adapters/Ocr/OcrPlannerFactory.cs`, `Program.cs`, `QualityBenchmarkApplication.cs`

**H3 — Ölçüm.** ✅ `9c3224e`. Mutation sayısı **129 çıktı** — tam olarak tahmin edilen 120 legacy +
9 lattice KAZANÇ, tek çakışma (`main-3.xhtml`, `ıı`→`ı`, `logicalStart 152473`) D83 gereği
legacy'ye gitti. Dokuz KAZANÇ'ın hepsi bağımsız olarak `odun-kesmek.engine-diff.json`'daki
`gain.items` ile birebir eşleşiyor. **DUR tetiklenmedi** —
`OcrMutationBaselineTests.FullBookFix_HybridEngineMutationProfileIsPinned` bu sayıyı pinler ve
aynı koşunun çıktı epub'u üzerinde kitap sağlığını in-process ölçer (ikinci bir tam-kitap koşusuna
gerek kalmadan): `totalTokens 46656, unresolvableTokens 2666, suspiciousTokens 166, oran
57,14/1000` — legacy'nin `57,30`'undan biraz daha iyi (9 KAZANÇ doğru düzeltme olduğu için).
Süre (`PerformanceBudgetTests.FullBookHybridFixStaysWithinBudgetOfLegacy`, legacy ve hybrid **aynı
oturumda** ölçüldü — D61): legacy `25,62 sn`, hybrid `44,57 sn` — hem 120 sn sabit kapıyı hem
legacy+35 sn yumuşak kapıyı geçiyor.

Benchmark (`--ocr-engine hybrid test-data/odun-kesmek`) sonucu: precision **%98,52**, recall
**%92,36**; `ProtectedViolated 0`, `ProtectedChanged 0`, `UnexpectedTextChanges 0`,
`NonTextChanges 0`. **Kapı sonucu: FAIL** — precision, legacy'nin kendi ölçümü olan eşiğin
(%98,85) altında kaldı; eşik D77 gereği **değiştirilmedi**. Sebep: `correctlyFixed` 259→266'ya
çıkarken (9 KAZANÇ'ın çoğu doğru), `wronglyFixed` 3'ten **4'e** çıktı — yeni yanlış
`odun-kesmek-garbage-0001` (`':,ohbet'` → `':,sohbet'`, doğrusu `'sohbet'`): lattice'in kazandığı
`ohbet`→`sohbet` bölgesi, aynı kelimenin baştaki `':,'` çöp karakterli başka bir geçtiği yerde
yalnızca kısmi düzeltiliyor. Diğer üç yanlış (`glyph-0030`, `mixed-0022`, `auto-0174`) legacy'den
miras — hibritte yeni değiller. Recall (%92,36) ve `ClassRecall:Hyphenation` (%98,30) eşiklerin
üzerinde; yalnızca precision düştü. Bu, Risk kaydı #1'in ("hibrit iki motorun hatalarını toplar")
beklendiği gibi gerçekleşmesidir.
*Çıktı:* `docs/baselines/odun-kesmek.fix-hybrid.json`, güncellenmiş `quality-gate.json`
(`current.ocrStageMeasurement.hybrid`), güncellenmiş `docs/baselines/README.md`.

*Gözlem (açık, H4'e girdi):* Benchmark kapı FAIL'i legacy'nin 3 bilinen yanlışının **dışında**
dördüncü bir yanlış gösterdi: `odun-kesmek-garbage-0001` (`':,ohbet'` → `':,sohbet'`, doğrusu
`'sohbet'`). Bu, `odun-kesmek.engine-diff.json`'daki 9 KAZANÇ öğesinden biri olan `ohbet`→`sohbet`
ile **aynı motor kararının** kitaptaki başka bir geçtiği yerde (baştaki `':,'` çöp karakterleriyle)
uygulanması — pinlenen mutation baseline'da (`odun-kesmek.fix-hybrid.json`) bu KAZANÇ tek bir
`logicalStart`'ta (87365) görünüyor, ama ground truth'ta aynı hata kalıbının en az iki farklı
geçtiği yer var ve biri farklı bir sınıfa (`GarbageInsertion`) düşüyor. Kalem bunu düzeltmez —
H4'ün "lattice'in eklediği her mutation'ı elle incele" işine bu dördüncü vaka da dahil edilmeli;
zaten Risk kaydı #1'in beklediği şey budur, eşik oynatılmadı (D77).

**H4 — İnceleme.** Lattice'in eklediği ~9 mutation'ın **her biri** bağlamıyla elle incelenir.
**H4 — İnceleme.** ✅ `28e623e`. Dokuz lattice mutation'ının her biri, hibrit çıktısı EPUB'ında kaynak
metinle yan yana, cümle bağlamıyla okundu. **8 doğru, 1 yeni yanlış** — D90'ın ölçütü
sağlanmadı, H5 bloke.

Tek yeni yanlış `odun-kesmek-garbage-0001`: `':,ohbet'` → `':,sohbet'`, doğrusu `'sohbet'`.
Diğer üç yanlış (`glyph-0030`, `mixed-0022`, `auto-0174`) legacy'den miras — X2'nin konusu.

**Bulgu — çöpün yeri belirleyici.** Bağlam okuması tek bir örüntü gösterdi: **iç** çöp bölgeye
giriyor ve yutuluyor, **baştaki** çöp girmiyor ve yerinde kalıyor.

| Mutation | Çöpün yeri | Sonuç metni |
|---|---|---|
| `kü-^:ük` → `küçük` | iç | `Sapık ve küçük görücü` ✅ |
| `bi-^:imde` → `biçimde` | iç | `yapamadığım biçimde` ✅ |
| `Strind-berg` / `Gert-rude` / `Za-al'a` | — (tireleme) | ✅ |
| `koli ukta` → `koltukta` | — (boşluk) | `berjer koltukta.` ✅ |
| `ıı<ıda` → `yılda` | iç `<` **+ baş `.`** | `bu .yılda halledilmiş` ⚠️ |
| `entz` → `Gentz` | **baş `< ;`** | `önce < ;Gentz Sokağı'na` ⚠️ |
| `ohbet` → `sohbet` | **baş `:,`** | `gece :,sohbet ettim` ❌ |

Son ikisi kapıdan **geçiyor** çünkü ground truth kayıtlarının (`garbage-0012`, `glyph-0018`)
`original` alanı baştaki çöpü içermiyor; yalnızca `garbage-0001`'inki (`:,ohbet`) içeriyor.
Yani üç vakanın üçü de aynı kusur, ama ölçüm yalnızca birini görüyor — **kapı bu sınıfı
eksik sayıyor.** Okuyucu üçünü de görüyor.

**H4b-1 — Çöp tutmanın bedeli.** ✅ `7a52ba9`. İlk teşhis **yanlıştı**: dedektör baştaki çöpü zaten bölgeye
katıyor (`IncludeGarbagePrefix`). Kusur kafesteydi ve ilk düzeltme denemesi de **ölü çıktı** —
yalnız silme arc'ı eklemek hiçbir sayıyı değiştirmedi, çünkü `LatticeDecoder.Append` LM maliyetini
yalnız `Word`/`Identity` arc'larına uyguluyor: çöpü **tutmak 0,00**, silmek 0,40 idi ve silme her
zaman kendi maliyeti kadar kaybediyordu.

Kök neden: **çöp tutmak bedavaydı.** `OcrEditCostModel`'e `RetainedGarbage = 0.25` eklendi
(seçildi, kalibre edilmedi — D34) ve token-arası sert çöp koşusu iki arc alıyor: tutmak 0,25×glif,
silmek 0,20×glif. İkisi **aynı karakterleri** sayar — aksi hâlde `:,` için 0,25 < 0,40 olur ve
tutma yine kazanırdı. Ayrıntı ve iki koruma: **D91**.

Ölçüldü (tek tam kitap koşusu):

| | H4 sonrası | H4b-1 sonrası |
|---|---|---|
| Doğru düzeltilen | 266 | **267** |
| Yanlış düzeltilen | 4 | **3** — üçü de legacy'nin, **yeni sıfır** (D90 ✅) |
| Precision | %98,52 ❌ | **%98,89** |
| Recall | %92,36 | **%92,71** |
| **Kapı** | **FAIL** | **PASS** |
| Hibrit mutation | 129 | 130 |
| Lattice Apply | 31 | 32 |
| Lattice pass süresi | 28,31 sn | 28,76 sn (bütçe 30) |

Bölge sayısı 563'te sabit ve `MaxVisitedStates` 1713'te sabit — bu bir **karar** değişikliği,
tespit değişikliği değil. Dört pinlenmiş baseline kasten değişti ve **ölçülerek** yeniden yazıldı.

*Yan bulgu:* kayıp taksonomisinde `TargetNotInLattice` 27 → 25, `DecoderRankedOther` 23 → 25 —
silme arc'ı iki vakada daha hedefi kafese soktu, decoder henüz seçmiyor. Bu iki vaka artık
**ulaşılabilir** ve P-bloğunun (skor ekseni) hedef kütlesine giriyor.

*Ön koşul:* R1 (inceleme raporu)

**H5 — Anahtarı çevir.** Varsayılan motor `hybrid` olur. Golden SHA-256 **kasten değişir** ve
yeni değeri **ölçülerek** yazılır. `OcrMutationBaselineTests`, `PerformanceBudgetTests`,
`MorphologyCallTraceTests`, `measure` baseline'ı yeniden ölçülür.
**H5 — Anahtar çevrildi.** ✅ Beş ön koşulun beşi de ölçüldü ve sağlandı. Varsayılan motor artık
**hibrit** — ama yalnızca composition root'larda (Cli + Benchmarks). `EpubFixService`'in kütüphane
varsayılanı legacy **kaldı**: Core hibridi kuramaz, çünkü `LatticeOcrPlannerFactory` Adapters'ta
(D27/D58). Mimari zorunluluk, tercih değil — **D92**.

Bunun sonucu: kütüphane varsayılanını pinleyen golden SHA **değişmedi**. Üretim korumasız kalmasın
diye hibrit için **ikinci golden** eklendi (`9f5faea2…`, ölçülerek yazıldı). İki yol da pinli.

Kapı eşikleri hibridin ölçülen değerlerine **çekildi** (D92): precision `267/270`, recall `267/288`,
`ClassRecall:Hyphenation` `173/176`. Eski profil `supersededProfiles` altında arşivlendi. Profil
artık `shippedEngine` alanı taşıyor ve pin testi eşikleri o alanın gösterdiği motora karşı
doğruluyor — kural değişmedi, işaret ettiği motor değişti.

| Ön koşul | Ölçülen |
|---|---|
| 1 — yeni yanlış düzeltme sıfır (D90) | ✅ 3 yanlış, üçü de legacy'den miras (X2) |
| 2 — `measure` kötüleşmiyor | ✅ **57,14** vs legacy 57,30; şüpheli token 165 vs 169 |
| 3 — kapıdan geçiyor | ✅ PASS |
| 4 — bütünlük sıfır | ✅ `ProtectedViolated/Changed`, `UnexpectedTextChanges`, `NonTextChanges` = 0 |
| 5 — süre | ✅ 44,57 sn (sabit kapı 120, D61 alt bütçesi 60,62) |

**Bedeli açıkça kayıtta:** varsayılan koşu 25,62 → 44,57 sn, yani **%74 yavaşladı**. Karşılığı 10 ek
doğru düzeltme ve precision %98,85 → %98,89.

*Kabul edilen yan etki:* `--ocr-engine legacy` artık kapıdan **FAIL** raporluyor. Kusur değil,
kapının ifadesi: gönderilen hat kendi yedeğinden iyidir.



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
G1 ✅─┐
G2   ├──► H1 ✅──► H2 ✅──► H3 ✅──► H4 ✅──► H4b-1 ✅──► H5 ✅  ← M2 TAMAM
G3   ┘
R1 ──┘                   ▲       ▲
                         │       │
P1 ─► P2                 │      R3
  └─► P3                 │
                        B1 ─► B2
```

**Kritik yol: TAMAMLANDI.** ~~G1 → H1 → H2 → H3 → H4 → H4b-1 → H5~~ — M2 bitti, hibrit üretimde.
Sıradaki iş kritik yolda değil: **G3** (kapının sessiz zayıflaması) → **B1** (ikinci kitap) → **P1–P3**.

**Paralel yürüyebilenler:** G2 · G3 · R1 · P1 — dördü ayrı dosya ailelerine dokunur.

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
| 1 | **Hibrit iki motorun hatalarını toplar** — legacy'nin 3 yanlışı + lattice'in eklediği ne varsa | Yüksek | H4 elle inceleme, **yeni** yanlış düzeltme sıfır kısıtı (D90); X2 legacy'nin kendi hatalarını hedefler. H3'te ölçüldü: hibrit tam olarak 1 yeni yanlış getirdi |
| 2 | **Birleştirme sessizce mutation düşürür** ve recall kaybı ölçülmez | Yüksek | H1: atlanan her mutation `Diagnostics`'e yazılır; R3 raporda en üstte gösterir |
| 3 | **Süre bütçesi patlar** — hibrit iki motorun maliyetini toplar (~28 + ~28 sn) | Orta | D61 zaten bunu öngörüyor (legacy + 35 sn); H3'te ölçülür, aşarsa DUR |
| 4 | **Tek kitaba overfit** — bütün ölçüm `odun-kesmek` üzerinde | Yüksek | M5 kalem olarak açıldı ve M4'ün önüne kondu |
| 5 | **İki motor birlikte çürür** — R4.3 kalıcı düştüğü için ikisi de yaşayacak | Orta | Legacy'ye yeni özellik eklenmez; X2 dışında yalnızca hata düzeltmesi alır |
| 6 | **Kapı gevşetilerek yeşile boyanır** | Yüksek | D77 (yalnızca yukarı) + D80 (taban değişimi ölçülür, seçilmez) |
| 7 | **Özel isimler "düzeltilir"** (*Auersberger*, *Rennweg*) | Orta | `ProperNameRisk` + `OriginalTokenIsValid` korunur; P1 bunları süpürülebilir yapar ama gevşetmez |
| 8 | **Recall için precision feda edilir** | Yüksek | Yanlış düzeltme sayısını **artıran** hiçbir değişiklik kabul edilmez (D90: ölçü, legacy'nin bugünkü sayısıdır — mutlak sıfır değil) |

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
- Yanlış düzeltme sayısını **artıran** hiçbir değişiklik kabul edilmez (D90: taban legacy'nin
  bugünkü sayısıdır, mutlak sıfır değil).
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
