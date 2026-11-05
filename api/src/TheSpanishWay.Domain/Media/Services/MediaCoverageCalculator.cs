using TheSpanishWay.Domain.Media.Entities;
using TheSpanishWay.Domain.Media.ValueObjects;
using TheSpanishWay.Domain.Shared.Enumerations;
using TheSpanishWay.Domain.Shared.Services;
using TheSpanishWay.Domain.Shared.ValueObjects;
using TheSpanishWay.Domain.Simulation.Enumerations;
using TheSpanishWay.Domain.Simulation.ValueObjects;

namespace TheSpanishWay.Domain.Media.Services;

/// <summary>
/// Whether an outlet runs a story, and how.
/// <para>
/// Each outlet decides independently. Pick-up is newsworthiness × issue interest ×
/// sensationalism; tone comes from editorial alignment with the actor, the outlet's recent
/// run on that party, and noise. Orientation biases tone without dictating it — a hostile
/// paper still runs the occasional fair piece.
/// </para>
/// </summary>
public static class MediaCoverageCalculator
{
    /// <summary>
    /// Evaluates one outlet against one stimulus. Draws from the <c>media</c> stream, so the
    /// same day replays to the same front pages.
    /// </summary>
    public static MediaCoverage Evaluate(
        MediaOutlet outlet,
        Stimulus stimulus,
        decimal agendaRelevance,
        GameDate today,
        IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(outlet);
        ArgumentNullException.ThrowIfNull(stimulus);
        ArgumentNullException.ThrowIfNull(random);

        var newsworthiness = Newsworthiness(stimulus, agendaRelevance);
        var interest = outlet.InterestIn(stimulus.Issue);
        var sensationalism = outlet.Sensationalism.Value / 100m;

        var pickUpChance = newsworthiness * interest * (0.5m + sensationalism);

        if (!random.Chance(RandomStream.Media, pickUpChance))
        {
            return new MediaCoverage(
                outlet.Id, stimulus.Actor, stimulus.Issue,
                CoverageLevel.NotCovered, MediaTone.Neutral, 0m, today);
        }

        var level = LevelFor(newsworthiness, random);
        var tone = ToneFor(outlet, stimulus, random);

        if (stimulus.Actor is { } actor)
        {
            outlet.RememberFraming(actor, ToneBias(tone));
        }

        // Reach is the outlet's audience scaled by how prominently it ran the story.
        var reach = Precision.ToStored(
            (double)(outlet.Audience.Value / 100m) * (double)Prominence(level));

        return new MediaCoverage(outlet.Id, stimulus.Actor, stimulus.Issue, level, tone, reach, today);
    }

    /// <summary>
    /// How interesting a thing is before any particular outlet considers it. Visibility
    /// dominates: most internal party business never leaves the building.
    /// </summary>
    private static decimal Newsworthiness(Stimulus stimulus, decimal agendaRelevance)
    {
        var visibility = stimulus.Visibility switch
        {
            Visibility.Public => 1m,
            Visibility.Parliamentary => 0.6m,
            Visibility.Internal => 0.15m,
            _ => 0.5m,
        };

        // Agenda salience counts, but a quiet issue can still break through if something
        // dramatic enough happens on it.
        return Math.Clamp(stimulus.Intensity * visibility * (0.4m + (0.6m * agendaRelevance)), 0m, 1m);
    }

    private static CoverageLevel LevelFor(decimal newsworthiness, IRandomSource random)
    {
        var roll = random.NextUnitInterval(RandomStream.Media);
        var score = (newsworthiness * 0.7m) + (roll * 0.3m);

        return score switch
        {
            >= 0.8m => CoverageLevel.Saturation,
            >= 0.6m => CoverageLevel.Prominent,
            >= 0.35m => CoverageLevel.Standard,
            _ => CoverageLevel.Passing,
        };
    }

    private static MediaTone ToneFor(MediaOutlet outlet, Stimulus stimulus, IRandomSource random)
    {
        var alignment = stimulus.Actor is { } actor
            ? (outlet.AffinityToward(actor) - 50m) / 50m
            : 0m;

        var memory = stimulus.Actor is { } remembered
            ? outlet.RecentFramingBiasToward(remembered)
            : 0m;

        // A valence stimulus is bad news for whoever it lands on, whatever the outlet
        // thinks of them: nobody writes a flattering corruption story.
        var inherent = stimulus.Kind == StimulusKind.Valence ? -0.6m * stimulus.Intensity : 0m;

        var noise = (random.NextUnitInterval(RandomStream.Media) - 0.5m) * 0.6m;

        // Independence damps the outlet's own leanings without removing them.
        var leaning = (alignment * (1m - (outlet.Independence.Value / 200m)))
            + (memory * 0.3m) + inherent + noise;

        return leaning switch
        {
            >= 0.45m => MediaTone.Supportive,
            >= 0.15m => MediaTone.Favourable,
            > -0.15m => MediaTone.Neutral,
            > -0.45m => MediaTone.Critical,
            _ => MediaTone.Hostile,
        };
    }

    private static decimal ToneBias(MediaTone tone) => tone switch
    {
        MediaTone.Hostile => -1m,
        MediaTone.Critical => -0.5m,
        MediaTone.Neutral => 0m,
        MediaTone.Favourable => 0.5m,
        MediaTone.Supportive => 1m,
        _ => 0m,
    };

    private static decimal Prominence(CoverageLevel level) => level switch
    {
        CoverageLevel.NotCovered => 0m,
        CoverageLevel.Passing => 0.25m,
        CoverageLevel.Standard => 0.5m,
        CoverageLevel.Prominent => 0.75m,
        CoverageLevel.Saturation => 1m,
        _ => 0m,
    };
}
