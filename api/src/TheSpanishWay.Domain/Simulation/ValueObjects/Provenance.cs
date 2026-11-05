using TheSpanishWay.Domain.Simulation.Enumerations;

namespace TheSpanishWay.Domain.Simulation.ValueObjects;

/// <summary>What caused a change.</summary>
public enum ChangeSource
{
    /// <summary>An action a party executed.</summary>
    ActionExecution = 0,

    /// <summary>A simulation event that fired.</summary>
    EventOccurrence = 1,

    /// <summary>Ordinary drift of a world variable toward its target.</summary>
    NaturalEvolution = 2,

    /// <summary>Set up when the game was created.</summary>
    ScenarioInitialisation = 3,
}

/// <summary>
/// Attached to every base mutation and every modifier, so any change can be traced back to
/// what caused it.
/// <para>
/// Two things depend on this. The player-facing explanation ("why did my support fall?")
/// reads it instead of replaying the game, and government attribution reads
/// <see cref="Origin"/> to decide how much of a change an incumbent should be blamed for.
/// </para>
/// </summary>
public sealed record Provenance(
    ChangeSource Source,
    string SourceId,
    int EffectIndex,
    ChangeOrigin Origin)
{
    /// <summary>Drift toward a target, which no political actor chose.</summary>
    public static Provenance NaturalEvolution(string variableKey) =>
        new(ChangeSource.NaturalEvolution, variableKey, 0, ChangeOrigin.External);

    public static Provenance Scenario(string scenarioId) =>
        new(ChangeSource.ScenarioInitialisation, scenarioId, 0, ChangeOrigin.Policy);

    public override string ToString() => $"{Source}({SourceId})#{EffectIndex}/{Origin}";
}
