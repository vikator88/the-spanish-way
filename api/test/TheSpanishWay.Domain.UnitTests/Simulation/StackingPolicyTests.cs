using Shouldly;
using TheSpanishWay.Domain.Simulation.Enumerations;
using TheSpanishWay.Domain.Simulation.ValueObjects;
using TheSpanishWay.TestKit;

namespace TheSpanishWay.Domain.UnitTests.Simulation;

/// <summary>
/// What a second copy of the same effect does. Explicit per effect, because "another
/// scandal while the first is still running" has no obvious default.
/// </summary>
public sealed class StackingPolicyTests
{
    private static EffectInstruction Repeat(StackingPolicy policy, decimal value = 10m, int days = 30) =>
        EffectInstruction.Temporary(
            EffectScenario.Current(), EffectOperation.Add, value, days,
            EffectScenario.SomeAction(), policy, StackKey.Named("same-effect"));

    [Fact]
    public void Stack_lets_both_apply()
    {
        var scenario = EffectScenario.WithVariable(value: 0m, maximum: 100m);

        scenario.Engine.Apply([Repeat(StackingPolicy.Stack)], scenario.Context);
        scenario.Engine.Apply([Repeat(StackingPolicy.Stack)], scenario.Context);

        scenario.Modifiers.Count.ShouldBe(2);
        scenario.EffectiveValue().ShouldBe(20m);
    }

    [Fact]
    public void RefreshDuration_prolongs_rather_than_deepens()
    {
        var scenario = EffectScenario.WithVariable(value: 0m, maximum: 100m);

        scenario.Engine.Apply([Repeat(StackingPolicy.RefreshDuration, days: 10)], scenario.Context);
        scenario.AdvanceDays(5);
        scenario.Engine.Apply([Repeat(StackingPolicy.RefreshDuration, days: 10)], scenario.Context);

        scenario.Modifiers.Count.ShouldBe(1);
        scenario.EffectiveValue().ShouldBe(10m, "the impression is prolonged, not doubled");

        // The original would have lapsed on day 10; the refresh carries it to day 15.
        scenario.AdvanceDays(6);
        scenario.EffectiveValue().ShouldBe(10m);

        scenario.AdvanceDays(5);
        scenario.EffectiveValue().ShouldBe(0m);
    }

    [Fact]
    public void RefreshDuration_is_the_default()
    {
        var scenario = EffectScenario.WithVariable(value: 0m, maximum: 100m);

        var instruction = EffectInstruction.Temporary(
            EffectScenario.Current(), EffectOperation.Add, 10m, 30, EffectScenario.SomeAction());

        instruction.Policy.ShouldBe(StackingPolicy.RefreshDuration);
    }

    [Fact]
    public void Replace_swaps_the_old_modifier_for_the_new()
    {
        var scenario = EffectScenario.WithVariable(value: 0m, maximum: 100m);

        scenario.Engine.Apply([Repeat(StackingPolicy.Replace, value: 10m)], scenario.Context);
        scenario.Engine.Apply([Repeat(StackingPolicy.Replace, value: 25m)], scenario.Context);

        scenario.Modifiers.Count.ShouldBe(1);
        scenario.EffectiveValue().ShouldBe(25m);
    }

    [Fact]
    public void IgnoreDuplicate_drops_the_newcomer()
    {
        var scenario = EffectScenario.WithVariable(value: 0m, maximum: 100m);

        scenario.Engine.Apply([Repeat(StackingPolicy.IgnoreDuplicate, value: 10m)], scenario.Context);
        scenario.Engine.Apply([Repeat(StackingPolicy.IgnoreDuplicate, value: 99m)], scenario.Context);

        scenario.Modifiers.Count.ShouldBe(1);
        scenario.EffectiveValue().ShouldBe(10m);
    }

    [Fact]
    public void Different_stack_keys_never_collide()
    {
        var scenario = EffectScenario.WithVariable(value: 0m, maximum: 100m);

        scenario.Engine.Apply(
            [
                EffectInstruction.Temporary(EffectScenario.Current(), EffectOperation.Add, 10m, 30,
                    EffectScenario.SomeAction(), StackingPolicy.IgnoreDuplicate, StackKey.Named("scandal-a")),
                EffectInstruction.Temporary(EffectScenario.Current(), EffectOperation.Add, 10m, 30,
                    EffectScenario.SomeAction(), StackingPolicy.IgnoreDuplicate, StackKey.Named("scandal-b")),
            ],
            scenario.Context);

        scenario.Modifiers.Count.ShouldBe(2);
        scenario.EffectiveValue().ShouldBe(20m);
    }

    [Fact]
    public void The_default_stack_key_is_source_effect_and_target()
    {
        var target = EffectScenario.Current();

        var key = StackKey.Default("corruption-scandal", 2, target);

        key.Value.ShouldBe($"corruption-scandal#2@{target}");
        StackKey.Default("corruption-scandal", 3, target).ShouldNotBe(key);
    }
}
