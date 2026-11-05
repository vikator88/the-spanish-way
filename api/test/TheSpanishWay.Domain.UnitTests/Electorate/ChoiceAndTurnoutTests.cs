using Shouldly;
using TheSpanishWay.Domain.Electorate.Services;
using TheSpanishWay.Domain.Electorate.ValueObjects;
using TheSpanishWay.Domain.Shared.Identifiers;

namespace TheSpanishWay.Domain.UnitTests.Electorate;

public sealed class ChoiceAndTurnoutTests
{
    private static readonly PartyId A = PartyId.From(Guid.Parse("0000000a-0000-0000-0000-00000000000a"));
    private static readonly PartyId B = PartyId.From(Guid.Parse("0000000a-0000-0000-0000-00000000000b"));

    [Fact]
    public void Probabilities_sum_to_one()
    {
        var probabilities = ChoiceProbabilityCalculator.Compute(
            [ChoiceOption.For(A, 0.4m), ChoiceOption.For(B, -0.2m), ChoiceOption.Blank(-0.6m)],
            0.2m).Value;

        probabilities.Total.ShouldBe(1m);
    }

    [Fact]
    public void The_softmax_survives_utilities_that_would_overflow_a_naive_exponent()
    {
        // Subtracting the maximum before exponentiating is what keeps this finite. Without
        // it, exp(1000 / 0.2) is infinity and every probability becomes NaN.
        var result = ChoiceProbabilityCalculator.Compute(
            [ChoiceOption.For(A, 1000m), ChoiceOption.For(B, 999m)], 0.2m);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Total.ShouldBe(1m);
        result.Value.For(A).ShouldBeGreaterThan(result.Value.For(B));
    }

    [Fact]
    public void Shifting_every_utility_by_a_constant_changes_nothing()
    {
        // Softmax depends only on differences, which the stable form makes explicit.
        var baseline = ChoiceProbabilityCalculator.Compute(
            [ChoiceOption.For(A, 0.3m), ChoiceOption.For(B, 0.1m)], 0.2m).Value;

        var shifted = ChoiceProbabilityCalculator.Compute(
            [ChoiceOption.For(A, 5.3m), ChoiceOption.For(B, 5.1m)], 0.2m).Value;

        shifted.For(A).ShouldBe(baseline.For(A), tolerance: 0.000002m);
    }

    [Fact]
    public void A_party_that_does_not_stand_here_receives_nothing()
    {
        var probabilities = ChoiceProbabilityCalculator.Compute(
            [ChoiceOption.For(A, 0.4m), ChoiceOption.Blank(-0.5m)], 0.2m).Value;

        probabilities.For(B).ShouldBe(0m);
        probabilities.ByParty.Keys.ShouldNotContain(B);
    }

    [Fact]
    public void Blank_is_an_option_but_abstention_is_not()
    {
        // Turnout decides whether a segment votes; the softmax decides whom it votes for.
        // v0.1 puts abstention in both places, counting it twice.
        var probabilities = ChoiceProbabilityCalculator.Compute(
            [ChoiceOption.For(A, 0.4m), ChoiceOption.Blank(0.4m)], 0.2m).Value;

        probabilities.Blank.ShouldBe(0.5m, tolerance: 0.000002m);
        typeof(ChoiceProbabilities).GetProperty("Abstention").ShouldBeNull();
    }

    [Fact]
    public void An_empty_ballot_is_rejected()
    {
        ChoiceProbabilityCalculator.Compute([], 0.2m).Error.Code.ShouldBe("choice.no_options");
    }

    [Fact]
    public void A_zero_temperature_is_rejected()
    {
        ChoiceProbabilityCalculator.Compute([ChoiceOption.For(A, 0m)], 0m)
            .Error.Code.ShouldBe("choice.invalid_temperature");
    }

    [Fact]
    public void Turnout_stays_between_zero_and_one()
    {
        var wildlyEnthusiastic = TurnoutCalculator.Compute(
            SegmentBehaviour.Default, new TurnoutInfluences(50m, 0m, 50m));

        var utterlyDisengaged = TurnoutCalculator.Compute(
            SegmentBehaviour.Default, new TurnoutInfluences(0m, 50m, 0m));

        wildlyEnthusiastic.ShouldBeLessThanOrEqualTo(1m);
        utterlyDisengaged.ShouldBeGreaterThanOrEqualTo(0m);
    }

    [Fact]
    public void Enthusiasm_raises_turnout_and_disengagement_lowers_it()
    {
        var baseline = TurnoutCalculator.Compute(SegmentBehaviour.Default, TurnoutInfluences.None);

        TurnoutCalculator.Compute(SegmentBehaviour.Default, new TurnoutInfluences(0.5m, 0m, 0m))
            .ShouldBeGreaterThan(baseline);

        TurnoutCalculator.Compute(SegmentBehaviour.Default, new TurnoutInfluences(0m, 0.5m, 0m))
            .ShouldBeLessThan(baseline);
    }

    [Fact]
    public void A_campaign_mobilises()
    {
        var quiet = TurnoutCalculator.Compute(SegmentBehaviour.Default, TurnoutInfluences.None);
        var campaigning = TurnoutCalculator.Compute(
            SegmentBehaviour.Default, new TurnoutInfluences(0m, 0m, 0.4m));

        campaigning.ShouldBeGreaterThan(quiet);
    }

    [Fact]
    public void Segments_with_different_baselines_turn_out_differently()
    {
        var keen = SegmentBehaviour.Default with { BaseTurnout = 0.85m };
        var reluctant = SegmentBehaviour.Default with { BaseTurnout = 0.45m };

        TurnoutCalculator.Compute(keen, TurnoutInfluences.None)
            .ShouldBeGreaterThan(TurnoutCalculator.Compute(reluctant, TurnoutInfluences.None));
    }

    [Fact]
    public void Turnout_is_computed_without_reference_to_any_party()
    {
        // The two models never meet, which is the whole point of separating them.
        typeof(TurnoutCalculator).GetMethods()
            .SelectMany(m => m.GetParameters())
            .Select(p => p.ParameterType.Name)
            .ShouldNotContain(nameof(ChoiceProbabilities));
    }
}
