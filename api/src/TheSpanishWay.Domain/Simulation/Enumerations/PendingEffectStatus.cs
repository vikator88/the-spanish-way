namespace TheSpanishWay.Domain.Simulation.Enumerations;

/// <summary>Where a queued effect is in its life.</summary>
public enum PendingEffectStatus
{
    /// <summary>Waiting for its apply date.</summary>
    Scheduled = 0,

    /// <summary>Applied and finished.</summary>
    Applied = 1,

    /// <summary>Recurring, with applications still to come.</summary>
    Recurring = 2,

    /// <summary>Removed before it applied, for example because its source was undone.</summary>
    Cancelled = 3,
}
