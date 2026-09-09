using EpubFixer.Core.Decision.Models;
using EpubFixer.Core.Epub.Models;

namespace EpubFixer.Core.Correction.Models;

public sealed record HyphenationCorrectionPlan(
    HyphenationDecision Decision,
    HyphenationCorrectionKind CorrectionKind,
    string UnhyphenatedText,
    TextSourceLocation LeftSource,
    TextSourceLocation HyphenSource,
    TextSourceLocation RightSource);
