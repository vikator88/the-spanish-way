using Shouldly;
using TheSpanishWay.Domain.Electorate.ValueObjects;
using TheSpanishWay.Domain.Games.Aggregates;
using TheSpanishWay.Domain.Politics.Entities;
using TheSpanishWay.Domain.Shared.Enumerations;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Architecture.Tests;

/// <summary>
/// The seven high-severity findings from the design review, each checked against the code
/// rather than against the plan.
/// <para>
/// These are the issues the review said would force a rework if built as v0.1 specified.
/// Having them asserted means a later refactor cannot quietly undo one.
/// </para>
/// </summary>
public sealed class ReviewGateTests
{
    [Fact]
    public void Issue1_the_running_world_is_one_aggregate()
    {
        // PoliticalParty as its own aggregate would force a cross-aggregate transaction on
        // every tick, which is what aggregates exist to prevent.
        var aggregateRoots = ArchitectureReference.Domain.GetTypes()
            .Where(t => t.BaseType is { IsGenericType: true }
                     && t.BaseType.GetGenericTypeDefinition() == typeof(Domain.Common.AggregateRoot<>))
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        // GameSession is the mutable one; ScenarioDefinition is immutable once published.
        aggregateRoots.ShouldBe([nameof(GameSession), "ScenarioDefinition"]);
        typeof(PartyState).BaseType!.Name.ShouldNotContain("AggregateRoot");
    }

    [Fact]
    public void Issue2_content_cannot_write_perceptions()
    {
        // Authored content writing "retiree trust −10" would be placing votes by hand.
        TargetType.SegmentPerception.IsComputed().ShouldBeTrue();
        TargetType.ElectoralPreference.IsComputed().ShouldBeTrue();
        TargetType.Turnout.IsComputed().ShouldBeTrue();
        TargetType.Seats.IsComputed().ShouldBeTrue();
    }

    [Fact]
    public void Issue3_events_bind_roles()
    {
        var definition = typeof(Domain.Content.ValueObjects.GameEventDefinition);

        definition.GetProperty("Roles").ShouldNotBeNull(
            "a reusable scandal must be able to say which party and which character");
        typeof(Domain.Content.Entities.EventOccurrence).GetProperty("Bindings").ShouldNotBeNull();
    }

    [Fact]
    public void Issue4_temporary_effects_use_a_modifier_layer()
    {
        // Reverting a clamped Add by subtraction is wrong; expiry must be removal.
        typeof(Domain.Simulation.ValueObjects.Modifier).GetProperty("ExpiresOn").ShouldNotBeNull();
        typeof(Domain.World.Entities.WorldVariable).GetProperty("BaseValue").ShouldNotBeNull();
        typeof(Domain.Simulation.ValueObjects.EffectiveValue).ShouldNotBeNull();
    }

    [Fact]
    public void Issue5_abstention_appears_once()
    {
        // Turnout decides whether; the softmax decides whom.
        typeof(ChoiceProbabilities).GetProperty("Abstention").ShouldBeNull();
        typeof(ChoiceOption).GetProperty("IsBlank").ShouldNotBeNull();
        typeof(Domain.Electorate.Services.TurnoutCalculator).ShouldNotBeNull();
    }

    [Fact]
    public void Issue6_parliament_government_and_attribution_all_have_defaults()
    {
        typeof(Parliament).GetMethod(nameof(Parliament.CommandsAbsoluteMajority)).ShouldNotBeNull();
        typeof(Domain.Politics.Services.GovernmentFormation).ShouldNotBeNull();
        typeof(Domain.Politics.Services.GovernmentPerformanceCalculator).ShouldNotBeNull();
        typeof(Domain.Politics.Services.PartySurvivalPolicy).ShouldNotBeNull();
    }

    [Fact]
    public void Issue7_segments_can_differ_by_province()
    {
        // Identical segment ideology everywhere rules out regionalist parties, which under
        // this electoral system decide who governs.
        typeof(ProvincialIdeologyOffset).ShouldNotBeNull();
        typeof(CandidacyRegister).GetMethod(nameof(CandidacyRegister.IsStanding)).ShouldNotBeNull();
    }

    [Fact]
    public void Issue8_no_fact_has_two_owners()
    {
        typeof(PartyState).GetProperty("Seats").ShouldBeNull("seats live in Parliament");
        typeof(GameSession).GetProperty(nameof(GameSession.CurrentDate))!.SetMethod.ShouldBeNull();
        typeof(GameSession).GetProperty(nameof(GameSession.Phase))!.SetMethod.ShouldBeNull();
        typeof(Domain.Actors.Entities.CharacterState).GetProperty("PartyId").ShouldBeNull();
        typeof(Domain.Politics.ValueObjects.PartyMembership).GetProperty("Loyalty").ShouldBeNull();
    }

    [Fact]
    public void Issue14_a_legislature_is_one_command()
    {
        typeof(GameSession).GetMethod("AdvanceGameDay").ShouldBeNull();
        typeof(GameSession).GetMethod(nameof(GameSession.AdvanceUntil)).ShouldNotBeNull();
    }

    [Fact]
    public void Issue22_no_version_field_reads_like_a_concurrency_token()
    {
        typeof(Domain.Games.ValueObjects.SchemaVersion).ShouldNotBeNull();
        ArchitectureReference.Domain.GetTypes()
            .Select(t => t.Name)
            .ShouldNotContain("StateVersion");
    }

    [Fact]
    public void In_game_time_is_never_wall_clock_time()
    {
        typeof(Domain.Common.IDomainEvent)
            .GetProperty(nameof(Domain.Common.IDomainEvent.OccurredOn))!
            .PropertyType.ShouldBe(typeof(GameDate));

        typeof(Domain.Politics.ValueObjects.PartyMembership)
            .GetProperty(nameof(Domain.Politics.ValueObjects.PartyMembership.Since))!
            .PropertyType.ShouldBe(typeof(GameDate));
    }
}
