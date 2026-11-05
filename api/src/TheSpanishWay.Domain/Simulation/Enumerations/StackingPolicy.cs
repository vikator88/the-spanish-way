namespace TheSpanishWay.Domain.Simulation.Enumerations;

/// <summary>
/// What happens when an effect lands on a target that already carries one with the same
/// stack key. Explicit per effect, because "a second scandal while the first is still
/// running" has no obvious default.
/// </summary>
public enum StackingPolicy
{
    /// <summary>Both apply. Use where repetition genuinely compounds.</summary>
    Stack = 0,

    /// <summary>
    /// The existing modifier's clock restarts and its value is left alone. The default:
    /// most repeated stimuli prolong an impression rather than deepen it.
    /// </summary>
    RefreshDuration = 1,

    /// <summary>The existing modifier is removed and the new one takes its place.</summary>
    Replace = 2,

    /// <summary>The new modifier is dropped and the existing one continues untouched.</summary>
    IgnoreDuplicate = 3,
}
