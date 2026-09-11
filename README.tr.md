<div align="center">

[English](README.md) · **Türkçe**

# EpubFixer

**Türkçe EPUB dosyalarındaki OCR kaynaklı metin bozulmalarını, kitabın kendi dilinden öğrenerek çevrimdışı analiz eden ve güvenli düzeltmeleri uygulayan konsol aracı.**

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
![Platform](https://img.shields.io/badge/platform-Windows%20x64-0078D4?logo=windows)
![Language](https://img.shields.io/badge/language-T%C3%BCrk%C3%A7e-E30A17)
![Offline](https://img.shields.io/badge/processing-100%25%20offline-2E7D32)

[Hızlı başlangıç](#hızlı-başlangıç) · [Nasıl çalışır?](#nasıl-çalışır) · [Kalite ölçümü](#kalite-ölçümü) · [Yol haritası](#yol-haritası)

</div>

---

E-kitap okuyucuları EPUB formatında oldukça iyi bir deneyim sunuyor. Ancak PDF'den dönüştürülen EPUB'larda satır sonlarından kalan tireler, parçalanmış kelimeler ve `1 / l / ı` gibi OCR karakter karışıklıkları okuma akışını kolayca bozabiliyor.

EpubFixer şu sorudan doğdu:

> Bir EPUB dosyasındaki OCR hatalarının ne kadarı yalnızca algoritmalar ve sözlükler kullanılarak, metnin anlamını değiştirme riski düşük tutularak düzeltilebilir?

Proje bu soruyu **LLM, harici API ve internet bağlantısı kullanmadan** araştırıyor. Kitabın kendi kelime haznesini, Türkçe morfoloji analizini ve OCR'a özgü hata desenlerini birlikte değerlendiriyor. Temel yaklaşım agresif olmak değil, temkinli olmak: sistem yeterince emin değilse metni değiştirmiyor.

## Öne çıkanlar

- EPUB içindeki XHTML belgelerini okuma sırasına göre işler.
- Kitaba özel, büyük/küçük harf duyarlı bir kelime haznesi ve kullanım sıklıkları oluşturur.
- Sözcükleri [TRmorph](https://github.com/coltekin/TRmorph) tabanlı Türkçe morfoloji analiziyle değerlendirir.
- Satır sonu, paragraf sınırı ve metin düğümü sınırındaki parçalanmaları kaynak konumlarıyla birlikte takip eder.
- Şüpheli karakterleri, gömülü rakamları, parçalanmış sözcükleri ve OCR'a özgü yapısal bozulmaları tespit eder.
- Düzeltme adaylarını kitap içi sıklık, morfolojik geçerlilik, düzenleme mesafesi ve hata yapısıyla puanlar.
- Kararları `AutoFix`, `Review` veya `Defer` olarak ayırarak belirsiz durumları otomatik değişiklikten korur.
- Kaynak EPUB'ın üzerine yazmaz; yeni bir dosya üretir ve çıktıyı yeniden okuyarak doğrular.
- Değişmeyen arşiv girdilerinin byte düzeyinde aynı kaldığını denetler.

## Mevcut durum

EpubFixer aktif geliştirme aşamasında olan deneysel bir projedir. Bugünkü yetenekler iki ayrı seviyededir:

| Yetenek | Tespit | Aday/karar raporu | EPUB'a otomatik uygulama |
| --- | :---: | :---: | :---: |
| Hatalı tireleme ve bölünmüş kelimeler | ✅ | ✅ | ✅ |
| Şüpheli karakter ve genel OCR bozulmaları | ✅ | ✅ | 🚧 Geliştiriliyor |

`fix` komutu şu anda yalnızca güven eşiğini geçen **tireleme ve bölünmüş kelime** düzeltmelerini dosyaya uygular. Genel OCR düzeltme motoru aday ve karar raporları üretir; bu kararların EPUB'a güvenli biçimde uygulanması yol haritasındadır.

## Hızlı başlangıç

### Gereksinimler

- Windows x64
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Git

TRmorph ve `flookup` çalışma zamanı projeye dahil olduğundan ayrıca sözlük, model veya servis kurmanız gerekmez.

### Kurulum

```powershell
git clone https://github.com/leventozz/epub-fixer.git
cd epub-fixer
dotnet restore
dotnet build
```

### Bir EPUB'ı düzeltme

```powershell
dotnet run --project src/EpubFixer.Cli -- fix "kitap.epub" -o "kitap.fixed.epub"
```

Çıktı dosyası önceden var olmamalıdır. EpubFixer kaynak dosyayı değiştirmez ve işlem sonunda şunları raporlar:

- bulunan ve uygulanan düzeltme sayıları,
- ertelenen adaylar,
- giriş/çıkış SHA-256 değerleri,
- değiştirilen XHTML belgeleri,
- arşiv bütünlüğü ve yeniden okuma sonucu.

> [!IMPORTANT]
> Otomatik metin düzeltme doğası gereği hatasız garanti edilemez. Çıktıyı kaynak dosyadan ayrı tutun ve önemli kitaplarda sonucu gözden geçirin.

## Analiz ve raporlar

EPUB'ı değiştirmeden OCR analizi, düzeltme adayları ve karar raporu oluşturabilirsiniz:

```powershell
dotnet run --project src/EpubFixer.Cli -- analyze "kitap.epub" `
  --ocr-report "ocr-analysis.md" `
  --ocr-correction-report "ocr-candidates.md" `
  --ocr-decision-report "ocr-decisions.md"
```

Tireleme kararlarını ve kitabın mantıksal metin akışını incelemek için:

```powershell
dotnet run --project src/EpubFixer.Cli -- analyze "kitap.epub" `
  --dump-text "logical-text.txt" `
  --hyphen-report "hyphen-decisions.json" `
  --hyphen-analysis-report "hyphen-analysis.md"
```

Tüm `analyze` seçenekleri:

| Seçenek | Açıklama |
| --- | --- |
| `--dump-text <dosya>` | EPUB'dan oluşturulan mantıksal metin akışını yazar. |
| `--hyphen-report <dosya>` | Tireleme kanıtlarını ve kararlarını JSON olarak yazar. |
| `--hyphen-analysis-report <dosya>` | Tireleme adaylarının ayrıntılı Markdown analizini üretir. |
| `--ocr-report <dosya>` | Şüpheli OCR oluşumlarını raporlar. |
| `--ocr-correction-report <dosya>` | Her oluşum için düzeltme adaylarını raporlar. |
| `--ocr-decision-report <dosya>` | Adayları otomatik düzeltme, inceleme ve erteleme olarak sınıflandırır. |
| `--post-fix-lexicon-report <dosya>` | Tireleme düzeltmeleri sonrasındaki kelime haznesi değişimini gösterir. |
| `--apply-inline` | Satır içi planları analiz oturumu içinde uygular ve sonucu konsolda ölçer; EPUB yazmaz. |
| `--apply-paragraph` | Satır içi ve paragraflar arası planları analiz oturumu içinde uygular; EPUB yazmaz. |

## Nasıl çalışır?

```text
EPUB
  │
  ├─► Paket ve spine okuma
  │       │
  │       └─► XHTML metinlerinden kaynak konumlarını koruyan mantıksal akış
  │
  ├─► Kitaba özel kelime haznesi + kullanım sıklıkları
  │
  ├─► OCR / tireleme anomalisi tespiti
  │
  ├─► Aday üretimi
  │       ├─ kitap içi komşu kelimeler
  │       ├─ OCR karakter dönüşümleri
  │       ├─ parçaları birleştirme
  │       └─ Türkçe morfoloji kontrolü
  │
  ├─► Güven odaklı karar: AutoFix / Review / Defer
  │
  └─► Güvenli planları uygula → yeni EPUB yaz → bütünlüğü doğrula
```

Sistem yalnızca “en yakın” sözcüğü seçmez. Bir önerinin kitapta kaç kez geçtiğini, Türkçe morfoloji açısından geçerli olup olmadığını, kaynakla arasındaki karakter farkını, büyük/küçük harf yapısını ve dönüşümün tipik bir OCR hatasına benzeyip benzemediğini birlikte değerlendirir.

Bu tasarım özellikle özel adları, gerçek tireli ifadeleri (`e-posta`, `Mayıs-Haziran`) ve zayıf kanıtlı tahminleri korumayı hedefler.

## Güvenlik ve bütünlük yaklaşımı

- Girdi ve çıktı yolu aynı olamaz.
- Var olan çıktı dosyasının üzerine yazılmaz.
- Değişiklikler doğrudan kaynak metin düğümlerine ve kayıtlı konumlara uygulanır; global arama/değiştirme yapılmaz.
- Güncelliğini yitirmiş veya yapısal güvenlik koşullarını karşılamayan planlar atlanır.
- Yeni EPUB önce geçici dosyaya yazılır, doğrulanır ve ancak başarılıysa hedef adına taşınır.
- Girdi dosyasının işlem sırasında değişmediği SHA-256 ile kontrol edilir.
- Metin dışı kaynakların ve dokunulmayan arşiv girdilerinin bütünlüğü doğrulanır.

## Kalite ölçümü

Projede gerçek bir EPUB üzerinden konum bazlı ground-truth veri seti ve regresyon kalite kapısı bulunur. Dahil edilen `odun-kesmek` veri setinin güncel sonucu:

| Metrik | Sonuç |
| --- | ---: |
| Etiketlenmiş hata | 148 |
| Bulunan | 148 |
| Doğru düzeltilen | 148 |
| Yanlış düzeltilen | 0 |
| Korunması gereken oluşum | 9 / 9 |
| Beklenmeyen metin veya metin dışı değişiklik | 0 |
| Kalite kapısı | **PASS** |

Bu değerler **tek bir etiketlenmiş veri setine** aittir; farklı kitaplar için genel başarı oranı iddiası değildir. Sonucu yerel olarak yeniden üretmek için:

```powershell
dotnet run --project benchmarks/EpubFixer.QualityBenchmarks -- test-data/odun-kesmek
```

Test paketini çalıştırmak için:

```powershell
dotnet test
```

## Proje yapısı

```text
src/
├── EpubFixer.Cli       Komut satırı arayüzü ve raporlama
├── EpubFixer.Core      EPUB, tespit, kanıt, karar ve düzeltme katmanları
└── EpubFixer.TrMorph   Türkçe morfoloji adaptörü ve yerel çalışma zamanı

tests/
└── EpubFixer.Tests     Birim, entegrasyon ve gerçek EPUB regresyon testleri

benchmarks/
└── EpubFixer.QualityBenchmarks   Ground-truth kalite kapısı
```

## Sınırlamalar

- Şimdilik yalnızca Türkçe metinler hedeflenmektedir.
- Paketlenmiş morfoloji çalışma zamanı nedeniyle mevcut dağıtım Windows x64 odaklıdır.
- Genel OCR kararları henüz çıktı EPUB'ına otomatik uygulanmaz.
- Şifreli/DRM korumalı EPUB dosyaları desteklenmez.
- Karmaşık veya standart dışı EPUB yapılarında sonuçlar değişebilir.

## Yol haritası

- [ ] Genel OCR kararlarını kaynak konumlarını koruyarak EPUB'a uygulama
- [ ] Düzeltmeleri kullanıcıya sunan etkileşimli inceleme modu
- [ ] Daha fazla kitap ve hata türüyle benchmark veri setini genişletme
- [ ] Linux ve macOS çalışma zamanı desteği
- [ ] Yayınlanabilir tek dosyalı CLI paketleri
- [ ] Ek diller için genişletilebilir morfoloji katmanı

## Katkıda bulunma

Hata örnekleri, yeni OCR desenleri, güvenli karar kuralları ve farklı EPUB yapıları için testler özellikle değerlidir.

1. Depoyu fork'layın ve bir özellik dalı oluşturun.
2. Davranış değişikliği için test ekleyin.
3. `dotnet test` ve ilgili kalite benchmark'ını çalıştırın.
4. Değişikliğin kapsamını ve güvenlik etkisini açıklayan bir pull request açın.

Hata bildirirken mümkünse küçük ve telif açısından paylaşılabilir bir EPUB örneği, beklenen sonuç ve gerçek sonucu ekleyin. Telifli kitapların tamamını issue veya pull request'e yüklemeyin.

## Üçüncü taraf bileşenler

Türkçe morfoloji analizi için TRmorph sonlu durum dönüştürücüsü ve foma `flookup` çalışma zamanı kullanılır. Kaynak, sürüm ve lisans bilgileri [THIRD-PARTY-NOTICES.md](src/EpubFixer.TrMorph/Resources/win-x64/THIRD-PARTY-NOTICES.md) dosyasındadır.

---

<div align="center">

**Tamamen çevrimdışı. LLM yok. Harici API yok. Biraz algoritma, biraz sözlük, bolca merak.**

</div>
