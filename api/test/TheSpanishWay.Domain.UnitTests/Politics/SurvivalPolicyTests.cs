using Shouldly;
using TheSpanishWay.Domain.Games.Services;
using TheSpanishWay.Domain.Politics.Enumerations;
using TheSpanishWay.Domain.Politics.Services;
using TheSpanishWay.TestKit;

namespace TheSpanishWay.Domain.UnitTests.Politics;

public sealed class SurvivalPolicyTests
{
    [Fact]
    public void A_healthy_party_survives()
    {
        PartySurvivalPolicy.Evaluate(PoliticsFixtures.Party()).Survives.ShouldBeTrue();
    }

    [Fact]
    public void Six_months_of_deficit_dissolves_a_party()
    {
        var party = PoliticsFixtures.Party(budget: -5000m);

        for (var month = 0; month < 6; month++)
        {
            party.RecordMonthEnd();
        }

        var verdict = PartySurvivalPolicy.Apply(party);

        verdict.Survives.ShouldBeFalse();
        verdict.Reason.ShouldBe(DissolutionReason.Bankruptcy);
        party.Status.ShouldBe(PartyStatus.Dissolved);
    }

    [Fact]
    public void Five_months_of_deficit_does_not()
    {
        var party = PoliticsFixtures.Party(budget: -5000m);

        for (var month = 0; month < 5; month++)
        {
            party.RecordMonthEnd();
        }

        PartySurvivalPolicy.Evaluate(party).Survives.ShouldBeTrue();
    }

    [Fact]
    public void Collapsing_cohesion_dissolves_a_party()
    {
        var party = PoliticsFixtures.Party(cohesion: 10m);

        PartySurvivalPolicy.Evaluate(party).Reason.ShouldBe(DissolutionReason.Fragmentation);
    }

    [Fact]
    public void Two_seatless_elections_dissolve_a_party()
    {
        var party = PoliticsFixtures.Party();

        party.RecordElectionResult(0);
        PartySurvivalPolicy.Evaluate(party).Survives.ShouldBeTrue("one bad night is survivable");

        party.RecordElectionResult(0);
        PartySurvivalPolicy.Evaluate(party).Reason.ShouldBe(DissolutionReason.ElectoralIrrelevance);
    }

    [Fact]
    public void Winning_a_seat_resets_the_count()
    {
        var party = PoliticsFixtures.Party();

        party.RecordElectionResult(0);
        party.RecordElectionResult(5);
        party.RecordElectionResult(0);

        PartySurvivalPolicy.Evaluate(party).Survives.ShouldBeTrue();
    }

    [Fact]
    public void A_party_with_nobody_left_is_no_longer_a_party()
    {
        var party = PoliticsFixtures.Party();
        party.Expel(PoliticsFixtures.Leader);
        party.Expel(PoliticsFixtures.Deputy);

        PartySurvivalPolicy.Evaluate(party).Reason.ShouldBe(DissolutionReason.Leaderless);
    }

    [Fact]
    public void Losing_an_election_alone_does_not_end_the_game()
    {
        var party = PoliticsFixtures.Party();
        party.RecordElectionResult(0);

        GameEndPolicy.Evaluate(party).IsOver.ShouldBeFalse();
    }

    [Fact]
    public void The_player_party_dissolving_ends_the_game_with_a_reason()
    {
        var party = PoliticsFixtures.Party(cohesion: 5m);

        var verdict = GameEndPolicy.Evaluate(party);

        verdict.IsOver.ShouldBeTrue();
        verdict.Reason.ShouldBe("The party tore itself apart.");
    }

    [Fact]
    public void Thresholds_are_configuration()
    {
        var party = PoliticsFixtures.Party(cohesion: 20m);

        PartySurvivalPolicy.Evaluate(party).Survives.ShouldBeTrue();
        PartySurvivalPolicy.Evaluate(party, new SurvivalThresholds(6, 25m, 2)).Survives.ShouldBeFalse();
    }
}
