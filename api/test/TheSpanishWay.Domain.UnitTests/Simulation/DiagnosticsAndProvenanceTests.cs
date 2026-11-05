using Shouldly;
using TheSpanishWay.Domain.Simulation.Enumerations;
using TheSpanishWay.Domain.Simulation.ValueObjects;
using TheSpanishWay.TestKit;

namespace TheSpanishWay.Domain.UnitTests.Simulation;

/// <summary>
/// Truncation is reported rather than lost, and every change says what caused it.
/// </summary>
public sealed class DiagnosticsAndProvenanceTests
{
    [Fact]
    public void Truncation_is_reported_with_the_amount_refused()
    {
        var scenario = EffectScenario.WithVariable(value: 9.5m, maximum: 10m);

        var result = scenario.Engine.Apply(
            [EffectInstruction.Immediate(EffectScenario.Current(), EffectOperation.Add, 2m, EffectScenario.SomeAction())],
            scenario.Context);

        result.IsSuccess.ShouldBeTrue("truncation is information, not failure");
        result.Value.HadTruncation.ShouldBeTrue();

        var diagnostic = result.Value.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Truncated.ShouldBe(1.5m);
        diagnostic.Applied.ShouldBe(10m);
        diagnostic.Target.ShouldBe(EffectScenario.Current());
    }

    [Fact]
    public void An_effect_that_fits_produces_no_diagnostic()
    {
        var scenario = EffectScenario.WithVariable(value: 5m, maximum: 100m);

        var result = scenario.Engine.Apply(
            [EffectInstruction.Immediate(EffectScenario.Current(), EffectOperation.Add, 2m, EffectScenario.SomeAction())],
            scenario.Context);

        result.Value.HadTruncation.ShouldBeFalse();
    }

    [Fact]
    public void A_diagnostic_carries_the_provenance_of_what_caused_it()
    {
        var scenario = EffectScenario.WithVariable(value: 9.5m, maximum: 10m);
        var provenance = EffectScenario.SomeAction("oil-shock", ChangeOrigin.External);

        var result = scenario.Engine.Apply(
            [EffectInstruction.Immediate(EffectScenario.Current(), EffectOperation.Add, 2m, provenance)],
            scenario.Context);

        var diagnostic = result.Value.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Provenance.SourceId.ShouldBe("oil-shock");
        diagnostic.Provenance.Origin.ShouldBe(ChangeOrigin.External);
    }

    [Fact]
    public void A_modifier_carries_its_provenance()
    {
        var scenario = EffectScenario.WithVariable(value: 5m, maximum: 100m);
        var provenance = EffectScenario.SomeAction("press-conference");

        var result = scenario.Engine.Apply(
            [EffectInstruction.Temporary(EffectScenario.Current(), EffectOperation.Add, 3m, 10, provenance)],
            scenario.Context);

        var modifier = result.Value.ModifiersAdded.ShouldHaveSingleItem();
        modifier.Provenance.Source.ShouldBe(ChangeSource.ActionExecution);
        modifier.Provenance.SourceId.ShouldBe("press-conference");
    }

    [Fact]
    public void External_and_policy_origins_are_distinguishable()
    {
        // Government attribution reads this tag: an incumbent is not automatically
        // responsible for an oil shock.
        var scenario = EffectScenario.WithVariable(value: 5m, maximum: 100m);

        scenario.Engine.Apply(
            [
                EffectInstruction.Temporary(EffectScenario.Current(), EffectOperation.Add, 1m, 10,
                    EffectScenario.SomeAction("budget", ChangeOrigin.Policy), stackKey: StackKey.Named("a")),
                EffectInstruction.Temporary(EffectScenario.Current(), EffectOperation.Add, 1m, 10,
                    EffectScenario.SomeAction("oil", ChangeOrigin.External), stackKey: StackKey.Named("b")),
            ],
            scenario.Context);

        var modifiers = scenario.Modifiers.All();
        modifiers.Count(m => m.Provenance.Origin == ChangeOrigin.Policy).ShouldBe(1);
        modifiers.Count(m => m.Provenance.Origin == ChangeOrigin.External).ShouldBe(1);
    }

    [Fact]
    public void Natural_evolution_is_tagged_external()
    {
        Provenance.NaturalEvolution("inflation").Origin.ShouldBe(ChangeOrigin.External);
        Provenance.NaturalEvolution("inflation").Source.ShouldBe(ChangeSource.NaturalEvolution);
    }
}
