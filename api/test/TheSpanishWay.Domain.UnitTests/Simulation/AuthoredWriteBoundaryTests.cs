using Shouldly;
using TheSpanishWay.Domain.Content.Services;
using TheSpanishWay.Domain.Simulation.Enumerations;
using TheSpanishWay.Domain.Simulation.ValueObjects;
using TheSpanishWay.TestKit;

using TheSpanishWay.Domain.Shared.ValueObjects;

using TheSpanishWay.Domain.Shared.Enumerations;

using TheSpanishWay.Domain.Shared.Services;

namespace TheSpanishWay.Domain.UnitTests.Simulation;

/// <summary>
/// The boundary that protects the product's central promise: content may change the world,
/// but it may never write what voters think. Perceptions, preferences, turnout and seats
/// are reachable only through stimuli.
/// </summary>
public sealed class AuthoredWriteBoundaryTests
{
    private static readonly AttributeRegistry Registry = AttributeRegistry.Default();

    [Theory]
    [InlineData(TargetType.SegmentPerception, "trust")]
    [InlineData(TargetType.SegmentPerception, "respect")]
    [InlineData(TargetType.ElectoralPreference, "share")]
    [InlineData(TargetType.Turnout, "rate")]
    [InlineData(TargetType.Seats, "count")]
    public void Content_cannot_write_a_computed_attribute(TargetType target, string attribute)
    {
        var result = Registry.ValidateAuthoredWrite(
            AttributeRef.From(target, "anything", attribute), EffectOperation.Add);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("attribute_registry.computed_attribute");
    }

    [Fact]
    public void An_effect_targeting_a_perception_is_rejected_by_the_engine()
    {
        var scenario = EffectScenario.WithVariable();

        var result = scenario.Engine.Apply(
            [
                EffectInstruction.Immediate(
                    AttributeRef.From(TargetType.SegmentPerception, "retirees", "trust"),
                    EffectOperation.Add, -10m, EffectScenario.SomeAction()),
            ],
            scenario.Context);

        result.IsFailure.ShouldBeTrue("an author writing 'retiree trust -10' is placing votes by hand");
    }

    [Theory]
    [InlineData(TargetType.WorldVariable)]
    [InlineData(TargetType.Party)]
    [InlineData(TargetType.Character)]
    [InlineData(TargetType.PartyRelation)]
    [InlineData(TargetType.AgendaTopic)]
    [InlineData(TargetType.PartyResource)]
    [InlineData(TargetType.ProvincePresence)]
    public void Authorable_targets_are_authorable(TargetType target)
    {
        target.IsAuthorable().ShouldBeTrue();
        target.IsComputed().ShouldBeFalse();
    }

    [Fact]
    public void An_unknown_attribute_fails_validation_rather_than_doing_nothing()
    {
        var result = Registry.ValidateAuthoredWrite(
            AttributeRef.From(TargetType.Party, "some-party", "charm"), EffectOperation.Add);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("attribute_registry.unknown_attribute");
    }

    [Fact]
    public void An_unpermitted_operation_is_refused()
    {
        // Multiplying an evolution rate is not meaningful, so the registry does not allow it.
        var result = Registry.ValidateAuthoredWrite(
            AttributeRef.From(TargetType.WorldVariable, "inflation", "evolutionrate"),
            EffectOperation.Multiply);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("attribute_registry.operation_not_permitted");
    }

    [Fact]
    public void Attribute_keys_are_case_insensitive_so_content_and_code_cannot_disagree()
    {
        Registry.ValidateAuthoredWrite(
            AttributeRef.From(TargetType.Party, "p", "COHESION"), EffectOperation.Add)
            .IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Trajectory_can_be_retargeted_without_moving_the_value()
    {
        var scenario = EffectScenario.WithVariable(value: 5m, maximum: 100m, target: 5m, evolutionRate: 0.5m);

        scenario.Engine.Apply(
            [
                EffectInstruction.Immediate(
                    AttributeRef.From(TargetType.WorldVariable, "inflation", "target"),
                    EffectOperation.Set, 20m, EffectScenario.SomeAction()),
            ],
            scenario.Context).IsSuccess.ShouldBeTrue();

        scenario.BaseValue().ShouldBe(5m, "retargeting changes where it is heading, not where it is");
        scenario.World.Variables[0].TargetValue.ShouldBe(20m);
    }
}
