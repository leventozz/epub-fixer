using EpubFixer.QualityBenchmarks.Models;

namespace EpubFixer.QualityBenchmarks;

public sealed record QualityBenchmarkDataset(
    string Name,
    string DirectoryPath,
    string InputEpubPath,
    string GroundTruthPath,
    GroundTruthDocument GroundTruth);
