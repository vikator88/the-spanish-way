using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Shared.ValueObjects;
using TheSpanishWay.Domain.World.Enumerations;
using TheSpanishWay.Domain.World.ValueObjects;

namespace TheSpanishWay.Domain.World.Entities;

/// <summary>
/// One configurable indicator of the simulated world.
/// <para>
/// This holds only the <em>base</em> value — what permanent effects and natural evolution
/// have made it. Temporary effects live separately as modifiers and are never folded in
/// here, so their expiry is exact rather than an attempted subtraction. The value a rule
/// actually reads is the effective value, computed from both. See design D5.
/// </para>
/// </summary>
public sealed class WorldVariable
{
    private Bounded<decimal> _base;

    private WorldVariable(
        VariableKey key,
        Bounded<decimal> baseValue,
        VariablePolarity polarity,
        decimal? targetValue,
        decimal evolutionRate)
    {
        Key = key;
        _base = baseValue;
        Polarity = polarity;
        TargetValue = targetValue;
        EvolutionRate = evolutionRate;
    }

    public VariableKey Key { get; }

    /// <summary>What permanent change has made this indicator. Modifiers are not included.</summary>
    public decimal BaseValue => _base.Value;

    public decimal Minimum => _base.Minimum;

    public decimal Maximum => _base.Maximum;

    public VariablePolarity Polarity { get; }

    /// <summary>Where natural evolution is pulling this indicator, if anywhere.</summary>
    public decimal? TargetValue { get; private set; }

    /// <summary>
    /// How fast it is pulled, as a fraction of the remaining gap closed per cadence step.
    /// Structural indicators such as education use a far smaller rate than inflation, which
    /// is what makes some things slow to change and others volatile.
    /// </summary>
    public decimal EvolutionRate { get; private set; }

    public static Result<WorldVariable> Create(
        VariableKey key,
        decimal value,
        decimal minimum,
        decimal maximum,
        VariablePolarity polarity,
        decimal? targetValue = null,
        decimal evolutionRate = 0m)
    {
        var bounded = Bounded.Create(value, minimum, maximum);

        if (bounded.IsFailure)
        {
            return Result.Failure<WorldVariable>(bounded.Error);
        }

        if (evolutionRate is < 0m or > 1m)
        {
            return Result.Failure<WorldVariable>(
                new Error(
                    "world_variable.invalid_evolution_rate",
                    $"Evolution rate {evolutionRate} for '{key}' is not a fraction between 0 and 1."));
        }

        if (targetValue is { } target && (target < minimum || target > maximum))
        {
            return Result.Failure<WorldVariable>(
                new Error(
                    "world_variable.target_out_of_bounds",
                    $"Target {target} for '{key}' lies outside [{minimum}, {maximum}]."));
        }

        return Result.Success(
            new WorldVariable(key, bounded.Value, polarity, targetValue, evolutionRate));
    }

    /// <summary>Replaces the base value, clamping to bounds and reporting what did not fit.</summary>
    public BoundedChange<decimal> SetBase(decimal value)
    {
        var change = _base.With(value);
        _base = change.Result;

        return change;
    }

    public BoundedChange<decimal> AddToBase(decimal delta)
    {
        var change = _base.Add(delta);
        _base = change.Result;

        return change;
    }

    public BoundedChange<decimal> MultiplyBase(decimal factor)
    {
        var change = _base.Multiply(factor);
        _base = change.Result;

        return change;
    }

    /// <summary>
    /// Retargets natural evolution. Changing where a variable is heading is a different act
    /// from changing where it is, and content must be able to do the former without the latter.
    /// </summary>
    public Result SetTargetValue(decimal? targetValue)
    {
        if (targetValue is { } target && (target < Minimum || target > Maximum))
        {
            return Result.Failure(
                new Error(
                    "world_variable.target_out_of_bounds",
                    $"Target {target} for '{Key}' lies outside [{Minimum}, {Maximum}]."));
        }

        TargetValue = targetValue;

        return Result.Success();
    }

    public Result SetEvolutionRate(decimal rate)
    {
        if (rate is < 0m or > 1m)
        {
            return Result.Failure(
                new Error(
                    "world_variable.invalid_evolution_rate",
                    $"Evolution rate {rate} for '{Key}' is not a fraction between 0 and 1."));
        }

        EvolutionRate = rate;

        return Result.Success();
    }

    /// <summary>
    /// Moves the base one step toward its target, closing <see cref="EvolutionRate"/> of the
    /// remaining gap. A variable with no target, or a rate of zero, does not drift.
    /// </summary>
    public BoundedChange<decimal> EvolveOneStep()
    {
        if (TargetValue is not { } target || EvolutionRate == 0m)
        {
            return new BoundedChange<decimal>(_base, 0m);
        }

        return AddToBase((target - BaseValue) * EvolutionRate);
    }

    public WorldVariable Copy() => new(Key, _base, Polarity, TargetValue, EvolutionRate);
}
