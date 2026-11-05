using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Games.Entities;
using TheSpanishWay.Domain.Games.Enumerations;
using TheSpanishWay.Domain.Games.Events;
using TheSpanishWay.Domain.Games.ValueObjects;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Domain.Games.Aggregates;

/// <summary>
/// The one mutable aggregate root of a running game.
/// <para>
/// A daily tick touches parties, characters, perceptions and world variables together under
/// a single revision, which makes the whole simulated world one consistency boundary. Giving
/// <c>PoliticalParty</c> its own repository would force a cross-aggregate transaction on
/// every tick — precisely what aggregates exist to prevent. See design D2.
/// </para>
/// <para>
/// This class stays thin on purpose. It validates status and revision, delegates to state
/// components and domain services, and records what happened. The simulation's rules live in
/// those components and services, not here: the answer to "one giant Game class" is internal
/// modularity, not more aggregates.
/// </para>
/// </summary>
public sealed class GameSession : AggregateRoot<GameId>
{
    private readonly GameState _state;

    private GameSession(
        GameId id,
        OwnerId owner,
        string name,
        PartyId playerParty,
        ScenarioVersionRef scenario,
        SimulationVersion simulationVersion,
        RandomSeed seed,
        GameState state)
        : base(id)
    {
        Owner = owner;
        Name = name;
        PlayerPartyId = playerParty;
        Scenario = scenario;
        SimulationVersion = simulationVersion;
        Seed = seed;
        Status = GameStatus.Created;
        Revision = Revision.Initial;
        _state = state;
    }

    public OwnerId Owner { get; }

    public string Name { get; }

    /// <summary>
    /// The one party the player controls. Fixed for the life of the game: there is no
    /// operation that reassigns it.
    /// </summary>
    public PartyId PlayerPartyId { get; }

    /// <summary>A pinned published version, so editing a scenario cannot reach this game.</summary>
    public ScenarioVersionRef Scenario { get; }

    public SimulationVersion SimulationVersion { get; }

    /// <summary>Fixed at creation. Together with the command log it reproduces the whole game.</summary>
    public RandomSeed Seed { get; }

    public GameStatus Status { get; private set; }

    public Revision Revision { get; private set; }

    /// <summary>Read through from <see cref="GameState"/>, which owns it. Never stored here.</summary>
    public GameDate CurrentDate => _state.CurrentDate;

    /// <summary>Read through from <see cref="GameState"/>. Orthogonal to <see cref="Status"/>.</summary>
    public GamePhase Phase => _state.Phase;

    public GameState State => _state;

    public bool IsPlayable => Status is GameStatus.Running or GameStatus.AwaitingDecision;

    public bool IsTerminal => Status is GameStatus.Finished or GameStatus.Abandoned;

    public static Result<GameSession> Create(
        GameId id,
        OwnerId owner,
        string name,
        PartyId playerParty,
        ScenarioVersionRef scenario,
        SimulationVersion simulationVersion,
        RandomSeed seed,
        GameDate startDate,
        IReadOnlyCollection<PartyId> scenarioParties)
    {
        ArgumentNullException.ThrowIfNull(scenarioParties);

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<GameSession>(
                new Error("game.name_required", "A game needs a name."));
        }

        if (owner == OwnerId.Empty)
        {
            return Result.Failure<GameSession>(
                new Error("game.owner_required", "A game needs an owner."));
        }

        // The player's party must be one the scenario actually contains, or the game starts
        // pointing at a party that will never exist in its state.
        if (!scenarioParties.Contains(playerParty))
        {
            return Result.Failure<GameSession>(
                new Error(
                    "game.player_party_not_in_scenario",
                    $"Player party {playerParty} is not among the scenario's parties."));
        }

        var session = new GameSession(
            id, owner, name, playerParty, scenario, simulationVersion, seed, GameState.Begin(startDate));

        session.RecordEvent(new GameCreated(id, owner, playerParty, startDate));

        return Result.Success(session);
    }

    /// <summary>Restores a persisted game. Records nothing: loading is not a domain fact.</summary>
    public static GameSession Restore(
        GameId id,
        OwnerId owner,
        string name,
        PartyId playerParty,
        ScenarioVersionRef scenario,
        SimulationVersion simulationVersion,
        RandomSeed seed,
        GameStatus status,
        Revision revision,
        GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return new GameSession(id, owner, name, playerParty, scenario, simulationVersion, seed, state)
        {
            Status = status,
            Revision = revision,
        };
    }

    public Result Start()
    {
        if (Status != GameStatus.Created)
        {
            return Rejected("start", GameStatus.Created);
        }

        Status = GameStatus.Running;
        RecordEvent(new GameStarted(Id, CurrentDate));

        return Result.Success();
    }

    public Result Pause()
    {
        if (!IsPlayable)
        {
            return Rejected("pause", GameStatus.Running, GameStatus.AwaitingDecision);
        }

        Status = GameStatus.Paused;
        RecordEvent(new GamePaused(Id, CurrentDate));

        return Result.Success();
    }

    /// <summary>
    /// Resumes into <see cref="GameStatus.AwaitingDecision"/> when a blocking event is still
    /// open, so a game paused mid-decision does not quietly resume as if answered.
    /// </summary>
    public Result Resume()
    {
        if (Status != GameStatus.Paused)
        {
            return Rejected("resume", GameStatus.Paused);
        }

        Status = _state.HasOpenBlockingDecisions
            ? GameStatus.AwaitingDecision
            : GameStatus.Running;

        RecordEvent(new GameResumed(Id, Status, CurrentDate));

        return Result.Success();
    }

    public Result Abandon()
    {
        if (IsTerminal)
        {
            return Rejected("abandon", GameStatus.Running, GameStatus.AwaitingDecision, GameStatus.Paused);
        }

        if (Status == GameStatus.Created)
        {
            return Rejected("abandon", GameStatus.Running, GameStatus.AwaitingDecision, GameStatus.Paused);
        }

        Status = GameStatus.Abandoned;
        RecordEvent(new GameAbandoned(Id, CurrentDate));

        return Result.Success();
    }

    /// <summary>Raised by the loop when a blocking event fires. Halts the simulation.</summary>
    public Result RaiseDecision(EventOccurrenceId occurrence)
    {
        if (!IsPlayable)
        {
            return Rejected("raise a decision", GameStatus.Running, GameStatus.AwaitingDecision);
        }

        _state.RaiseBlockingDecision(occurrence);
        Status = GameStatus.AwaitingDecision;
        RecordEvent(new DecisionRequired(Id, occurrence, CurrentDate));

        return Result.Success();
    }

    /// <summary>Returns to <see cref="GameStatus.Running"/> only once every blocking event is answered.</summary>
    public Result ResolveDecision(EventOccurrenceId occurrence)
    {
        if (Status != GameStatus.AwaitingDecision)
        {
            return Rejected("resolve a decision", GameStatus.AwaitingDecision);
        }

        if (!_state.ResolveBlockingDecision(occurrence))
        {
            return Result.Failure(
                new Error(
                    "game.decision_not_open",
                    $"Occurrence {occurrence} is not awaiting a decision."));
        }

        RecordEvent(new DecisionResolved(Id, occurrence, CurrentDate));

        if (!_state.HasOpenBlockingDecisions)
        {
            Status = GameStatus.Running;
        }

        return Result.Success();
    }

    public Result End(string reason)
    {
        if (IsTerminal)
        {
            return Rejected("end", GameStatus.Running, GameStatus.AwaitingDecision);
        }

        Status = GameStatus.Finished;
        _state.ChangePhase(GamePhase.GameOver);
        RecordEvent(new GameEnded(Id, reason, CurrentDate));

        return Result.Success();
    }

    public Result ChangePhase(GamePhase phase)
    {
        if (IsTerminal)
        {
            return Rejected("change phase", GameStatus.Running, GameStatus.AwaitingDecision);
        }

        if (phase == Phase)
        {
            return Result.Success();
        }

        var previous = Phase;
        _state.ChangePhase(phase);
        RecordEvent(new PhaseChanged(Id, previous, phase, CurrentDate));

        return Result.Success();
    }

    /// <summary>
    /// Runs simulated days in memory and stops at the first condition that applies.
    /// <para>
    /// One command, one persist, one revision increment, however many days were covered —
    /// which is what makes a four-year legislature playable at all.
    /// </para>
    /// </summary>
    public Result<AdvanceOutcome> AdvanceUntil(StopCondition stop, int maximumDays)
    {
        ArgumentNullException.ThrowIfNull(stop);

        if (!IsPlayable)
        {
            return Result.Failure<AdvanceOutcome>(
                new Error(
                    "game.not_advanceable",
                    $"A game that is {Status} cannot be advanced."));
        }

        if (Status == GameStatus.AwaitingDecision)
        {
            return Result.Failure<AdvanceOutcome>(
                new Error(
                    "game.decision_pending",
                    "The simulation cannot advance past an unanswered decision."));
        }

        if (maximumDays < 1)
        {
            return Result.Failure<AdvanceOutcome>(
                new Error("game.invalid_advance_window", "A command must advance at least one day."));
        }

        var startingPhase = Phase;
        var days = 0;
        var reason = StopReason.MaximumDaysReached;

        while (days < maximumDays)
        {
            // Step 1 of the daily order: advance the date, then compute cadence flags.
            // Steps 2-11 arrive with their slices; the loop's shape and stop rules are what
            // this slice pins down. See domain/simulation-loop.
            _state.AdvanceOneDay();
            days++;

            if (Status == GameStatus.AwaitingDecision)
            {
                reason = StopReason.DecisionRequired;
                break;
            }

            if (Status == GameStatus.Finished)
            {
                reason = StopReason.GameEnded;
                break;
            }

            if (stop is StopCondition.PhaseChange && Phase != startingPhase)
            {
                reason = StopReason.PhaseChanged;
                break;
            }

            if (stop is StopCondition.OnDate onDate && CurrentDate >= onDate.Date)
            {
                reason = StopReason.RequestedDateReached;
                break;
            }

            if (stop is StopCondition.AfterDays afterDays && days >= afterDays.Days)
            {
                reason = StopReason.RequestedDaysElapsed;
                break;
            }
        }

        // One increment per command, not per day.
        Revision = Revision.Next();

        return Result.Success(new AdvanceOutcome(days, CurrentDate, reason, Status));
    }

    public GameStateSnapshot ToSnapshot() => _state.ToSnapshot();

    private Result Rejected(string operation, params GameStatus[] permitted) =>
        Result.Failure(
            new Error(
                "game.invalid_status_transition",
                $"Cannot {operation} a game that is {Status}; "
                + $"permitted from {string.Join(" or ", permitted)}."));
}
