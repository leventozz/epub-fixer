namespace EpubFixer.Core.Quality;

public sealed record BookHealth(
    int TotalTokens,
    int UnresolvableTokens,
    int SuspiciousTokens,
    double UnresolvableRate,
    IReadOnlyList<string> WorstExamples)
{
    public bool Equals(BookHealth? other) =>
        other is not null
        && TotalTokens == other.TotalTokens
        && UnresolvableTokens == other.UnresolvableTokens
        && SuspiciousTokens == other.SuspiciousTokens
        && UnresolvableRate.Equals(other.UnresolvableRate)
        && WorstExamples.SequenceEqual(other.WorstExamples, StringComparer.Ordinal);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(TotalTokens);
        hash.Add(UnresolvableTokens);
        hash.Add(SuspiciousTokens);
        hash.Add(UnresolvableRate);
        foreach (var example in WorstExamples)
        {
            hash.Add(example, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }
}
