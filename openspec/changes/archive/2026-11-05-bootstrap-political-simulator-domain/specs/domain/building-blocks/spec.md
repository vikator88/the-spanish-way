# Spec Delta

## Purpose

Provides the foundation every other module depends on, split in two: `Common` for the DDD tactical
patterns that would read the same in any domain (entity and aggregate base types, value objects,
domain-event recording, result handling), and `Shared` for this game's own shared kernel — strongly-typed
identifiers, bounded numerics, the in-game calendar and deterministic randomness — classified by kind.

## ADDED Requirements

### Requirement: Foundation folder structure

The Domain's foundation SHALL be split into `Common`, holding only domain-agnostic DDD tactical
patterns, and `Shared`, holding the game's own shared kernel classified by kind.

#### Scenario: Common holds only DDD patterns
- **WHEN** the `Common` folder is inspected
- **THEN** it contains only `Entity<TId>`, `AggregateRoot<TId>`, `ValueObject`, `IDomainEvent`,
  `Result`, `Result<T>` and `Error`

#### Scenario: Game vocabulary lives in Shared
- **WHEN** a game-specific type such as `GameDate` or `PartyId` is located
- **THEN** it is under `Shared`, not `Common`

#### Scenario: Shared types are classified by kind
- **WHEN** a type under `Shared` is located
- **THEN** it sits in `ValueObjects/`, `Identifiers/`, `Enumerations/` or `Services/`, and satisfies
  that classification

#### Scenario: The split is enforced
- **WHEN** a game-specific type is added to `Common`, or a `Shared` type is left unclassified
- **THEN** an architecture test fails naming the offending type

### Requirement: Entity base type

The Domain SHALL provide `Entity<TId>` with identity-based equality, so that two entities with the same
identifier are equal regardless of their attribute values.

#### Scenario: Identity equality
- **WHEN** two `Entity<TId>` instances of the same concrete type carry the same `Id` but different attributes
- **THEN** they compare as equal and produce the same hash code

#### Scenario: Cross-type inequality
- **WHEN** two entities of different concrete types carry the same underlying identifier value
- **THEN** they compare as not equal

#### Scenario: Identity is immutable
- **WHEN** an entity has been constructed
- **THEN** its `Id` has no public or protected setter

### Requirement: Aggregate root base type

The Domain SHALL provide `AggregateRoot<TId>`, deriving from `Entity<TId>`, that records domain events
without dispatching them.

#### Scenario: Recording an event
- **WHEN** an aggregate operation calls `RecordEvent(domainEvent)`
- **THEN** the event appears in `DomainEvents` in the order recorded

#### Scenario: Events are read-only to callers
- **WHEN** a caller obtains `DomainEvents`
- **THEN** the collection is read-only and mutating it is not possible

#### Scenario: Clearing events
- **WHEN** `ClearDomainEvents()` is called
- **THEN** `DomainEvents` is empty

#### Scenario: The aggregate never dispatches
- **WHEN** `RecordEvent` is called
- **THEN** no handler is invoked and no dispatcher, publisher or mediator type is referenced

### Requirement: Value object base type

The Domain SHALL provide a `ValueObject` base type with structural equality derived from its components.

#### Scenario: Structural equality
- **WHEN** two value objects of the same type expose equal equality components
- **THEN** they compare as equal and produce the same hash code

#### Scenario: Immutability
- **WHEN** a value object is constructed
- **THEN** none of its properties can be mutated afterwards

### Requirement: Domain event contract

The Domain SHALL define `IDomainEvent` carrying the game date on which the fact occurred, and SHALL NOT
define or reference any dispatch, notification or handler interface.

#### Scenario: Event carries game time
- **WHEN** a domain event is created
- **THEN** it exposes the `GameDate` at which the fact occurred

#### Scenario: Events use no wall-clock time
- **WHEN** a domain event is inspected
- **THEN** it does not expose `DateTime` or `DateTimeOffset` as its occurrence time

#### Scenario: Events are immutable records
- **WHEN** a domain event type is declared
- **THEN** it is a `sealed record` named in the past tense, for example `PartyLeaderChanged`

### Requirement: Result type

The Domain SHALL provide `Result` and `Result<T>` with a structured `Error`, so that rule violations are
returned as values rather than signalled by exceptions.

#### Scenario: Failure carries a structured error
- **WHEN** a domain operation rejects a request
- **THEN** it returns a failed `Result` whose `Error` carries a stable machine-readable code and a message

#### Scenario: Accessing the value of a failure
- **WHEN** `Value` is read on a failed `Result<T>`
- **THEN** the access throws, because reading a value that does not exist is programmer error

#### Scenario: Rule violations do not throw
- **WHEN** a caller invokes a domain operation with input that violates a business rule
- **THEN** no exception is thrown and a failed `Result` is returned

### Requirement: Strongly-typed identifiers

Every domain identifier SHALL be a distinct readonly struct type, so that passing a `PartyId` where a
`CharacterId` is expected fails to compile.

#### Scenario: Identifier types are distinct
- **WHEN** a method expecting `CharacterId` is called with a `PartyId`
- **THEN** compilation fails

#### Scenario: Identifier set
- **WHEN** the `Shared/Identifiers` folder is inspected
- **THEN** it defines at least `GameId`, `PartyId`, `CharacterId`, `ProvinceId`, `SegmentId`,
  `MediaOutletId`, `PollsterId`, `IssueId`, `EventDefinitionId`, `EventOccurrenceId`,
  `ActionDefinitionId`, `ActionExecutionId`, `EffectDefinitionId`, `PendingEffectId`, `ModifierId`,
  `ScenarioId` and `ElectionId`

#### Scenario: Deterministic ordering
- **WHEN** a collection keyed by an identifier is iterated during simulation
- **THEN** the identifier type provides a total ordering used to produce a stable sequence

### Requirement: Bounded numeric type

The Domain SHALL provide `Bounded<T>` carrying a value, inclusive bounds and a clamping operation that
reports truncation, so that no effect can push an attribute outside its configured range.

#### Scenario: Value stays within bounds
- **WHEN** a value of 98 with bounds 0 to 100 receives an addition of 7
- **THEN** the resulting value is 100

#### Scenario: Truncation is reported
- **WHEN** a clamp truncates a change
- **THEN** the operation returns the truncated amount as a diagnostic rather than discarding it silently

#### Scenario: Invalid bounds are rejected
- **WHEN** a `Bounded<T>` is constructed with a minimum greater than its maximum
- **THEN** construction fails

### Requirement: Game calendar

The Domain SHALL provide `GameDate` as a value object of year, month and day with safe arithmetic,
total ordering and cadence boundary detection, and SHALL use it for all in-game time.

#### Scenario: Date arithmetic across month boundaries
- **WHEN** one day is added to 31 January 2027
- **THEN** the result is 1 February 2027

#### Scenario: Ordering
- **WHEN** two game dates are compared
- **THEN** the earlier date compares as less than the later date

#### Scenario: Weekly cadence
- **WHEN** the number of days elapsed since the game's start date is an exact multiple of 7
- **THEN** the day is flagged as a weekly boundary

#### Scenario: Monthly cadence
- **WHEN** the current date is the first day of a calendar month
- **THEN** the day is flagged as a monthly boundary

#### Scenario: Quarterly cadence
- **WHEN** the current date is 1 January, 1 April, 1 July or 1 October
- **THEN** the day is flagged as a quarterly boundary

#### Scenario: Game time is not server time
- **WHEN** any domain rule needs the current date
- **THEN** it reads `GameState.CurrentDate` and never a system clock

### Requirement: Deterministic random source

The Domain SHALL provide `IRandomSource` backed by a counter-based generator keyed on
`(seed, stream, game day, index)`, with one named stream per subsystem.

#### Scenario: Reproducible draws
- **WHEN** the same seed, stream name, game day and draw index are used in two separate processes
- **THEN** both produce an identical value

#### Scenario: Streams are independent
- **WHEN** an additional draw is added to the `media` stream
- **THEN** every subsequent draw on the `events` stream is unchanged

#### Scenario: Named streams exist
- **WHEN** the random source is configured
- **THEN** it exposes the streams `events`, `bindings`, `media`, `reactions`, `actors`, `polls` and `ties`

#### Scenario: Platform randomness is not used
- **WHEN** the implementation is inspected
- **THEN** it does not call `System.Random` or any other platform random API

#### Scenario: Only seed and indices are persisted
- **WHEN** a game is saved
- **THEN** the snapshot stores the seed and each stream's index for the current day, and nothing else
  about generator internals
