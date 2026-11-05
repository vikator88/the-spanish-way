using Shouldly;
using TheSpanishWay.Domain.Shared.Identifiers;

namespace TheSpanishWay.Domain.UnitTests.Shared.Identifiers;

public sealed class IdentifierTests
{
    [Fact]
    public void The_model_defines_every_identifier_the_spec_requires()
    {
        var expected = new[]
        {
            "GameId", "PartyId", "CharacterId", "ProvinceId", "SegmentId", "MediaOutletId",
            "PollsterId", "IssueId", "EventDefinitionId", "EventOccurrenceId",
            "ActionDefinitionId", "ActionExecutionId", "EffectDefinitionId", "PendingEffectId",
            "ModifierId", "ScenarioId", "ElectionId",
        };

        var declared = typeof(PartyId).Assembly
            .GetTypes()
            .Where(t => t.IsValueType && t.Name.EndsWith("Id", StringComparison.Ordinal))
            .Select(t => t.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var name in expected)
        {
            declared.ShouldContain(name);
        }
    }

    [Fact]
    public void Identifiers_are_distinct_types()
    {
        // The compile-time guarantee is the point: this asserts the runtime half of it.
        typeof(PartyId).ShouldNotBe(typeof(CharacterId));
    }

    [Fact]
    public void Identifiers_of_different_kinds_sharing_a_value_are_not_interchangeable()
    {
        var value = Guid.Parse("33333333-3333-3333-3333-333333333333");

        PartyId.From(value).Value.ShouldBe(CharacterId.From(value).Value);
        typeof(PartyId).IsAssignableFrom(typeof(CharacterId)).ShouldBeFalse();
    }

    [Fact]
    public void Identifiers_order_totally_so_iteration_can_be_stable()
    {
        var a = PartyId.From(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var b = PartyId.From(Guid.Parse("22222222-2222-2222-2222-222222222222"));

        (a < b).ShouldBeTrue();

        new[] { b, a }.OrderBy(x => x).ShouldBe([a, b]);
    }

    [Fact]
    public void Identifiers_expose_no_ambient_factory()
    {
        // An id minted from a clock or entropy during a tick would break replay, so
        // generation is the caller's explicit decision rather than a convenience here.
        typeof(PartyId).GetMethod("New").ShouldBeNull();
    }
}
