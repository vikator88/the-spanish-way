namespace TheSpanishWay.Domain.Common;

/// <summary>
/// The entry point to a consistency boundary. Everything inside an aggregate is loaded,
/// changed and saved together under one revision.
/// <para>
/// This game has exactly one mutable aggregate root, <c>GameSession</c>, because a daily
/// tick touches parties, characters, perceptions and world variables at once. See
/// domain/game-session.
/// </para>
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : struct, IEquatable<TId>
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot(TId id)
        : base(id)
    {
    }

    /// <summary>
    /// Facts recorded since this aggregate was loaded, in the order they happened.
    /// The Application layer collects and clears these after a successful commit.
    /// </summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>Discards collected events. Called once they have been handed off for publication.</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();

    /// <summary>
    /// Records that something happened. This does not notify anyone: see the type remarks.
    /// </summary>
    protected void RecordEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }
}
