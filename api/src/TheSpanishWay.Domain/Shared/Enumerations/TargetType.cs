using TheSpanishWay.Domain.Shared.ValueObjects;

using TheSpanishWay.Domain.Shared.Services;

namespace TheSpanishWay.Domain.Shared.Enumerations;

/// <summary>
/// What kind of thing an effect or condition is addressing.
/// <para>
/// The split matters: the first group is <em>authorable</em>, the second is
/// <em>computed</em>. Content may never write a perception, preference, turnout figure or
/// seat count, because doing so would let an author place votes by hand and decide who
/// reacts. Those are reachable only through stimuli. See design D4.
/// </para>
/// </summary>
public enum TargetType
{
    WorldVariable = 0,
    Party = 1,
    Character = 2,
    PartyRelation = 3,
    CharacterRelation = 4,
    AgendaTopic = 5,
    PartyResource = 6,
    ProvincePresence = 7,

    /// <summary>Computed. Writable only by the reaction pipeline.</summary>
    SegmentPerception = 100,

    /// <summary>Computed. Derived from utility and inertia.</summary>
    ElectoralPreference = 101,

    /// <summary>Computed. Decided by the turnout model.</summary>
    Turnout = 102,

    /// <summary>Computed. An election outcome, never an input.</summary>
    Seats = 103,
}

/// <summary>Tells the authorable target types from the computed ones.</summary>
public static class TargetTypeExtensions
{
    /// <summary>
    /// True for targets content must not write. The numeric split is deliberate so a new
    /// computed target cannot be added without choosing a value above the boundary.
    /// </summary>
    public static bool IsComputed(this TargetType target) => (int)target >= 100;

    public static bool IsAuthorable(this TargetType target) => !target.IsComputed();
}
