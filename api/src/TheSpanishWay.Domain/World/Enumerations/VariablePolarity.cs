namespace TheSpanishWay.Domain.World.Enumerations;

/// <summary>
/// What a high value of an indicator means. Declared rather than inferred, because no rule
/// should have to know that high employment is good while high inflation is not — and
/// because "good" differs by indicator in ways a key name cannot carry.
/// </summary>
public enum VariablePolarity
{
    /// <summary>Higher is better: employment, education, welfare.</summary>
    HigherIsBetter = 0,

    /// <summary>Higher is worse: inflation, inequality, public debt.</summary>
    HigherIsWorse = 1,

    /// <summary>Neither, on its own. Judged only in context.</summary>
    Neutral = 2,
}
