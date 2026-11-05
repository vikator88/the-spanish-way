using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Content.Enumerations;
using TheSpanishWay.Domain.Content.ValueObjects;
using TheSpanishWay.Domain.Simulation.ValueObjects;

using TheSpanishWay.Domain.Shared.ValueObjects;

using TheSpanishWay.Domain.Shared.Enumerations;

using TheSpanishWay.Domain.Shared.Services;

namespace TheSpanishWay.Domain.Content.Services;

/// <summary>
/// Supplies the current value of an attribute, so conditions can be evaluated without the
/// evaluator knowing anything about how the world is put together.
/// </summary>
public interface IAttributeReader
{
    /// <summary>The effective value, or a failure when the attribute names nothing that exists.</summary>
    Result<decimal> Read(AttributeRef target);
}

/// <summary>
/// Evaluates data-driven conditions.
/// <para>
/// Interprets a closed set of operators over values it is given. It compiles nothing,
/// evaluates no authored string as code, and cannot reach anything the attribute reader
/// does not offer — so content can describe a predicate without being able to execute one.
/// </para>
/// </summary>
public sealed class ConditionEvaluator(IAttributeReader reader)
{
    private readonly IAttributeReader _reader = reader;

    /// <summary>
    /// Evaluates a group against the current state. An empty group holds, which is what
    /// makes a definition with no trigger conditions always eligible.
    /// </summary>
    public Result<bool> Evaluate(ConditionGroup group, RoleBindings bindings)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(bindings);

        if (group.IsEmpty)
        {
            return Result.Success(true);
        }

        var results = new List<bool>();

        foreach (var condition in group.Conditions)
        {
            var evaluated = Evaluate(condition, bindings);

            if (evaluated.IsFailure)
            {
                return evaluated;
            }

            results.Add(evaluated.Value);

            // Short-circuit once the answer cannot change.
            if (group.Operator == LogicalOperator.All && !evaluated.Value)
            {
                return Result.Success(false);
            }

            if (group.Operator == LogicalOperator.Any && evaluated.Value)
            {
                return Result.Success(true);
            }
        }

        foreach (var nested in group.Groups)
        {
            var evaluated = Evaluate(nested, bindings);

            if (evaluated.IsFailure)
            {
                return evaluated;
            }

            results.Add(evaluated.Value);

            if (group.Operator == LogicalOperator.All && !evaluated.Value)
            {
                return Result.Success(false);
            }

            if (group.Operator == LogicalOperator.Any && evaluated.Value)
            {
                return Result.Success(true);
            }
        }

        return Result.Success(
            group.Operator == LogicalOperator.All ? results.All(r => r) : results.Any(r => r));
    }

    public Result<bool> Evaluate(Condition condition, RoleBindings bindings)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(bindings);

        var resolved = ResolveTarget(condition.Target, bindings);

        if (resolved.IsFailure)
        {
            return Result.Failure<bool>(resolved.Error);
        }

        var read = _reader.Read(resolved.Value);

        // Existence is answerable even when the target is missing; everything else is not.
        if (condition.Operator == ComparisonOperator.Exists)
        {
            return Result.Success(read.IsSuccess);
        }

        if (condition.Operator == ComparisonOperator.NotExists)
        {
            return Result.Success(read.IsFailure);
        }

        if (read.IsFailure)
        {
            return Result.Failure<bool>(read.Error);
        }

        var actual = read.Value;

        return condition.Operator switch
        {
            ComparisonOperator.Equal => Result.Success(actual == condition.Value),
            ComparisonOperator.NotEqual => Result.Success(actual != condition.Value),
            ComparisonOperator.GreaterThan => Result.Success(actual > condition.Value),
            ComparisonOperator.GreaterThanOrEqual => Result.Success(actual >= condition.Value),
            ComparisonOperator.LessThan => Result.Success(actual < condition.Value),
            ComparisonOperator.LessThanOrEqual => Result.Success(actual <= condition.Value),
            ComparisonOperator.InRange =>
                Result.Success(actual >= condition.Value && actual <= condition.UpperValue),
            _ => Result.Failure<bool>(
                new Error("condition.unsupported_operator", $"Operator {condition.Operator} is not supported.")),
        };
    }

    private static Result<AttributeRef> ResolveTarget(AttributeRef target, RoleBindings bindings)
    {
        var instance = bindings.ResolveInstance(target.Instance);

        return instance.IsFailure
            ? Result.Failure<AttributeRef>(instance.Error)
            : AttributeRef.Create(target.Target, instance.Value, target.Attribute);
    }
}
