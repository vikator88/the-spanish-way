using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.ValueObjects;
using TheSpanishWay.Domain.Simulation.Enumerations;

using TheSpanishWay.Domain.Shared.Enumerations;

using TheSpanishWay.Domain.Shared.Services;

namespace TheSpanishWay.Domain.Simulation.ValueObjects;

/// <summary>
/// A temporary change, held apart from the value it changes.
/// <para>
/// This is the whole point of the modifier layer. Reverting a temporary effect by
/// subtracting it is wrong whenever a bound intervened: inflation at 9.5 with a ceiling of
/// 10 takes a temporary +2, clamps to +0.5, and subtracting 2 on expiry leaves 8.0 rather
/// than 9.5. Because a modifier is never folded into the base, expiry is simply removal,
/// and removal is exact. See design D5.
/// </para>
/// </summary>
public sealed record Modifier(
    ModifierId Id,
    AttributeRef Target,
    EffectOperation Operation,
    decimal Value,
    GameDate? ExpiresOn,
    StackKey StackKey,
    StackingPolicy Policy,
    Provenance Provenance,
    int Sequence)
{
    /// <summary>A modifier with no expiry, removed only when something takes it away.</summary>
    public bool IsIndefinite => ExpiresOn is null;

    /// <summary>
    /// Expiry is inclusive of its final day: a modifier expiring on the 10th is gone when
    /// the 11th is processed, having been in force for the whole of the 10th.
    /// </summary>
    public bool HasExpiredOn(GameDate date) => ExpiresOn is { } expiry && date > expiry;

    /// <summary>Restarts the clock without touching the value: the <c>RefreshDuration</c> policy.</summary>
    public Modifier RefreshedUntil(GameDate expiresOn) => this with { ExpiresOn = expiresOn };
}
