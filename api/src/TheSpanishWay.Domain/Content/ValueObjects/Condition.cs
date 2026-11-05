using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Content.Enumerations;
using TheSpanishWay.Domain.Simulation.ValueObjects;

using TheSpanishWay.Domain.Shared.ValueObjects;

using TheSpanishWay.Domain.Shared.Enumerations;

using TheSpanishWay.Domain.Shared.Services;

namespace TheSpanishWay.Domain.Content.ValueObjects;

/// <summary>
/// One data-driven predicate: look at this attribute, compare it this way, to this value.
/// <para>
/// The target's instance may be a role token such as <c>$accused</c>, resolved when the
/// definition fires.
/// </para>
/// </summary>
public sealed record Condition(
    AttributeRef Target,
    ComparisonOperator Operator,
    decimal? Value,
    decimal? UpperValue)
{
    public static Result<Condition> Compare(
        AttributeRef target,
        ComparisonOperator op,
        decimal value)
    {
        if (op is ComparisonOperator.InRange)
        {
            return Result.Failure<Condition>(
                new Error("condition.range_needs_two_bounds", "InRange needs both a lower and an upper bound."));
        }

        if (op is ComparisonOperator.Exists or ComparisonOperator.NotExists)
        {
            return Result.Failure<Condition>(
                new Error("condition.existence_takes_no_value", $"{op} does not take a value."));
        }

        return Result.Success(new Condition(target, op, value, null));
    }

    public static Result<Condition> InRange(AttributeRef target, decimal lower, decimal upper)
    {
        if (lower > upper)
        {
            return Result.Failure<Condition>(
                new Error("condition.inverted_range", $"Range [{lower}, {upper}] is inverted."));
        }

        return Result.Success(new Condition(target, ComparisonOperator.InRange, lower, upper));
    }

    public static Condition Exists(AttributeRef target) =>
        new(target, ComparisonOperator.Exists, null, null);

    public static Condition NotExists(AttributeRef target) =>
        new(target, ComparisonOperator.NotExists, null, null);

    /// <summary>True when this condition reads a role rather than a literal instance.</summary>
    public bool ReferencesRole => RoleName.IsRoleToken(Target.Instance);
}

/// <summary>
/// Conditions combined with AND or OR, nesting to whatever depth a definition needs.
/// <para>
/// An empty group holds: a definition with no trigger conditions is always eligible, which
/// is what "a purely random event" means.
/// </para>
/// </summary>
public sealed record ConditionGroup(
    LogicalOperator Operator,
    IReadOnlyList<Condition> Conditions,
    IReadOnlyList<ConditionGroup> Groups)
{
    public static ConditionGroup Always { get; } = new(LogicalOperator.All, [], []);

    public static ConditionGroup All(params Condition[] conditions) =>
        new(LogicalOperator.All, conditions, []);

    public static ConditionGroup Any(params Condition[] conditions) =>
        new(LogicalOperator.Any, conditions, []);

    public static ConditionGroup Nested(LogicalOperator op, params ConditionGroup[] groups) =>
        new(op, [], groups);

    public bool IsEmpty => Conditions.Count == 0 && Groups.Count == 0;

    /// <summary>Every condition in this group and all its descendants.</summary>
    public IEnumerable<Condition> Flatten()
    {
        foreach (var condition in Conditions)
        {
            yield return condition;
        }

        foreach (var child in Groups.SelectMany(g => g.Flatten()))
        {
            yield return child;
        }
    }
}
