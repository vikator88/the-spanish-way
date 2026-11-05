using Shouldly;
using TheSpanishWay.Domain.Content.Entities;
using TheSpanishWay.Domain.Content.Enumerations;
using TheSpanishWay.Domain.Content.ValueObjects;
using TheSpanishWay.Domain.Shared.Enumerations;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Simulation.Enumerations;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Domain.UnitTests.Content;

public sealed class EventOccurrenceTests
{
    private static readonly GameDate Today = GameDate.From(2027, 3, 1);

    private static EventOccurrence Fire(
        bool blocking = false, int? durationDays = null, string? defaultOption = null)
    {
        var definition = GameEventDefinition.Create(
            EventDefinitionId.From(Guid.Parse("0000000e-0000-0000-0000-000000000001")),
            "scandal", EventCategory.Random, 1m,
            isBlocking: blocking, durationDays: durationDays, defaultOptionKey: defaultOption).Value;

        return EventOccurrence.Fire(
            EventOccurrenceId.From(Guid.Parse("0000000f-0000-0000-0000-000000000001")),
            definition, RoleBindings.Empty, Today);
    }

    [Fact]
    public void A_new_occurrence_is_open()
    {
        var occurrence = Fire();

        occurrence.Status.ShouldBe(EventOccurrenceStatus.Active);
        occurrence.IsOpen.ShouldBeTrue();
    }

    [Fact]
    public void A_resolved_occurrence_cannot_be_resolved_again()
    {
        var occurrence = Fire();
        occurrence.Resolve("condemn", Today).IsSuccess.ShouldBeTrue();

        var second = occurrence.Resolve("deny", Today);

        second.IsFailure.ShouldBeTrue();
        second.Error.Code.ShouldBe("occurrence.already_closed");
        occurrence.ChosenOptionKey.ShouldBe("condemn", "the first answer stands");
    }

    [Fact]
    public void An_expired_occurrence_refuses_a_decision()
    {
        var occurrence = Fire(durationDays: 5, defaultOption: "ignore");

        var result = occurrence.Resolve("condemn", Today.AddDays(6));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("occurrence.expired");
    }

    [Fact]
    public void A_decision_on_the_final_day_is_still_accepted()
    {
        var occurrence = Fire(durationDays: 5, defaultOption: "ignore");

        occurrence.Resolve("condemn", Today.AddDays(5)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void A_lapsed_occurrence_takes_its_default_option()
    {
        var occurrence = Fire(durationDays: 5, defaultOption: "ignore");

        occurrence.Lapse(Today.AddDays(6));

        occurrence.Status.ShouldBe(EventOccurrenceStatus.AutoResolved);
        occurrence.ChosenOptionKey.ShouldBe("ignore");
    }

    [Fact]
    public void An_occurrence_with_no_default_simply_expires()
    {
        var occurrence = Fire(durationDays: 5);

        occurrence.Lapse(Today.AddDays(6));

        occurrence.Status.ShouldBe(EventOccurrenceStatus.Expired);
        occurrence.ChosenOptionKey.ShouldBeNull();
    }

    [Fact]
    public void An_occurrence_keeps_the_bindings_it_fired_with()
    {
        var definition = GameEventDefinition.Create(
            EventDefinitionId.From(Guid.NewGuid()), "scandal", EventCategory.Random, 1m).Value;

        var bindings = RoleBindings.Empty.With(RoleName.From("$accused"), "party-a");

        var occurrence = EventOccurrence.Fire(
            EventOccurrenceId.From(Guid.NewGuid()), definition, bindings, Today);

        // Stored, not recomputed: a saved game can say who the scandal was about even after
        // the state that selected them has moved on.
        occurrence.Bindings.Resolve(RoleName.From("$accused")).Value.ShouldBe("party-a");
    }

    [Fact]
    public void A_blocking_occurrence_declares_itself()
    {
        Fire(blocking: true).IsBlocking.ShouldBeTrue();
        Fire(blocking: false).IsBlocking.ShouldBeFalse();
    }

    [Fact]
    public void A_non_blocking_event_that_expires_needs_a_default_option()
    {
        var result = GameEventDefinition.Create(
            EventDefinitionId.From(Guid.NewGuid()), "drifting", EventCategory.Random, 1m,
            isBlocking: false, durationDays: 10,
            effects: [EffectDefinition.Immediate(
                AttributeRef.From(TargetType.WorldVariable, "inflation", "current"),
                EffectOperation.Add, 1m)]);

        result.Error.Code.ShouldBe("event_definition.missing_default_option");
    }
}
