using Shouldly;
using TheSpanishWay.Domain.Simulation.Enumerations;
using TheSpanishWay.Domain.Simulation.ValueObjects;
using TheSpanishWay.TestKit;

namespace TheSpanishWay.Domain.UnitTests.Simulation;

/// <summary>
/// The base-plus-modifier layering, which exists so temporary effects can be undone exactly.
/// </summary>
public sealed class ModifierLayerTests
{
    [Fact]
    public void A_clamped_temporary_effect_returns_to_its_exact_original_on_expiry()
    {
        // The case the whole design turns on. Inflation 9.5, ceiling 10, temporary +2.
        // Subtracting 2 on expiry would leave 8.0; removing a modifier leaves 9.5.
        var scenario = EffectScenario.WithVariable(value: 9.5m, maximum: 10m);

        scenario.Engine.Apply(
            [
                EffectInstruction.Temporary(
                    EffectScenario.Current(), EffectOperation.Add, 2m, 30, EffectScenario.SomeAction()),
            ],
            scenario.Context).IsSuccess.ShouldBeTrue();

        scenario.EffectiveValue().ShouldBe(10m, "the ceiling holds while the modifier is in force");
        scenario.BaseValue().ShouldBe(9.5m, "a temporary effect never touches the base");

        scenario.AdvanceDays(31);

        scenario.EffectiveValue().ShouldBe(9.5m);
        scenario.Modifiers.Count.ShouldBe(0);
    }

    [Fact]
    public void A_permanent_effect_moves_the_base()
    {
        var scenario = EffectScenario.WithVariable(value: 5m, maximum: 10m);

        scenario.Engine.Apply(
            [EffectInstruction.Immediate(EffectScenario.Current(), EffectOperation.Add, 2m, EffectScenario.SomeAction())],
            scenario.Context);

        scenario.BaseValue().ShouldBe(7m);
        scenario.Modifiers.Count.ShouldBe(0, "a permanent effect creates no modifier");
    }

    [Fact]
    public void Add_then_multiply_is_the_documented_order()
    {
        // (10 + 5) * 2 = 30, not 10 + (5 * 2) = 20. Authors must be able to predict which.
        var scenario = EffectScenario.WithVariable(value: 10m, maximum: 1000m);
        var action = EffectScenario.SomeAction();

        scenario.Engine.Apply(
            [
                EffectInstruction.Temporary(EffectScenario.Current(), EffectOperation.Add, 5m, 10, action,
                    stackKey: StackKey.Named("add")),
                EffectInstruction.Temporary(EffectScenario.Current(), EffectOperation.Multiply, 2m, 10, action,
                    stackKey: StackKey.Named("multiply")),
            ],
            scenario.Context);

        scenario.EffectiveValue().ShouldBe(30m);
    }

    [Fact]
    public void Set_overrides_the_base_before_additions_apply()
    {
        var scenario = EffectScenario.WithVariable(value: 10m, maximum: 1000m);
        var action = EffectScenario.SomeAction();

        scenario.Engine.Apply(
            [
                EffectInstruction.Temporary(EffectScenario.Current(), EffectOperation.Set, 50m, 10, action,
                    stackKey: StackKey.Named("set")),
                EffectInstruction.Temporary(EffectScenario.Current(), EffectOperation.Add, 5m, 10, action,
                    stackKey: StackKey.Named("add")),
            ],
            scenario.Context);

        scenario.EffectiveValue().ShouldBe(55m);
    }

    [Fact]
    public void An_effective_value_is_clamped_like_the_base()
    {
        var scenario = EffectScenario.WithVariable(value: 9m, minimum: 0m, maximum: 10m);

        scenario.Engine.Apply(
            [EffectInstruction.Temporary(EffectScenario.Current(), EffectOperation.Add, 100m, 10, EffectScenario.SomeAction())],
            scenario.Context);

        scenario.EffectiveValue().ShouldBe(10m);
    }

    [Fact]
    public void Expiry_is_inclusive_of_its_final_day()
    {
        var scenario = EffectScenario.WithVariable(value: 5m, maximum: 100m);

        scenario.Engine.Apply(
            [EffectInstruction.Temporary(EffectScenario.Current(), EffectOperation.Add, 3m, 10, EffectScenario.SomeAction())],
            scenario.Context);

        scenario.AdvanceDays(10);
        scenario.EffectiveValue().ShouldBe(8m, "still in force on its final day");

        scenario.AdvanceDays(1);
        scenario.EffectiveValue().ShouldBe(5m);
    }

    [Fact]
    public void Several_temporary_effects_expire_independently()
    {
        var scenario = EffectScenario.WithVariable(value: 0m, maximum: 100m);
        var action = EffectScenario.SomeAction();

        scenario.Engine.Apply(
            [
                EffectInstruction.Temporary(EffectScenario.Current(), EffectOperation.Add, 10m, 5, action,
                    stackKey: StackKey.Named("short")),
                EffectInstruction.Temporary(EffectScenario.Current(), EffectOperation.Add, 20m, 50, action,
                    stackKey: StackKey.Named("long")),
            ],
            scenario.Context);

        scenario.EffectiveValue().ShouldBe(30m);

        scenario.AdvanceDays(6);
        scenario.EffectiveValue().ShouldBe(20m);

        scenario.AdvanceDays(50);
        scenario.EffectiveValue().ShouldBe(0m);
    }

    [Fact]
    public void An_indefinite_modifier_never_expires_on_its_own()
    {
        var scenario = EffectScenario.WithVariable(value: 5m, maximum: 100m);

        scenario.Engine.Apply(
            [
                new EffectInstruction(
                    EffectScenario.Current(), EffectOperation.Add, 3m, null, null, null,
                    StackKey.Named("forever"), StackingPolicy.Stack, EffectScenario.SomeAction()),
            ],
            scenario.Context);

        // No duration means a permanent base change, not an immortal modifier.
        scenario.BaseValue().ShouldBe(8m);
        scenario.Modifiers.Count.ShouldBe(0);
    }
}
