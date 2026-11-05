using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Domain.Content.Entities;

/// <summary>
/// When each definition last fired, and which chained definitions have been unlocked.
/// <para>
/// State, not history: cooldowns are consulted on every tick, and the snapshot alone must be
/// enough to resume. History records exist separately and are never read back to compute the
/// next state.
/// </para>
/// </summary>
public sealed class EventLedger
{
    private readonly Dictionary<EventDefinitionId, GameDate> _lastFired;
    private readonly HashSet<EventDefinitionId> _unlockedFollowUps;

    private EventLedger(
        Dictionary<EventDefinitionId, GameDate> lastFired,
        HashSet<EventDefinitionId> unlockedFollowUps)
    {
        _lastFired = lastFired;
        _unlockedFollowUps = unlockedFollowUps;
    }

    public static EventLedger Empty() => new([], []);

    public static EventLedger Restore(
        IReadOnlyDictionary<EventDefinitionId, GameDate> lastFired,
        IReadOnlyCollection<EventDefinitionId> unlockedFollowUps) =>
        new(new Dictionary<EventDefinitionId, GameDate>(lastFired), [.. unlockedFollowUps]);

    public GameDate? LastFired(EventDefinitionId definition) =>
        _lastFired.TryGetValue(definition, out var date) ? date : null;

    /// <summary>
    /// True when enough days have passed since this definition last fired. A definition that
    /// has never fired is always off cooldown.
    /// </summary>
    public bool IsOffCooldown(EventDefinitionId definition, int? cooldownDays, GameDate today)
    {
        if (cooldownDays is not { } cooldown || LastFired(definition) is not { } last)
        {
            return true;
        }

        return today.DaysSince(last) >= cooldown;
    }

    public void RecordFired(EventDefinitionId definition, GameDate on) => _lastFired[definition] = on;

    /// <summary>
    /// A chained definition becomes eligible only once something has unlocked it. Follow-ups
    /// are made <em>possible</em> by their parent, not made to happen.
    /// </summary>
    public void UnlockFollowUp(EventDefinitionId definition) => _unlockedFollowUps.Add(definition);

    public bool IsFollowUpUnlocked(EventDefinitionId definition) =>
        _unlockedFollowUps.Contains(definition);

    public IReadOnlyDictionary<EventDefinitionId, GameDate> LastFiredSnapshot() =>
        _lastFired.OrderBy(p => p.Key).ToDictionary(p => p.Key, p => p.Value);

    public IReadOnlyList<EventDefinitionId> UnlockedFollowUps() =>
        [.. _unlockedFollowUps.OrderBy(id => id)];
}
