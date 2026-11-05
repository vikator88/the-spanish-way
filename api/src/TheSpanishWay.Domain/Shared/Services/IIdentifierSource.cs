using TheSpanishWay.Domain.Shared.Identifiers;

using TheSpanishWay.Domain.Shared.ValueObjects;

using TheSpanishWay.Domain.Shared.Enumerations;

namespace TheSpanishWay.Domain.Shared.Services;

/// <summary>
/// Mints identifiers for things created during a tick: modifiers, queued effects, event
/// occurrences.
/// <para>
/// An abstraction rather than <c>Guid.NewGuid()</c> because ids created mid-simulation must
/// be reproducible. A replay that mints different ids produces a different snapshot, and the
/// save-and-resume tests would then be comparing noise rather than behaviour.
/// </para>
/// </summary>
public interface IIdentifierSource
{
    ModifierId NextModifierId();

    PendingEffectId NextPendingEffectId();

    EventOccurrenceId NextEventOccurrenceId();
}
