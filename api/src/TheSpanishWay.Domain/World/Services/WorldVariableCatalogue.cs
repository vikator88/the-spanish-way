using TheSpanishWay.Domain.World.Entities;
using TheSpanishWay.Domain.World.Enumerations;
using TheSpanishWay.Domain.World.ValueObjects;

namespace TheSpanishWay.Domain.World.Services;

/// <summary>
/// The MVP indicators, as placeholder values a scenario will eventually supply.
/// <para>
/// Every number here is a configuration placeholder, not a design claim: v0.1 is explicit
/// that initial values and evolution speeds are an open calibration decision to be settled
/// by seed sweeps, not by argument.
/// </para>
/// <para>
/// The evolution rates differ on purpose. Inflation closes 15% of its gap per weekly step
/// while education closes 1% per monthly step, which is what makes short-term indicators
/// volatile and structural ones nearly immovable within one legislature.
/// </para>
/// </summary>
public static class WorldVariableCatalogue
{
    public static VariableKey Employment { get; } = VariableKey.From("employment");

    public static VariableKey Inflation { get; } = VariableKey.From("inflation");

    public static VariableKey EconomicGrowth { get; } = VariableKey.From("economicgrowth");

    public static VariableKey PublicDebt { get; } = VariableKey.From("publicdebt");

    public static VariableKey PublicDeficit { get; } = VariableKey.From("publicdeficit");

    public static VariableKey Education { get; } = VariableKey.From("education");

    public static VariableKey Inequality { get; } = VariableKey.From("inequality");

    public static VariableKey Security { get; } = VariableKey.From("security");

    public static VariableKey Welfare { get; } = VariableKey.From("welfare");

    public static VariableKey InstitutionalTrust { get; } = VariableKey.From("institutionaltrust");

    /// <summary>The five economic indicators, which evolve weekly.</summary>
    public static IReadOnlyList<WorldVariable> Economy() =>
    [
        Build(Employment, 62m, 0m, 100m, VariablePolarity.HigherIsBetter, 64m, 0.08m),
        Build(Inflation, 3.2m, -5m, 30m, VariablePolarity.HigherIsWorse, 2m, 0.15m),
        Build(EconomicGrowth, 1.8m, -15m, 15m, VariablePolarity.HigherIsBetter, 2m, 0.12m),
        Build(PublicDebt, 108m, 0m, 300m, VariablePolarity.HigherIsWorse, 105m, 0.03m),
        Build(PublicDeficit, 4.1m, -10m, 25m, VariablePolarity.HigherIsWorse, 3m, 0.06m),
    ];

    /// <summary>The five social indicators, which evolve monthly and far more slowly.</summary>
    public static IReadOnlyList<WorldVariable> Society() =>
    [
        Build(Education, 58m, 0m, 100m, VariablePolarity.HigherIsBetter, 60m, 0.01m),
        Build(Inequality, 33m, 0m, 100m, VariablePolarity.HigherIsWorse, 32m, 0.02m),
        Build(Security, 67m, 0m, 100m, VariablePolarity.HigherIsBetter, 68m, 0.04m),
        Build(Welfare, 61m, 0m, 100m, VariablePolarity.HigherIsBetter, 62m, 0.02m),
        Build(InstitutionalTrust, 42m, 0m, 100m, VariablePolarity.HigherIsBetter, 45m, 0.05m),
    ];

    private static WorldVariable Build(
        VariableKey key,
        decimal value,
        decimal minimum,
        decimal maximum,
        VariablePolarity polarity,
        decimal target,
        decimal rate)
    {
        var result = WorldVariable.Create(key, value, minimum, maximum, polarity, target, rate);

        return result.IsSuccess
            ? result.Value
            : throw new InvalidOperationException($"Catalogue defines an invalid variable: {result.Error}");
    }
}
