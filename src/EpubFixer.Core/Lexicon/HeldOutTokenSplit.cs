namespace EpubFixer.Core.Lexicon;

internal static class HeldOutTokenSplit
{
    public static IReadOnlySet<int> SelectHeldOutLogicalStarts(
        IReadOnlyList<CleanToken> tokens,
        double heldOutFraction = 0.20)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        if (tokens.Count == 0)
        {
            return new HashSet<int>();
        }

        var heldOutCount = Math.Max(1, (int)Math.Ceiling(tokens.Count * heldOutFraction));
        var heldOutStart = Math.Max(0, tokens.Count - heldOutCount);
        return tokens
            .Skip(heldOutStart)
            .Select(token => token.LogicalStart)
            .ToHashSet();
    }
}
