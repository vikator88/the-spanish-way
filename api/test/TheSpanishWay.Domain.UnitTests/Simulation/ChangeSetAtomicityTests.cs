using Shouldly;
using TheSpanishWay.Domain.Simulation.Enumerations;
using TheSpanishWay.Domain.Simulation.ValueObjects;
using TheSpanishWay.TestKit;

using TheSpanishWay.Domain.Shared.ValueObjects;

using TheSpanishWay.Domain.Shared.Enumerations;

using TheSpanishWay.Domain.Shared.Services;

namespace TheSpanishWay.Domain.UnitTests.Simulation;

/// <summary>
/// All or nothing. Effects propose changes, the engine validates the whole batch, and only
/// then writes — so an action that fails on its third effect cannot leave the first two
/// applied.
/// </summary>
public sealed class ChangeSetAtomicityTests
{
    private static AttributeRef Unknown() =>
        AttributeRef.From(TargetType.WorldVariable, "inflation", "no-such-attribute");

    private static AttributeRef MissingVariable() =>
        AttributeRef.From(TargetType.WorldVariable, "nonexistent", "current");

    [Fact]
    public void One_invalid_effect_rejects_the_whole_batch()
    {
        var scenario = EffectScenario.WithVariable(value: 5m, maximum: 100m);
        var action = EffectScenario.SomeAction();

        var result = scenario.Engine.Apply(
            [
                EffectInstruction.Immediate(EffectScenario.Current(), EffectOperation.Add, 1m, action),
                EffectInstruction.Immediate(Unknown(), EffectOperation.Add, 1m, action),
                EffectInstruction.Immediate(EffectScenario.Current(), EffectOperation.Add, 1m, action),
            ],
            scenario.Context);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("effect_engine.change_set_rejected");
        scenario.BaseValue().ShouldBe(5m, "the two valid effects must not have applied either");
    }

    [Fact]
    public void A_rejected_batch_leaves_the_state_identical()
    {
        var scenario = EffectScenario.WithFullWorld();
        var before = Fingerprint(scenario);
        var action = EffectScenario.SomeAction();

        scenario.Engine.Apply(
            [
                EffectInstruction.Immediate(EffectScenario.Current(), EffectOperation.Add, 3m, action),
                EffectInstruction.Temporary(EffectScenario.Current("education"), EffectOperation.Add, 5m, 10, action),
                EffectInstruction.Delayed(EffectScenario.Current("welfare"), EffectOperation.Add, 1m, 5, action),
                EffectInstruction.Immediate(MissingVariable(), EffectOperation.Add, 1m, action),
            ],
            scenario.Context).IsFailure.ShouldBeTrue();

        Fingerprint(scenario).ShouldBe(before);
        scenario.Modifiers.Count.ShouldBe(0);
        scenario.Pending.Count.ShouldBe(0);
    }

    [Fact]
    public void An_effect_naming_a_variable_that_does_not_exist_is_rejected()
    {
        var scenario = EffectScenario.WithVariable();

        var result = scenario.Engine.Apply(
            [EffectInstruction.Immediate(MissingVariable(), EffectOperation.Add, 1m, EffectScenario.SomeAction())],
            scenario.Context);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void A_valid_batch_applies_every_effect()
    {
        var scenario = EffectScenario.WithFullWorld();
        var action = EffectScenario.SomeAction();

        var result = scenario.Engine.Apply(
            [
                EffectInstruction.Immediate(EffectScenario.Current(), EffectOperation.Add, 1m, action),
                EffectInstruction.Temporary(EffectScenario.Current("education"), EffectOperation.Add, 5m, 10, action),
                EffectInstruction.Delayed(EffectScenario.Current("welfare"), EffectOperation.Add, 1m, 5, action),
            ],
            scenario.Context);

        result.IsSuccess.ShouldBeTrue();
        result.Value.BaseMutations.Count.ShouldBe(1);
        result.Value.ModifiersAdded.Count.ShouldBe(1);
        result.Value.EffectsQueued.Count.ShouldBe(1);
        result.Value.ChangeCount.ShouldBe(3);
    }

    /// <summary>
    /// Stands in for "the serialised snapshot is byte-identical": every base value, every
    /// modifier and every queued effect, in a stable order.
    /// </summary>
    private static string Fingerprint(EffectScenario scenario) =>
        string.Join(
            "|",
            scenario.World.Variables.Select(v =>
                $"{v.Key}={v.BaseValue}:{v.TargetValue}:{v.EvolutionRate}"))
        + "||" + string.Join("|", scenario.Modifiers.All().Select(m => $"{m.Target}:{m.Value}:{m.ExpiresOn}"))
        + "||" + string.Join("|", scenario.Pending.All().Select(p => $"{p.Target}:{p.Value}:{p.ApplyOn}"));
}
