using TheSpanishWay.Domain.Shared.ValueObjects;

using TheSpanishWay.Domain.Shared.Enumerations;

using TheSpanishWay.Domain.Shared.Services;

namespace TheSpanishWay.Domain.Simulation.ValueObjects;

/// <summary>
/// Reports that bounds refused part of a change.
/// <para>
/// Carried out of the engine rather than discarded, so the game can say "that effect was
/// truncated by 1.5 at the upper bound" instead of quietly losing it. Truncation is
/// information, not failure: the command still succeeds.
/// </para>
/// </summary>
public sealed record ClampDiagnostic(
    AttributeRef Target,
    decimal Attempted,
    decimal Applied,
    decimal Truncated,
    Provenance Provenance)
{
    public override string ToString() =>
        $"{Target}: attempted {Attempted}, applied {Applied}, truncated {Truncated} ({Provenance}).";
}
