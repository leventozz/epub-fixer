using EpubFixer.Core.Epub.Models;

namespace EpubFixer.Core.Detection.Models;

public sealed record HyphenationCandidate(
    string LeftPart,
    string RightPart,
    string UnhyphenatedText,
    HyphenationDetectionKind DetectionKind,
    TextSourceLocation LeftSource,
    TextSourceLocation HyphenSource,
    TextSourceLocation RightSource);
