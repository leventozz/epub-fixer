using EpubFixer.Core.Correction.Models;
using EpubFixer.Core.Epub.Models;
using EpubFixer.Core.Detection.Models;

namespace EpubFixer.Core.Fix.Models;

public sealed record EpubIntegrityResult(
    int EntryCount,
    int UntouchedEntryCount,
    IReadOnlyList<string> ModifiedDocumentPaths,
    bool ResourceInventoryMatches,
    bool UntouchedResourcesMatch,
    bool MimetypePackagingValid,
    bool ReadBackValidated);

public sealed record EpubFixResult(
    string InputPath,
    string OutputPath,
    int OriginalCandidateCount,
    int OriginalAutoFixCandidateCount,
    HyphenationCorrectionApplyResult InlineApplyResult,
    HyphenationCorrectionApplyResult CrossParagraphApplyResult,
    int RemainingCandidateCount,
    int RemainingAutoFixCandidateCount,
    string InputSha256,
    string OutputSha256,
    EpubWriteResult WriteResult,
    EpubIntegrityResult Integrity,
    int V2AutoFixCandidateCount,
    V2DetectionKindCounts V2DetectionKinds,
    int V2PlannedCount,
    HyphenationCorrectionApplyResult V2InlineApplyResult,
    HyphenationCorrectionApplyResult V2CrossParagraphApplyResult,
    int V2UnsupportedDocumentBoundaryCount);

public sealed record V2DetectionKindCounts(
    int Inline,
    int CrossParagraph,
    int DocumentBoundary,
    int Other);
