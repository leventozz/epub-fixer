using EpubFixer.Core.Detection;
using EpubFixer.Core.Epub;
using EpubFixer.QualityBenchmarks.Models;

namespace EpubFixer.QualityBenchmarks;

public sealed class QualityBenchmarkRunner
{
    public QualityBenchmarkResult Run(QualityBenchmarkDataset dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        var package = new EpubPackageReader().Read(dataset.InputEpubPath);
        new QualityBenchmarkGroundTruthValidator().Validate(
            dataset.GroundTruth,
            package.LogicalText);
        var candidates = new HyphenationDetector().Detect(package.LogicalText);

        return new QualityBenchmarkMatcher().Match(
            dataset.GroundTruth.KnownErrors,
            candidates);
    }
}
