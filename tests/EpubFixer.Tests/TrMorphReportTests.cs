using EpubFixer.Core.Morphology;

namespace EpubFixer.Tests;

public sealed class TrMorphReportTests
{
    [Fact]
    public void ProtectedLoader_PreservesDuplicateOccurrencesAndJoinedForms()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ground-truth-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """
            {"protectedOccurrences":[
              {"id":"one","original":"e-posta"},
              {"id":"two","original":"e-posta"},
              {"id":"three","original":"Mayıs-Haziran"}]}
            """);
        try
        {
            var result = GroundTruthProtectedOccurrenceLoader.Load(path);
            Assert.Equal(3, result.Count);
            Assert.Equal(["one", "two", "three"], result.Select(item => item.Id));
            Assert.Equal(["eposta", "eposta", "MayısHaziran"], result.Select(item => item.JoinedForm));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
