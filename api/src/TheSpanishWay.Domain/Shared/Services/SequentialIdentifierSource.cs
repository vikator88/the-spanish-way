using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.ValueObjects;

using TheSpanishWay.Domain.Shared.Enumerations;

namespace TheSpanishWay.Domain.Shared.Services;

/// <summary>
/// Mints identifiers deterministically from the game's seed and a counter.
/// <para>
/// Not <c>Guid.NewGuid()</c>, which is entropy-backed, and not <c>CreateVersion7</c>, which
/// is clock-backed. Either would give a replay different ids from the original run, and the
/// save-and-resume tests compare snapshots — so ids are part of the determinism contract,
/// not an incidental detail.
/// </para>
/// </summary>
public sealed class SequentialIdentifierSource(RandomSeed seed, int startingCounter = 0) : IIdentifierSource
{
    private readonly RandomSeed _seed = seed;
    private int _counter = startingCounter;

    /// <summary>How many identifiers have been minted; persisted so a resume continues the sequence.</summary>
    public int Counter => _counter;

    public ModifierId NextModifierId() => ModifierId.From(Next(ModifierNamespace));

    public PendingEffectId NextPendingEffectId() => PendingEffectId.From(Next(PendingEffectNamespace));

    public EventOccurrenceId NextEventOccurrenceId() =>
        EventOccurrenceId.From(Next(EventOccurrenceNamespace));

    private const int ModifierNamespace = 1;
    private const int PendingEffectNamespace = 2;
    private const int EventOccurrenceNamespace = 3;

    private Guid Next(int kind)
    {
        // The seed, the kind and the counter fully determine the value, so the same run
        // always mints the same identifiers in the same order.
        var counter = _counter++;

        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes[..8], _seed.Value);
        BitConverter.TryWriteBytes(bytes[8..12], kind);
        BitConverter.TryWriteBytes(bytes[12..], counter);

        return new Guid(bytes);
    }
}
