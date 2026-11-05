using Shouldly;
using TheSpanishWay.Domain.Electorate.Services;
using TheSpanishWay.Domain.Electorate.ValueObjects;
using TheSpanishWay.Domain.Games.ValueObjects;
using TheSpanishWay.Domain.Shared.Enumerations;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.Services;
using TheSpanishWay.Domain.Shared.ValueObjects;
using TheSpanishWay.TestKit;

namespace TheSpanishWay.Simulation.Tests;

/// <summary>
/// Bands rather than values. A balance test that asserted an exact outcome would break on
/// every tuning change and tell nobody anything; what matters is that the distribution of
/// outcomes stays somewhere sane.
/// </summary>
public sealed record BalanceBands(
    decimal MinimumLeadingShare,
    decimal MaximumLeadingShare,
    decimal MinimumTurnout,
    decimal MaximumTurnout)
{
    /// <summary>Placeholders, to be argued about with sweep results rather than in advance.</summary>
    public static BalanceBands Default { get; } = new(0.25m, 0.75m, 0.45m, 0.95m);
}

/// <summary>
/// Runs the model across many seeds and checks the spread of outcomes.
/// <para>
/// Marked as a longer-running category so the default run stays fast; this is the suite that
/// answers "is the game balanced?", which no amount of rule-level testing can.
/// </para>
/// </summary>
[Trait("Category", "Balance")]
public sealed class BalanceSweepTests
{
    private const int Seeds = 200;

    private static readonly PartyId A = PoliticsFixtures.PartyA;
    private static readonly PartyId B = PoliticsFixtures.PartyB;

    [Fact]
    public void Two_hundred_seeds_keep_vote_shares_inside_their_bands()
    {
        var bands = BalanceBands.Default;
        var leadingShares = new List<decimal>();

        for (var seed = 0; seed < Seeds; seed++)
        {
            var random = new CounterBasedRandomSource(
                RandomSeed.From((ulong)seed), GameDate.From(2027, 3, 1), RandomStreamIndices.Empty());

            // A small random advantage either way, as a real contest would have.
            var swing = (random.NextUnitInterval(RandomStream.Polls) - 0.5m) * 0.4m;

            var probabilities = ChoiceProbabilityCalculator.Compute(
                [ChoiceOption.For(A, swing), ChoiceOption.For(B, -swing), ChoiceOption.Blank(-0.6m)],
                SegmentBehaviour.Default.ChoiceTemperature).Value;

            leadingShares.Add(Math.Max(probabilities.For(A), probabilities.For(B)));
        }

        leadingShares.Count.ShouldBe(Seeds);
        leadingShares.Average().ShouldBeInRange(bands.MinimumLeadingShare, bands.MaximumLeadingShare);
    }

    [Fact]
    public void Turnout_across_seeds_stays_plausible()
    {
        var bands = BalanceBands.Default;
        var turnouts = new List<decimal>();

        for (var seed = 0; seed < Seeds; seed++)
        {
            var random = new CounterBasedRandomSource(
                RandomSeed.From((ulong)seed), GameDate.From(2027, 3, 1), RandomStreamIndices.Empty());

            var enthusiasm = random.NextUnitInterval(RandomStream.Reactions) * 0.6m;
            var disengagement = random.NextUnitInterval(RandomStream.Reactions) * 0.6m;

            turnouts.Add(TurnoutCalculator.Compute(
                SegmentBehaviour.Default,
                new TurnoutInfluences(enthusiasm, disengagement, 0m)));
        }

        turnouts.Min().ShouldBeGreaterThan(bands.MinimumTurnout - 0.2m);
        turnouts.Max().ShouldBeLessThan(bands.MaximumTurnout);
        turnouts.Average().ShouldBeInRange(bands.MinimumTurnout, bands.MaximumTurnout);
    }

    [Fact]
    public void No_seed_produces_a_degenerate_result()
    {
        // A one-party state or a perfectly even split across 200 seeds would mean the model
        // is not responding to its inputs at all.
        for (var seed = 0; seed < Seeds; seed++)
        {
            var random = new CounterBasedRandomSource(
                RandomSeed.From((ulong)seed), GameDate.From(2027, 3, 1), RandomStreamIndices.Empty());

            var swing = (random.NextUnitInterval(RandomStream.Polls) - 0.5m) * 0.4m;

            var probabilities = ChoiceProbabilityCalculator.Compute(
                [ChoiceOption.For(A, swing), ChoiceOption.For(B, -swing), ChoiceOption.Blank(-0.6m)],
                SegmentBehaviour.Default.ChoiceTemperature).Value;

            probabilities.For(A).ShouldBeGreaterThan(0m);
            probabilities.For(B).ShouldBeGreaterThan(0m);
            probabilities.Total.ShouldBe(1m);
        }
    }

    [Fact]
    public void A_sweep_is_reproducible()
    {
        static decimal Run(int seed)
        {
            var random = new CounterBasedRandomSource(
                RandomSeed.From((ulong)seed), GameDate.From(2027, 3, 1), RandomStreamIndices.Empty());

            return random.NextUnitInterval(RandomStream.Polls);
        }

        Enumerable.Range(0, 50).Select(Run).ShouldBe(Enumerable.Range(0, 50).Select(Run));
    }

    [Fact]
    public void Bands_are_configuration_not_assertions()
    {
        // Adjusting what counts as balanced should be a data change, not a code change:
        // every band lives on a record rather than inside an assertion.
        var bands = BalanceBands.Default with { MaximumLeadingShare = 0.9m };

        bands.MaximumLeadingShare.ShouldBe(0.9m);
        BalanceBands.Default.MinimumLeadingShare.ShouldBeLessThan(BalanceBands.Default.MaximumLeadingShare);
        BalanceBands.Default.MinimumTurnout.ShouldBeLessThan(BalanceBands.Default.MaximumTurnout);
    }
}
