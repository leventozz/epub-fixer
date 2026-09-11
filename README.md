<div align="center">

**English** · [Türkçe](README.tr.md)

# EpubFixer

**A privacy-first CLI that analyzes OCR artifacts in Turkish EPUB files, learns from each book's own vocabulary, and applies high-confidence corrections entirely offline.**

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
![Platform](https://img.shields.io/badge/platform-Windows%20x64-0078D4?logo=windows)
![Language](https://img.shields.io/badge/language-Turkish-E30A17)
![Offline](https://img.shields.io/badge/processing-100%25%20offline-2E7D32)

[Quick start](#quick-start) · [How it works](#how-it-works) · [Quality benchmark](#quality-benchmark) · [Roadmap](#roadmap)

</div>

---

E-readers offer an excellent experience with EPUB files. EPUBs converted from PDF, however, often contain leftover line-break hyphens, fragmented words, and OCR character confusions such as `1 / l / ı`. These artifacts quickly get in the way of reading.

EpubFixer started with a question:

> How many OCR errors in an EPUB can be corrected using only algorithms and dictionaries, while keeping the risk of changing the text's meaning low?

The project explores that question **without an LLM, external API, or internet connection**. It combines the book's own vocabulary with Turkish morphological analysis and OCR-specific error patterns. The guiding principle is caution, not aggression: when the evidence is not strong enough, EpubFixer leaves the text unchanged.

## Highlights

- Processes XHTML documents in the EPUB spine's reading order.
- Builds a case-sensitive, book-specific lexicon with occurrence frequencies.
- Evaluates words using [TRmorph](https://github.com/coltekin/TRmorph)-based Turkish morphological analysis.
- Tracks word fragments across line, paragraph, and text-node boundaries while preserving source locations.
- Detects suspicious characters, embedded digits, fragmented words, and structural OCR artifacts.
- Ranks correction candidates using in-book frequency, morphological validity, edit distance, and error structure.
- Classifies decisions as `AutoFix`, `Review`, or `Defer`, keeping uncertain cases away from automatic mutation.
- Never overwrites the source EPUB; it creates a new file and validates it by reading it back.
- Verifies that untouched archive entries remain byte-identical.

## Current status

EpubFixer is an experimental project under active development. Its current capabilities fall into two levels:

| Capability | Detection | Candidate/decision report | Automatic EPUB mutation |
| --- | :---: | :---: | :---: |
| Incorrect hyphenation and split words | ✅ | ✅ | ✅ |
| Suspicious characters and general OCR artifacts | ✅ | ✅ | 🚧 In development |

The `fix` command currently writes only **hyphenation and split-word** corrections that pass the confidence threshold. The general OCR pipeline can generate candidate and decision reports; safely applying those decisions to the EPUB is on the roadmap.

## Quick start

### Requirements

- Windows x64
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Git

The TRmorph and `flookup` runtime files are bundled with the project, so no separate dictionary, model, or service setup is required.

### Installation

```powershell
git clone https://github.com/leventozz/epub-fixer.git
cd epub-fixer
dotnet restore
dotnet build
```

### Fix an EPUB

```powershell
dotnet run --project src/EpubFixer.Cli -- fix "book.epub" -o "book.fixed.epub"
```

The output file must not already exist. EpubFixer leaves the source untouched and reports:

- detected and applied correction counts,
- deferred candidates,
- input and output SHA-256 hashes,
- modified XHTML documents,
- archive integrity and read-back validation results.

> [!IMPORTANT]
> Automatic text correction cannot be guaranteed to be error-free. Keep the output separate from the source and review the result for important books.

## Analysis and reports

You can generate OCR analysis, correction candidate, and decision reports without modifying the EPUB:

```powershell
dotnet run --project src/EpubFixer.Cli -- analyze "book.epub" `
  --ocr-report "ocr-analysis.md" `
  --ocr-correction-report "ocr-candidates.md" `
  --ocr-decision-report "ocr-decisions.md"
```

To inspect hyphenation decisions and the book's logical text stream:

```powershell
dotnet run --project src/EpubFixer.Cli -- analyze "book.epub" `
  --dump-text "logical-text.txt" `
  --hyphen-report "hyphen-decisions.json" `
  --hyphen-analysis-report "hyphen-analysis.md"
```

Available `analyze` options:

| Option | Description |
| --- | --- |
| `--dump-text <file>` | Writes the logical text stream constructed from the EPUB. |
| `--hyphen-report <file>` | Writes hyphenation evidence and decisions as JSON. |
| `--hyphen-analysis-report <file>` | Produces a detailed Markdown analysis of hyphenation candidates. |
| `--ocr-report <file>` | Reports suspicious OCR occurrences. |
| `--ocr-correction-report <file>` | Reports correction candidates for each occurrence. |
| `--ocr-decision-report <file>` | Classifies candidates for automatic correction, review, or deferral. |
| `--post-fix-lexicon-report <file>` | Shows lexicon changes after in-memory hyphenation corrections. |
| `--apply-inline` | Applies inline plans within the analysis session and prints metrics; does not write an EPUB. |
| `--apply-paragraph` | Applies inline and cross-paragraph plans within the analysis session; does not write an EPUB. |

## How it works

```text
EPUB
  │
  ├─► Package and spine reader
  │       │
  │       └─► Source-aware logical stream built from XHTML text
  │
  ├─► Book-specific lexicon and occurrence frequencies
  │
  ├─► OCR and hyphenation anomaly detection
  │
  ├─► Candidate generation
  │       ├─ in-book lexical neighbors
  │       ├─ OCR character transformations
  │       ├─ fragment composition
  │       └─ Turkish morphology checks
  │
  ├─► Confidence-first decision: AutoFix / Review / Defer
  │
  └─► Apply safe plans → write a new EPUB → validate integrity
```

The system does not simply choose the “nearest” word. It considers how often a proposal appears in the book, whether it is morphologically valid Turkish, its character-level distance from the source, its letter-case pattern, and whether the transformation resembles a known OCR artifact.

This design is intended to protect proper names, genuinely hyphenated expressions such as `e-posta` and `Mayıs-Haziran`, and guesses supported by weak evidence.

## Safety and integrity

- Input and output paths cannot be the same.
- Existing output files are never overwritten.
- Changes target recorded source nodes and offsets; no global search and replace is performed.
- Stale plans and plans that fail structural safety checks are skipped.
- A new EPUB is written to a temporary file, validated, and moved to the destination only after validation succeeds.
- SHA-256 verification ensures that the input does not change during processing.
- Non-text resources and untouched archive entries are checked for integrity.

## Quality benchmark

The repository includes a location-aware ground-truth dataset built from a real EPUB, together with a regression quality gate. The current result for the included `odun-kesmek` dataset is:

| Metric | Result |
| --- | ---: |
| Labeled errors | 148 |
| Detected | 148 |
| Correctly fixed | 148 |
| Wrongly fixed | 0 |
| Protected occurrences left unchanged | 9 / 9 |
| Unexpected text or non-text changes | 0 |
| Quality gate | **PASS** |

These figures describe **one labeled dataset**; they are not a claim of general accuracy across different books. Reproduce the benchmark locally with:

```powershell
dotnet run --project benchmarks/EpubFixer.QualityBenchmarks -- test-data/odun-kesmek
```

Run the test suite with:

```powershell
dotnet test
```

## Project structure

```text
src/
├── EpubFixer.Cli       Command-line interface and reporting
├── EpubFixer.Core      EPUB, detection, evidence, decision, and correction layers
└── EpubFixer.TrMorph   Turkish morphology adapter and local runtime

tests/
└── EpubFixer.Tests     Unit, integration, and real-EPUB regression tests

benchmarks/
└── EpubFixer.QualityBenchmarks   Ground-truth quality gate
```

## Limitations

- Only Turkish text is currently targeted.
- The bundled morphology runtime makes the current distribution Windows x64-specific.
- General OCR decisions are not yet applied automatically to the output EPUB.
- Encrypted or DRM-protected EPUB files are not supported.
- Results may vary for complex or non-standard EPUB structures.

## Roadmap

- [ ] Apply general OCR decisions while preserving exact EPUB source locations
- [ ] Add an interactive review mode for proposed corrections
- [ ] Expand the benchmark with more books and error categories
- [ ] Add Linux and macOS runtime support
- [ ] Publish single-file CLI distributions
- [ ] Introduce an extensible morphology layer for additional languages

## Contributing

Error samples, new OCR patterns, conservative decision rules, and tests for different EPUB structures are particularly valuable.

1. Fork the repository and create a feature branch.
2. Add tests for behavioral changes.
3. Run `dotnet test` and the relevant quality benchmark.
4. Open a pull request describing the scope and safety implications of the change.

When reporting a bug, include a small, copyright-safe EPUB sample when possible, together with the expected and actual results. Do not upload complete copyrighted books to issues or pull requests.

## Third-party components

Turkish morphological analysis uses the TRmorph finite-state transducer and the foma `flookup` runtime. Provenance, pinned revisions, hashes, and license details are documented in [THIRD-PARTY-NOTICES.md](src/EpubFixer.TrMorph/Resources/win-x64/THIRD-PARTY-NOTICES.md).

---

<div align="center">

**Fully offline. No LLM. No external API. Just algorithms, dictionaries, and curiosity.**

</div>
