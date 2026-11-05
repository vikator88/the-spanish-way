using Shouldly;
using TheSpanishWay.Domain.Electorate.Services;
using TheSpanishWay.Domain.Electorate.ValueObjects;
using TheSpanishWay.Domain.Shared.Identifiers;

namespace TheSpanishWay.Domain.UnitTests.Electorate;

public sealed class InertiaAndAggregationTests
{
    private static readonly PartyId A = PartyId.From(Guid.Parse("0000000a-0000-0000-0000-00000000000a"));
    private static readonly PartyId B = PartyId.From(Guid.Parse("0000000a-0000-0000-0000-00000000000b"));

    private static readonly SegmentId Young = SegmentId.From(Guid.Parse("0000000b-0000-0000-0000-000000000001"));
    private static readonly SegmentId Retired = SegmentId.From(Guid.Parse("0000000b-0000-0000-0000-000000000002"));

    private static readonly ProvinceId Madrid = ProvinceId.From(Guid.Parse("0000000d-0000-0000-0000-000000000001"));
    private static readonly ProvinceId Soria = ProvinceId.From(Guid.Parse("0000000d-0000-0000-0000-000000000002"));

    [Fact]
    public void Preferences_move_part_of_the_way_not_all_of_it()
    {
        var current = new Dictionary<PartyId, decimal> { [A] = 0.8m, [B] = 0.2m };
        var target = new Dictionary<PartyId, decimal> { [A] = 0.2m, [B] = 0.8m };

        var blended = PreferenceInertia.Blend(current, target, monthlyVolatility: 0.25m, elapsedDays: 30);

        blended[A].ShouldBeLessThan(0.8m);
        blended[A].ShouldBeGreaterThan(0.2m, "voters do not jump to a newly computed ideal");
    }

    [Fact]
    public void Volatility_is_expressed_per_thirty_days()
    {
        PreferenceInertia.LambdaFor(0.25m, elapsedDays: 30).ShouldBe(0.25m, tolerance: 0.000002m);
    }

    [Fact]
    public void Stepping_weekly_and_stepping_daily_reach_the_same_place()
    {
        // Review issue #12: with a fixed lambda per recalculation, changing the cadence
        // from weekly to daily would silently change the whole game.
        var current = new Dictionary<PartyId, decimal> { [A] = 0.8m, [B] = 0.2m };
        var target = new Dictionary<PartyId, decimal> { [A] = 0.2m, [B] = 0.8m };

        var weekly = current;

        for (var step = 0; step < 12; step++)
        {
            weekly = (Dictionary<PartyId, decimal>)PreferenceInertia
                .Blend(weekly, target, 0.25m, elapsedDays: 7);
        }

        var daily = current;

        for (var step = 0; step < 84; step++)
        {
            daily = (Dictionary<PartyId, decimal>)PreferenceInertia
                .Blend(daily, target, 0.25m, elapsedDays: 1);
        }

        daily[A].ShouldBe(weekly[A], tolerance: 0.002m);
    }

    [Fact]
    public void Blended_preferences_are_renormalised_to_a_distribution()
    {
        var blended = PreferenceInertia.Blend(
            new Dictionary<PartyId, decimal> { [A] = 0.8m, [B] = 0.2m },
            new Dictionary<PartyId, decimal> { [A] = 0.5m, [B] = 0.5m },
            0.3m, 30);

        blended.Values.Sum().ShouldBe(1m, tolerance: 0.000002m);
    }

    [Fact]
    public void A_loyal_segment_moves_more_slowly_than_a_volatile_one()
    {
        var current = new Dictionary<PartyId, decimal> { [A] = 1m, [B] = 0m };
        var target = new Dictionary<PartyId, decimal> { [A] = 0m, [B] = 1m };

        var loyal = PreferenceInertia.Blend(current, target, 0.05m, 30);
        var volatile_ = PreferenceInertia.Blend(current, target, 0.60m, 30);

        loyal[A].ShouldBeGreaterThan(volatile_[A]);
    }

    [Fact]
    public void No_elapsed_time_means_no_movement()
    {
        PreferenceInertia.LambdaFor(0.5m, elapsedDays: 0).ShouldBe(0m);
    }

    [Fact]
    public void Two_provinces_with_different_compositions_produce_different_results()
    {
        // The same national state, aggregated through different populations. This is why
        // nothing computes a national share and apportions it downward.
        var youngFavoursA = new ChoiceProbabilities(
            new Dictionary<PartyId, decimal> { [A] = 0.7m, [B] = 0.3m }, 0m);

        var retiredFavoursB = new ChoiceProbabilities(
            new Dictionary<PartyId, decimal> { [A] = 0.2m, [B] = 0.8m }, 0m);

        var youngProvince = ProvincialVoteCalculator.Compute(Madrid,
        [
            new SegmentVoteInputs(Young, 800_000L, 0.7m, youngFavoursA),
            new SegmentVoteInputs(Retired, 200_000L, 0.8m, retiredFavoursB),
        ]);

        var oldProvince = ProvincialVoteCalculator.Compute(Soria,
        [
            new SegmentVoteInputs(Young, 200_000L, 0.7m, youngFavoursA),
            new SegmentVoteInputs(Retired, 800_000L, 0.8m, retiredFavoursB),
        ]);

        youngProvince.ShareOf(A).ShouldBeGreaterThan(oldProvince.ShareOf(A));
    }

    [Fact]
    public void Votes_are_population_times_turnout_times_probability()
    {
        var result = ProvincialVoteCalculator.Compute(Madrid,
        [
            new SegmentVoteInputs(Young, 1_000_000L, 0.5m,
                new ChoiceProbabilities(new Dictionary<PartyId, decimal> { [A] = 0.6m, [B] = 0.4m }, 0m)),
        ]);

        // 1,000,000 × 0.5 × 0.6 = 300,000.
        result.VotesByParty[A].ShouldBe(300_000L);
        result.VotesByParty[B].ShouldBe(200_000L);
        result.Turnout.ShouldBe(0.5m);
    }

    [Fact]
    public void Blank_ballots_count_as_valid_votes()
    {
        var result = ProvincialVoteCalculator.Compute(Madrid,
        [
            new SegmentVoteInputs(Young, 1_000_000L, 1m,
                new ChoiceProbabilities(new Dictionary<PartyId, decimal> { [A] = 0.9m }, 0.1m)),
        ]);

        result.BlankVotes.ShouldBe(100_000L);
        result.ValidVotes.ShouldBe(1_000_000L);
    }

    [Fact]
    public void A_segment_that_does_not_turn_out_casts_nothing()
    {
        var result = ProvincialVoteCalculator.Compute(Madrid,
        [
            new SegmentVoteInputs(Young, 1_000_000L, 0m,
                new ChoiceProbabilities(new Dictionary<PartyId, decimal> { [A] = 1m }, 0m)),
        ]);

        result.ValidVotes.ShouldBe(0L);
        result.Electorate.ShouldBe(1_000_000L, "they are still electors, they just stayed home");
    }

    [Fact]
    public void National_shares_are_aggregated_upward_from_constituencies()
    {
        var first = ProvincialVoteCalculator.Compute(Madrid,
        [
            new SegmentVoteInputs(Young, 1_000_000L, 1m,
                new ChoiceProbabilities(new Dictionary<PartyId, decimal> { [A] = 0.6m, [B] = 0.4m }, 0m)),
        ]);

        var second = ProvincialVoteCalculator.Compute(Soria,
        [
            new SegmentVoteInputs(Retired, 1_000_000L, 1m,
                new ChoiceProbabilities(new Dictionary<PartyId, decimal> { [A] = 0.2m, [B] = 0.8m }, 0m)),
        ]);

        var national = ProvincialVoteCalculator.NationalShares([first, second]);

        national[A].ShouldBe(0.4m, tolerance: 0.000002m);
        national[B].ShouldBe(0.6m, tolerance: 0.000002m);
    }

    [Fact]
    public void Provincial_presence_changes_utility_rather_than_adding_votes()
    {
        // Review of v0.1: territorial presence must modify utility and mobilisation, never
        // award a fixed vote quantity.
        var without = PoliticalUtilityCalculator.Compute(
            UtilityInputs.Neutral, UtilityWeights.Default);

        var with = PoliticalUtilityCalculator.Compute(
            UtilityInputs.Neutral with { ProvincialPresence = 100m }, UtilityWeights.Default);

        with.ShouldBeGreaterThan(without);

        // And it reaches votes only by passing through the softmax.
        typeof(ProvincialVoteCalculator).GetMethods()
            .SelectMany(m => m.GetParameters())
            .Select(p => p.ParameterType.Name)
            .ShouldNotContain(nameof(UtilityInputs));
    }
}
