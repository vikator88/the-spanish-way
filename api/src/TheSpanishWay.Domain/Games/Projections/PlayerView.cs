using TheSpanishWay.Domain.Electorate.ValueObjects;
using TheSpanishWay.Domain.Games.Enumerations;
using TheSpanishWay.Domain.Shared.Enumerations;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.Services;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Domain.Games.Projections;

/// <summary>One indicator as the player sees it, which may not be as it is.</summary>
public sealed record VisibleIndicator(
    string Key,
    decimal? Value,
    IndicatorVisibility Visibility,
    GameDate? AsOf)
{
    public static VisibleIndicator Hidden(string key) => new(key, null, IndicatorVisibility.Hidden, null);
}

/// <summary>
/// Everything the player is allowed to know.
/// <para>
/// Anything sent to a browser can be read by whoever is holding it, so the client receives
/// this and never <c>GameState</c>. Polls rather than hidden preferences; indicators at
/// their configured visibility; no perception records, no utilities, no provenance beyond
/// what the player is meant to see.
/// </para>
/// </summary>
public sealed record PlayerView(
    GameId GameId,
    GameDate CurrentDate,
    GamePhase Phase,
    GameStatus Status,
    PartyId PlayerParty,
    IReadOnlyList<VisibleIndicator> Indicators,
    IReadOnlyList<Poll> RecentPolls,
    IReadOnlyList<string> NewsHeadlines,
    IReadOnlyList<EventOccurrenceId> OpenDecisions,
    IReadOnlyList<ActionDefinitionId> AvailableActions);

/// <summary>
/// Builds the player's picture of the world, applying visibility rules.
/// <para>
/// Noise is derived from the seed and the date rather than drawn, so asking twice on the
/// same day gives the same answer. A player who could reroll an estimate by refreshing
/// would be able to average the noise away and recover the true value.
/// </para>
/// </summary>
public static class PlayerViewProjection
{
    public static VisibleIndicator Project(
        string key,
        decimal trueValue,
        IndicatorVisibility visibility,
        GameDate today,
        RandomSeed seed,
        decimal noiseScale = 0.05m)
    {
        return visibility switch
        {
            IndicatorVisibility.Exact => new VisibleIndicator(key, trueValue, visibility, today),

            IndicatorVisibility.Noisy => new VisibleIndicator(
                key, ApplyReproducibleNoise(key, trueValue, today, seed, noiseScale), visibility, today),

            IndicatorVisibility.Delayed => new VisibleIndicator(
                key, trueValue, visibility, today.AddDays(-30)),

            _ => VisibleIndicator.Hidden(key),
        };
    }

    /// <summary>
    /// Noise that depends only on the indicator, the day and the seed — so it is stable
    /// within a day and cannot be averaged away by asking repeatedly.
    /// </summary>
    private static decimal ApplyReproducibleNoise(
        string key,
        decimal trueValue,
        GameDate today,
        RandomSeed seed,
        decimal noiseScale)
    {
        var keyedSeed = RandomSeed.From(seed.Value ^ RandomSeed.FromText(key).Value);
        var draw = CounterBasedRandom.Draw(keyedSeed, RandomStream.Polls, today.DayNumber, 0);
        var offset = (CounterBasedRandom.ToUnitInterval(draw) - 0.5m) * 2m * noiseScale;

        return Precision.ToStored(trueValue * (1m + offset));
    }
}
