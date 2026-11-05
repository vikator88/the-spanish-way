using Shouldly;
using TheSpanishWay.Domain.Electorate.Services;
using TheSpanishWay.Domain.Electorate.ValueObjects;
using TheSpanishWay.Domain.Shared.Enumerations;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Domain.UnitTests.Electorate;

public sealed class AffinityTests
{
    private static AxisWeights WeightedOn(IdeologicalAxis axis) =>
        AxisWeights.Create(new Dictionary<IdeologicalAxis, decimal> { [axis] = 1m }).Value;

    [Fact]
    public void Identical_profiles_give_perfect_affinity()
    {
        var profile = IdeologicalProfile.From(40m, -20m, 60m, 0m, -30m, 10m);

        IdeologicalAffinityCalculator.Between(profile, profile, AxisWeights.Uniform).ShouldBe(100m);
    }

    [Fact]
    public void Maximally_opposed_profiles_give_zero_affinity()
    {
        var left = IdeologicalProfile.Uniform(-100m);
        var right = IdeologicalProfile.Uniform(100m);

        IdeologicalAffinityCalculator.Between(left, right, AxisWeights.Uniform).ShouldBe(0m);
    }

    [Fact]
    public void Half_the_range_apart_gives_half_affinity()
    {
        // 100 apart on every axis, out of a possible 200, so affinity is 100 − 100/2 = 50.
        var a = IdeologicalProfile.Uniform(-50m);
        var b = IdeologicalProfile.Uniform(50m);

        IdeologicalAffinityCalculator.Between(a, b, AxisWeights.Uniform).ShouldBe(50m);
    }

    [Fact]
    public void Weights_change_which_disagreements_matter()
    {
        // The two profiles agree on the economy and disagree completely on territory.
        var segment = IdeologicalProfile.From(0m, 0m, -100m, 0m, 0m, 0m);
        var party = IdeologicalProfile.From(0m, 0m, 100m, 0m, 0m, 0m);

        var caresAboutTerritory = IdeologicalAffinityCalculator.Between(
            segment, party, WeightedOn(IdeologicalAxis.Territory));

        var caresAboutEconomy = IdeologicalAffinityCalculator.Between(
            segment, party, WeightedOn(IdeologicalAxis.Economy));

        caresAboutTerritory.ShouldBe(0m);
        caresAboutEconomy.ShouldBe(100m, "the one axis it weighs is the one they agree on");
    }

    [Fact]
    public void Affinity_is_measured_against_the_perceived_position_not_the_declared_one()
    {
        var segment = IdeologicalProfile.Uniform(0m);
        var party = IdeologicalProfile.Uniform(50m);

        var asDeclared = IdeologicalAffinityCalculator.Perceived(
            segment, party, AxisWeights.Uniform, SegmentPartyPerception.Neutral);

        // The same party, seen as 40% more extreme than it presents itself.
        var asSuspected = IdeologicalAffinityCalculator.Perceived(
            segment, party, AxisWeights.Uniform,
            SegmentPartyPerception.Create(50m, 50m, 0.4m).Value);

        asSuspected.ShouldBeLessThan(asDeclared);
    }

    [Fact]
    public void A_segment_can_see_a_party_as_more_moderate_than_it_is()
    {
        var segment = IdeologicalProfile.Uniform(0m);
        var party = IdeologicalProfile.Uniform(50m);

        var softened = IdeologicalAffinityCalculator.Perceived(
            segment, party, AxisWeights.Uniform,
            SegmentPartyPerception.Create(50m, 50m, -0.5m).Value);

        softened.ShouldBeGreaterThan(
            IdeologicalAffinityCalculator.Between(segment, party, AxisWeights.Uniform));
    }

    [Fact]
    public void A_province_can_shift_a_segment_without_moving_it_nationally()
    {
        // This is what makes regionalist politics possible: the same segment, more
        // decentralising in one province than in the country at large.
        var national = IdeologicalProfile.From(0m, 0m, 0m, 0m, 0m, 0m);

        var offset = ProvincialIdeologyOffset.Territorial(
            SegmentId.From(Guid.NewGuid()), ProvinceId.From(Guid.NewGuid()), 80m);

        var inProvince = national.ShiftedBy(offset.Offsets);

        inProvince.On(IdeologicalAxis.Territory).ShouldBe(80m);
        inProvince.On(IdeologicalAxis.Economy).ShouldBe(0m);
        national.On(IdeologicalAxis.Territory).ShouldBe(0m, "the national profile is untouched");
    }

    [Fact]
    public void A_regionalist_party_scores_far_better_where_the_offset_favours_it()
    {
        var regionalist = IdeologicalProfile.From(0m, 0m, 90m, 0m, 0m, 0m);
        var segment = IdeologicalProfile.From(0m, 0m, 0m, 0m, 0m, 0m);
        var weights = WeightedOn(IdeologicalAxis.Territory);

        var elsewhere = IdeologicalAffinityCalculator.Between(segment, regionalist, weights);

        var inRegion = IdeologicalAffinityCalculator.Between(
            segment.ShiftedBy(new Dictionary<IdeologicalAxis, decimal>
            {
                [IdeologicalAxis.Territory] = 80m,
            }),
            regionalist,
            weights);

        inRegion.ShouldBeGreaterThan(elsewhere + 30m);
    }

    [Fact]
    public void A_perception_carries_no_affinity_of_its_own()
    {
        // Storing affinity here as well as computing it would feed ideology into utility
        // twice and silently double its weight.
        typeof(SegmentPartyPerception).GetProperty("Affinity").ShouldBeNull();
    }

    [Fact]
    public void A_profile_rejects_a_position_outside_the_axis()
    {
        IdeologicalProfile.Create(new Dictionary<IdeologicalAxis, decimal>
        {
            [IdeologicalAxis.Economy] = 150m,
            [IdeologicalAxis.SocialValues] = 0m,
            [IdeologicalAxis.Territory] = 0m,
            [IdeologicalAxis.Immigration] = 0m,
            [IdeologicalAxis.Institutions] = 0m,
            [IdeologicalAxis.Security] = 0m,
        }).Error.Code.ShouldBe("ideology.position_out_of_range");
    }

    [Fact]
    public void There_is_one_ideology_type_shared_by_segments_and_parties()
    {
        // A separate PartyIdeology of identical shape would only create an opportunity for
        // the two to drift apart, and they must stay directly comparable.
        typeof(IdeologicalProfile).Assembly.GetTypes()
            .Select(t => t.Name)
            .ShouldNotContain("PartyIdeology");
    }
}
