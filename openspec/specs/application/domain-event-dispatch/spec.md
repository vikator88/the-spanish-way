# domain-event-dispatch Specification

## Purpose

Defines how facts recorded in the Domain become notifications handled elsewhere: collection from the
aggregate, publication through the custom mediator after the transaction commits, the adapter that keeps
the Domain free of Application types, and the rule that no handler may be load-bearing for simulation
correctness.

## Requirements

### Requirement: Three kinds of event are kept distinct

The codebase SHALL distinguish domain events, application notifications and in-game simulation events by
type, location and naming.

#### Scenario: Domain event
- **WHEN** a domain event is defined
- **THEN** it is a past-tense `sealed record` implementing `IDomainEvent` under `Domain/**/Events/`

#### Scenario: Application notification
- **WHEN** a notification is defined
- **THEN** it implements `INotification` under `Application/**/Notifications/`

#### Scenario: Simulation event
- **WHEN** in-game content is defined
- **THEN** it is a `GameEventDefinition`, `EventOccurrence` or `ActiveEvent`, and is never called a domain
  event

#### Scenario: No conflation
- **WHEN** the three concepts are reviewed
- **THEN** no type serves two of these roles

### Requirement: The Domain records, the Application dispatches

Domain events SHALL be recorded on the aggregate root and SHALL NOT be dispatched by the Domain.

#### Scenario: Recording
- **WHEN** a domain operation produces a fact worth announcing
- **THEN** it calls `RecordEvent` and continues

#### Scenario: Domain references no dispatcher
- **WHEN** the Domain project is inspected
- **THEN** it contains no publisher, dispatcher, mediator or handler type

#### Scenario: Collection
- **WHEN** a command completes successfully
- **THEN** the Application collects `DomainEvents` from the aggregate and clears them

### Requirement: Notification adapter

Domain events SHALL be published as notifications through a generic adapter, so that `IDomainEvent` never
needs to implement an Application interface.

#### Scenario: Adapter wraps the event
- **WHEN** a domain event is published
- **THEN** it is wrapped in `DomainEventNotification<TDomainEvent>` which implements `INotification`

#### Scenario: Handlers receive the domain event
- **WHEN** a handler subscribes to a domain event
- **THEN** it implements `INotificationHandler<DomainEventNotification<TDomainEvent>>` and reads the
  original event from the wrapper

#### Scenario: Domain stays clean
- **WHEN** `IDomainEvent` is inspected
- **THEN** it does not implement or reference `INotification`

### Requirement: Publication happens after commit

Domain events SHALL be published only after the transaction that persisted their state has committed.

#### Scenario: Ordering
- **WHEN** a command succeeds
- **THEN** state is committed first and notifications are published afterwards

#### Scenario: Rollback publishes nothing
- **WHEN** a command fails and the transaction rolls back
- **THEN** no notification is published

#### Scenario: Same-transaction history
- **WHEN** history or outbox rows accompany a state change
- **THEN** they are written in the same transaction as the snapshot

### Requirement: Handlers are not load-bearing

A notification handler SHALL never be required for the simulation state to be valid.

#### Scenario: Handler failure does not fail the command
- **WHEN** a notification handler throws
- **THEN** the originating command has already succeeded, the failure is logged, and the state remains valid

#### Scenario: Simulation does not read handler output
- **WHEN** the next simulated day is computed
- **THEN** it reads only the snapshot, never a read model a handler produced

#### Scenario: Handlers are side-effect consumers
- **WHEN** handlers are reviewed
- **THEN** they update read models, build news projections and emit analytics, and change no simulation state

### Requirement: Lifecycle of the MVP event set

The MVP SHALL define the domain events the rest of the system reacts to.

#### Scenario: Event set
- **WHEN** the Domain's events are enumerated
- **THEN** they include at least `GameCreated`, `GameStarted`, `GamePaused`, `GameResumed`,
  `GameAbandoned`, `GameEnded`, `DecisionRequired`, `DecisionResolved`, `ActionExecuted`, `EventOccurred`,
  `EffectApplied`, `PartyLeaderChanged`, `MembershipChanged`, `PollPublished`, `ElectionCompleted`,
  `GovernmentFormed` and `PartyDissolved`

#### Scenario: Events carry the game date
- **WHEN** any of these events is recorded
- **THEN** it carries the `GameDate` on which the fact occurred

### Requirement: In-process delivery for the MVP

Publication SHALL be in-process for the MVP, with an outbox introduced only when reliable external
delivery is actually required.

#### Scenario: No messaging infrastructure
- **WHEN** the MVP's dependencies are inspected
- **THEN** no message broker or distributed messaging library is present

#### Scenario: Outbox is a later addition
- **WHEN** external delivery becomes necessary
- **THEN** an outbox table is introduced behind the existing publication point without changing the Domain
