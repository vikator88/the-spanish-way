using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Games.Enumerations;
using TheSpanishWay.Domain.Games.ValueObjects;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.Services;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Domain.Games.Entities;

/// <summary>
/// All mutable simulation state, owned by <c>GameSession</c> and persisted as one snapshot.
/// <para>
/// This is where the game date and phase live, and the only place. The session reads them
/// through rather than keeping copies, so no two fields can disagree. See
/// domain/game-state, "The game date has exactly one owner".
/// </para>
/// <para>
/// A state component, not an aggregate: it has no identity of its own and is never loaded
/// or saved apart from its session. It is also free of any serialisation concern — the
/// snapshot it exports is a plain domain record, and turning that into JSONB is
/// Infrastructure's problem.
/// </para>
/// </summary>
public sealed class GameState
{
    private readonly HashSet<EventOccurrenceId> _openBlockingDecisions;

    private GameState(
        GameDate startDate,
        GameDate currentDate,
        GamePhase phase,
        SchemaVersion schemaVersion,
        RandomStreamIndices randomStreams,
        IEnumerable<EventOccurrenceId> openBlockingDecisions)
    {
        StartDate = startDate;
        CurrentDate = currentDate;
        Phase = phase;
        SchemaVersion = schemaVersion;
        RandomStreams = randomStreams;
        _openBlockingDecisions = [.. openBlockingDecisions];
    }

    /// <summary>
    /// The scenario's opening date, kept because weekly cadence counts from it rather than
    /// from an ISO Monday.
    /// </summary>
    public GameDate StartDate { get; }

    /// <summary>The single source of truth for "when is it" in this game.</summary>
    public GameDate CurrentDate { get; private set; }

    public GamePhase Phase { get; private set; }

    public SchemaVersion SchemaVersion { get; }

    public RandomStreamIndices RandomStreams { get; }

    /// <summary>
    /// Blocking occurrences still awaiting the player. Populated once events exist; the
    /// status machine already consults it, because resuming a paused game has to know
    /// whether it is resuming into a decision.
    /// </summary>
    public IReadOnlyCollection<EventOccurrenceId> OpenBlockingDecisions =>
        _openBlockingDecisions.OrderBy(id => id).ToList();

    public bool HasOpenBlockingDecisions => _openBlockingDecisions.Count > 0;

    // Remaining components arrive with their slices, each owning its own invariants:
    // WorldState and provinces (slice 3), party and character states (slices 3 and 6),
    // ElectorateState (slice 5), MediaState (slice 7), Parliament and GovernmentState
    // (slice 8), and the modifier, pending-effect and active-event queues (slices 3 and 4).

    public static GameState Begin(GameDate startDate) =>
        new(
            startDate,
            startDate,
            GamePhase.Normal,
            SchemaVersion.Current,
            RandomStreamIndices.Empty(),
            []);

    /// <summary>
    /// Moves the calendar on by one day. Time never runs backwards, so this is the only way
    /// the date changes and it only ever goes forward.
    /// </summary>
    public Result AdvanceOneDay()
    {
        CurrentDate = CurrentDate.AddDays(1);
        RandomStreams.ResetForNewDay();

        return Result.Success();
    }

    /// <summary>Rejected rather than silently ignored: a backwards date is a bug, not a no-op.</summary>
    public Result MoveTo(GameDate date)
    {
        if (date < CurrentDate)
        {
            return Result.Failure(
                new Error(
                    "game_state.date_moved_backwards",
                    $"The game date cannot move backwards, from {CurrentDate} to {date}."));
        }

        CurrentDate = date;

        return Result.Success();
    }

    public void ChangePhase(GamePhase phase) => Phase = phase;

    public void RaiseBlockingDecision(EventOccurrenceId occurrence) =>
        _openBlockingDecisions.Add(occurrence);

    public bool ResolveBlockingDecision(EventOccurrenceId occurrence) =>
        _openBlockingDecisions.Remove(occurrence);

    /// <summary>True on every seventh day counted from the scenario's start date.</summary>
    public bool IsWeeklyBoundary() => CurrentDate.IsWeeklyBoundary(StartDate);

    public bool IsMonthlyBoundary() => CurrentDate.IsMonthlyBoundary();

    public bool IsQuarterlyBoundary() => CurrentDate.IsQuarterlyBoundary();

    public GameStateSnapshot ToSnapshot() =>
        new(
            SchemaVersion,
            StartDate,
            CurrentDate,
            Phase,
            RandomStreams.ToSnapshot(),
            [.. _openBlockingDecisions.OrderBy(id => id)]);

    public static GameState FromSnapshot(GameStateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new GameState(
            snapshot.StartDate,
            snapshot.CurrentDate,
            snapshot.Phase,
            snapshot.SchemaVersion,
            RandomStreamIndices.Restore(snapshot.RandomStreamIndices),
            snapshot.OpenBlockingDecisions);
    }
}
