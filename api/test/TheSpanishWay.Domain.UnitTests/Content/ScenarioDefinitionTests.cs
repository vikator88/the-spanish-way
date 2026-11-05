using Shouldly;
using TheSpanishWay.Domain.Content.Aggregates;
using TheSpanishWay.Domain.Content.Enumerations;
using TheSpanishWay.Domain.Content.Services;
using TheSpanishWay.Domain.Content.ValueObjects;
using TheSpanishWay.Domain.Shared.Enumerations;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.ValueObjects;
using TheSpanishWay.Domain.Simulation.Enumerations;

namespace TheSpanishWay.Domain.UnitTests.Content;

/// <summary>
/// Editing content must never reach a game already in progress.
/// </summary>
public sealed class ScenarioDefinitionTests
{
    private static readonly ContentValidator Validator = new(AttributeRegistry.Default());

    private static readonly PartyId SomeParty =
        PartyId.From(Guid.Parse("0000000a-0000-0000-0000-000000000001"));

    private static ScenarioDefinition Draft()
    {
        var scenario = ScenarioDefinition.Draft(
            ScenarioId.From(Guid.Parse("00000000-0000-0000-0000-000000000002")), "Spain 2027");

        scenario.AddParty(SomeParty);

        return scenario;
    }

    private static GameEventDefinition ValidEvent(string key = "scandal") =>
        GameEventDefinition.Create(
            EventDefinitionId.From(Guid.NewGuid()), key, EventCategory.Random, 0.1m,
            effects:
            [
                EffectDefinition.Immediate(
                    AttributeRef.From(TargetType.WorldVariable, "inflation", "current"),
                    EffectOperation.Add, 1m),
            ]).Value;

    [Fact]
    public void A_draft_can_be_edited()
    {
        var scenario = Draft();

        scenario.AddEvent(ValidEvent()).IsSuccess.ShouldBeTrue();
        scenario.Events.ShouldHaveSingleItem();
        scenario.IsPublished.ShouldBeFalse();
    }

    [Fact]
    public void Publishing_freezes_the_version()
    {
        var scenario = Draft();
        scenario.AddEvent(ValidEvent());

        scenario.Publish(Validator).IsSuccess.ShouldBeTrue();

        scenario.IsPublished.ShouldBeTrue();
        scenario.AddEvent(ValidEvent("another")).Error.Code.ShouldBe("scenario.published_is_immutable");
    }

    [Fact]
    public void Publishing_refuses_invalid_content()
    {
        var scenario = Draft();

        scenario.AddEvent(GameEventDefinition.Create(
            EventDefinitionId.From(Guid.NewGuid()), "broken", EventCategory.Random, 1m,
            effects:
            [
                EffectDefinition.Immediate(
                    AttributeRef.From(TargetType.SegmentPerception, "$x", "trust"),
                    EffectOperation.Add, -10m),
            ]).Value);

        scenario.Publish(Validator).Error.Code.ShouldBe("scenario.invalid_content");
        scenario.IsPublished.ShouldBeFalse("a failed publish leaves the draft editable");
    }

    [Fact]
    public void Editing_creates_a_new_version_and_leaves_the_published_one_alone()
    {
        var published = Draft();
        published.AddEvent(ValidEvent());
        published.Publish(Validator);

        var next = published.CreateNextVersion().Value;
        next.AddEvent(ValidEvent("new-event"));

        next.Version.ShouldBe(2);
        next.Events.Count.ShouldBe(2);
        published.Version.ShouldBe(1);
        published.Events.Count.ShouldBe(1, "the published version is untouched");
    }

    [Fact]
    public void A_game_pins_a_version_not_a_bare_scenario_id()
    {
        var scenario = Draft();
        scenario.AddEvent(ValidEvent());
        scenario.Publish(Validator);

        var reference = scenario.Reference;

        reference.Scenario.ShouldBe(scenario.Id);
        reference.Version.ShouldBe(1);

        // The next version carries a different reference, so a running game pinned to v1
        // keeps resolving content from v1.
        scenario.CreateNextVersion().Value.Reference.ShouldNotBe(reference);
    }

    [Fact]
    public void An_unpublished_draft_cannot_be_branched()
    {
        Draft().CreateNextVersion().Error.Code.ShouldBe("scenario.not_published");
    }

    [Fact]
    public void A_scenario_cannot_be_published_twice()
    {
        var scenario = Draft();
        scenario.AddEvent(ValidEvent());
        scenario.Publish(Validator);

        scenario.Publish(Validator).Error.Code.ShouldBe("scenario.already_published");
    }

    [Fact]
    public void A_scenario_needs_at_least_one_party()
    {
        var empty = ScenarioDefinition.Draft(ScenarioId.From(Guid.NewGuid()), "Empty");

        empty.Publish(Validator).Error.Code.ShouldBe("scenario.no_parties");
    }

    [Fact]
    public void Duplicate_keys_are_rejected()
    {
        var scenario = Draft();
        scenario.AddEvent(ValidEvent("scandal"));

        scenario.AddEvent(ValidEvent("scandal")).Error.Code.ShouldBe("scenario.duplicate_event");
    }

    [Fact]
    public void A_scenario_is_not_the_mutable_state_of_a_game()
    {
        // No tick ever writes here: everything a game changes lives in GameState.
        typeof(ScenarioDefinition).GetMethods()
            .Select(m => m.Name)
            .ShouldNotContain("Advance");
    }
}
