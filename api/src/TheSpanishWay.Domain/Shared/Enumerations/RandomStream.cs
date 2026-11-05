namespace TheSpanishWay.Domain.Shared.Enumerations;

/// <summary>
/// One independent random stream per subsystem.
/// <para>
/// With a single global sequence, adding one draw in the media model shifts every later
/// event roll, so any content tweak or bug fix changes the whole game and replay tests
/// break for unrelated reasons. Separate streams make each subsystem's draws immune to
/// the others. See domain/building-blocks, "Deterministic random source".
/// </para>
/// </summary>
public enum RandomStream
{
    /// <summary>Which event definitions fire.</summary>
    Events = 0,

    /// <summary>Which party or character fills a declared role when an event fires.</summary>
    Bindings = 1,

    /// <summary>Outlet pick-up, framing and tone.</summary>
    Media = 2,

    /// <summary>Per-segment reaction noise.</summary>
    Reactions = 3,

    /// <summary>Choices made by the government and rival parties.</summary>
    Actors = 4,

    /// <summary>Polling sampling noise.</summary>
    Polls = 5,

    /// <summary>Drawing lots for a tied D'Hondt seat.</summary>
    Ties = 6,
}
