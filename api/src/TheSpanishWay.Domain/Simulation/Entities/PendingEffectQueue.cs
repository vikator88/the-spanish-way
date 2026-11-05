using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.ValueObjects;
using TheSpanishWay.Domain.Simulation.Enumerations;
using TheSpanishWay.Domain.Simulation.ValueObjects;

namespace TheSpanishWay.Domain.Simulation.Entities;

/// <summary>
/// Effects waiting for their date: delayed, gradual and recurring.
/// <para>
/// Ordered by apply date, then by the order they were queued, so two effects falling due on
/// the same day always land in the same sequence. That ordering is not cosmetic — with
/// clamping and multiplication in play, the order changes the result.
/// </para>
/// </summary>
public sealed class PendingEffectQueue
{
    private readonly List<PendingEffect> _effects;
    private readonly Dictionary<PendingEffectId, int> _sequence;
    private int _nextSequence;

    private PendingEffectQueue(IEnumerable<PendingEffect> effects)
    {
        _effects = [.. effects];
        _sequence = [];
        _nextSequence = 0;

        foreach (var effect in _effects)
        {
            _sequence[effect.Id] = _nextSequence++;
        }
    }

    public static PendingEffectQueue Empty() => new([]);

    public static PendingEffectQueue Restore(IEnumerable<PendingEffect> effects) => new(effects);

    public int Count => _effects.Count;

    public IReadOnlyList<PendingEffect> All() => [.. Ordered(_effects)];

    public void Enqueue(PendingEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);

        _sequence[effect.Id] = _nextSequence++;
        _effects.Add(effect);
    }

    public IReadOnlyList<PendingEffect> DueOn(GameDate date) =>
        [.. Ordered(_effects.Where(e => e.IsDueOn(date)))];

    /// <summary>
    /// Records that an effect has applied: a recurring one is rescheduled, a one-shot is
    /// dropped. The queue holds only what is still to come.
    /// </summary>
    public void MarkApplied(PendingEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);

        _effects.Remove(effect);

        var next = effect.Next();

        if (next is not null)
        {
            Enqueue(next);
        }
    }

    public bool Cancel(PendingEffect effect) => _effects.Remove(effect);

    private IEnumerable<PendingEffect> Ordered(IEnumerable<PendingEffect> effects) =>
        effects
            .OrderBy(e => e.ApplyOn)
            .ThenBy(e => _sequence.TryGetValue(e.Id, out var seq) ? seq : int.MaxValue)
            .ThenBy(e => e.Id);
}
