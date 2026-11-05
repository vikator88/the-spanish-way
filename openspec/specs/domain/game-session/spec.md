# game-session Specification

## Purpose

Defines `GameSession`, the single mutable aggregate root of a running game: its identity, lifecycle
status machine, phase, concurrency revision, pinned scenario and simulation versions, random seed, and
the small command surface through which every state change must pass.

## Requirements

### Requirement: GameSession is the only mutable aggregate root

A running game SHALL be represented by exactly one aggregate root, `GameSession`, which owns the
`GameState` snapshot; no party, character, province, media outlet or electorate component is separately
loaded, saved or versioned.

#### Scenario: One root per game
- **WHEN** a game is loaded
- **THEN** exactly one `GameSession` is materialised and it carries the whole `GameState`

#### Scenario: Components have no repository
- **WHEN** the Application layer is inspected
- **THEN** no repository interface exists for `PartyState`, `CharacterState`, `ElectorateState`,
  `MediaState`, `WorldState`, `Parliament` or `GovernmentState`

#### Scenario: Atomicity across components
- **WHEN** a single command changes a party, a character and a world variable
- **THEN** all three changes are persisted under the same single revision increment

### Requirement: Session identity and immutable header

A `GameSession` SHALL carry an immutable `GameId`, owner, name, creation date, random seed, pinned
scenario version and simulation version.

#### Scenario: Seed is fixed at creation
- **WHEN** a game has been created
- **THEN** its `RandomSeed` cannot be changed for the remainder of its life

#### Scenario: Scenario is pinned by version
- **WHEN** a game is created from a scenario
- **THEN** it stores a published scenario version reference, not a bare scenario identifier

#### Scenario: Editing a scenario does not affect running games
- **WHEN** a scenario is edited and republished as a new version
- **THEN** every already-created game continues to resolve content from the version it pinned

### Requirement: Exactly one player party

A game SHALL have exactly one player-controlled party, and that party SHALL exist in the game's state.

#### Scenario: Player party must exist
- **WHEN** a game is created with a `PlayerPartyId` absent from the scenario's initial parties
- **THEN** creation fails with a validation error

#### Scenario: Player party cannot be reassigned
- **WHEN** a caller attempts to change `PlayerPartyId` on an existing game
- **THEN** no such operation exists on the aggregate

### Requirement: Lifecycle status machine

A `GameSession` SHALL occupy exactly one of `Created`, `Running`, `AwaitingDecision`, `Paused`,
`Finished` or `Abandoned`, and SHALL permit only the transitions defined below.

#### Scenario: Start
- **WHEN** `Start` is called on a `Created` game
- **THEN** the status becomes `Running` and a `GameStarted` domain event is recorded

#### Scenario: Blocking event raised
- **WHEN** the simulation raises a blocking event while `Running`
- **THEN** the status becomes `AwaitingDecision`

#### Scenario: Last blocking decision resolved
- **WHEN** the final open blocking event is resolved while `AwaitingDecision`
- **THEN** the status returns to `Running`

#### Scenario: Pause from either active status
- **WHEN** `Pause` is called while `Running` or `AwaitingDecision`
- **THEN** the status becomes `Paused`

#### Scenario: Resume with no blocking events
- **WHEN** `Resume` is called while `Paused` and no blocking event is open
- **THEN** the status becomes `Running`

#### Scenario: Resume with blocking events open
- **WHEN** `Resume` is called while `Paused` and at least one blocking event is open
- **THEN** the status becomes `AwaitingDecision`

#### Scenario: Terminal condition
- **WHEN** the game-over policy is satisfied
- **THEN** the status becomes `Finished`, `FinishedAt` is set, and a `GameEnded` domain event is recorded

#### Scenario: Abandon
- **WHEN** `Abandon` is called while `Running`, `AwaitingDecision` or `Paused`
- **THEN** the status becomes `Abandoned`

#### Scenario: Finished is terminal
- **WHEN** any command other than a read is issued to a `Finished` or `Abandoned` game
- **THEN** the command is rejected with a failed `Result`

#### Scenario: Paused and AwaitingDecision are distinct
- **WHEN** the status enum is inspected
- **THEN** `Paused` and `AwaitingDecision` are separate values with different permitted commands

### Requirement: Phase is orthogonal to status

A `GameSession` SHALL carry a `GamePhase` of `Normal`, `ElectionCampaign`, `ElectionDay`,
`GovernmentFormation` or `GameOver`, independent of its status.

#### Scenario: Phase and status vary independently
- **WHEN** a game is `Paused` during an election campaign
- **THEN** its status is `Paused` and its phase remains `ElectionCampaign`

#### Scenario: Campaign phase entry
- **WHEN** the current date reaches the configured number of days before a scheduled election
- **THEN** the phase becomes `ElectionCampaign`

### Requirement: Command surface

A `GameSession` SHALL expose only the operations `Start`, `AdvanceUntil`, `ExecuteAction`,
`ResolveEvent`, `Pause`, `Resume` and `Abandon`, and SHALL expose no public property setter that permits
invalid state.

#### Scenario: No arbitrary mutation
- **WHEN** the aggregate's public surface is inspected
- **THEN** every mutable property is either private-set or settable only through a named operation

#### Scenario: Components are reached through intention-revealing methods
- **WHEN** a leadership change is required
- **THEN** it happens through `partyState.ChangeLeader(characterId)`, not by assigning a `LeaderId` property

### Requirement: Optimistic concurrency

Every state-changing operation SHALL be performed against an expected revision and SHALL increment the
revision exactly once per successful command.

#### Scenario: Stale revision rejected
- **WHEN** a command is submitted with an expected revision lower than the stored revision
- **THEN** the command is rejected with a concurrency conflict and nothing is written

#### Scenario: One increment per command
- **WHEN** `AdvanceUntil` simulates 400 days in a single call
- **THEN** the revision increases by exactly 1

#### Scenario: Never overwrite newer state
- **WHEN** two commands race from the same revision
- **THEN** at most one succeeds and the loser does not overwrite the winner's state

### Requirement: Session-level domain events

A `GameSession` SHALL record domain events for the lifecycle facts that other components care about,
and SHALL NOT dispatch them.

#### Scenario: Lifecycle events exist
- **WHEN** the session's events are inspected
- **THEN** at least `GameCreated`, `GameStarted`, `GamePaused`, `GameResumed`, `GameAbandoned`,
  `DecisionRequired`, `DecisionResolved` and `GameEnded` are defined

#### Scenario: Events survive until collected
- **WHEN** a command completes successfully
- **THEN** the recorded events remain on the aggregate until the Application layer collects and clears them
