# game-state Specification

## Purpose

Defines the `GameState` snapshot owned by `GameSession`: the mutable simulation state, its component
structure, the single-source-of-truth rules that remove the duplicated facts in the v0.1 spec, and the
stable-ordering and self-sufficiency guarantees that make a game resumable and reproducible.

## Requirements

### Requirement: GameState composition

`GameState` SHALL contain the current date, the phase, the random stream indices, and the state
components `WorldState`, party states, character states, `Parliament`, `GovernmentState`,
`ElectorateState`, `MediaState`, relation sets, and the active-event, pending-effect and modifier queues.

#### Scenario: Components are present
- **WHEN** a `GameState` is inspected
- **THEN** it exposes world, party, character, parliament, government, electorate, media, relations and
  queue components

#### Scenario: State is a domain concept
- **WHEN** `GameState` is inspected
- **THEN** it carries no serialisation attribute, no EF Core type and no JSON type

### Requirement: The game date has exactly one owner

The current game date SHALL live in `GameState` only, and SHALL be projected onto the relational game
header for querying rather than stored twice as a source of truth.

#### Scenario: No duplicate date
- **WHEN** `GameSession` is inspected
- **THEN** it exposes no settable `CurrentDate` of its own

#### Scenario: Header projection
- **WHEN** a game is persisted
- **THEN** the relational header's date column is written from `GameState.CurrentDate`

#### Scenario: Date advances monotonically
- **WHEN** any operation attempts to set a date earlier than the current date
- **THEN** the operation fails and the state is unchanged

### Requirement: Single source of truth for duplicated facts

Each fact the v0.1 spec records in two places SHALL have exactly one owner, with the other representation
derived.

#### Scenario: Seats belong to parliament
- **WHEN** a party's seat count is needed
- **THEN** it is read from `Parliament`, and `PartyState` exposes no `Seats` property

#### Scenario: Leadership belongs to membership
- **WHEN** a party's leader is needed
- **THEN** it is derived from the membership whose role is Leader, and `PartyState` exposes no independent
  `LeaderId` that could disagree

#### Scenario: Party affiliation belongs to membership
- **WHEN** a character's party is needed
- **THEN** it is derived from active memberships, and `CharacterState` exposes no independent `PartyId`

#### Scenario: Membership timestamps use game time
- **WHEN** a membership records when it began
- **THEN** it stores a `GameDate`, not a `DateTimeOffset`

### Requirement: Renamed concepts

State components SHALL use names that do not collide with differently-meaning concepts elsewhere in the
model.

#### Scenario: Internal support
- **WHEN** a membership's standing inside its party is read
- **THEN** the property is named `InternalSupport`

#### Scenario: Character loyalty is a trait
- **WHEN** a character's personality is read
- **THEN** `Loyalty` is a character trait and is distinct from any membership's `InternalSupport`

### Requirement: Stable iteration order

Every iteration over a collection during simulation SHALL follow a deterministic order derived from
identifiers, never dictionary or hash-set enumeration order.

#### Scenario: Deterministic traversal
- **WHEN** the simulation iterates parties, characters, provinces, segments, outlets, modifiers, pending
  effects or active events
- **THEN** the sequence is ordered by identifier

#### Scenario: Insertion order does not change outcomes
- **WHEN** the same scenario is loaded with its parties declared in a different file order
- **THEN** the simulation produces identical results

### Requirement: Snapshot self-sufficiency

`GameState` SHALL be complete enough to resume a game without any process-local memory, server wall-clock
time, or history records.

#### Scenario: Resume from snapshot alone
- **WHEN** a process is restarted and a game is loaded purely from its snapshot
- **THEN** subsequent simulation produces the same results as an uninterrupted run

#### Scenario: History is never read back
- **WHEN** the next state is computed
- **THEN** no election, poll, execution or occurrence history record is consulted

#### Scenario: No wall-clock dependency
- **WHEN** a game is resumed on a machine whose system clock differs by years
- **THEN** the simulation is unaffected

### Requirement: Schema version and upcasting

`GameState` SHALL carry a `SchemaVersion` describing the shape of its serialised form, distinct from the
concurrency `Revision` and from the `SimulationVersion`.

#### Scenario: Version names are unambiguous
- **WHEN** the version fields are inspected
- **THEN** they are named `SchemaVersion`, `SimulationVersion` and `Revision`, and no field is named
  `StateVersion`

#### Scenario: Loading an older schema
- **WHEN** a snapshot with an older `SchemaVersion` is loaded
- **THEN** a chain of upcasters runs in order to bring it to the current version before use

#### Scenario: Loading an unknown newer schema
- **WHEN** a snapshot with a `SchemaVersion` higher than the running code supports is loaded
- **THEN** loading fails explicitly rather than reading the snapshot partially
