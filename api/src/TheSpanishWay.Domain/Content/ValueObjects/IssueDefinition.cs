using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Content.Enumerations;
using TheSpanishWay.Domain.Shared.Enumerations;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.World.ValueObjects;

namespace TheSpanishWay.Domain.Content.ValueObjects;

/// <summary>How strongly an issue loads onto one ideological axis.</summary>
public sealed record AxisLoading(IdeologicalAxis Axis, decimal Weight);

/// <summary>
/// One topic politics can be about.
/// <para>
/// A positional issue declares which axes it loads onto, so a stance on it can be compared
/// with a segment's ideology. A valence issue declares which world variables measure it, so
/// "the economy is bad" has something concrete behind it. The two are never mixed.
/// </para>
/// </summary>
public sealed record IssueDefinition(
    IssueId Id,
    string Key,
    IssueKind Kind,
    IReadOnlyList<AxisLoading> AxisLoadings,
    IReadOnlyList<VariableKey> LinkedVariables)
{
    public static Result<IssueDefinition> CreatePositional(
        IssueId id,
        string key,
        IReadOnlyList<AxisLoading> loadings)
    {
        ArgumentNullException.ThrowIfNull(loadings);

        var validKey = ValidateKey(key);

        if (validKey.IsFailure)
        {
            return Result.Failure<IssueDefinition>(validKey.Error);
        }

        if (loadings.Count == 0)
        {
            return Result.Failure<IssueDefinition>(
                new Error(
                    "issue.no_axis_loadings",
                    $"Positional issue '{key}' must load onto at least one axis, or nothing can react to a stance on it."));
        }

        if (loadings.Any(l => l.Weight <= 0m))
        {
            return Result.Failure<IssueDefinition>(
                new Error("issue.non_positive_loading", $"Issue '{key}' has an axis loading of zero or less."));
        }

        if (loadings.Select(l => l.Axis).Distinct().Count() != loadings.Count)
        {
            return Result.Failure<IssueDefinition>(
                new Error("issue.duplicate_axis", $"Issue '{key}' loads onto the same axis twice."));
        }

        return Result.Success(
            new IssueDefinition(id, validKey.Value, IssueKind.Positional, [.. loadings.OrderBy(l => l.Axis)], []));
    }

    public static Result<IssueDefinition> CreateValence(
        IssueId id,
        string key,
        IReadOnlyList<VariableKey> linkedVariables)
    {
        ArgumentNullException.ThrowIfNull(linkedVariables);

        var validKey = ValidateKey(key);

        if (validKey.IsFailure)
        {
            return Result.Failure<IssueDefinition>(validKey.Error);
        }

        if (linkedVariables.Count == 0)
        {
            return Result.Failure<IssueDefinition>(
                new Error(
                    "issue.no_linked_variables",
                    $"Valence issue '{key}' must link to at least one world variable, or there is nothing to judge."));
        }

        return Result.Success(
            new IssueDefinition(id, validKey.Value, IssueKind.Valence, [], [.. linkedVariables.OrderBy(v => v)]));
    }

    /// <summary>
    /// Loadings normalised to sum to 1, so an issue spread over three axes does not count
    /// three times as much as one concentrated on a single axis.
    /// </summary>
    public IReadOnlyList<AxisLoading> NormalisedLoadings()
    {
        var total = AxisLoadings.Sum(l => l.Weight);

        return total == 0m
            ? AxisLoadings
            : [.. AxisLoadings.Select(l => l with { Weight = l.Weight / total })];
    }

    private static Result<string> ValidateKey(string key) =>
        string.IsNullOrWhiteSpace(key)
            ? Result.Failure<string>(new Error("issue.empty_key", "An issue needs a key."))
            : Result.Success(key.Trim().ToLowerInvariant());
}
