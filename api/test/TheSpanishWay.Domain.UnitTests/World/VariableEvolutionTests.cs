using Shouldly;
using TheSpanishWay.Domain.World.Entities;
using TheSpanishWay.Domain.World.Services;

namespace TheSpanishWay.Domain.UnitTests.World;

/// <summary>
/// The world moves on its own, and not everything moves at the same speed.
/// </summary>
public sealed class VariableEvolutionTests
{
    private static WorldState FullWorld() =>
        WorldState.From([.. WorldVariableCatalogue.Economy(), .. WorldVariableCatalogue.Society()]);

    [Fact]
    public void The_mvp_defines_five_economic_and_five_social_indicators()
    {
        WorldVariableCatalogue.Economy().Count.ShouldBe(5);
        WorldVariableCatalogue.Society().Count.ShouldBe(5);
    }

    [Fact]
    public void The_world_evolves_with_no_player_involvement()
    {
        var world = FullWorld();
        var before = world.Variable(WorldVariableCatalogue.Inflation).Value.BaseValue;

        VariableEvolution.AdvanceEconomy(world);

        world.Variable(WorldVariableCatalogue.Inflation).Value.BaseValue.ShouldNotBe(before);
    }

    [Fact]
    public void Structural_indicators_move_far_more_slowly_than_short_term_ones()
    {
        // A year of each indicator's own cadence: 52 weekly steps for inflation, 12 monthly
        // steps for education.
        var world = FullWorld();

        var inflationStart = world.Variable(WorldVariableCatalogue.Inflation).Value.BaseValue;
        var educationStart = world.Variable(WorldVariableCatalogue.Education).Value.BaseValue;

        for (var week = 0; week < 52; week++)
        {
            VariableEvolution.AdvanceEconomy(world);
        }

        for (var month = 0; month < 12; month++)
        {
            VariableEvolution.AdvanceSociety(world);
        }

        var inflation = world.Variable(WorldVariableCatalogue.Inflation).Value;
        var education = world.Variable(WorldVariableCatalogue.Education).Value;

        var inflationMoved = Math.Abs(inflation.BaseValue - inflationStart)
            / (inflation.Maximum - inflation.Minimum);
        var educationMoved = Math.Abs(education.BaseValue - educationStart)
            / (education.Maximum - education.Minimum);

        educationMoved.ShouldBeLessThan(
            inflationMoved, "education is structural; inflation is volatile");
    }

    [Fact]
    public void Economy_and_society_evolve_on_their_own_cadences()
    {
        var world = FullWorld();
        var educationBefore = world.Variable(WorldVariableCatalogue.Education).Value.BaseValue;

        VariableEvolution.AdvanceEconomy(world);

        world.Variable(WorldVariableCatalogue.Education).Value.BaseValue
            .ShouldBe(educationBefore, "a weekly economic step must not move society");
    }

    [Fact]
    public void Evolution_converges_on_its_target_rather_than_overshooting()
    {
        var world = FullWorld();

        for (var step = 0; step < 500; step++)
        {
            VariableEvolution.AdvanceEconomy(world);
        }

        var inflation = world.Variable(WorldVariableCatalogue.Inflation).Value;

        inflation.BaseValue.ShouldBe(inflation.TargetValue!.Value, 0.01m);
    }

    [Fact]
    public void Evolution_is_deterministic()
    {
        var first = FullWorld();
        var second = FullWorld();

        for (var step = 0; step < 20; step++)
        {
            VariableEvolution.AdvanceEconomy(first);
            VariableEvolution.AdvanceEconomy(second);
        }

        first.Variables.Select(v => v.BaseValue)
            .ShouldBe(second.Variables.Select(v => v.BaseValue));
    }
}
