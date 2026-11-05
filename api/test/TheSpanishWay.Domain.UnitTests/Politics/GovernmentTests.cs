using Shouldly;
using TheSpanishWay.Domain.Actions.Enumerations;
using TheSpanishWay.Domain.Actions.Services;
using TheSpanishWay.Domain.Politics.Entities;
using TheSpanishWay.Domain.Politics.Enumerations;
using TheSpanishWay.Domain.Politics.Services;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.ValueObjects;
using TheSpanishWay.TestKit;

namespace TheSpanishWay.Domain.UnitTests.Politics;

public sealed class GovernmentTests
{
    private static readonly PartyId A = PoliticsFixtures.PartyA;
    private static readonly PartyId B = PoliticsFixtures.PartyB;
    private static readonly PartyId C = PartyId.From(Guid.Parse("0000000a-0000-0000-0000-00000000000c"));

    private static readonly GameDate Today = PoliticsFixtures.Today;

    private static Parliament Chamber(int a, int b, int c = 0) =>
        Parliament.From(350, new Dictionary<PartyId, int> { [A] = a, [B] = b, [C] = c });

    [Fact]
    public void An_absolute_majority_is_176_of_350()
    {
        Parliament.Empty().AbsoluteMajority.ShouldBe(176);
    }

    [Fact]
    public void The_first_investiture_vote_needs_an_absolute_majority()
    {
        var chamber = Chamber(a: 170, b: 100, c: 80);

        GovernmentFormation.Vote(chamber, [A], [B, C], isFirstVote: true)
            .Invested.ShouldBeFalse("170 is short of 176");

        GovernmentFormation.Vote(chamber, [A, C], [B], isFirstVote: true)
            .Invested.ShouldBeTrue("250 clears it");
    }

    [Fact]
    public void The_second_vote_needs_only_more_for_than_against()
    {
        var chamber = Chamber(a: 170, b: 100, c: 80);

        // C abstains, so 170 for beats 100 against even though it is under 176.
        var outcome = GovernmentFormation.Vote(chamber, [A], [B], isFirstVote: false);

        outcome.Invested.ShouldBeTrue();
        outcome.VotesFor.ShouldBe(170);
        outcome.VotesAgainst.ShouldBe(100);
    }

    [Fact]
    public void A_failed_first_vote_calls_for_a_second()
    {
        var outcome = GovernmentFormation.Vote(Chamber(170, 180), [A], [B], isFirstVote: true);

        outcome.RequiresSecondVote.ShouldBeTrue();
    }

    [Fact]
    public void A_failed_formation_forces_a_repeat_election_after_two_months()
    {
        GovernmentFormation.MustRepeatElection(Today, Today.AddDays(59)).ShouldBeFalse();
        GovernmentFormation.MustRepeatElection(Today, Today.AddDays(60)).ShouldBeTrue();
    }

    [Fact]
    public void Coalition_membership_and_external_support_are_recorded_separately()
    {
        var government = GovernmentFormation.Form(
            A, coalition: [A, B], votedInFavour: [A, B, C], Today, [A, B, C]).Value;

        government.Coalition.ShouldBe([A, B], ignoreOrder: true);
        government.ParliamentarySupport.ShouldBe([C]);
        government.PositionOf(C).ShouldBe(GovernmentPosition.ParliamentarySupport);
        government.PositionOf(B).ShouldBe(GovernmentPosition.Coalition);
        government.PositionOf(A).ShouldBe(GovernmentPosition.Lead);
    }

    [Fact]
    public void A_party_cannot_both_govern_and_merely_support()
    {
        GovernmentState.Form(A, [A, B], [B], Today, [A, B])
            .Error.Code.ShouldBe("government.coalition_and_support_overlap");
    }

    [Fact]
    public void A_government_cannot_include_a_party_that_is_not_in_the_game()
    {
        GovernmentState.Form(A, [A], [], Today, [B]).Error.Code.ShouldBe("government.unknown_party");
    }

    [Fact]
    public void Opposition_is_a_valid_position_and_carries_no_penalty()
    {
        var government = GovernmentFormation.Form(A, [A], [A], Today, [A, B]).Value;

        government.PositionOf(B).ShouldBe(GovernmentPosition.Opposition);
    }

    [Fact]
    public void Government_parties_back_their_own_bill()
    {
        var voter = PoliticsFixtures.Party();

        ParliamentaryVotePolicy.DecideFor(
            voter, voter.DeclaredIdeology, GovernmentPosition.Coalition, trustInProposer: 0m)
            .ShouldBe(VoteChoice.For);
    }

    [Fact]
    public void An_opposition_party_votes_by_distance_and_trust()
    {
        var voter = PoliticsFixtures.Party();

        var aligned = ParliamentaryVotePolicy.DecideFor(
            voter, voter.DeclaredIdeology, GovernmentPosition.Opposition, trustInProposer: 80m);

        var opposed = ParliamentaryVotePolicy.DecideFor(
            voter, IdeologicalProfile.Uniform(100m), GovernmentPosition.Opposition, trustInProposer: 10m);

        aligned.ShouldBe(VoteChoice.For);
        opposed.ShouldBe(VoteChoice.Against);
    }

    [Fact]
    public void Trust_shifts_a_marginal_vote_without_overriding_ideology()
    {
        var voter = PoliticsFixtures.Party();
        var distant = IdeologicalProfile.Uniform(100m);

        // However much it trusts the proposer, a party does not vote for something it
        // fundamentally opposes.
        ParliamentaryVotePolicy.DecideFor(voter, distant, GovernmentPosition.Opposition, 100m)
            .ShouldNotBe(VoteChoice.For);
    }

    [Fact]
    public void Seats_come_only_from_an_election()
    {
        var parliament = Parliament.Empty();

        parliament.ReplaceWith(new Dictionary<PartyId, int> { [A] = 150, [B] = 200 }).IsSuccess.ShouldBeTrue();
        parliament.SeatsFor(A).ShouldBe(150);
        parliament.LargestParty.ShouldBe(B);
    }

    [Fact]
    public void A_chamber_cannot_hold_more_seats_than_it_has()
    {
        Parliament.Empty(350)
            .ReplaceWith(new Dictionary<PartyId, int> { [A] = 300, [B] = 300 })
            .Error.Code.ShouldBe("parliament.too_many_seats");
    }

    [Fact]
    public void External_changes_are_attributed_less_than_policy_ones()
    {
        // An incumbent is not fully blamed for an oil shock, but is not let off either.
        var policy = GovernmentPerformanceCalculator.Score(
            [("inflation", -10m, -1m, Domain.Simulation.Enumerations.ChangeOrigin.Policy)]);

        var external = GovernmentPerformanceCalculator.Score(
            [("inflation", -10m, -1m, Domain.Simulation.Enumerations.ChangeOrigin.External)]);

        policy.ShouldBeGreaterThan(external, "falling inflation it caused earns more credit");
        external.ShouldBeGreaterThan(50m, "but it still gets some");
    }

    [Fact]
    public void An_uneventful_term_is_judged_neither_well_nor_badly()
    {
        GovernmentPerformanceCalculator.Score([]).ShouldBe(50m);
    }
}
