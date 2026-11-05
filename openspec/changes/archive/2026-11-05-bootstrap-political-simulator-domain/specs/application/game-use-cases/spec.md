# Spec Delta

## Purpose

Defines the Application layer's use-case surface: the commands and queries that coordinate the domain,
the concurrency and idempotency guarantees around them, the rule that keeps business logic out of
handlers, and the `PlayerView` projection that stops hidden simulation state from reaching the browser.

## ADDED Requirements

### Requirement: Handlers coordinate, they do not decide

An Application handler SHALL load state, authorise, invoke domain operations, persist and project —
and SHALL contain no business rule.

#### Scenario: No formulas in handlers
- **WHEN** the Application project is inspected
- **THEN** it contains no electoral formula, effect semantics, perception rule, event eligibility rule or
  party-survival rule

#### Scenario: Handler shape
- **WHEN** a command handler is read
- **THEN** it loads the session, calls one or more aggregate operations, returns the aggregate's `Result`
  and maps it to a response

#### Scenario: Rules stay put under change
- **WHEN** a new rule is needed
- **THEN** it is added to a domain service or state component, not to a handler

### Requirement: Command surface

The MVP SHALL expose commands for creating and running a game.

#### Scenario: Commands exist
- **WHEN** the command surface is enumerated
- **THEN** it includes `CreateGame`, `StartGame`, `AdvanceUntil`, `ExecuteGameAction`, `ResolveEvent`,
  `PauseGame`, `ResumeGame` and `AbandonGame`

#### Scenario: Elections are not a player command
- **WHEN** an election is held
- **THEN** it is run by the simulation loop, not by a command the player issues

#### Scenario: Commands carry the expected revision
- **WHEN** a state-changing command is submitted
- **THEN** it carries the revision the client last observed

### Requirement: Query surface

The MVP SHALL expose queries that serve the player's view of the game, kept separate from the
operator-only query for the full simulation state.

#### Scenario: Queries exist
- **WHEN** the query surface is enumerated
- **THEN** it includes `GetGameOverview`, `GetPlayerView`, `GetAvailableActions`, `GetActiveDecisions`,
  `GetPollHistory`, `GetElectionResults`, `GetNewsFeed` and `GetGameHistory`

#### Scenario: No player-facing query returns raw state
- **WHEN** any query reachable from the player-facing API surface returns
- **THEN** its response carries no raw `GameState`, hidden preference, perception record, utility value or
  effect provenance

#### Scenario: Player and operator queries are separated
- **WHEN** the query surface is inspected
- **THEN** the player-facing queries and `GetGameState` live in different namespaces

#### Scenario: Queries do not mutate
- **WHEN** a query executes
- **THEN** no state is written and no revision changes

### Requirement: PlayerView projection

Everything returned to the game client SHALL pass through a `PlayerView` projection that applies
configured visibility to each indicator.

#### Scenario: Visibility levels
- **WHEN** an indicator is projected
- **THEN** it is rendered as exact, noisy, delayed or hidden according to the scenario's configuration

#### Scenario: Hidden preferences never leave
- **WHEN** a `PlayerView` is produced
- **THEN** it contains no hidden electoral preference, perception record, utility value or effect
  provenance beyond what the player is meant to see

#### Scenario: Polls, not truth
- **WHEN** the player asks how they are doing
- **THEN** they receive poll estimates with their margins, not the hidden preference distribution

#### Scenario: Noise is reproducible
- **WHEN** a noisy indicator is projected twice for the same day
- **THEN** it shows the same value, because the noise is derived from the seed rather than drawn afresh

### Requirement: Operator access to the full state

The MVP SHALL expose a `GetGameState` query returning the complete `GameState` for debugging, support and
tooling, separated from the player-facing surface by authorisation and by namespace.

#### Scenario: Full state is returned
- **WHEN** `GetGameState` executes
- **THEN** it returns the complete `GameState`, including hidden preferences, perception records, queues,
  modifiers with their provenance, and the random stream indices

#### Scenario: Operator authorisation
- **WHEN** `GetGameState` is executed by a caller without the operator role
- **THEN** it is refused, and game ownership alone does not authorise it

#### Scenario: Not reachable from the player-facing surface
- **WHEN** the player-facing API surface is enumerated
- **THEN** no endpoint returns the result of `GetGameState`

#### Scenario: Read-only
- **WHEN** `GetGameState` executes
- **THEN** no state is written and the revision does not change

#### Scenario: Reproducing a bug report
- **WHEN** a player reports a problem and an operator inspects the game
- **THEN** `GetGameState` together with the command log is sufficient to diagnose it without instrumenting
  a running simulation

#### Scenario: Tests may assert against full state
- **WHEN** a determinism or replay test compares two runs
- **THEN** it may compare full `GameState` values rather than player-visible projections

### Requirement: Optimistic concurrency handling

State-changing commands SHALL use the game revision for optimistic concurrency and SHALL never overwrite
newer state.

#### Scenario: Conflict is reported
- **WHEN** the stored revision differs from the expected revision
- **THEN** the command fails with a conflict error and nothing is written

#### Scenario: Bounded retry
- **WHEN** a command is declared safe to retry and hits a conflict
- **THEN** the pipeline reloads and retries up to the configured limit before failing

#### Scenario: Unsafe commands are not retried
- **WHEN** a command that is not safe to retry hits a conflict
- **THEN** it fails immediately with a conflict error

### Requirement: Idempotency

A command carrying a client command identifier SHALL execute at most once per game.

#### Scenario: Replay returns the original result
- **WHEN** the same client command identifier is submitted twice for the same game
- **THEN** the stored result of the first execution is returned and nothing executes again

#### Scenario: Uniqueness scope
- **WHEN** a client command identifier is validated
- **THEN** uniqueness is enforced per game

#### Scenario: Retrying a network failure is safe
- **WHEN** a client retries after a timeout with the same identifier
- **THEN** the action is not executed twice

### Requirement: Command log

Every accepted state-changing command SHALL be appended to a per-game command log sufficient to replay
the game.

#### Scenario: Log contents
- **WHEN** a command is accepted
- **THEN** the log records its type, parameters, client command identifier and the resulting revision

#### Scenario: Replay from the log
- **WHEN** the log is replayed with the original seed and scenario version
- **THEN** the game reaches the same state

#### Scenario: Bug reports become tests
- **WHEN** a player reports a problem
- **THEN** the command log for that game is sufficient to reproduce it deterministically

### Requirement: Authorisation

A player SHALL act only on a game they own.

#### Scenario: Ownership check
- **WHEN** a command or query targets a game owned by another user
- **THEN** it is refused

#### Scenario: Authorisation is not a domain rule
- **WHEN** authorisation is located
- **THEN** it lives in the Application layer, not in the Domain
