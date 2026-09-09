namespace EpubFixer.TrMorph;

public sealed class TurkishMorphologyException : Exception
{
    public TurkishMorphologyException(string message) : base(message) { }
    public TurkishMorphologyException(string message, Exception innerException) : base(message, innerException) { }
}
