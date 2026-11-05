using TheSpanishWay.Domain.Common;

using TheSpanishWay.Domain.Shared.ValueObjects;

using TheSpanishWay.Domain.Shared.Enumerations;

using TheSpanishWay.Domain.Shared.Services;

namespace TheSpanishWay.Domain.Simulation.ValueObjects;

/// <summary>
/// What applying a batch of effects did. Either every change landed or none did — the
/// engine validates the whole set before writing anything, so an action that fails halfway
/// through cannot leave the world half-changed.
/// </summary>
public sealed record EffectApplicationResult(
    IReadOnlyList<AttributeRef> BaseMutations,
    IReadOnlyList<Modifier> ModifiersAdded,
    IReadOnlyList<PendingEffect> EffectsQueued,
    IReadOnlyList<ClampDiagnostic> Diagnostics)
{
    public static EffectApplicationResult Empty { get; } = new([], [], [], []);

    /// <summary>True when bounds refused part of something. Not a failure.</summary>
    public bool HadTruncation => Diagnostics.Count > 0;

    public int ChangeCount => BaseMutations.Count + ModifiersAdded.Count + EffectsQueued.Count;
}

/// <summary>Why a change set was rejected, naming the effect that caused it.</summary>
public sealed record EffectRejection(int EffectIndex, AttributeRef Target, Error Error)
{
    public override string ToString() => $"Effect #{EffectIndex} on {Target}: {Error}";
}
