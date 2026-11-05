using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Shared.Identifiers;

namespace TheSpanishWay.Domain.Games.ValueObjects;

/// <summary>
/// The shape of the serialised snapshot. Bumped when the stored JSON changes, and handled
/// on load by a chain of upcasters.
/// <para>
/// Deliberately not called <c>StateVersion</c>: v0.1 uses that name, where it sits beside
/// <see cref="Revision"/> and reads like a concurrency token. It is not one.
/// </para>
/// </summary>
public readonly record struct SchemaVersion(int Value) : IComparable<SchemaVersion>
{
    /// <summary>The shape this build writes.</summary>
    public static readonly SchemaVersion Current = new(1);

    public int CompareTo(SchemaVersion other) => Value.CompareTo(other.Value);

    public static bool operator <(SchemaVersion left, SchemaVersion right) => left.CompareTo(right) < 0;

    public static bool operator <=(SchemaVersion left, SchemaVersion right) => left.CompareTo(right) <= 0;

    public static bool operator >(SchemaVersion left, SchemaVersion right) => left.CompareTo(right) > 0;

    public static bool operator >=(SchemaVersion left, SchemaVersion right) => left.CompareTo(right) >= 0;

    public override string ToString() => $"v{Value}";
}

/// <summary>
/// The rules and formulas a game runs under. Replay equality is guaranteed only within one
/// simulation version: changing a formula moves every running game forward rather than
/// keeping the old rule set alive. See design D12.
/// </summary>
public readonly record struct SimulationVersion(int Value) : IComparable<SimulationVersion>
{
    public static readonly SimulationVersion Current = new(1);

    public int CompareTo(SimulationVersion other) => Value.CompareTo(other.Value);

    public static bool operator <(SimulationVersion left, SimulationVersion right) => left.CompareTo(right) < 0;

    public static bool operator <=(SimulationVersion left, SimulationVersion right) => left.CompareTo(right) <= 0;

    public static bool operator >(SimulationVersion left, SimulationVersion right) => left.CompareTo(right) > 0;

    public static bool operator >=(SimulationVersion left, SimulationVersion right) => left.CompareTo(right) >= 0;

    public override string ToString() => $"sim-v{Value}";
}

/// <summary>
/// The published scenario version a game is pinned to. A game references a *version*, never a
/// bare scenario id, so editing content can never reach a game already in progress.
/// </summary>
public readonly record struct ScenarioVersionRef(ScenarioId Scenario, int Version)
{
    public static Result<ScenarioVersionRef> Create(ScenarioId scenario, int version)
    {
        if (scenario == ScenarioId.Empty)
        {
            return Result.Failure<ScenarioVersionRef>(
                new Error("scenario_version.missing_scenario", "A scenario reference needs a scenario id."));
        }

        if (version < 1)
        {
            return Result.Failure<ScenarioVersionRef>(
                new Error(
                    "scenario_version.not_published",
                    $"Version {version} is not a published version number."));
        }

        return Result.Success(new ScenarioVersionRef(scenario, version));
    }

    public override string ToString() => $"{Scenario}@{Version}";
}

/// <summary>
/// The optimistic concurrency token. Increments exactly once per successful state-changing
/// command, however many simulated days that command covered.
/// </summary>
public readonly record struct Revision(int Value) : IComparable<Revision>
{
    /// <summary>A game that has never been written.</summary>
    public static readonly Revision Initial = new(0);

    public Revision Next() => new(Value + 1);

    public int CompareTo(Revision other) => Value.CompareTo(other.Value);

    public static bool operator <(Revision left, Revision right) => left.CompareTo(right) < 0;

    public static bool operator <=(Revision left, Revision right) => left.CompareTo(right) <= 0;

    public static bool operator >(Revision left, Revision right) => left.CompareTo(right) > 0;

    public static bool operator >=(Revision left, Revision right) => left.CompareTo(right) >= 0;

    public override string ToString() => $"r{Value}";
}
