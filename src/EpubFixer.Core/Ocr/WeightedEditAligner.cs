using EpubFixer.Core.Lexicon;
using EpubFixer.Core.Ocr.Models;

namespace EpubFixer.Core.Ocr;

public interface IWeightedEditAligner
{
    bool TryAlign(ReadOnlySpan<char> source, ReadOnlySpan<char> target, double budget, out EditAlignment alignment);
}

public sealed class WeightedEditAligner(
    OcrEditCostModel? costs = null,
    OcrConfusionSet? confusionSet = null) : IWeightedEditAligner
{
    private const double Epsilon = 1e-9;
    private readonly OcrEditCostModel costs = costs ?? new();
    private readonly OcrConfusionSet confusionSet = confusionSet ?? OcrConfusionSet.Default;

    public bool TryAlign(ReadOnlySpan<char> source, ReadOnlySpan<char> target, double budget, out EditAlignment alignment)
    {
        if (budget < 0) throw new ArgumentOutOfRangeException(nameof(budget));

        var sourceText = TurkishWordNormalizer.Normalize(source.ToString());
        var targetText = TurkishWordNormalizer.Normalize(target.ToString());
        var sourceLength = sourceText.Length;
        var targetLength = targetText.Length;
        var scores = new double[sourceLength + 1, targetLength + 1];
        var parents = new Parent[sourceLength + 1, targetLength + 1];

        for (var i = 1; i <= sourceLength; i++)
        {
            var operation = DeleteOperation(sourceText[i - 1], i - 1);
            scores[i, 0] = scores[i - 1, 0] + OperationCost(operation.Kind);
            parents[i, 0] = new Parent(i - 1, 0, operation, 2);
        }

        for (var j = 1; j <= targetLength; j++)
        {
            var operation = InsertOperation(targetText[j - 1], 0);
            scores[0, j] = scores[0, j - 1] + OperationCost(operation.Kind);
            parents[0, j] = new Parent(0, j - 1, operation, 3);
        }

        if (targetLength > 0 && RowMinimum(scores, 0, targetLength) > budget)
        {
            alignment = Empty();
            return false;
        }

        for (var i = 1; i <= sourceLength; i++)
        {
            for (var j = 1; j <= targetLength; j++)
            {
                var substitution = SubstituteOperation(sourceText[i - 1], targetText[j - 1], i - 1);
                var bestCost = scores[i - 1, j - 1] + OperationCost(substitution.Kind);
                var best = new Parent(i - 1, j - 1, substitution, substitution.Kind == EditOperationKind.Keep ? 0 : 1);

                var deletion = DeleteOperation(sourceText[i - 1], i - 1);
                Consider(scores[i - 1, j] + OperationCost(deletion.Kind), new Parent(i - 1, j, deletion, 2));

                var insertion = InsertOperation(targetText[j - 1], i);
                Consider(scores[i, j - 1] + OperationCost(insertion.Kind), new Parent(i, j - 1, insertion, 3));

                scores[i, j] = bestCost;
                parents[i, j] = best;

                void Consider(double candidateCost, Parent candidate)
                {
                    if (candidateCost < bestCost - Epsilon
                        || Math.Abs(candidateCost - bestCost) < Epsilon && candidate.Priority < best.Priority)
                    {
                        bestCost = candidateCost;
                        best = candidate;
                    }
                }
            }

            if (RowMinimum(scores, i, targetLength) > budget)
            {
                alignment = Empty();
                return false;
            }
        }

        var cost = scores[sourceLength, targetLength];
        if (cost > budget + Epsilon)
        {
            alignment = Empty();
            return false;
        }

        alignment = new EditAlignment(cost, Backtrack(parents, sourceLength, targetLength));
        return true;
    }

    private static EditAlignment Empty() => new(0, Array.Empty<EditOperation>());

    private static double RowMinimum(double[,] scores, int row, int targetLength)
    {
        var minimum = double.PositiveInfinity;
        for (var j = 0; j <= targetLength; j++)
        {
            minimum = Math.Min(minimum, scores[row, j]);
        }

        return minimum;
    }

    private static IReadOnlyList<EditOperation> Backtrack(Parent[,] parents, int sourceLength, int targetLength)
    {
        var operations = new List<EditOperation>();
        var i = sourceLength;
        var j = targetLength;
        while (i > 0 || j > 0)
        {
            var parent = parents[i, j];
            operations.Add(parent.Operation);
            i = parent.PreviousSource;
            j = parent.PreviousTarget;
        }

        operations.Reverse();
        return operations;
    }

    private EditOperation SubstituteOperation(char source, char target, int sourceIndex)
    {
        var kind = source == target
            ? EditOperationKind.Keep
            : confusionSet.IsKnownConfusion(source, target)
                ? EditOperationKind.KnownGlyphSubstitution
                : EditOperationKind.OrdinarySubstitution;

        return new EditOperation(kind, sourceIndex, source, target);
    }

    private EditOperation DeleteOperation(char source, int sourceIndex)
    {
        var kind = source switch
        {
            ' ' or '\t' => EditOperationKind.SpaceDeletion,
            '\r' or '\n' => EditOperationKind.LineBreakDeletion,
            '-' or '\u00ad' => EditOperationKind.HyphenDeletion,
            _ when confusionSet.IsGarbageGlyph(source) => EditOperationKind.GarbageDeletion,
            _ => EditOperationKind.OrdinaryDeletion
        };

        return new EditOperation(kind, sourceIndex, source, '\0');
    }

    private static EditOperation InsertOperation(char target, int sourceIndex)
    {
        var kind = target is ' ' or '\t'
            ? EditOperationKind.SpaceInsertion
            : EditOperationKind.OrdinaryInsertion;
        return new EditOperation(kind, sourceIndex, '\0', target);
    }

    private double OperationCost(EditOperationKind kind) => kind switch
    {
        EditOperationKind.Keep => costs.Keep,
        EditOperationKind.KnownGlyphSubstitution => costs.KnownGlyphSubstitution,
        EditOperationKind.OrdinarySubstitution => costs.OrdinarySubstitution,
        EditOperationKind.SpaceDeletion => costs.SpaceDeletion,
        EditOperationKind.SpaceInsertion => costs.SpaceInsertion,
        EditOperationKind.HyphenDeletion => costs.HyphenDeletion,
        EditOperationKind.LineBreakDeletion => costs.LineBreakDeletion,
        EditOperationKind.GarbageDeletion => costs.GarbageDeletion,
        EditOperationKind.OrdinaryDeletion => costs.OrdinaryDeletion,
        EditOperationKind.OrdinaryInsertion => costs.OrdinaryInsertion,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private readonly record struct Parent(
        int PreviousSource,
        int PreviousTarget,
        EditOperation Operation,
        int Priority);
}
