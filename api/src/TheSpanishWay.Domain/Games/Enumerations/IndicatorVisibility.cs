namespace TheSpanishWay.Domain.Games.Enumerations;

/// <summary>
/// How much of an indicator the player is allowed to see.
/// <para>
/// Imperfect information is a design principle, not a UI choice: the player sees estimates
/// and selected indicators, never every internal value. Per-indicator and configured by
/// scenario.
/// </para>
/// </summary>
public enum IndicatorVisibility
{
    /// <summary>Published officially; the player sees the true value.</summary>
    Exact = 0,

    /// <summary>Estimated, with reproducible error.</summary>
    Noisy = 1,

    /// <summary>Known, but only as it stood some time ago.</summary>
    Delayed = 2,

    /// <summary>Not shown at all.</summary>
    Hidden = 3,
}
