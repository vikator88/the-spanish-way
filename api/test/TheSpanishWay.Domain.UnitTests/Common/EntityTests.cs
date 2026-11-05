using Shouldly;
using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Shared.Identifiers;

namespace TheSpanishWay.Domain.UnitTests.Common;

public sealed class EntityTests
{
    private sealed class Party(PartyId id, string name) : Entity<PartyId>(id)
    {
        public string Name { get; } = name;
    }

    private sealed class Character(PartyId id) : Entity<PartyId>(id);

    private static readonly Guid SharedId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Same_id_and_type_are_equal_regardless_of_attributes()
    {
        var left = new Party(PartyId.From(SharedId), "Partido A");
        var right = new Party(PartyId.From(SharedId), "renamed since");

        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void Different_concrete_types_sharing_an_id_value_are_not_equal()
    {
        Entity<PartyId> party = new Party(PartyId.From(SharedId), "Partido A");
        Entity<PartyId> character = new Character(PartyId.From(SharedId));

        party.ShouldNotBe(character);
    }

    [Fact]
    public void Different_ids_are_not_equal()
    {
        var left = new Party(PartyId.From(SharedId), "Partido A");
        var right = new Party(PartyId.From(Guid.Parse("22222222-2222-2222-2222-222222222222")), "Partido A");

        left.ShouldNotBe(right);
    }

    [Fact]
    public void Null_is_never_equal_to_an_entity()
    {
        var party = new Party(PartyId.From(SharedId), "Partido A");

        party.Equals(null).ShouldBeFalse();
        (party == null).ShouldBeFalse();
    }

    [Fact]
    public void Id_is_fixed_at_construction()
    {
        typeof(Entity<PartyId>)
            .GetProperty(nameof(Entity<PartyId>.Id))!
            .SetMethod
            .ShouldBeNull("an entity must not be able to change which thing it is");
    }
}
