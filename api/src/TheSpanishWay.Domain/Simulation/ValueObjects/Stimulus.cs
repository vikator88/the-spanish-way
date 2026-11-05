using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Simulation.Enumerations;

namespace TheSpanishWay.Domain.Simulation.ValueObjects;

/// <summary>
/// Something politically meaningful that happened, before anyone has reacted to it.
/// <para>
/// This is the stage that keeps the game's central promise. v0.1 lets an effect write "trust
/// among retirees −10", which means the author is placing votes by hand <em>and</em> deciding
/// who reacts — defeating heterogeneous reactions entirely. Here content says only what
/// happened: who did it, about what, how strongly, and how publicly. Who minds, and how much,
/// is computed. See design D4.
/// </para>
/// </summary>
public sealed record Stimulus(
    PartyId? Actor,
    CharacterId? Figure,
    IssueId Issue,
    StimulusKind Kind,
    decimal? Stance,
    decimal Intensity,
    Visibility Visibility)
{
    public static Result<Stimulus> Positional(
        PartyId actor,
        IssueId issue,
        decimal stance,
        decimal intensity,
        Visibility visibility = Visibility.Public,
        CharacterId? figure = null)
    {
        if (stance is < -100m or > 100m)
        {
            return Result.Failure<Stimulus>(
                new Error("stimulus.stance_out_of_range", $"Stance {stance} is outside −100 to +100."));
        }

        return Validated(new Stimulus(actor, figure, issue, StimulusKind.Positional, stance, intensity, visibility));
    }

    public static Result<Stimulus> Valence(
        PartyId actor,
        IssueId issue,
        decimal intensity,
        Visibility visibility = Visibility.Public,
        CharacterId? figure = null) =>
        Validated(new Stimulus(actor, figure, issue, StimulusKind.Valence, null, intensity, visibility));

    private static Result<Stimulus> Validated(Stimulus stimulus)
    {
        if (stimulus.Intensity is < 0m or > 1m)
        {
            return Result.Failure<Stimulus>(
                new Error("stimulus.intensity_out_of_range", $"Intensity {stimulus.Intensity} is not a fraction."));
        }

        // A valence stimulus with a stance would be claiming there are two sides to
        // corruption, which is not how valence issues work.
        if (stimulus.Kind == StimulusKind.Valence && stimulus.Stance is not null)
        {
            return Result.Failure<Stimulus>(
                new Error("stimulus.valence_with_stance", "A valence stimulus cannot carry a stance."));
        }

        if (stimulus.Kind == StimulusKind.Positional && stimulus.Stance is null)
        {
            return Result.Failure<Stimulus>(
                new Error("stimulus.positional_without_stance", "A positional stimulus needs a stance."));
        }

        return Result.Success(stimulus);
    }
}

/// <summary>
/// The authored form of a stimulus, with its actor still a role.
/// <para>
/// The whole authoring surface is six fields. "The party took a hard pro-pension stance,
/// intensity 0.7" is the entire instruction, and six segments then react by six different
/// amounts without an author writing six numbers.
/// </para>
/// </summary>
public sealed record StimulusTemplate(
    string ActorRole,
    IssueId Issue,
    StimulusKind Kind,
    decimal? Stance,
    decimal Intensity,
    Visibility Visibility)
{
    public static StimulusTemplate Positional(
        string actorRole, IssueId issue, decimal stance, decimal intensity,
        Visibility visibility = Visibility.Public) =>
        new(actorRole, issue, StimulusKind.Positional, stance, intensity, visibility);

    public static StimulusTemplate Valence(
        string actorRole, IssueId issue, decimal intensity,
        Visibility visibility = Visibility.Public) =>
        new(actorRole, issue, StimulusKind.Valence, null, intensity, visibility);
}
