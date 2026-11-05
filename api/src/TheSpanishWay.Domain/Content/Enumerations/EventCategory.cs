namespace TheSpanishWay.Domain.Content.Enumerations;

/// <summary>
/// Why an event can happen. Expressed as data rather than as subclasses, because the
/// difference is in the trigger rules, not in behaviour.
/// </summary>
public enum EventCategory
{
    /// <summary>Can fire on its own, subject to its probability.</summary>
    Random = 0,

    /// <summary>Fires only when its trigger conditions hold.</summary>
    Conditional = 1,

    /// <summary>Only reachable as the follow-up of another event.</summary>
    Chained = 2,
}

/// <summary>Where an occurrence is in its life.</summary>
public enum EventOccurrenceStatus
{
    /// <summary>Live, and awaiting resolution.</summary>
    Active = 0,

    /// <summary>The player chose an option.</summary>
    Resolved = 1,

    /// <summary>Its expiry passed with its default option taken.</summary>
    AutoResolved = 2,

    /// <summary>Its expiry passed with nothing to apply.</summary>
    Expired = 3,
}
