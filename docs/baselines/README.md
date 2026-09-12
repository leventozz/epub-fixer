# Baselines

Bu klasordeki dosyalar Faz 0 olcumlerinin anlik kayitlaridir. Metrik tanimi degisirse baseline
gecersizdir ve yeni tanimla yeniden uretilmelidir.

## Book health definition v2

- `totalTokens`: `WordTokenizer().Tokenize(stream).Count`.
- Normalizasyon: NFC + `ToLower(tr-TR)`.
- `unresolvableTokens`: normalize token ve kesme isareti varsa kok parcasi `tr_50k` veya TRmorph
  tarafindan taninmiyorsa sayilir.
- `suspiciousTokens`: bosluksuz ham kelimelerde gomulu rakam, garbage glyph, ic/onde cift
  noktalama ve komsu tek karakterli `i/ı/l/1/I/İ` kurallariyla sayilir.
  Ham kelimeler TextNode sinirinda devam eder; Paragraph ve Document sinirinda kesilir.
- `unresolvableRate`: 1000 token basina cozumlenemeyen token.
- `worstExamples`: cozumlenemeyen yuzey bicimleri; frekans azalan, esitlikte ordinal artan.

## Commands

- `dotnet run --project src/EpubFixer.Cli -- measure test-data/odun-kesmek/input.epub`
- `dotnet run --project benchmarks/EpubFixer.QualityBenchmarks -- test-data/odun-kesmek`
