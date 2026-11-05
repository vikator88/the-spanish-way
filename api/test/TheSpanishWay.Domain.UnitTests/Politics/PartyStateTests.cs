using Shouldly;
using TheSpanishWay.Domain.Politics.Entities;
using TheSpanishWay.Domain.Politics.Enumerations;
using TheSpanishWay.Domain.Politics.ValueObjects;
using TheSpanishWay.Domain.Shared.ValueObjects;
using TheSpanishWay.TestKit;

namespace TheSpanishWay.Domain.UnitTests.Politics;

public sealed class PartyStateTests
{
    [Fact]
    public void Seats_are_not_a_party_attribute()
    {
        // Seats are an election outcome and live in Parliament, so a party cannot carry a
        // figure that disagrees with the chamber. Review issue #8.
        typeof(PartyState).GetProperty("Seats").ShouldBeNull();
    }

    [Fact]
    public void The_leader_is_derived_from_membership()
    {
        var party = PoliticsFixtures.Party();

        party.LeaderId.ShouldBe(PoliticsFixtures.Leader);
        typeof(PartyState).GetProperty(nameof(PartyState.LeaderId))!.SetMethod.ShouldBeNull();
    }

    [Fact]
    public void Leadership_can_only_pass_to_an_active_member()
    {
        var party = PoliticsFixtures.Party();
        var outsider = Domain.Shared.Identifiers.CharacterId.From(Guid.NewGuid());

        party.ChangeLeader(outsider).Error.Code.ShouldBe("party.leader_not_a_member");
        party.LeaderId.ShouldBe(PoliticsFixtures.Leader, "the old leader stays");
    }

    [Fact]
    public void A_leadership_change_demotes_the_previous_leader()
    {
        var party = PoliticsFixtures.Party();

        var previous = party.ChangeLeader(PoliticsFixtures.Deputy);

        previous.Value.ShouldBe(PoliticsFixtures.Leader);
        party.LeaderId.ShouldBe(PoliticsFixtures.Deputy);
        party.ActiveMemberships.Single(m => m.Character == PoliticsFixtures.Leader)
            .Role.ShouldBe(PartyRole.Member);
    }

    [Fact]
    public void A_character_cannot_hold_two_active_memberships_in_one_party()
    {
        var party = PoliticsFixtures.Party();

        party.AddMember(PartyMembership.Create(
            PoliticsFixtures.Deputy, PartyRole.Member, PoliticsFixtures.Today).Value)
            .Error.Code.ShouldBe("party.duplicate_membership");
    }

    [Theory]
    [InlineData(MembershipStatus.Expelled)]
    [InlineData(MembershipStatus.Resigned)]
    [InlineData(MembershipStatus.Defected)]
    public void Leaving_a_party_is_an_explicit_transition(MembershipStatus expected)
    {
        var party = PoliticsFixtures.Party();

        var result = expected switch
        {
            MembershipStatus.Expelled => party.Expel(PoliticsFixtures.Deputy),
            MembershipStatus.Resigned => party.AcceptResignation(PoliticsFixtures.Deputy),
            _ => party.RecordDefection(PoliticsFixtures.Deputy),
        };

        result.IsSuccess.ShouldBeTrue();

        // The record stays, carrying how it ended: the party's history is not erased.
        party.Memberships.Single(m => m.Character == PoliticsFixtures.Deputy)
            .Status.ShouldBe(expected);
        party.ActiveMemberships.ShouldNotContain(m => m.Character == PoliticsFixtures.Deputy);
    }

    [Fact]
    public void Internal_support_is_not_the_same_thing_as_loyalty()
    {
        // A character's Loyalty is a personality trait; a membership's InternalSupport is
        // how much the party's own people back them. Conflating them loses both meanings.
        typeof(PartyMembership).GetProperty("Loyalty").ShouldBeNull();
        typeof(PartyMembership).GetProperty(nameof(PartyMembership.InternalSupport)).ShouldNotBeNull();
        typeof(Domain.Actors.Entities.CharacterState)
            .GetProperty(nameof(Domain.Actors.Entities.CharacterState.Loyalty)).ShouldNotBeNull();
    }

    [Fact]
    public void Membership_records_in_game_time()
    {
        typeof(PartyMembership).GetProperty(nameof(PartyMembership.Since))!
            .PropertyType.ShouldBe(typeof(GameDate));
    }

    [Fact]
    public void Cohesion_and_credibility_stay_in_range()
    {
        var party = PoliticsFixtures.Party(cohesion: 95m);

        party.AdjustCohesion(20m);

        party.Cohesion.Value.ShouldBe(100m);
    }

    [Fact]
    public void Losing_an_election_does_not_dissolve_a_party()
    {
        var party = PoliticsFixtures.Party();

        party.RecordElectionResult(seatsWon: 0);

        party.Status.ShouldBe(PartyStatus.Active, "only the survival policy can dissolve a party");
        party.ConsecutiveElectionsWithoutSeats.ShouldBe(1);
    }

    [Fact]
    public void Consecutive_deficits_are_tracked_for_the_survival_policy()
    {
        var party = PoliticsFixtures.Party(budget: -100m);

        party.RecordMonthEnd();
        party.RecordMonthEnd();

        party.ConsecutiveMonthsInDeficit.ShouldBe(2);

        party.Receive(1000m);
        party.RecordMonthEnd();

        party.ConsecutiveMonthsInDeficit.ShouldBe(0, "a month in the black resets the run");
    }

    [Fact]
    public void Provincial_presence_is_recorded_per_province()
    {
        var party = PoliticsFixtures.Party();
        var province = Domain.Shared.Identifiers.ProvinceId.From(Guid.NewGuid());

        party.SetPresence(new PartyProvincePresence(province, 70m, 60m, 50m));

        party.PresenceIn(province).OrganisationalStrength.ShouldBe(70m);
        party.PresenceIn(Domain.Shared.Identifiers.ProvinceId.From(Guid.NewGuid()))
            .OrganisationalStrength.ShouldBe(0m);
    }

    [Fact]
    public void Party_relations_are_directed_and_may_be_asymmetric()
    {
        var forward = PartyRelation.Neutral(PoliticsFixtures.PartyA, PoliticsFixtures.PartyB) with { Trust = 80m };
        var back = PartyRelation.Neutral(PoliticsFixtures.PartyB, PoliticsFixtures.PartyA);

        forward.Trust.ShouldNotBe(back.Trust);
    }

    [Fact]
    public void A_character_carries_no_party_id()
    {
        // Affiliation is derived from memberships, so the two cannot disagree.
        typeof(Domain.Actors.Entities.CharacterState).GetProperty("PartyId").ShouldBeNull();
    }
}
