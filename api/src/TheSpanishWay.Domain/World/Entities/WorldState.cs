using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.World.ValueObjects;

namespace TheSpanishWay.Domain.World.Entities;

/// <summary>
/// The state of the simulated world: its indicators, and later its provinces, demography
/// and public agenda.
/// <para>
/// Deliberately not a dumping ground. It groups world-level state only; parties, characters
/// and the electorate are their own components.
/// </para>
/// </summary>
public sealed class WorldState
{
    private readonly Dictionary<VariableKey, WorldVariable> _variables;

    private WorldState(IEnumerable<WorldVariable> variables) =>
        _variables = variables.ToDictionary(v => v.Key);

    public static WorldState From(IEnumerable<WorldVariable> variables)
    {
        ArgumentNullException.ThrowIfNull(variables);

        return new WorldState(variables);
    }

    public static WorldState Empty() => new([]);

    /// <summary>Indicators in key order, because iteration order is part of the determinism contract.</summary>
    public IReadOnlyList<WorldVariable> Variables =>
        [.. _variables.Values.OrderBy(v => v.Key)];

    public bool Has(VariableKey key) => _variables.ContainsKey(key);

    public Result<WorldVariable> Variable(VariableKey key)
    {
        if (!_variables.TryGetValue(key, out var variable))
        {
            return Result.Failure<WorldVariable>(
                new Error("world.unknown_variable", $"No world variable named '{key}'."));
        }

        return Result.Success(variable);
    }

    public Result Add(WorldVariable variable)
    {
        ArgumentNullException.ThrowIfNull(variable);

        if (!_variables.TryAdd(variable.Key, variable))
        {
            return Result.Failure(
                new Error("world.duplicate_variable", $"World variable '{variable.Key}' is already defined."));
        }

        return Result.Success();
    }

    /// <summary>A deep copy, used to prove that a rejected change set left nothing behind.</summary>
    public WorldState Copy() => new(_variables.Values.Select(v => v.Copy()));
}
