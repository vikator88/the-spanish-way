using TheSpanishWay.Domain.Shared.ValueObjects;
using TheSpanishWay.Domain.Simulation.Enumerations;
using TheSpanishWay.Domain.Simulation.ValueObjects;

using TheSpanishWay.Domain.Shared.Enumerations;

using TheSpanishWay.Domain.Shared.Services;

namespace TheSpanishWay.Domain.Simulation.Entities;

/// <summary>
/// Every temporary change currently in force, indexed by what it changes.
/// <para>
/// Lives in <c>GameState</c> and is persisted with it: modifiers are state, not something
/// recomputed from history. Enumeration is always ordered, because the simulation's
/// determinism depends on never iterating in hash order.
/// </para>
/// </summary>
public sealed class ModifierStore
{
    private readonly List<Modifier> _modifiers;
    private int _nextSequence;

    private ModifierStore(IEnumerable<Modifier> modifiers)
    {
        _modifiers = [.. modifiers];
        _nextSequence = _modifiers.Count == 0 ? 0 : _modifiers.Max(m => m.Sequence) + 1;
    }

    public static ModifierStore Empty() => new([]);

    public static ModifierStore Restore(IEnumerable<Modifier> modifiers) => new(modifiers);

    public int Count => _modifiers.Count;

    /// <summary>The next sequence number, which fixes the order modifiers combine in.</summary>
    public int NextSequence() => _nextSequence++;

    /// <summary>All modifiers, in a stable order.</summary>
    public IReadOnlyList<Modifier> All() =>
        [.. _modifiers.OrderBy(m => m.Sequence).ThenBy(m => m.Id)];

    public IReadOnlyList<Modifier> For(AttributeRef target) =>
        [.. _modifiers.Where(m => m.Target == target).OrderBy(m => m.Sequence).ThenBy(m => m.Id)];

    /// <summary>
    /// Adds a modifier, honouring the stacking policy of whatever is already there under the
    /// same key. Returns the modifier now in force, or null when the policy dropped it.
    /// </summary>
    public Modifier? Add(Modifier modifier)
    {
        ArgumentNullException.ThrowIfNull(modifier);

        var existing = _modifiers
            .Where(m => m.StackKey == modifier.StackKey && m.Target == modifier.Target)
            .OrderBy(m => m.Sequence)
            .ToList();

        if (existing.Count == 0)
        {
            _modifiers.Add(modifier);

            return modifier;
        }

        switch (modifier.Policy)
        {
            case StackingPolicy.Stack:
                _modifiers.Add(modifier);

                return modifier;

            case StackingPolicy.Replace:
                _modifiers.RemoveAll(m => existing.Contains(m));
                _modifiers.Add(modifier);

                return modifier;

            case StackingPolicy.IgnoreDuplicate:
                return null;

            case StackingPolicy.RefreshDuration:
            default:
                // Prolong the impression rather than deepen it: the clock restarts and the
                // value is left exactly as it was.
                var current = existing[^1];

                // The incoming expiry wins when there is one; an indefinite refresh leaves
                // the existing clock alone rather than making the modifier permanent.
                var refreshed = current with { ExpiresOn = modifier.ExpiresOn ?? current.ExpiresOn };
                _modifiers[_modifiers.IndexOf(current)] = refreshed;

                return refreshed;
        }
    }

    public bool Remove(Modifier modifier) => _modifiers.Remove(modifier);

    /// <summary>
    /// Drops everything whose expiry has passed, returning what was removed.
    /// <para>
    /// Expiry runs before due effects are applied, so a <c>RefreshDuration</c> effect that
    /// reapplies on the very day it would expire survives rather than briefly vanishing.
    /// </para>
    /// </summary>
    public IReadOnlyList<Modifier> ExpireAsOf(GameDate date)
    {
        var expired = _modifiers.Where(m => m.HasExpiredOn(date)).OrderBy(m => m.Sequence).ToList();

        foreach (var modifier in expired)
        {
            _modifiers.Remove(modifier);
        }

        return expired;
    }
}
