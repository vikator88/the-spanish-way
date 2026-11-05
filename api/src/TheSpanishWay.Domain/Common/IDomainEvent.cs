using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Domain.Common;

/// <summary>
/// A fact that has already happened in the domain, named in the past tense.
/// <para>
/// The Domain only ever <em>records</em> these; it never dispatches them and knows
/// nothing about handlers, notifications or a mediator. Publication happens in the
/// Application layer after the transaction commits. See
/// application/domain-event-dispatch.
/// </para>
/// <para>
/// Not to be confused with a <em>simulation event</em> (a corruption scandal, a natural
/// disaster), which is authored game content modelled by <c>GameEventDefinition</c>.
/// </para>
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// The in-game date the fact occurred. Never a wall-clock time: the simulation
    /// runs on the game calendar, and a replay must not depend on when it was run.
    /// </summary>
    GameDate OccurredOn { get; }
}
