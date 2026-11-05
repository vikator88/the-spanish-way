# Spec Delta

## Purpose

Defines the persistence abstractions the Application owns and the EF Core plus PostgreSQL storage
strategy Infrastructure will implement — the relational header, the JSONB snapshot, the append-only
history and command log, and the version policy — while deliberately deferring mappings, migrations and
every Npgsql detail.

## ADDED Requirements

### Requirement: Abstractions belong to the Application layer

Repository, unit-of-work and supporting abstractions SHALL be declared in the Application layer and
implemented in Infrastructure.

#### Scenario: Interface location
- **WHEN** the abstractions are located
- **THEN** `IGameSessionRepository`, `IScenarioRepository`, `IHistoryStore`, `ICommandLog` and
  `IUnitOfWork` are declared under `TheSpanishWay.Application/Abstractions/`

#### Scenario: Implementation location
- **WHEN** an implementation is located
- **THEN** it lives in `TheSpanishWay.Infrastructure` and is registered at the composition root

#### Scenario: The Domain declares none of them
- **WHEN** the Domain is inspected
- **THEN** it declares no repository or persistence abstraction

### Requirement: EF Core entities are not the domain model

Persistence mapping SHALL live in Infrastructure and SHALL NOT dictate the shape of domain types.

#### Scenario: No persistence attributes in the Domain
- **WHEN** a domain type is inspected
- **THEN** it carries no mapping attribute, no navigation property added for the ORM's benefit, and no
  parameterless constructor added solely for materialisation

#### Scenario: Mapping is against the snapshot
- **WHEN** `GameState` is persisted
- **THEN** it is serialised as a single snapshot document rather than mapped component by component to
  relational tables

### Requirement: Storage strategy

The database SHALL hold a relational game header, a JSONB current snapshot, periodic historical
snapshots, append-only history records and an append-only command log.

#### Scenario: Header columns
- **WHEN** the header is inspected
- **THEN** it carries the game identifier, owner, name, status, phase, current date, revision,
  `SchemaVersion`, `SimulationVersion` and the pinned scenario version

#### Scenario: Current snapshot
- **WHEN** a game is saved
- **THEN** its `GameState` is stored as one JSONB document associated with the header

#### Scenario: Periodic snapshots
- **WHEN** a monthly boundary is crossed
- **THEN** a historical snapshot is retained for charts and for fast replay from a midpoint

#### Scenario: History is insert-only
- **WHEN** an election, poll, action execution or event occurrence is recorded
- **THEN** it is inserted and never updated or deleted

#### Scenario: One transaction
- **WHEN** a command succeeds
- **THEN** the header, the snapshot, the history records and the command log entry are written in a single
  transaction

### Requirement: PostgreSQL and EF Core

The implementation SHALL target PostgreSQL through EF Core, with the concrete mapping deferred.

#### Scenario: Provider
- **WHEN** Infrastructure is configured
- **THEN** it uses EF Core with the Npgsql provider

#### Scenario: Deferred scope
- **WHEN** this change is implemented
- **THEN** Infrastructure is scaffolded with its project, dependency wiring and abstraction stubs, and
  contains no mapping configuration or migration

#### Scenario: Snapshot column type
- **WHEN** the snapshot is mapped
- **THEN** it is stored in a `jsonb` column

### Requirement: Schema migration of saved state

A change to the snapshot's serialised shape SHALL be handled by a chain of upcasters applied on load.

#### Scenario: Upcaster chain
- **WHEN** a snapshot at `SchemaVersion` 1 is loaded by code at version 3
- **THEN** the version 1 to 2 and version 2 to 3 upcasters run in order

#### Scenario: Upcasters are tested
- **WHEN** an upcaster is added
- **THEN** a test loads a stored fixture at the old version and asserts the upgraded result

#### Scenario: Schema change is not a rules change
- **WHEN** only the serialisation shape changes
- **THEN** `SimulationVersion` is unchanged

### Requirement: Simulation version policy

Replay equality SHALL be guaranteed only within a single `SimulationVersion`, and upgrades SHALL be
recorded rather than retrofitted.

#### Scenario: Rules change bumps the version
- **WHEN** any rule or formula changes
- **THEN** `SimulationVersion` is incremented

#### Scenario: Running games move forward
- **WHEN** a game created under an older simulation version is loaded by newer code
- **THEN** it continues under the new rules and the upgrade date is recorded in its history

#### Scenario: Old rule sets are not retained
- **WHEN** the policy is documented
- **THEN** it states explicitly that historical rule sets are not kept alive for exact replay of old games

### Requirement: No wall-clock dependency in rules

Server time SHALL be available to the Application for auditing only, and SHALL never influence a domain
rule.

#### Scenario: Clock abstraction
- **WHEN** real time is needed for an audit column
- **THEN** it is obtained through an `IClock` abstraction in the Application layer

#### Scenario: Rules use game time
- **WHEN** a domain rule needs a date
- **THEN** it uses `GameDate` from `GameState`
