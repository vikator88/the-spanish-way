using Shouldly;
using TheSpanishWay.Domain.Electorate.Services;
using TheSpanishWay.Domain.Electorate.ValueObjects;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.ValueObjects;
using TheSpanishWay.TestKit;

namespace TheSpanishWay.Domain.UnitTests.Electorate;

public sealed class PollingTests
{
    private static readonly PartyId A = PoliticsFixtures.PartyA;
    private static readonly PartyId B = PoliticsFixtures.PartyB;
    private static readonly GameDate Today = PoliticsFixtures.Today;

    private static Pollster Pollster(decimal houseEffectForA = 0m) =>
        new(PollsterId.From(Guid.Parse("0000000e-0000-0000-0000-00000000000f")), "Metroscopia",
            new Dictionary<PartyId, decimal> { [A] = houseEffectForA });

    private static Dictionary<PartyId, decimal> Truth() =>
        new() { [A] = 0.40m, [B] = 0.60m };

    [Fact]
    public void A_poll_estimates_rather_than_reveals()
    {
        var poll = PollingService.Publish(Pollster(), Truth(), 1000, Today, new ScriptedRandom(0.9m));

        poll.ShareFor(A).ShouldNotBe(0.40m, "a poll is a sample, not the register");
    }

    [Fact]
    public void A_poll_is_reproducible_from_the_same_seed()
    {
        var first = PollingService.Publish(Pollster(), Truth(), 1000, Today, new ScriptedRandom(0.3m));
        var second = PollingService.Publish(Pollster(), Truth(), 1000, Today, new ScriptedRandom(0.3m));

        first.ShareFor(A).ShouldBe(second.ShareFor(A));
    }

    [Fact]
    public void A_house_effect_biases_a_pollster_consistently()
    {
        // The same pollster, two months running: the direction of its bias is the same
        // because it lives in state rather than being drawn fresh.
        var friendly = Pollster(houseEffectForA: 3m);

        var march = PollingService.Publish(friendly, Truth(), 2000, Today, new ScriptedRandom(0.5m));
        var april = PollingService.Publish(friendly, Truth(), 2000, Today.AddDays(30), new ScriptedRandom(0.5m));

        march.ShareFor(A).ShouldBeGreaterThan(0.40m);
        april.ShareFor(A).ShouldBeGreaterThan(0.40m);
    }

    [Fact]
    public void A_larger_sample_has_a_smaller_margin_of_error()
    {
        var small = PollingService.Publish(Pollster(), Truth(), 400, Today, new ScriptedRandom(0.5m));
        var large = PollingService.Publish(Pollster(), Truth(), 4000, Today, new ScriptedRandom(0.5m));

        large.MarginOfError.ShouldBeLessThan(small.MarginOfError);
    }

    [Fact]
    public void Estimates_sum_to_one()
    {
        var poll = PollingService.Publish(Pollster(), Truth(), 1000, Today, new ScriptedRandom(0.7m));

        poll.EstimatedShares.Values.Sum().ShouldBe(1m, tolerance: 0.000002m);
    }

    [Fact]
    public void A_poll_records_who_ran_it_and_how()
    {
        var poll = PollingService.Publish(Pollster(), Truth(), 1500, Today, new ScriptedRandom(0.5m));

        poll.Pollster.ShouldBe(Pollster().Id);
        poll.SampleSize.ShouldBe(1500);
        poll.PublishedOn.ShouldBe(Today);
    }

    [Fact]
    public void Hidden_preference_poll_and_result_are_three_separate_things()
    {
        // Nothing in the model lets one stand in for another.
        typeof(Poll).GetProperty("TrueShares").ShouldBeNull();
        typeof(ElectionRecord).GetProperty("PollEstimate").ShouldBeNull();
    }

    [Fact]
    public void There_is_no_undecided_category_in_the_hidden_state()
    {
        // "Undecided" is something a pollster reports, not something a voter is.
        typeof(ChoiceProbabilities).GetProperty("Undecided").ShouldBeNull();
    }
}
