# Karar Defteri

> Bu belge **hâlâ bağlayıcı** olan mimari kararları tutar — bugünkü kodun neden böyle olduğunu
> açıklayan kayıtlar. Tarihsel kararlar (bir kerelik sıra tercihleri, artık geçersiz tespitler,
> kapanmış borçlar) buraya taşınmadı; `adc202d` ve öncesindeki faz planlarında, git geçmişindedir.
>
> Numaralandırma **korunmuştur**: D9 hâlâ D9'dur. Atlanan numaralar tarihsel kararlardır, hata değil.
>
> Yeni karar ihtiyacı doğarsa agent kendi başına karara varmaz: gerekçeyi bildirir, karar buraya
> eklenir.

---

## 1. Süreç ve ölçüm

| # | Karar | Gerekçe |
|---|---|---|
| **D41** | Bir baseline ölçülemediğinde dosyaya `status: "aborted"` + `reason` + `measuredFields: null` yazılır. **Hiçbir alan tahminle veya sabitle doldurulmaz.** | İlk R3.5 baseline'ında üç alan kodda literal yazılıydı ve ölçüm gibi commit edilmişti. Ölçülmemiş bir sayı, eksik bir sayıdan tehlikelidir: sonraki kararlar ona dayanır. |
| **D71** | **Legacy'nin çıktısı ground truth sayılmaz.** Her kayıt bağlamıyla elle doğrulanır (`verifiedBy: "manual"`); legacy'nin önerisi ayrı alanda (`legacyProposal`) tutulur. | Aksi hâlde legacy'nin kendi yanlışları "doğru cevap" olarak sabitlenir ve ölçülemez hâle gelir. R5.0d bunu doğruladı: yeni tabanda legacy'nin 3 yanlış düzeltmesi ilk kez görünür oldu (`olın`→`olan`, doğrusu **`John`**). |
| **D70** | Ground truth genişletmesi rastgele değil, **motorların gerçekten dokunduğu bölgelerden** örneklenir. | 160 kayıt ile üretimdeki 120 mutation arasında hiçbir kesişim yoktu: kapı OCR motoru hakkında hiçbir şey ölçmüyordu. Karar sınırından örneklemek ground truth'u motorun ölçüm aleti yapar. **Yan etkisi kayıtlı:** hiçbir motorun dokunmadığı sınıflar (`MissingSpace`) havuza hiç girmez. |
| **D77** | Kalite kapısı eşikleri **yalnızca yukarı** hareket eder. Aşağı hareket karar gerektirir. | Recall peşinde koşarken kapıyı indirmek en kolay yoldur; bu yüzden açıkça yasaklanır. Gevşetilen kapı regresyonu gizler. |
| **D80** | **Ölçüm tabanı değiştiğinde** eşikler yeni taban üzerinde yeniden kurulur. Üç koruma: (1) yeni eşik **ölçülür, seçilmez** — iyi olan motorun değeri, aşağı yuvarlanmadan; (2) eski profil `supersededProfiles` altında **tabanıyla** arşivlenir; (3) D77 taban içinde aynen geçerlidir. | Eşik, *aynı ölçümün* tekrarları hakkında bir iddiadır. Ground truth 160 → 288 olunca %92,5 daha sıkı bir çıta değil, **başka bir çıta** oldu. Taban değişimini gevşetme saymak, doğru ölçüme geçmeyi cezalandırır. |
| **D79** | Kalemler **tek oturumluk iş birimlerine** bölünür: tek commit, en fazla bir full-book koşusu, tek dosya ailesi, tek doğrulanabilir çıktı. Elle etiketleme ≤ 25 kayıtlık partiler. | Saatler süren bir kalem yarım kaldığında ara durum test edilmemiş, commit'lenmemiş ve devredilemez olur — hiç başlanmamış iki küçük kalemden pahalıdır. Etiketleme partisi keyfi değil: doğrulama hızı ~1 kayıt/dk ölçüldü, 129 kayıt ~2 saat sürdü. |

---

## 2. Katmanlar ve bağımlılıklar

| # | Karar | Gerekçe |
|---|---|---|
| **D14** | Core'un mimari testi "hiç I/O yok" yerine **izin listeli** yazılır: `EpubPackageReader`, `EpubPackageWriter`, `EpubFixService`, `EpubOutputValidator`. | Bu dört dosya zaten `System.IO` kullanıyor; hepsini porta almak ayrı bir iş. Test yine de **beşinci** ihlali engeller. Yeni dosya izin listesine eklenmez. |
| **D10** | `MorphologyOracleException` **Core'da** tanımlanır. | `TurkishMorphologyException` `EpubFixer.TrMorph` içinde; Core'un onu kullanması bağımlılık kuralını ihlal eder. |
| **D27 / D58** | **SymSpell bir detaydır**; Core onu görmez. `LatticeOcrCorrectionPlanner` Core'da kalır, matcher `Func<BookVocabulary, ILexiconMatcher>` olarak enjekte edilir. | Detaylar kenarda, port arkasında durur. Kurulum sırası (knowledge → matcher → lattice → gate → planner) **politikadır** ve tek yerde yaşamalıdır. |
| **D57** | Ayrı bir **`src/EpubFixer.Adapters`** projesi: `SymSpellChecker`, `SymSpellLexiconMatcher`, `FileTurkishFrequencyListSource`, `tr_50k`. | İki composition root (Cli, Benchmarks) aynı detaya ihtiyaç duyuyor; benchmark'ın Cli'ı referanslaması bir exe'yi kütüphane gibi kullanmaktır. |
| **D53** | `IOcrCorrectionPlanner` `EpubFixService`'e **opsiyonel ctor parametresi** olarak girer, varsayılanı `LegacyOcrCorrectionPlanner`. | Mevcut çağrı yerleri değişmeden kalır; "hiçbir davranış değişmedi" iddiası test diff'i olmadan görünür olur. |
| **D7** | Orkestratörler `IMorphologyOracleBuilder` alır; **yaprak politika bileşenleri** saf `IMorphologyOracle` alır. | Yaprak bileşenler bilgi tabanı kurmaz, sorgular. Builder'ı yaprağa vermek her yaprağa kendi prefill'ini yazma yetkisi verir. |
| **D5** | `IMorphologyOracleBuilder.Build` **birden fazla kez** çağrılabilir ve **monotondur**: dönen her oracle o ana kadar çözülmüş her şeyi bilir. "Tek batch" = *kelime başına değil, aşama başına*. | `EpubFixService.Fix` metni değiştirerek birden fazla aşama koşuyor; her aşama yeni yüzey biçimleri üretir. |

---

## 3. Bilgi tabanı (hazne, morfoloji, dil modeli)

| # | Karar | Gerekçe |
|---|---|---|
| **D9** | Oracle anahtarı **NFC + Ordinal + case-sensitive**. `TurkishWordNormalizer.Normalize` kullanılmaz. | O metot küçük harfe çeviriyor; morfolojide `Auersberger` ≠ `auersberger` ve **özel isim davranışı buna bağlı**. |
| **D17** | **İki normalizasyon uzayı**: morfoloji NFC + case-sensitive (D9), hazne/LM NFC + tr-TR lower. Hazne baskın **yüzey biçimini** ayrıca saklar. | Haznede `Berjer` ile `berjer` aynı kelime; morfolojide değil. Decoder EPUB'a yazarken doğru yüzey biçimine ihtiyaç duyar. |
| **D20** | Hazneye giriş filtreleri (şüpheli token, tek karakter, doğrulanmamış özel isim eşiği) **kapsama uğruna gevşetilmez**. | Kontaminasyon = motorun hatayı "doğru" sayması. Bu, kaçırılmış bir düzeltmeden pahalıdır. |
| **D21** | `BookHealthMeter`'ın tanıyıcısı motorun **kendi bilgi tabanına bağlanmaz**. | Kuzey yıldızı metriği motorun haznesine bağlanırsa kendini onaylar: kitapta iki kez geçen bir OCR artığı "çözümlendi" sayılır ve sayı gerçek olmayan bir iyileşme gösterir. |
| **D22** | LM **stupid backoff**'tur ve normalize olasılık değil **skor** döndürür. Kalite ölçütü perplexity değil **held-out bigram isabet oranı**dır. | Stupid backoff normalize değildir; perplexity raporlamak yanlış bir kesinlik iddiasıdır. |

---

## 4. Lattice motoru

| # | Karar | Gerekçe |
|---|---|---|
| **D26** | Karışım tablosu eski motordan **kopyalanır, ortaklaştırılmaz**. | Matcher maliyeti, arc maliyeti ve "olağan ikame ≤ 1" kuralı aynı hesaptır; üç yerde yazılırsa üçü ayrışır. Ama ölmekte olan motorla ortak tip paylaşmak da onu canlı tutar. |
| **D28** | Maliyet sayıları **`OcrEditCostModel`'den** alınır. Belgelerdeki "0.25 / 0.5" değerleri eskidir (gerçek: **0.30 / 0.70**). | Tek doğruluk kaynağı koddur. |
| **D29** | Kafeste **identity** ve **literal** arc'ları zorunludur; `LatticeArc` bir `Kind` alanı taşır. | "Dokunma" yolunun kafeste maliyetsiz var olması gerekir: kapı en iyi yolu identity yoluyla karşılaştırarak karar verir ve her pencerede tam bir yol garanti edilir. |
| **D42 / D59** | Hard boundary'ler `WordLatticeBuilder`'a **dışarıdan** verilir (`LogicalTextStreamBoundaries.HardOffsets`). Core'daki `\n\n` taraması yalnızca ham metin/fixture yolu için fallback. | `LogicalTextStreamBuilder` text node'ları ayraçsız birleştiriyor; pencere paragraf sınırını aşarsa kafes olmayan bir bağlam uydurur. |
| **D43** | Çözücü state anahtarı **`(düğüm, PreviousWord)`**; anahtar başına en iyi `kBest` state tutulur, `Text` ve `Arcs` state'te taşınmaz (geri işaretçi + backtrack). | Bigram LM'de gelecek maliyeti yalnızca bu ikiliye bağlıdır → anahtar başına en ucuzu tutmak Top-1'i **exact** bırakır. Beam değil. |
| **D32** | Kapı kuralı "orijinal zaten geçerli" morfoloji oracle'ını **çağırmaz**; hazne üzerinden karar verir (`BookCount ≥ 2` veya `Source ∈ {Frequency, Morphology}`). | Hazne morfolojik olarak geçerli formları zaten içeriyor. Oracle çağrısı "bilinmeyende exception" kuralı yüzünden prefill borcu doğurur. |
| **D45** | Kapıya dört kural: `UnsafeLengthChange`, `ProperNameRisk`, apostrof öncesi kök değişimi, `SuspiciousReplacement` (`"ie"`/`"ıe"`). | Precision savunması. **Borç:** eşikleri `LatticeOptions` dışında sabit — roadmap P1 bunu kapatır. |
| **D46** | `MaxOrdinarySubstitutions` yalnızca ikameleri değil **tüm olağan düzenlemeleri** (ikame + ekleme + silme) sınırlar. Ad değişmedi. | Yalnızca ikame sayılırken `1 → göster` gibi saf eklemeden oluşan uydurmalar kuralı hiç görmeden geçiyordu. |
| **D47** | Fixture ölçümü **gerçek `tr_50k` haznesi + gerçek LM (λ=0,5)** ile yapılır. λ=0 ile ölçüm **geçersizdir**. | Identity arc'ları 0 maliyetlidir; λ=0 iken "dokunma" yolu daima en ucuzdur ve Top-1 motorun kalitesinden bağımsız olarak 0/10 çıkar. Ölçümü kelimeye iten tek kuvvet LM terimidir. |

---

## 5. Üretim hattı ve mutation

| # | Karar | Gerekçe |
|---|---|---|
| **D54** | `OcrCorrectionMutation.Decision` → **`OcrMutationProvenance`**. `OcrCorrectionMutationPlan`, `OcrMutationSourceSpan`, `Applier` değişmez. | Mutation'ın somut `OcrCorrectionDecision`'a bağlı olması iki hattın veri modeli uyumsuzluğunun köküydü. |
| **D55** | Çakışan / boş / değişmeyen bölge düzeltmeleri **atlanır**, plan başarısız edilmez. Üretilen plan daima `IsValid`. | `EpubFixService` geçersiz planda exception atıyor: tek bir çakışma = tüm kitabın düzeltilmemesi. "Emin değilsen dokunma" ilkesi **region düzeyinde** uygulanır, kitap düzeyinde değil. |
| **D56** | `RegionMutationPlanner.Create` **`RegionMutationPlanResult`** döner (plan + `Skipped` tanılama). | D55'in atlananları görünür olmalı; **sessizce düşen düzeltme, ölçülemeyen recall kaybıdır.** |
| **D36** | Enum'lara yeni değer **sona** eklenir. | Mevcut karşılaştırma kodu enum değerlerini dizi indeksi olarak kullanıyor; sıra değişimi raporu sessizce bozar. |
| **D61** | Süre bütçesi: `fix --apply-ocr-corrections` ≤ **120 sn** (hard gate) **ve** legacy toplamının **≤ +35 sn** üstü. | Salt 120 sn yetmez: legacy'nin bugünkü süresi bilinmiyorsa regresyon fark edilmez. Alt bütçe olmadan gate son anda patlar. |

---

## 6. Strateji (sürüm 2 ile verildi, 2026-09-15)

| # | Karar | Gerekçe |
|---|---|---|
| **D81** | **Motor stratejisi hibrittir: legacy birincil, lattice tamamlayıcı.** `D52` ("iki motor birbirini dışlar, planları hiçbir zaman birleştirilmez") **iptal edilmiştir.** | D52'nin gerekçesi "iki farklı gerekçe modelinden gelen çakışan mutation'ları çözmek riski ikiye katlar" idi. Bu gerekçe **D82 ile ortadan kalkar**: birleştirme karar modellerini uzlaştırmaz, mutation'ları geometriyle ayıklar. Karşılığında ölçülmüş 9 vakalık kazanç alınır. Yerine geçme hedefi ise ölçülebilir biçimde ulaşılamaz: ulaşılabilir tavan 77 vaka, KAYIP 42'nin altına inemez. |
| **D82** | Birleştirme **plan düzeyindedir**, ardışık koşum değil: iki planner aynı `finalStream`'i alır, planlarını **aynı snapshot** üzerine kurar, sonuçlar geometriyle birleştirilir. | Ardışık koşum (legacy uygula → stream'i yeniden kur → lattice koş) stream'in yeniden inşasını ve morfoloji prefill'inin tekrarını gerektirir; süre bütçesini ve `ExpectedText` doğrulamasını birlikte riske atar. Plan düzeyi ayrıca **A/B ölçümünü birebir korur**: lattice hibritte tam olarak A/B'de gördüğü girdiyi görür, dolayısıyla 9 KAZANÇ doğrulanabilir bir tahmindir. |
| **D83** | Logical aralık çakışmasında **birincil (legacy) kazanır**; ikincilin mutation'ı atlanır ve `Diagnostics`'e yazılır. | Bilinen tek çakışmada (`kol-1 ıı kta`) **her iki motor da yanlış**. Kural kalite üzerinden seçilemez; öngörülebilirlik üzerinden seçilir. Atlananların kaydı D56'nın kuralıdır. |
| **D84** | **R4.3 (eski yolların kaldırılması) kalıcı olarak düşürülmüştür.** Legacy üretim motorudur, ölmekte olan kod değildir. | D62/D67 R4.3'ü "lattice legacy'nin üst kümesi olursa" koşuluna bağlamıştı; D81 ile bu koşul artık **hedef bile değil**. Sonucu kabul edilir ve yazılır: iki motor kalıcı olarak birlikte yaşar. Risk kaydı #5 bunu izler. |
| **D85** | Faz planları (`phase-0..5-plan.md`, `phase-5-tasks.md`) **silindi**; yerine tek roadmap + bu belge. | 6.740 satır plan, 7.676 satır üretim koduna eşlik ediyordu ve büyük kısmı tamamlanmış işin tarihçesiydi. Tarihçe git'te yaşar; yol haritası **önde** olanı göstermelidir. Bağlayıcı kararlar bu belgeye taşındı, tarihsel olanlar bırakıldı. |
| **D86** | Lattice'in başarı ölçütü **"legacy'ye yetişmek" değil, "legacy'nin üstüne net kazanç koymak"**tır. KAYIP 119 artık bir açık değildir. | KAYIP, "legacy düzeltiyor ama lattice düzeltmiyor" demektir. Hibritte legacy **zaten düzeltiyor**. O 119 vakanın peşinden gitmek, çözülmüş bir problemi ikinci kez çözmektir. |
| **D87** | **Tek kitap borcu birinci sınıf iş kalemidir** (roadmap M5) ve M4'ün (eşik kalibrasyonu) **önüne** konur. | Bütün ölçüm `odun-kesmek` üzerinde. Sürüm 1 bunu açık borç olarak kaydetti ama kalem açmadı. Hibrit hattı ikinci kitapta doğrulanmadan kalibrasyona girmek, tek kitaba iki kat daha fazla overfit etmektir. |
| **D88** | `CompositeOcrCorrectionPlanner`'da plan geçerliliği **yalnızca birincilin (legacy) `Failures`'ına** bağlıdır. İkincilin (lattice) failure'ları `Plan.Failures`'a eklenmez; yalnızca `Diagnostics`'e yazılır. | Naif birleştirme (`primary.Failures + secondary.Failures`) D55'in ilkesini kitap düzeyine taşırdı: lattice'in tek bir failure'ı, legacy'nin tek başına sorunsuz düzelttiği kitabı `IsValid = false` yapıp tamamen düzeltilmemiş bırakırdı. D81'in "legacy birincil, lattice tamamlayıcı" stratejisiyle tutarlı seçenek, ikincilin başarısızlığını **region düzeyinde görünür** (D56) ama **kitap düzeyinde etkisiz** tutmaktır. |
| **D89** | Motor adı → planner çözümlemesi **tek bir yerde** yaşar (`OcrPlannerFactory`, Adapters). Tanınmayan bir ad **exception atar**, sessizce legacy'ye düşmez. İki composition root (Cli, Benchmarks) çözümleme, arg doğrulama ve kullanım metni için aynı factory'yi çağırır. | Switch'i iki root'ta kopyalamak, listelerin **sessizce ayrışması** demektir: Cli `hybrid`'i tanırken benchmark tanımazsa benchmark legacy'ye düşer ve **ölçüm yanlış motoru ölçer** — üstelik yeşil görünerek. Bu, G1 sırasında kapıda bulunan sessiz-fallback hatasının (G3) motor seçimindeki tam karşılığıdır; aynı ders iki yerde ödenmez. D57'nin desenini izler: iki root'un ihtiyaç duyduğu detay Adapters'ta tek kez kurulur. |
| **D90** | Hibritin kabul ölçütü **"yanlış düzeltme sıfır" değil, "legacy'ye göre yeni yanlış düzeltme sıfır"**dır. Legacy'nin bugünkü 3 yanlış düzeltmesi hibritin borcu değildir; X2 kaleminin konusudur. | Mutlak sıfır ölçütü **tutarsızdı**: legacy bugün varsayılan motor ve üretimde 3 yanlış düzeltmeyle koşuyor. Hibrit legacy'nin mutation'larını birincil olarak taşıdığı için (D81/D83) o üçünü zorunlu olarak miras alır — yani mutlak sıfır ölçütüyle hibrit **hiçbir zaman** geçemez, ne kadar iyileşirse iyileşsin. Ölçüt, ölçmek istediği şeyi ölçmeli: hibride geçmenin **zarar verip vermediğini**. H3 bunu ilk kez sayıya döktü — hibrit tam olarak **1** yeni yanlış getirdi (`garbage-0001`), ve o tek vaka çözülürse kapı yeşile döner (precision %98,52 → %98,89, eşik %98,85). |
| **D91** | Kafeste sert çöp glifi (`^ ; < > :`) taşıyan **token-arası** koşu iki arc alır: **tutmak** `RetainedGarbage` (0,25) × glif, **silmek** `GarbageDeletion` (0,20) × glif ve tek boşluk yazar. İki arc **aynı karakterleri** sayar. İki koruma: sert çöp yoksa koşu bedava ve silinemez kalır (sıradan noktalama vergilendirilmez, iki cümle birbirine kaynamaz); boşluk yoksa koşu token içindedir ve word arc'ına bırakılır. | İlk deneme yalnızca silme arc'ı ekledi ve **ölü çıktı**: `LatticeDecoder.Append` LM maliyetini yalnız `Word`/`Identity` arc'larına uyguluyor, dolayısıyla `Literal` (çöpü tut) 0,00 iken silme 0,40 idi — silme her zaman tam kendi maliyeti kadar kaybediyordu ve tam kitap koşusunda **hiçbir sayı değişmedi**. Kök neden silme arc'ının yokluğu değil, **çöp tutmanın bedava olması**ydı: maliyet modeli "çöp silmek ucuzdur" diyordu ama karşılaştırma tabanı sıfırdı. Simetri zorunlu — tutma yalnız sert glifleri, silme tüm gliferi sayarsa `:,` için 0,25 < 0,40 olur ve tutma yine kazanır. 0,25 **seçildi, kalibre edilmedi** (D34): modelde zaten kullanılan 0,20'nin üstündeki en küçük adım. Ölçüldü: `garbage-0001` yanlıştan doğruya döndü, precision %98,52 → %98,89, **kapı yeşil**, yeni yanlış düzeltme sıfır (D90). |
| **D92** | Varsayılan motor **hibrit**tir ve **yalnızca composition root'larda** (Cli, Benchmarks) çevrilir; `EpubFixService`'in kütüphane varsayılanı `LegacyOcrCorrectionPlanner` **kalır**. Kapı eşikleri, gönderilen motorun ölçülen değerlerine **yukarı çekilir** ve profil `shippedEngine` alanını taşır. | Core hibridi **kuramaz**: `LatticeOcrPlannerFactory` Adapters'ta çünkü SymSpell'e bağlı (D27/D58). Core'un varsayılanı hibrit olsaydı Core SymSpell'i görmek zorunda kalırdı — bağımlılık kuralının ihlali. Bu yüzden çevirme mimari olarak yalnızca root'larda mümkün, ve bunun bir sonucu var: kütüphane varsayılanını pinleyen golden SHA **değişmez**. Üretimi korumasız bırakmamak için hibrit için **ikinci bir golden** eklendi. Eşikler hibridin değerine çekildi çünkü hibrit her iki eksende de legacy'nin üstünde (precision %98,89 > %98,85, recall %92,71 > %89,93, ClassRecall:Hyphenation %98,30 > %96,59) — D77'nin cırcırı bunu zaten *izin veriyor*, D92 *zorunlu* kılıyor: aksi hâlde kapı, gönderilmeyen bir yapılandırmayı ölçmeye devam ederdi. Yan etki kabul edildi: `--ocr-engine legacy` artık FAIL raporluyor. Bu bir kusur değil, kapının **ifadesi**: gönderilen hat kendi yedeğinden iyidir. |

---

## 7. Arşiv

Bu belgeye taşınmayan kararlar tarihseldir: bir kerelik sıra tercihleri (D1, D3, D12, D39, D49),
artık geçersiz tespitler (D50, D64, D66), kapanmış borçlar (D48, D51), tamamlanmış kalemlerin
kabul ölçütleri (D6, D11, D23, D25, D35, D44) ve sürüm 2 ile yerini kaybeden stratejik kararlar
(D52 → D81, D60, D62, D67, D68, D69, D72–D76, D78).

Tamamı `adc202d` commit'indeki faz planlarındadır:

```bash
git show adc202d:docs/phase-3-plan.md
```

**Atıf konvansiyonu:** Kod yorumlarında ve baseline dosyalarında geçen `docs/phase-5-plan.md@adc202d`
biçimindeki atıflar **silinmiş** bir belgeye, arşiv commit'i üzerinden işaret eder. Böyle bir atıf
gördüğünde dosyayı diskte arama — `git show adc202d:<yol>` ile aç. Bu atıflar ölçümün kökenini
kaydeder; yeni iş için kaynak değildirler. Yeni kod bu biçimde atıf **eklemez**, bu belgeye veya
roadmap'e atıf yapar.
