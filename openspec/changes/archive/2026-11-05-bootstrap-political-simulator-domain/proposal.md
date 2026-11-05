# Proposal

## Why

`game-design/political-simulator-domain-context.md` (v0.1) specifies a political-simulation game whose
central promise is that **election results emerge from a simulation rather than being awarded by
content**. `game-design/Political Simulator — Domain Design Review.md` (Nov 4, 2026) audits that spec
and finds 24 issues, 7 of them high severity, that would force a rework if code were written against
v0.1 as-is.

Today `api/` is an empty directory. Nothing is committed, so this is the cheapest possible moment to
settle the five decisions the review says must be settled "before any code is written":

1. **The simulated world is one consistency boundary.** A daily tick touches parties, characters,
   perceptions and world variables at once under one revision. Making `PoliticalParty` a separately
   persisted aggregate would force cross-aggregate transactions on every tick.
2. **Content must not write perceptions directly.** If an event can author "retiree trust in party X
   −10", the author is hand-placing votes and the heterogeneous-reaction principle is dead.
3. **Temporary effects need a modifier layer.** Reverting a clamped `Add` on expiry is arithmetically
   wrong (inflation 9.5, ceiling 10, temporary +2 clamps to +0.5; subtracting 2 on expiry yields 8.0).
4. **Event definitions need role bindings.** A reusable "corruption scandal" needs a slot for *which*
   party and *which* character, filled when the event fires.
5. **The calendar needs a decision-point model.** 1,461 days per legislature cannot be advanced one
   click — and one snapshot write — at a time.

This change locks those decisions into an architecture and a domain model, and sets up the .NET 10
solution they will live in. It is the "review package" the review document asks to agree on before
slice 1 starts: aggregate boundaries, module map, and open decisions with defaults.

## What Changes

**Solution scaffold (new).** An `api/` tree on .NET 10 using Clean Architecture with four projects
under `src/` (`TheSpanishWay.Domain`, `.Application`, `.Infrastructure`, `.Api`), an XML `.slnx`
solution file at the `api/` root, and a `test/` tree. Dependencies point strictly inward; the Domain project references
no NuGet package other than the BCL and is enforced by an architecture test suite.

**Domain model (new).** A full DDD model implementing the *reviewed* design, not v0.1 verbatim:

- DDD building blocks: `Entity<TId>`, `AggregateRoot<TId>`, `ValueObject`, `IDomainEvent`,
  `Result`/`Result<T>`, `Error`, strongly-typed ids, `Bounded<T>`, `GameDate` and calendar arithmetic,
  and a counter-based `IRandomSource` with named per-subsystem streams.
- `GameSession` as the single mutable aggregate root, owning a `GameState` snapshot composed of state
  components (`WorldState`, `PartyState`, `CharacterState`, `Parliament`, `GovernmentState`,
  `ElectorateState`, `MediaState`, relation sets, and the modifier/pending-effect/active-event queues).
- `ScenarioDefinition` as a second, immutable-once-published aggregate that games pin *by version*.
- Append-only history records (`ElectionRecord`, `PollRecord`, `ExecutionRecord`, `OccurrenceRecord`).
- A data-driven content model: `GameEventDefinition`, `ActionDefinition`, `ConditionGroup`,
  `EffectDefinition`, role declarations, an **issue catalogue** (positional vs valence), and a code-side
  **attribute registry** that validates every `(target type, attribute key)` content references.
- An **effect engine** with base values, typed modifiers, pending effects, explicit stacking policy,
  change-set atomicity and provenance on every mutation.
- A **stimulus pipeline**: actions and events emit typed stimuli; only the reaction pipeline writes
  perceptions. Content is structurally forbidden from writing perceptions, preferences, turnout or seats.
- Pure electoral calculators: ideological affinity, utility with centred `[-1, 1]` components, stable
  softmax, turnout, preference inertia expressed per 30 days, provincial aggregation, seat distribution
  and D'Hondt with seeded tie-breaking.
- **Domain events** (`GameStarted`, `ActionExecuted`, `EventOccurred`, `PartyLeaderChanged`,
  `ElectionCompleted`, `GovernmentFormed`, `PartyDissolved`, `GameEnded`, …) recorded on the aggregate,
  never dispatched by the Domain itself.
- The **game loop**: `AdvanceUntil(stop, maxDays)` running many days in memory with a fixed 12-step
  daily order, cadence flags (weekly/monthly/quarterly/election), and an explicit `AwaitingDecision`
  status distinct from `Paused`.

**Application layer (new).** CQRS over a **hand-written mediator** — no MediatR dependency — with
`ICommand`/`IQuery`/handler contracts, a DI-resolved dispatcher with a cached invoker, and a pipeline
behaviour chain (logging → validation → idempotency → unit of work → concurrency retry). Domain events
collected from the aggregate are published through the same mediator **after** the transaction commits.
Player-facing queries return a `PlayerView` projection; the full `GameState` is reachable only through an
operator-only `GetGameState` query that is never wired to a player-facing endpoint.

**Infrastructure and API (contracts only).** Project scaffolds, the EF Core + PostgreSQL *strategy*
(relational header + JSONB snapshot + append-only history + command log), and the repository/unit-of-work
interfaces the Application depends on. Mappings, migrations and endpoints are explicitly deferred.

**Spec-level decisions resolved.** The review's 15 open decisions each get an MVP default recorded as a
requirement, so no implementer has to invent a mechanic silently.

## Capabilities

### New Capabilities

- `platform/solution-architecture`: the `api/` project tree, .NET 10 target, `.slnx` solution, build
  configuration, dependency-direction rules, and the architecture tests that enforce them.
- `domain/building-blocks`: `Entity`, `AggregateRoot`, `ValueObject`, domain-event recording, `Result`,
  typed ids, `Bounded<T>`, `GameDate`/calendar, and deterministic counter-based random streams.
- `domain/game-session`: the `GameSession` aggregate root — identity, status machine, phase, revision,
  pinned scenario version, simulation version, seed, and the commands it exposes.
- `domain/game-state`: the `GameState` snapshot and its state components, ownership of the game date,
  single-source-of-truth rules, and stable iteration order.
- `domain/world-state`: world variables with polarity and bounds, economy, society, demography,
  provinces, population composition, and the public agenda.
- `domain/political-organizations`: parties, memberships, territorial presence, party relations,
  parliament, government formation, and the party-survival policy.
- `domain/political-actors`: political characters, their traits, character relations, and internal
  reaction behaviour.
- `domain/media-and-agenda`: media outlets, pick-up and framing decisions, coverage facts, pollster
  house effects, and agenda evolution.
- `domain/electorate`: population segments, issue and axis sensitivities, perception records,
  preferences, turnout, polls, elections and seat allocation.
- `domain/content-catalogue`: scenario definitions, event/action definitions, conditions, effect
  definitions, role declarations, the issue catalogue, the attribute registry and content validation.
- `domain/effect-engine`: base/modifier/effective value layering, pending effects, stacking policy,
  change-set atomicity, clamp diagnostics and provenance.
- `domain/stimulus-pipeline`: the typed stimulus record and the authored-vs-computed boundary that keeps
  perceptions, preferences, turnout and seats out of content's reach.
- `domain/simulation-loop`: `AdvanceUntil`, stop conditions, the 12-step daily order, cadence
  definitions, decision points and auto-resolution.
- `application/mediator`: the custom CQRS mediator — request/handler contracts, dispatch, handler
  resolution and the pipeline behaviour chain.
- `application/domain-event-dispatch`: collecting domain events from the aggregate and publishing them
  as notifications after commit, including the domain/notification/simulation-event distinction.
- `application/game-use-cases`: the command and query surface, concurrency and idempotency handling, and
  the `PlayerView` projection boundary.
- `infrastructure/persistence-contracts`: the repository, unit-of-work and clock abstractions the
  Application owns, plus the EF Core + PostgreSQL storage strategy and versioning policy.
- `quality/simulation-test-strategy`: the test projects, the deterministic test doubles, and the
  invariant / effect / event / electoral / determinism / balance suites.

### Modified Capabilities

None. This is the first change in the project; `openspec/specs/` is empty.

## Impact

- **New directories:** `api/src/`, `api/test/`, plus build configuration at `api/`.
- **New projects:** 4 source projects, 6 test projects (two of them scaffolded but deferred).
- **Dependencies:** Domain takes none. Application takes `Microsoft.Extensions.DependencyInjection.Abstractions`
  and `Microsoft.Extensions.Logging.Abstractions` only. Infrastructure takes EF Core + Npgsql.
  Tests take xUnit v3, Shouldly, NSubstitute, Verify and NetArchTest.
- **Not touched:** `game-design/` is the input to this change and is not modified. Any correction to
  v0.1 that this change adopts is recorded in `design.md` under "Deviations from the v0.1 spec", not by
  editing the source documents.
- **Deferred by design:** EF mappings and migrations, HTTP endpoints, authentication, the content JSON
  files themselves, rival-party AI beyond the event-driven phase 1, and the frontend.
- **Risk surface:** the electoral calculators carry the most formula risk and the least feedback; they
  are sliced to be buildable and testable in parallel with everything else.
