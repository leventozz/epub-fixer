using EpubFixer.Core.Correction.Models;
using EpubFixer.Core.Epub.Models;

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
    EpubIntegrityResult Integrity);
