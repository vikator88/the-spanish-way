using Shouldly;
using TheSpanishWay.Domain.Electorate.Services;
using TheSpanishWay.Domain.Electorate.ValueObjects;
using TheSpanishWay.Domain.Shared.Identifiers;

namespace TheSpanishWay.Domain.UnitTests.Electorate;

/// <summary>
/// The two calibration tests the design says to write before any content exists.
/// <para>
/// They pin the behaviour of the utility and softmax pair, so that when balancing starts
/// there is a known reference point. Without them, "the numbers feel wrong" has nothing to
/// be measured against and every tuning argument is unfalsifiable.
/// </para>
/// </summary>
public sealed class CalibrationTests
{
    private static readonly PartyId PartyA = PartyId.From(Guid.Parse("0000000a-0000-0000-0000-000000000001"));
    private static readonly PartyId PartyB = PartyId.From(Guid.Parse("0000000a-0000-0000-0000-000000000002"));

    private const decimal DefaultTemperature = 0.20m;

    [Fact]
    public void Identical_parties_receive_identical_shares()
    {
        // Calibration test one. If this ever fails, something in the pipeline is treating
        // two indistinguishable parties differently — an ordering bias, most likely.
        var utility = PoliticalUtilityCalculator.Compute(UtilityInputs.Neutral, UtilityWeights.Default);

        var probabilities = ChoiceProbabilityCalculator.Compute(
            [
                ChoiceOption.For(PartyA, utility),
                ChoiceOption.For(PartyB, utility),
                ChoiceOption.Blank(-0.5m),
            ],
            DefaultTemperature).Value;

        probabilities.For(PartyA).ShouldBe(probabilities.For(PartyB));
    }

    [Fact]
    public void A_utility_advantage_of_one_tenth_produces_a_documented_share_gain()
    {
        // Calibration test two. At T = 0.2, a 0.1 utility advantage is exp(0.5) ≈ 1.6487
        // times the rival's weight, so between two parties alone the leader takes
        // 1.6487 / 2.6487 ≈ 62.2%. This number is the reference the balance work starts from;
        // changing the temperature or the centring changes it, and that should be deliberate.
        var behind = 0.0m;
        var ahead = 0.1m;

        var probabilities = ChoiceProbabilityCalculator.Compute(
            [ChoiceOption.For(PartyA, ahead), ChoiceOption.For(PartyB, behind)],
            DefaultTemperature).Value;

        probabilities.For(PartyA).ShouldBe(0.6225m, tolerance: 0.001m);
        probabilities.For(PartyB).ShouldBe(0.3775m, tolerance: 0.001m);
    }

    [Fact]
    public void The_default_temperature_sits_in_the_interpretable_band()
    {
        // Centring components onto −1..+1 is what makes this band meaningful at all.
        SegmentBehaviour.Default.ChoiceTemperature.ShouldBeInRange(0.1m, 0.3m);
    }

    [Fact]
    public void An_indifferent_segment_produces_zero_utility()
    {
        PoliticalUtilityCalculator.Compute(UtilityInputs.Neutral, UtilityWeights.Default).ShouldBe(0m);
    }

    [Fact]
    public void Utility_spans_minus_one_to_plus_one()
    {
        var best = PoliticalUtilityCalculator.Compute(
            new UtilityInputs(100m, 100m, 100m, 100m, 100m, 100m, 100m), UtilityWeights.Default);

        var worst = PoliticalUtilityCalculator.Compute(
            new UtilityInputs(0m, 0m, 0m, 0m, 0m, 0m, 0m), UtilityWeights.Default);

        best.ShouldBe(1m);
        worst.ShouldBe(-1m);
    }

    [Fact]
    public void Lower_temperature_concentrates_the_vote()
    {
        var options = new[] { ChoiceOption.For(PartyA, 0.1m), ChoiceOption.For(PartyB, 0m) };

        var decisive = ChoiceProbabilityCalculator.Compute(options, 0.1m).Value;
        var indifferent = ChoiceProbabilityCalculator.Compute(options, 0.5m).Value;

        decisive.For(PartyA).ShouldBeGreaterThan(indifferent.For(PartyA));
    }
}
