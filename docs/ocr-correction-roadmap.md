# OCR Düzeltme Motoru — Yol Haritası

> Bu belge, her maddesi ayrı bir oturumda ayrı bir agent'a devredilecek şekilde yazılmıştır.
> Her iş kaleminde **Amaç / Kapsam / Sözleşme / Kabul kriteri / Bağımlılık** alanları vardır.
> Belgenin sonundaki [devir şablonunu](#devir-şablonu) kullanın.

---

## 1. Hedef

Türkçe OCR ile üretilmiş bir EPUB'ın okunabilir hale gelmesi. **%100 doğruluk hedef değil.**
Kabul edilen denge:

| | Hedef |
|---|---|
| Precision (uygulanan düzeltmelerin doğruluğu) | ≥ %98 — yanlış düzeltme, düzeltilmemiş hatadan pahalıdır |
| Recall (yakalanan hata oranı) | ≥ %60 — geri kalanı **dokunulmadan** bırakılır |
| Full-book çalışma süresi | ≤ 120 sn (hard gate) |
| Çevrimdışı | Zorunlu — ağ yok, harici servis yok |

Emin olunmayan her durumda karar **"dokunma"**dır.

---

## 2. Mevcut durum tespiti

### İyi durumda — korunacak

- `EpubPackageReader` / `LogicalTextStreamBuilder` / `EpubPackageWriter` — DOM ve source-location güvenliği
- `OcrCorrectionMutationPlanner` / `OcrCorrectionMutationApplier` / `EpubOutputValidator` — mutation ve bütünlük doğrulama
- `OcrRegionDetector` / `OcrAnomalyDetector` — bozuk bölge tespiti
- `OcrEditCostModel` — kalibre edilmiş OCR maliyet modeli (yeni motorda aynen kullanılacak)
- `QualityBenchmark*` altyapısı — ground-truth koşum iskeleti

### Bozuk — değişecek

**B1 — Arama yanlış boyutta tanımlı.**
`NoisyChannelRegionReconstructor.Expand` her pozisyon için 29 harfin tamamını deniyor
(`TurkishLetters`), successor sayısı ≈ 29·L. Üstüne `RetentionKey` her expanded state için
`Expand`'i **bir kez daha** çağırıyor (one-step lookahead):

```
beam 256 × successor ~870          ≈ 2,2×10⁵  state / derinlik
+ lookahead re-expansion           ≈ 1,9×10⁸  state / derinlik
× 4 derinlik × 459 region          ≈ 3,5×10¹¹
```

Bu iş bitmez. "Pathological input" denen şey, yalnızca uzunluğu büyük olan ilk region'dır.

**B2 — Skorlayıcı tek-token, veri çok-token.**
`Score` tüm region string'ini tek kelime gibi değerlendiriyor
(`cleanLexicon.Contains`, `analyzer.IsValidWord`). Ama `OcrRegionDetector.CanExpand` komşu
fragment'lere yayılarak çok kelimeli span üretiyor. Boşluk içeren doğru cevap hiçbir zaman
geçerli sayılamaz → `Score` null döner → o region'da **hiç aday üretilmez**. 10 örneklik
fixture tek kelimelik hedeflerden oluştuğu için bu maskelenmişti.

**B3 — Birbirine bağlanmamış iki paralel hat.**

```
ÜRETİM   (fix --apply-ocr-corrections)
  OcrAnomalyDetector → OcrCorrectionCandidateGenerator → OcrCorrectionDecisionEvaluator
    → OcrCorrectionMutationPlanner → OcrCorrectionMutationApplier → EPUB

DENEYSEL (yalnızca debug-ocr-* / reader-preview komutları)
  OcrRegionDetector → NoisyChannelRegionReconstructor → DeterministicOcrCandidateReranker
    → (hiçbir yere bağlanmıyor)
```

Üzerinde çalışılan motor EPUB'a hiç dokunmuyor. Yeni motor **üretim hattına** inmeli.

**B4 — TRmorph sıcak döngüde.**
`IsValidWord` çağrı yerleri: `OcrRegionDetector` (fragment başına), `OcrAnomalyDetector`
(aday başına), `NoisyChannel.Score` (beam state başına), `DeterministicOcrCandidateReranker`
(bağlam kelimesi başına). Her biri native `flookup` process'ine gidiş-dönüş. Batching/cache
semptomu hafifletti, nedeni çözmedi.

**B5 — Katman ihlali.**
`CleanTurkishLexicon`, `BookContextIndex`, `NoisyChannelRegionReconstructor`,
`DeterministicOcrCandidateReranker`, `OcrEditCostModel` — hepsi `EpubFixer.Cli` içinde.
Bunlar I/O değil, **politika**. Core'a taşınmalı; Cli'da yalnızca dosya yükleme adapter'ı kalmalı.

**B6 — Yönü ölçen bir sayı yok.**
Tek ölçüt 10 örneklik fixture ve sayaç dolu raporlar. "Hiyeroglif gibi görünmesin" şu an
ölçülebilir değil. Ayrıca `QualityBenchmarkGateEvaluator` tüm oranlarda **tam %100** talep
ediyor (`AddPerfectRateFailure`) — istatistiksel bir motor için kullanılamaz bir kapı.

**B7 — Ground truth yanlış sınıfı kapsıyor.**
`test-data/odun-kesmek/ground-truth.json` 148 kayıt içeriyor ama ağırlıklı olarak tireleme
(`Auers-berger`, `Vi-yana`, `Jo-ana`) — zaten çözülmüş kolay sınıf. Zor sınıflar
(`koli ukta`, `ı ıç`, `:,ohbet`, `ge-^:cn`) temsil edilmiyor.

---

## 3. Hedef mimari

Temel fikir: **string'ler üzerinde arama yapmayı bırak; küçük bir pencerenin segmentasyonları
üzerinde arama yap, ve adayları *üreterek* değil *sorgulayarak* elde et.**

```
┌─ Build (kitap başına bir kez, disk cache) ────────────────────────────┐
│  MorphologyOracle   ← TRmorph, TEK batch, sonra runtime'da hiç çağrılmaz │
│  BookVocabulary     ← kitabın temiz token'ları + tr_50k + doğrulanmış formlar │
│  BookLanguageModel  ← kitabın temiz bölgelerinden bigram + unigram backoff │
└───────────────────────────────────────────────────────────────────────┘
                                  │
┌─ Decode (region başına, bounded) ─────────────────────────────────────┐
│  1. Pencere      region + iki yanından 1 token, ≤ 48 karakter          │
│  2. ILexiconMatcher(span, budget) → (word, cost)[]   ← sorgu, üretim değil │
│  3. WordLattice  arc = (i, j, word, cost); boşluk = ucuz normal sembol │
│  4. LatticeDecoder  Viterbi: best[j] = min(best[i] + cost + λ·−logP(w|prev)) │
│  5. AcceptanceGate  eşik + margin + "temiz token'a dokunma" + confusion sınırı │
└───────────────────────────────────────────────────────────────────────┘
                                  │
                    RegionMutationPlanner → mevcut Applier → EPUB
```

**Split/join neden bedava çözülür:** boşluk özel bir operatör değil, maliyeti düşük sıradan
bir sembol (silme 0.25, ekleme 0.5). Bir arc birden fazla iç boşluk kapsarsa → *join*; bir arc
token ortasında biterse → *split*. Üçü de tek weighted-edit uzayında.

```
 k  o  l  i  ␣  u  k  t  a
 0  1  2  3  4  5  6  7  8  9
 └────────── arc 0→9 "koltukta" ──────────┘   space-del 0.25 + subst(i→t) 1.0 = 1.25  ✓
 └── 0→4 "koli" ─┘└──── 5→9 "ukta" ────┘     her ikisi de OOV → budget aşımı          ✗
```

**Bağlam neden reranker'da değil decoder'da:** `P(koltukta | berjer)` bu kitapta çok yüksek.
Kararı verirken kullanmak, verdikten sonra düzeltmeye çalışmaktan hem doğru hem ucuz.

---

## 4. Fazlar

Faz 0 **önce** gelir: ölçüm olmadan optimizasyon yön duygusu olmadan yürümektir.

| Faz | Başlık | Çıktı |
|---|---|---|
| 0 | Ölçüm ve emniyet ağı | Yönü gösteren tek sayı + gerçekçi kalite kapısı |
| 1 | Morfolojiyi sıcak döngüden çıkar | TRmorph runtime'da sıfır çağrı |
| 2 | Kelime haznesi + dil modeli | Decode için gereken bilgi tabanı |
| 3 | Yeni düzeltme motoru | Lattice + Viterbi + gate |
| 4 | Üretim hattına bağlama | `fix` komutu yeni motoru kullanır |
| 5 | Kalite turu | Eşik kalibrasyonu, weighted matcher, insan incelemesi |
| 6 | Tavan yükseltme (opsiyonel) | Second-pass OCR |

---

### Faz 0 — Ölçüm ve emniyet ağı

#### R0.1 — Kitap sağlık metriği

- **Amaç:** "Hiyeroglif gibi görünmesin"i tek bir sayıya indirgemek. Yol haritasının kuzey yıldızı.
- **Kapsam:** `EpubFixer.Core/Quality/BookHealthMetric.cs` + CLI `measure` komutu.
- **Sözleşme:**
  ```csharp
  public sealed record BookHealth(
      int TotalTokens,
      int UnresolvableTokens,      // ne vocabulary'de ne morfolojik olarak geçerli
      int SuspiciousTokens,        // garbage glyph / gömülü rakam / izole ı,i,l,1
      double UnresolvableRate,     // 1000 kelimede
      IReadOnlyList<string> WorstExamples);

  public interface IBookHealthMeter { BookHealth Measure(LogicalTextStream stream); }
  ```
- **Kabul:** `measure` komutu hem kaynak hem çıktı EPUB için çalışır; `odun-kesmek` için
  bugünkü değer baseline olarak `docs/baselines/` altına yazılır. Birim testler sentetik
  stream'lerle.
- **Bağımlılık:** yok. **Boyut:** S.

#### R0.2 — Ground truth'u zor sınıflarla genişlet

- **Amaç:** Mevcut 148 kayıt ağırlıklı olarak tireleme; zor OCR sınıfları temsil edilmiyor.
- **Kapsam:** `test-data/odun-kesmek/ground-truth.json` şemasına `errorClass` alanı ekle,
  full-book'tan stratified örnekleme ile **sınıf başına ≥ 25 kayıt** olacak şekilde genişlet.
- **Hata sınıfları:** `Hyphenation`, `GlyphConfusion` (ı/i/l/1), `SpuriousSpace` (`koli ukta`),
  `MissingSpace`, `GarbageInsertion` (`:,ohbet`, `ge-^:cn`), `Fragmentation` (`1 ı iç`), `Mixed`.
- **Sözleşme:** `schemaVersion` 1 → 2; `QualityBenchmarkDatasetLoader` geriye dönük uyumlu kalır.
- **Kabul:** Loader yeni şemayı okur, eski dosyayı da okumaya devam eder;
  `QualityBenchmarkReportWriter` sınıf kırılımlı precision/recall basar.
- **Bağımlılık:** yok (R0.4 ile paralel yürüyebilir). **Boyut:** M — etiketleme emek ister.

#### R0.3 — Performans bütçesi testi

- **Amaç:** Regresyonun sessizce geri gelmemesi.
- **Kapsam:** `tests/EpubFixer.Tests/PerformanceBudgetTests.cs`.
- **Kabul:** Full `odun-kesmek` koşusu 120 sn'yi aşarsa test **kırmızı**. Ayrıca region başına
  ziyaret edilen state sayısı için üst sınır assert'i (algoritmik regresyonu saatlerce
  beklemeden yakalar).
- **Bağımlılık:** yok. **Boyut:** S.

#### R0.4 — Kalite kapısını gerçekçi hale getir

- **Amaç:** `QualityBenchmarkGateEvaluator` şu an her oranda tam %100 talep ediyor
  (`AddPerfectRateFailure`). İstatistiksel motorla kullanılamaz.
- **Kapsam:** Mutlak eşik yerine bütçe: `Precision ≥ 0.98`, `Recall ≥ 0.60`,
  `ProtectedViolated == 0` (bu sıfır kalmalı), `UnexpectedTextChanges == 0`.
- **Kabul:** Kapı eşikleri yapılandırılabilir ve testlerde açıkça belirtilir;
  precision hesabı sınıf kırılımlı raporlanır.
- **Bağımlılık:** R0.2 (sınıf kırılımı için). **Boyut:** S.

---

### Faz 1 — Morfolojiyi sıcak döngüden çıkar

#### R1.1 — `IMorphologyOracle` port'u

- **Amaç:** B4. Runtime'da TRmorph'a sıfır çağrı.
- **Kapsam:** `EpubFixer.Core/Morphology/IMorphologyOracle.cs` + in-memory implementasyon.
- **Sözleşme:**
  ```csharp
  public interface IMorphologyOracle
  {
      bool IsValid(string word);                                  // asla I/O yapmaz
      IReadOnlyList<TurkishMorphologicalAnalysis> Analyze(string word);
      bool IsKnown(string word);                                  // oracle bu kelimeyi gördü mü
  }

  public interface IMorphologyOracleBuilder
  {
      IMorphologyOracle Build(IEnumerable<string> vocabulary);    // TEK batch
  }
  ```
- **Kural:** `IsValid` bilinmeyen kelimede **exception atar**, sessizce `false` dönmez —
  eksik ön-doldurma sessiz kalite kaybı değil, gürültülü hata olsun.
- **Kabul:** `FomaTurkishMorphologyAnalyzer` tek `AnalyzeBatch` çağrısıyla oracle'ı doldurur;
  fake oracle ile birim testler.
- **Bağımlılık:** yok. **Boyut:** S.

#### R1.2 — Oracle disk cache'i

- **Amaç:** Aynı kitapta tekrar koşularda TRmorph'u hiç başlatmamak.
- **Kapsam:** `EpubFixer.Cli/Morphology/MorphologyOracleCache.cs` (adapter katmanı — I/O burada).
- **Sözleşme:** Cache anahtarı = kaynak EPUB SHA-256 + TRmorph fst hash'i. Bozuk/eksik cache
  sessizce yeniden üretilir.
- **Kabul:** İkinci koşuda `processInvocations == 0`.
- **Bağımlılık:** R1.1. **Boyut:** S.

#### R1.3 — Çağrı yerlerini oracle'a taşı

- **Amaç:** `ITurkishMorphologyAnalyzer` bağımlılığını sıcak yollardan kaldırmak.
- **Kapsam:** `OcrRegionDetector`, `OcrAnomalyDetector`, `OcrCorrectionCandidateGenerator`,
  `HyphenationMorphologyAnalyzer` → `IMorphologyOracle` alacak şekilde. Ön-doldurma pass'i:
  metnin tüm unique token'ları tek seferde.
- **Dikkat:** `OcrRegionDetector` ve `OcrAnomalyDetector` **kendi girdileri dışındaki** formları
  da soruyor (örn. `TrimBoundaryPunctuation` sonrası). Ön-doldurma bunları da kapsamalı;
  R1.1'deki "bilinmeyende exception" kuralı bu eksikleri açığa çıkaracaktır.
- **Kabul:** Mevcut tüm testler yeşil; `TurkishMorphologyCacheStatistics.ProcessInvocations`
  full koşuda ≤ 1.
- **Bağımlılık:** R1.1. **Boyut:** M.

---

### Faz 2 — Kelime haznesi ve dil modeli

#### R2.1 — `BookVocabulary`

- **Amaç:** Türkçe sondan eklemeli; 50k'lık liste tek başına yetersiz kapsama veriyor.
  Kitabın kendisi domain-matched ve özel isimleri içeriyor (*Auersberger*, *Simmeringer*, *Rennweg*).
- **Kapsam:** `EpubFixer.Core/Lexicon/BookVocabulary.cs`.
- **Sözleşme:**
  ```csharp
  public sealed class BookVocabulary
  {
      bool Contains(string word);
      double UnigramLogProbability(string word);   // backoff dahil
      VocabularySource SourceOf(string word);      // Book | Frequency | Morphology
  }
  ```
- **Bileşim:** kitabın token'ları (frekans ≥ 2 **veya** morfolojik olarak geçerli) + `tr_50k`
  + oracle'da geçerli formlar. Bozuk region'lardaki token'lar **hariç tutulur**
  (`BookContextIndex.Build` bunu zaten yapıyor — aynı yaklaşım).
- **Kabul:** `odun-kesmek` için kapsama raporu: kitabın temiz token'larının ≥ %95'i vocabulary'de.
- **Bağımlılık:** R1.1. **Boyut:** M.

#### R2.2 — `BookLanguageModel`

- **Amaç:** Bağlamı decoder'ın içine almak (B2'nin ikinci yarısı).
- **Kapsam:** `EpubFixer.Core/Lexicon/BookLanguageModel.cs`.
- **Sözleşme:**
  ```csharp
  public interface ILanguageModel
  {
      double LogProbability(string word, string? previousWord);   // stupid backoff
  }
  ```
- **Kabul:** `P(koltukta | berjer) > P(koltukta | <herhangi>)` gibi doğrulanabilir
  iddialar birim testlerde; bilinmeyen bigramda unigram'a, bilinmeyen unigram'da sabit
  cezaya düşer.
- **Bağımlılık:** R2.1. **Boyut:** S.

#### R2.3 — Katman düzeltmesi: Cli → Core

- **Amaç:** B5. Politika Core'a, I/O Cli'da kalsın.
- **Kapsam:** `CleanTurkishLexicon`, `BookContextIndex`, `OcrEditCostModel` → `EpubFixer.Core`.
  `tr_50k` dosya yüklemesi Cli'da `ITurkishFrequencyListSource` adapter'ı olarak kalır.
- **Kabul:** `EpubFixer.Core` hiçbir dosya sistemi/process API'si import etmez;
  bunu doğrulayan bir mimari testi eklenir.
- **Bağımlılık:** yok (Faz 3'ten **önce** yapılırsa Faz 3 doğru yere yazılır — sıralaması önemli).
- **Boyut:** M.

---

### Faz 3 — Yeni düzeltme motoru

> Bu fazın tamamı `EpubFixer.Core/Ocr/Lattice/` altında yeni tiplerdir.
> Mevcut `NoisyChannelRegionReconstructor`'a **dokunulmaz** — A/B karşılaştırması için yaşar.

#### R3.1 — `ILexiconMatcher` + SymSpell adapter

- **Amaç:** Aday getirme: *üretim* değil *sorgulama*.
- **Sözleşme:**
  ```csharp
  public readonly record struct LexiconMatch(string Word, double Cost);

  public interface ILexiconMatcher
  {
      IReadOnlyList<LexiconMatch> Match(ReadOnlySpan<char> span, double budget);
  }
  ```
- **İlk implementasyon:** `SymSpellLexiconMatcher` — SymSpell zaten dependency (6.7.3), hızlı kurulur.
  Sınırı: weighted cost desteklemiyor, `ı/i/l/1` karışmasını sıradan substitution sayıyor.
  Bu bilinçli bir geçici kabul; R5.2'de değiştirilecek.
- **Kabul:** `koltukta`, `üç`, `hiç`, `sohbet`, `geçen`, `yürümeye` hedefleri ilgili
  span'ler için dönen listede **var** (sıralama bu adımda önemsiz).
- **Bağımlılık:** R2.1, R2.3. **Boyut:** M.

#### R3.2 — `WordLatticeBuilder`

- **Amaç:** Split + join + karakter bozulmasını tek uzayda toplamak.
- **Sözleşme:**
  ```csharp
  public sealed record LatticeArc(int From, int To, string Word, double Cost);
  public sealed record WordLattice(string Window, int WindowOffset, IReadOnlyList<LatticeArc> Arcs);

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
      double BudgetCap = 3.0);
  ```
- **Kritik davranış:** boşluk maliyeti `OcrEditCostModel`'den gelir (silme 0.25, ekleme 0.5);
  arc'lar iç boşluk kapsayabilir ve token ortasında bitebilir.
- **Sınır:** `MaxWindowLength`'i aşan region **atlanır** ve `SkippedTooLong` olarak raporlanır —
  false negative ucuz, sonsuz arama pahalı.
- **Kabul:** Fixture'daki 10 hedefin her biri için ilgili arc lattice'te mevcut;
  arc sayısı pencere başına üst sınırın altında (regresyon assert'i).
- **Bağımlılık:** R3.1. **Boyut:** L — bu fazın kalbi.

#### R3.3 — `LatticeDecoder`

- **Amaç:** DAG üzerinde exact shortest path. Beam yok, yaklaşım yok.
- **Sözleşme:**
  ```csharp
  public sealed record DecodedPath(string Text, double Cost, IReadOnlyList<LatticeArc> Arcs);

  public interface ILatticeDecoder
  {
      IReadOnlyList<DecodedPath> Decode(WordLattice lattice, int kBest = 3);
  }
  ```
- **Skor:** `best[j] = min over arcs i→j of best[i] + arc.Cost + lambda * -LM.LogProbability(...)`.
  `lambda` `LatticeOptions`'a taşınır (R5.4'te kalibre edilecek).
- **Kabul:** `odun-kesmek-region-01` fixture'ının **10/10**'u Top-1. k-best çıktısı
  R3.4'ün margin hesabı için farklı path'ler döndürür.
- **Bağımlılık:** R3.2, R2.2. **Boyut:** M.

#### R3.4 — `CorrectionAcceptanceGate`

- **Amaç:** Precision. Yol haritasının en önemli tek kalemi.
- **Sözleşme:**
  ```csharp
  public enum AcceptanceVerdict { Apply, Review, Leave }
  public sealed record AcceptanceResult(AcceptanceVerdict Verdict, string? Replacement, IReadOnlyList<string> Reasons);

  public interface ICorrectionAcceptanceGate
  {
      AcceptanceResult Evaluate(CorruptedTextRegion region, IReadOnlyList<DecodedPath> paths);
  }
  ```
- **Kurallar (hepsi sağlanmalı):**
  1. En iyi path'in maliyeti mutlak eşiğin altında
  2. İkinci en iyi **farklı** path ile margin ≥ δ
  3. Orijinal token zaten geçerli değil — vocabulary'de frekans ≥ 2 **veya** morfolojik olarak
     geçerliyse **asla dokunma**
  4. Path'te confusion set dışı ("ordinary") substitution sayısı ≤ 1
- **Kabul:** "Temiz token'a dokunmaz" testi bu kalemin en kritik testidir — ground truth'un
  `protectedOccurrences` listesi üzerinde `ProtectedViolated == 0`.
- **Bağımlılık:** R3.3. **Boyut:** M.

#### R3.5 — `LatticeRegionReconstructor` + A/B

- **Amaç:** Yeni motoru mevcut port arkasına koymak, eskiyle yan yana ölçmek.
- **Kapsam:** `IOcrRegionReconstructor` implementasyonu + `OcrReconstructionComparison`'a
  üçüncü sütun olarak eklenmesi.
- **Kabul:** Karşılaştırma raporu üç motoru (Current / NoisyChannel / Lattice) aynı tabloda
  Top-1, Top-5, süre ve region başına state sayısıyla gösterir.
- **Bağımlılık:** R3.4. **Boyut:** S.

---

### Faz 4 — Üretim hattına bağlama

> B3'ün çözüldüğü faz. Yol haritasının **en riskli** kısmı — iki hattın veri modelleri uyumsuz.

#### R4.1 — `RegionMutationPlanner`

- **Amaç:** Region tabanlı düzeltmeyi mutation geometrisine çevirmek.
  Üretim hattı kelime-oluşumu tabanlı (`OcrWordCandidate`, tek logical span);
  lattice motoru region tabanlı (çok token, **token sayısını değiştirebilir**).
- **Sözleşme:**
  ```csharp
  public sealed record RegionCorrection(int LogicalStart, int LogicalEndExclusive, string Replacement, AcceptanceResult Acceptance);

  public sealed class RegionMutationPlanner
  {
      OcrCorrectionMutationPlan Create(IReadOnlyList<RegionCorrection> corrections, LogicalTextStream stream);
  }
  ```
- **Risk azaltıcı:** `OcrCorrectionMutationPlanner` zaten `logicalStart..logicalEnd` aralığını
  `stream.GetSourceLocationAt` ile karakter karakter span'lere çeviriyor
  ([OcrCorrectionMutationPlanner.cs:55-64](../src/EpubFixer.Core/Mutation/OcrCorrectionMutationPlanner.cs#L55)).
  Yeni planner o tekniği aynen kullanır — sıfırdan geometri yazılmayacak.
- **Kabul:** Üretilen plan mevcut `OcrCorrectionMutationApplier` ve `EpubOutputValidator` ile
  değişiklik gerektirmeden çalışır; overlap/conflict tespiti korunur.
- **Bağımlılık:** R3.4. **Boyut:** M.

#### R4.2 — `EpubFixService` entegrasyonu

- **Amaç:** `fix --apply-ocr-corrections` yeni motoru kullansın.
- **Kapsam:** `EpubFixService.Fix` içindeki OCR bloğu; motor seçimi CLI bayrağıyla
  (`--ocr-engine lattice|legacy`) geçişli olsun ki A/B üretim üzerinde de yapılabilsin.
- **Kabul:** `QualityBenchmark` full koşusu R0.4 kapısından geçer; çıktı EPUB
  `EpubOutputValidator`'dan geçer; R0.1 metriği baseline'a göre **iyileşir**.
- **Bağımlılık:** R4.1, R0.4. **Boyut:** M.

#### R4.3 — Eski yolların kaldırılması

- **Amaç:** Tek bir düzeltme yolu. İki motor yaşarsa ikisi de çürür.
- **Kapsam:** Lattice motoru kapıdan geçtikten **sonra**: `NoisyChannelRegionReconstructor`,
  `SymSpellRegionReconstructor`, `CurrentRegionReconstructor`, `DeterministicOcrCandidateReranker`
  ve `OcrCorrectionCandidateGenerator` kaldırılır.
- **Not:** Tek token'lı region, 1 arc'lı lattice'tir — `OcrCorrectionCandidateGenerator`'ın
  kapsadığı durumlar yeni motorun alt kümesidir. Kaldırmadan önce bunu ölçüyle doğrulayın:
  generator'ın AutoFix ürettiği her vakada lattice de aynı sonucu vermeli.
- **Kabul:** Kaldırma sonrası kalite kapısı hâlâ yeşil; ölü kod kalmaz.
- **Bağımlılık:** R4.2 + iki sürüm boyunca stabil koşu. **Boyut:** M.

---

### Faz 5 — Kalite turu

#### R5.1 — Confusion maliyet kalibrasyonu

- **Amaç:** `OcrEditCostModel` sabitleri elle ayarlanmış. Ground truth'tan öğrenilebilir.
- **Kapsam:** `benchmarks/` altında offline kalibrasyon aracı: ground-truth
  (bozuk → doğru) hizalamalarından karakter karışım sayımları → maliyet tablosu.
- **Kabul:** Öğrenilmiş tablo elle ayarlanmış tabloya karşı A/B'de Top-1'i düşürmez.
- **Bağımlılık:** R0.2, R3.5. **Boyut:** M.

#### R5.2 — `TrieLexiconMatcher` (weighted)

- **Amaç:** SymSpell'in weighted cost eksiğini kapatmak (R3.1'de bilinçli olarak ertelendi).
- **Kapsam:** Vocabulary bir trie'ye konur; `Match` trie üzerinde yürürken her node'da DP
  satırını taşır, `min(row) > budget` olunca **tüm alt ağacı budar** (Levenshtein-automaton /
  Schulz–Mihov yaklaşımı). Maliyet fonksiyonu `OcrEditCostModel`.
- **Kabul:** Aynı `ILexiconMatcher` sözleşmesi; A/B'de precision artar veya süre düşer,
  ikisi birden kötüleşmez. Üstteki hiçbir katman değişmez.
- **Bağımlılık:** R3.1, R5.1. **Boyut:** L.

#### R5.3 — Reader preview / diff raporu

- **Amaç:** İnsan doğrulama döngüsü. `FullBookReaderPreview.cs` (şu an commit edilmemiş)
  bu işin başlangıcı.
- **Kapsam:** Uygulanan her düzeltmeyi bağlamıyla yan yana gösteren HTML/Markdown diff;
  `Review` kararları ayrı bölümde.
- **Kabul:** 459 region'lık koşunun çıktısı tek dosyada gözden geçirilebilir.
- **Bağımlılık:** R4.2. **Boyut:** M.

#### R5.4 — Eşik ayarı

- **Amaç:** `lambda`, maliyet eşiği ve `δ` margin'ini precision/recall eğrisi üzerinde seçmek.
- **Kapsam:** Parametre süpürmesi + `docs/baselines/` altına eğri raporu.
- **Kabul:** Seçilen nokta R0.4 kapısını sağlar ve gerekçesi belgelenir.
- **Bağımlılık:** R5.1, R5.3. **Boyut:** S.

---

### Faz 6 — Tavan yükseltme (opsiyonel)

#### R6.1 — Second-pass OCR

- **Amaç:** En yüksek kalite tavanı. **Yalnızca orijinal PDF/görüntü elde varsa.**
- **Yaklaşım:** Tesseract 5 + `tur.traineddata` ile yeniden OCR; mevcut pipeline sadece
  artık hatalar için çalışır. Post-correction'ın hiçbir zaman ulaşamayacağı sınıfları
  (tamamen okunamamış kelimeler) çözer.
- **Not:** Farklı bir proje kapsamı — çevrimdışı kalır ama yeni bir native bağımlılık getirir.
  Faz 0–5 hedefe ulaşmak için yeterlidir; bu madde tavanı yükseltmek içindir.
- **Bağımlılık:** Faz 4 tamam. **Boyut:** XL.

#### R6.2 — Kalan hata raporu

- **Amaç:** Motorun çözemediği region'ları sınıflandırıp raporlamak — bir sonraki turun girdisi.
- **Bağımlılık:** R4.2. **Boyut:** S.

---

## 5. Acil ara çözüm (yol haritasından bağımsız)

Yeni motor yazılırken elde çalışan bir baseline kalması için:
[NoisyChannelRegionReconstructor.cs:66](../src/EpubFixer.Cli/OcrReconstruction/NoisyChannelRegionReconstructor.cs#L66)
içindeki `RetentionKey`'in gövdesindeki iç `Expand(text, state)` çağrısını kaldırın.
Tek başına ~500× hızlanma verir. **Kalite sorununu (B2) çözmez** — yalnızca koşunun bitmesini sağlar.

---

## 6. Risk kaydı

| # | Risk | Etki | Azaltma |
|---|---|---|---|
| 1 | Vocabulary kirli — bozuk token'lar vocabulary'ye sızarsa motor hatayı "doğru" sayar | Yüksek | R2.1'de region'lar hariç tutulur, frekans ≥ 2 eşiği, morfoloji doğrulaması |
| 2 | İki hattın veri modeli uyumsuzluğu (R4.1) | Yüksek | Mevcut planner'ın span üretme tekniği yeniden kullanılır; bu faz tek başına ele alınır |
| 3 | Ground truth'un tireleme ağırlıklı olması yanlış güven verir | Orta | R0.2 sınıf bazlı raporlama |
| 4 | Özel isimler (*Auersberger*, *Rennweg*) "düzeltilir" | Orta | R3.4 kural 3 + `ProperNameRisk` mantığı korunur |
| 5 | Kalite kapısının gevşetilmesi (R0.4) regresyonu gizler | Orta | `ProtectedViolated` ve `UnexpectedTextChanges` sıfır kalmaya devam eder |
| 6 | R5.2 (trie) hiç gerekmeyebilir | Düşük | Sözleşme aynı; SymSpell yeterliyse madde düşürülür |

---

## 7. Bağımlılık grafiği

```
R0.1 ─┐
R0.3 ─┤ (bağımsız, önce)
R0.2 ─┴─► R0.4 ─────────────────────────────┐
                                             │
R1.1 ─► R1.2                                 │
  └───► R1.3                                 │
  └───► R2.1 ─► R2.2 ──┐                     │
                        │                     │
R2.3 ───────────────────┤                     │
                        ▼                     │
              R3.1 ─► R3.2 ─► R3.3 ─► R3.4 ─► R3.5
                        │                     │
                        │              R4.1 ◄─┘
                        │                │
                        │              R4.2 ◄────────────────┘
                        │                │
                        │              R4.3
                        │                │
              R5.1 ◄────┘         R5.3 ◄─┘
                │                   │
              R5.2                R5.4
```

**Kritik yol:** R1.1 → R2.1 → R2.3 → R3.1 → R3.2 → R3.3 → R3.4 → R4.1 → R4.2

---

## Devir şablonu

Her iş kalemini ayrı bir oturuma devrederken:

```
EpubFixer projesinde docs/ocr-correction-roadmap.md yol haritasındaki <ID> kalemini
uygulayacaksın.

Önce şunları oku:
- docs/ocr-correction-roadmap.md — özellikle <ID> maddesi, "Hedef mimari" ve "Risk kaydı"
- <ilgili kaynak dosyalar>

Kurallar:
- Test-first: kırmızı test → minimum kod → refactor. Testi olmayan üretim kodu yazma.
- Bağımlılıklar içeri doğru: Core'a framework/IO importu girmez.
- Yol haritasındaki sözleşmeyi (interface imzaları) aynen kullan; değiştirmen gerekirse
  önce gerekçesini söyle.
- Kapsam <ID> ile sınırlı. Başka bir kalemin işini yapma; fark ettiğin sorunları raporla.

Bitirdiğinde: hangi testlerin eklendiği, kabul kriterinin karşılanıp karşılanmadığı ve
yol haritasında güncellenmesi gereken bir şey olup olmadığı.
```
