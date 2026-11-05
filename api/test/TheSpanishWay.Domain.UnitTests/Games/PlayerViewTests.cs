using Shouldly;
using TheSpanishWay.Domain.Games.Enumerations;
using TheSpanishWay.Domain.Games.Projections;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Domain.UnitTests.Games;

/// <summary>
/// Imperfect information is a design principle, so what the player sees is a projection and
/// not the state.
/// </summary>
public sealed class PlayerViewTests
{
    private static readonly GameDate Today = GameDate.From(2027, 3, 1);
    private static readonly RandomSeed Seed = RandomSeed.FromText("view");

    [Fact]
    public void An_exact_indicator_shows_the_true_value()
    {
        PlayerViewProjection.Project("inflation", 3.2m, IndicatorVisibility.Exact, Today, Seed)
            .Value.ShouldBe(3.2m);
    }

    [Fact]
    public void A_noisy_indicator_does_not()
    {
        PlayerViewProjection.Project("inflation", 3.2m, IndicatorVisibility.Noisy, Today, Seed)
            .Value.ShouldNotBe(3.2m);
    }

    [Fact]
    public void Noise_is_stable_within_a_day_so_it_cannot_be_averaged_away()
    {
        // A player who could reroll an estimate by refreshing would recover the true value
        // by taking the mean.
        var first = PlayerViewProjection.Project("inflation", 3.2m, IndicatorVisibility.Noisy, Today, Seed);
        var second = PlayerViewProjection.Project("inflation", 3.2m, IndicatorVisibility.Noisy, Today, Seed);

        second.Value.ShouldBe(first.Value);
    }

    [Fact]
    public void Noise_moves_from_one_day_to_the_next()
    {
        var today = PlayerViewProjection.Project("inflation", 3.2m, IndicatorVisibility.Noisy, Today, Seed);
        var tomorrow = PlayerViewProjection.Project(
            "inflation", 3.2m, IndicatorVisibility.Noisy, Today.AddDays(1), Seed);

        tomorrow.Value.ShouldNotBe(today.Value);
    }

    [Fact]
    public void Different_indicators_get_independent_noise()
    {
        var inflation = PlayerViewProjection.Project(
            "inflation", 50m, IndicatorVisibility.Noisy, Today, Seed);
        var employment = PlayerViewProjection.Project(
            "employment", 50m, IndicatorVisibility.Noisy, Today, Seed);

        employment.Value.ShouldNotBe(inflation.Value);
    }

    [Fact]
    public void A_delayed_indicator_shows_an_older_reading()
    {
        var delayed = PlayerViewProjection.Project(
            "education", 58m, IndicatorVisibility.Delayed, Today, Seed);

        delayed.Value.ShouldBe(58m);
        delayed.AsOf.ShouldBe(Today.AddDays(-30));
    }

    [Fact]
    public void A_hidden_indicator_shows_nothing_at_all()
    {
        var hidden = PlayerViewProjection.Project(
            "true-preference", 0.42m, IndicatorVisibility.Hidden, Today, Seed);

        hidden.Value.ShouldBeNull();
    }

    [Fact]
    public void The_player_view_carries_no_hidden_state()
    {
        var properties = typeof(PlayerView).GetProperties().Select(p => p.PropertyType.Name).ToList();

        properties.ShouldNotContain("GameState");
        properties.ShouldNotContain("SegmentPartyPerception");
        properties.ShouldNotContain("UtilityInputs");
        properties.ShouldNotContain("ChoiceProbabilities");
    }

    [Fact]
    public void The_player_sees_polls_rather_than_the_hidden_distribution()
    {
        typeof(PlayerView).GetProperty(nameof(PlayerView.RecentPolls)).ShouldNotBeNull();
        typeof(PlayerView).GetProperty("Preferences").ShouldBeNull();
    }
}
