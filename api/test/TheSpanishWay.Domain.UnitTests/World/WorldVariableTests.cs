using Shouldly;
using TheSpanishWay.Domain.World.Entities;
using TheSpanishWay.Domain.World.Enumerations;
using TheSpanishWay.Domain.World.Services;
using TheSpanishWay.Domain.World.ValueObjects;

namespace TheSpanishWay.Domain.UnitTests.World;

public sealed class WorldVariableTests
{
    private static WorldVariable Variable(
        decimal value = 50m,
        decimal min = 0m,
        decimal max = 100m,
        decimal? target = null,
        decimal rate = 0m) =>
        WorldVariable.Create(VariableKey.From("test"), value, min, max,
            VariablePolarity.HigherIsBetter, target, rate).Value;

    [Fact]
    public void Indicators_are_data_not_classes()
    {
        // No Inflation type, no Unemployment type: a scenario must be able to add an
        // indicator without a code change.
        var named = typeof(WorldVariable).Assembly
            .GetTypes()
            .Select(t => t.Name)
            .Where(n => n is "Inflation" or "Unemployment" or "Education" or "PublicDebt")
            .ToList();

        named.ShouldBeEmpty();
    }

    [Fact]
    public void Polarity_is_declared_rather_than_inferred_from_the_key()
    {
        var economy = WorldVariableCatalogue.Economy();

        economy.Single(v => v.Key == WorldVariableCatalogue.Employment)
            .Polarity.ShouldBe(VariablePolarity.HigherIsBetter);
        economy.Single(v => v.Key == WorldVariableCatalogue.Inflation)
            .Polarity.ShouldBe(VariablePolarity.HigherIsWorse);
    }

    [Fact]
    public void A_value_cannot_leave_its_bounds()
    {
        var variable = Variable(value: 98m);

        variable.AddToBase(7m);

        variable.BaseValue.ShouldBe(100m);
    }

    [Fact]
    public void Construction_rejects_an_out_of_range_target()
    {
        var result = WorldVariable.Create(
            VariableKey.From("x"), 50m, 0m, 100m, VariablePolarity.Neutral, targetValue: 150m);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("world_variable.target_out_of_bounds");
    }

    [Fact]
    public void Construction_rejects_an_evolution_rate_outside_zero_to_one()
    {
        WorldVariable.Create(VariableKey.From("x"), 50m, 0m, 100m, VariablePolarity.Neutral, evolutionRate: 1.5m)
            .Error.Code.ShouldBe("world_variable.invalid_evolution_rate");
    }

    [Fact]
    public void A_variable_with_no_target_does_not_drift()
    {
        var variable = Variable(value: 50m, target: null, rate: 0.5m);

        variable.EvolveOneStep();

        variable.BaseValue.ShouldBe(50m);
    }

    [Fact]
    public void Evolution_closes_the_configured_fraction_of_the_gap()
    {
        var variable = Variable(value: 0m, target: 100m, rate: 0.1m);

        variable.EvolveOneStep();

        variable.BaseValue.ShouldBe(10m);
    }

    [Fact]
    public void A_variable_key_is_normalised()
    {
        VariableKey.From("  Inflation  ").Value.ShouldBe("inflation");
        VariableKey.Create("   ").IsFailure.ShouldBeTrue();
    }
}
