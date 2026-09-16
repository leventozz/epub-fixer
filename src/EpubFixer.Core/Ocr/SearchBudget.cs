namespace EpubFixer.Core.Ocr;

public sealed class SearchBudget(long maxStates)
{
    public long Visited { get; private set; }

    public long MaxStates { get; } = maxStates > 0
        ? maxStates
        : throw new ArgumentOutOfRangeException(nameof(maxStates));

    public bool Exceeded { get; private set; }

    public void Visit()
    {
        Visited++;
        if (Visited > MaxStates)
        {
            Exceeded = true;
        }
    }
}
