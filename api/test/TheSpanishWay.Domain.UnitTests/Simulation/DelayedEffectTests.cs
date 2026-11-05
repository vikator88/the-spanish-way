using Shouldly;
using TheSpanishWay.Domain.Simulation.Enumerations;
using TheSpanishWay.Domain.Simulation.ValueObjects;
using TheSpanishWay.TestKit;

namespace TheSpanishWay.Domain.UnitTests.Simulation;

/// <summary>Delayed, gradual and recurring effects, and the queue that holds them.</summary>
public sealed class DelayedEffectTests
{
    [Fact]
    public void A_delayed_effect_does_nothing_until_its_date()
    {
        var scenario = EffectScenario.WithVariable(value: 5m, maximum: 100m);

        scenario.Engine.Apply(
            [EffectInstruction.Delayed(EffectScenario.Current(), EffectOperation.Add, 3m, 10, EffectScenario.SomeAction())],
            scenario.Context);

        scenario.BaseValue().ShouldBe(5m);
        scenario.Pending.Count.ShouldBe(1);

        scenario.AdvanceDays(9);
        scenario.Engine.ApplyDue(scenario.Context);
        scenario.BaseValue().ShouldBe(5m, "one day early");

        scenario.AdvanceDays(1);
        scenario.Engine.ApplyDue(scenario.Context);
        scenario.BaseValue().ShouldBe(8m);
        scenario.Pending.Count.ShouldBe(0);
    }

    [Fact]
    public void A_delayed_temporary_effect_becomes_a_modifier_on_its_date()
    {
        var scenario = EffectScenario.WithVariable(value: 5m, maximum: 100m);

        scenario.Engine.Apply(
            [
                EffectInstruction.Delayed(
                    EffectScenario.Current(), EffectOperation.Add, 3m, 5,
                    EffectScenario.SomeAction(), durationDays: 10),
            ],
            scenario.Context);

        scenario.AdvanceDays(5);
        scenario.Engine.ApplyDue(scenario.Context);

        scenario.BaseValue().ShouldBe(5m, "a temporary effect never touches the base");
        scenario.EffectiveValue().ShouldBe(8m);
    }

    [Fact]
    public void A_recurring_effect_applies_a_step_at_a_time()
    {
        var scenario = EffectScenario.WithVariable(value: 0m, maximum: 100m);

        var recurring = new EffectInstruction(
            EffectScenario.Current(), EffectOperation.Add, 2m, 1, null,
            new Recurrence(IntervalDays: 7, RemainingApplications: 3),
            StackKey.Named("gradual"), StackingPolicy.Stack, EffectScenario.SomeAction());

        scenario.Engine.Apply([recurring], scenario.Context);

        scenario.AdvanceDays(1);
        scenario.Engine.ApplyDue(scenario.Context);
        scenario.BaseValue().ShouldBe(2m);

        scenario.AdvanceDays(7);
        scenario.Engine.ApplyDue(scenario.Context);
        scenario.BaseValue().ShouldBe(4m);

        scenario.AdvanceDays(7);
        scenario.Engine.ApplyDue(scenario.Context);
        scenario.BaseValue().ShouldBe(6m);

        // Three applications were scheduled; a fourth must not happen.
        scenario.AdvanceDays(7);
        scenario.Engine.ApplyDue(scenario.Context);
        scenario.BaseValue().ShouldBe(6m);
        scenario.Pending.Count.ShouldBe(0);
    }

    [Fact]
    public void A_queued_effect_stores_its_resolved_value()
    {
        // "+1 to +3" is rolled when the effect is created, not when it applies, so a saved
        // game explains itself and reloading cannot reroll it.
        var scenario = EffectScenario.WithVariable(value: 0m, maximum: 100m);

        scenario.Engine.Apply(
            [EffectInstruction.Delayed(EffectScenario.Current(), EffectOperation.Add, 2m, 10, EffectScenario.SomeAction())],
            scenario.Context);

        scenario.Pending.All().ShouldHaveSingleItem().Value.ShouldBe(2m);
    }

    [Fact]
    public void Effects_due_on_the_same_day_apply_in_a_stable_order()
    {
        var first = EffectScenario.WithVariable(value: 10m, maximum: 1000m);
        var second = EffectScenario.WithVariable(value: 10m, maximum: 1000m);
        var action = EffectScenario.SomeAction();

        foreach (var scenario in new[] { first, second })
        {
            scenario.Engine.Apply(
                [
                    EffectInstruction.Delayed(EffectScenario.Current(), EffectOperation.Add, 5m, 3, action),
                    EffectInstruction.Delayed(EffectScenario.Current(), EffectOperation.Multiply, 2m, 3, action),
                ],
                scenario.Context);

            scenario.AdvanceDays(3);
            scenario.Engine.ApplyDue(scenario.Context);
        }

        first.BaseValue().ShouldBe(30m, "queued order is add then multiply");
        second.BaseValue().ShouldBe(first.BaseValue());
    }

    [Fact]
    public void Nothing_due_means_nothing_applied()
    {
        var scenario = EffectScenario.WithVariable(value: 5m, maximum: 100m);

        var result = scenario.Engine.ApplyDue(scenario.Context);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ChangeCount.ShouldBe(0);
    }
}
