namespace TheSpanishWay.Domain.Simulation.Enumerations;

/// <summary>
/// Whether a change came from something the government did or from something that happened
/// to it. Government attribution reads this tag: an incumbent is not automatically
/// responsible for every external shock. See design D17, decision 3.
/// </summary>
public enum ChangeOrigin
{
    /// <summary>A deliberate act by a political actor.</summary>
    Policy = 0,

    /// <summary>A shock from outside politics: a commodity price, a natural disaster, the world economy.</summary>
    External = 1,
}
