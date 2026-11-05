using Shouldly;
using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Domain.UnitTests.Common;

public sealed class AggregateRootTests
{
    private sealed record ThingHappened(GameDate OccurredOn, string What) : IDomainEvent;

    private sealed class Session(GameId id) : AggregateRoot<GameId>(id)
    {
        public void Do(string what) => RecordEvent(new ThingHappened(GameDate.From(2027, 3, 1), what));
    }

    private static Session NewSession() => new(GameId.From(Guid.NewGuid()));

    [Fact]
    public void Recorded_events_appear_in_order()
    {
        var session = NewSession();

        session.Do("first");
        session.Do("second");

        session.DomainEvents
            .OfType<ThingHappened>()
            .Select(e => e.What)
            .ShouldBe(["first", "second"]);
    }

    [Fact]
    public void A_new_aggregate_has_recorded_nothing()
    {
        NewSession().DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Clearing_discards_recorded_events()
    {
        var session = NewSession();
        session.Do("first");

        session.ClearDomainEvents();

        session.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Exposed_events_cannot_be_mutated_by_callers()
    {
        var session = NewSession();
        session.Do("first");

        session.DomainEvents.ShouldBeAssignableTo<IReadOnlyList<IDomainEvent>>();
        (session.DomainEvents as ICollection<IDomainEvent>)?.IsReadOnly.ShouldBe(true);
    }

    [Fact]
    public void Recording_notifies_nobody()
    {
        // The Domain records facts; the Application publishes them after commit. If a
        // dispatcher ever appears in the Domain, this is the test that should fail.
        var dispatchTypes = typeof(AggregateRoot<GameId>).Assembly
            .GetTypes()
            .Where(t => t.Name.Contains("Dispatcher", StringComparison.Ordinal)
                     || t.Name.Contains("Publisher", StringComparison.Ordinal)
                     || t.Name.Contains("Mediator", StringComparison.Ordinal)
                     || t.Name.Contains("Notification", StringComparison.Ordinal))
            .Select(t => t.FullName)
            .ToList();

        dispatchTypes.ShouldBeEmpty();
    }

    [Fact]
    public void Domain_events_carry_game_time_not_wall_clock_time()
    {
        typeof(IDomainEvent)
            .GetProperty(nameof(IDomainEvent.OccurredOn))!
            .PropertyType
            .ShouldBe(typeof(GameDate));
    }
}
