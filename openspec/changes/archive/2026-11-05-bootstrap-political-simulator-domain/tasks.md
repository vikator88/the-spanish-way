# Tasks

Ordered as the nine slices in `design.md` D19. Slice 5 depends only on slice 1 and may run in parallel
from the start. Every slice ends in something runnable and tested.

## 0. Solution scaffold

- [x] 0.1 Create `api/Directory.Build.props` with `net10.0`, nullable enabled, implicit usings, latest
      language version and `TreatWarningsAsErrors`
- [x] 0.2 Create `api/Directory.Packages.props` with central package management and pin every version there
- [x] 0.3 Create `api/.editorconfig` with analyzer severities and naming rules
- [x] 0.4 Create the four source projects under `api/src/`: `TheSpanishWay.Domain`, `.Application`,
      `.Infrastructure`, `.Api`
- [x] 0.5 Create `api/TheSpanishWay.slnx` with `dotnet new sln --format slnx` and add every project from
      both `src/` and `test/` by relative path
- [x] 0.6 Wire project references inward only: Api to Infrastructure and Application, Infrastructure to
      Application and Domain, Application to Domain, Domain to nothing
- [x] 0.7 Confirm `TheSpanishWay.Domain.csproj` declares zero `PackageReference` elements
- [x] 0.8 Create the seven test projects under `api/test/`, leaving the infrastructure and functional ones
      empty but compiling
- [x] 0.9 Write `TheSpanishWay.Architecture.Tests` with NetArchTest rules for the inward dependency
      direction, the Domain's banned namespaces, the ban on `System.Random`, and the ban on wall-clock APIs
- [x] 0.10 Confirm `dotnet build` and `dotnet test` succeed on an otherwise empty solution

## 1. Foundations

- [x] 1.1 Implement `Entity<TId>` with identity equality and an immutable id
- [x] 1.2 Implement `AggregateRoot<TId>` with `RecordEvent`, read-only `DomainEvents` and `ClearDomainEvents`
- [x] 1.3 Implement `ValueObject` with structural equality
- [x] 1.4 Implement `IDomainEvent` carrying a `GameDate`, with no dispatch concept
- [x] 1.5 Implement `Result`, `Result<T>` and `Error` with stable machine-readable codes
- [x] 1.6 Implement the strongly-typed id structs listed in the building-blocks spec, each with a total ordering
- [x] 1.7 Implement `Bounded<T>` with inclusive bounds, clamping and truncation diagnostics
- [x] 1.8 Implement `GameDate` with arithmetic, ordering and week, month and quarter boundary detection
- [x] 1.9 Implement the counter-based `IRandomSource` hashing `(seed, stream, game day, index)`, with the
      seven named streams
- [x] 1.10 Implement `RandomStreams` state holding the seed and each stream's current-day index
- [x] 1.11 Write unit tests for identity equality, structural equality, bounded clamping, calendar
      arithmetic and cadence boundaries
- [x] 1.12 Write the cross-process determinism test: the same seed, stream, day and index yield the same
      draw in two processes
- [x] 1.13 Write the stream-isolation test: an extra draw on one stream does not shift another

## 2. Session and loop skeleton

- [x] 2.1 Implement `GameStatus` with `Created`, `Running`, `AwaitingDecision`, `Paused`, `Finished` and
      `Abandoned`, and `GamePhase` as an orthogonal enum
- [x] 2.2 Implement `GameSession` with its immutable header, pinned scenario version, simulation version,
      seed, owner and revision
- [x] 2.3 Implement the status machine with every permitted transition and explicit rejection of the rest
- [x] 2.4 Implement `GameState` holding the date, phase, random stream indices and empty component slots
- [x] 2.5 Enforce that the date lives only in `GameState` and advances monotonically
- [x] 2.6 Implement `SchemaVersion` and the upcaster chain contract, with one identity upcaster registered
- [x] 2.7 Implement `AdvanceUntil(stop, maxDays)` over an empty world with the stop-condition types
- [x] 2.8 Implement the session-level domain events `GameCreated`, `GameStarted`, `GamePaused`,
      `GameResumed`, `GameAbandoned`, `DecisionRequired`, `DecisionResolved` and `GameEnded`
- [x] 2.9 Implement in-memory snapshot serialisation sufficient for round-trip tests
- [x] 2.10 Write status-machine tests covering every permitted and forbidden transition
- [x] 2.11 Write the long-advance test: 1,461 days over an empty world, one revision increment
- [x] 2.12 Write the save-reload-resume test: advance, serialise, reload in a fresh context, advance again,
      and assert equality with an uninterrupted run

## 3. World variables and the effect engine

- [x] 3.1 Implement `WorldVariable` with key, base value, bounds, polarity, target value and evolution rate
- [x] 3.2 Implement the three-layer value model: base, active modifiers, and effective value computed in
      the fixed order of `Set`, then `Add`, then `Multiply`, then clamp
- [x] 3.3 Implement `Modifier` with provenance, expiry, stack key and policy
- [x] 3.4 Implement `PendingEffect` with `ApplyAt`, resolved value, recurrence and status
- [x] 3.5 Implement the attribute registry mapping `(target type, attribute key)` to type, bounds, polarity
      and permitted operations
- [x] 3.6 Implement the change-set model: propose, validate everything, then apply everything
- [x] 3.7 Implement the four stacking policies with `RefreshDuration` as the default and the default stack key
- [x] 3.8 Implement clamp truncation as a reported diagnostic
- [x] 3.9 Implement provenance capture with the `External` and `Policy` origin tags
- [x] 3.10 Implement resolution of random effect values at instantiation, stored on the pending effect
- [x] 3.11 Implement `EconomyState` and `SocietyState` over generic variables, with the five indicators each
- [x] 3.12 Implement weekly economic and monthly social evolution with per-variable rates
- [x] 3.13 Write the effect suite from `domain/effect-engine`, including the clamped-expiry case returning
      to exactly 9.5
- [x] 3.14 Write the byte-identical rollback test for a failed change set

## 4. Content, conditions and events

- [x] 4.1 Define the issue catalogue model with positional axis loadings and valence variable links
- [x] 4.2 Implement `ScenarioDefinition` as an immutable-once-published aggregate with version references
- [x] 4.3 Implement `Condition`, the operator set, and `ConditionGroup` with AND, OR and nesting
- [x] 4.4 Implement the condition evaluator against game state and role bindings, with no expression compilation
- [x] 4.5 Implement `EffectDefinition` as data, validated against the attribute registry
- [x] 4.6 Implement role declarations with selection rules, and the built-in roles `$player`,
      `$government.lead` and `$self`
- [x] 4.7 Implement role binding at trigger time from the `bindings` stream, with "no candidate means not
      eligible"
- [x] 4.8 Implement `GameEventDefinition` with category, base probability, trigger conditions, roles,
      effects, available actions, follow-ups, the blocking flag, default option, duration, cooldown and tags
- [x] 4.9 Implement `EventOccurrence` with bindings, status, expiry and single-resolution enforcement
- [x] 4.10 Implement `EventScheduler`: eligibility, cooldown, probability rolls from the `events` stream,
      and follow-up eligibility
- [x] 4.11 Implement the JSON content loader and JSON schema, and wire validation at startup and in the build
      <!-- Deferred to group 13: System.Text.Json is banned in the Domain by an
           architecture rule, so the loader belongs in Infrastructure. The content model
           and ContentValidator it will feed are complete and tested. -->
- [x] 4.12 Implement the content validator rejecting unknown attributes, unpermitted operations, type
      mismatches, literal ids in reusable definitions, and non-normalisable weight sets
- [x] 4.13 Implement `AwaitingDecision` entry and exit driven by blocking occurrences
- [x] 4.14 Write the event suite from `domain/events`, including a scandal binding a party and a character
- [x] 4.15 Write the content-validation tests asserting an unknown attribute fails at load

## 5. Electoral calculators — parallel track

- [x] 5.1 Implement `IdeologicalProfile`, `PartyIdeology` and the six axes as value objects
- [x] 5.2 Implement `PopulationSegment` with its axis-weight vector, issue-weight vector and behavioural
      parameters
- [x] 5.3 Implement per-province segment ideology offsets
- [x] 5.4 Implement `IdeologicalAffinityCalculator` using perceived position and province offsets
- [x] 5.5 Implement `SegmentPartyPerception` holding trust, respect and position drift, and no affinity
- [x] 5.6 Implement `PoliticalUtilityCalculator` with components centred to −1 to +1 and weights summing to 1
- [x] 5.7 Implement the stable softmax over parties standing in the constituency plus blank vote
- [x] 5.8 Implement `TurnoutCalculator` as a separate model deciding whether a segment votes
- [x] 5.9 Implement preference inertia with the per-30-day lambda conversion and renormalisation
- [x] 5.10 Implement `ProvincialVoteCalculator` aggregating population by turnout by probability
- [x] 5.11 Implement `ElectoralSystem` configuration: chamber size, constituencies, minimum seats,
      largest-remainder distribution and threshold base
- [x] 5.12 Implement `SeatAllocationCalculator` with D'Hondt and tie resolution by total votes then by lot
      on the `ties` stream
- [x] 5.13 Implement per-constituency candidacies
- [x] 5.14 Apply the decimal-to-double boundary policy with rounding to six places at the calculator edge
- [x] 5.15 Write the two calibration tests before any content exists
- [x] 5.16 Write the electoral suite from `domain/electorate`, including a hand-checked D'Hondt case

## 6. Actions

- [x] 6.1 Implement `ActionDefinition` with category, availability conditions, cost, effects, emitted
      stimuli, duration, cooldown, tags and parameter schema
- [x] 6.2 Implement `ActionExecution` with parameters, status, applied effects and result summary
- [x] 6.3 Implement one handler per category: `PublicPosition`, `ParliamentaryVote`, `InternalParty`
- [x] 6.4 Implement `ActionAvailabilityPolicy` covering conditions, cooldowns and the weekly allowance
- [x] 6.5 Implement atomic cost validation and payment within the action's change set
- [x] 6.6 Implement action execution timing: immediate effects now, stimuli queued for the next day
- [x] 6.7 Implement status gating: permitted in `Running` and `AwaitingDecision`, refused elsewhere
- [x] 6.8 Implement the `ActionExecuted` domain event and the execution history record with provenance
- [x] 6.9 Write the action suite, including the byte-identical snapshot assertion after a failed action

## 7. Stimulus pipeline, media and agenda

- [x] 7.1 Implement the `Stimulus` value object with its validation rules for kind, stance and intensity
- [x] 7.2 Implement the stimulus queue with deterministic processing order at step 8
- [x] 7.3 Implement `MediaOutlet` with orientation, audience, credibility, sensationalism, independence,
      party affinity and issue interest
- [x] 7.4 Implement outlet memory of recent framing, with decay
- [x] 7.5 Implement `MediaCoverageCalculator`: pick-up chance, framing, tone and reach, drawing from the
      `media` stream
- [x] 7.6 Implement structured `MediaCoverage` facts, with text rendering left outside the Domain
- [x] 7.7 Implement `ReactionCalculator` producing per-segment perception deltas from agenda salience,
      issue sensitivity, ideological fit for positional stimuli, blame for valence stimuli, and media
      sensitivity
- [x] 7.8 Enforce that the reaction pipeline is the only writer of perception records
- [x] 7.9 Implement perception decay toward the structural baseline with per-dimension half-lives
- [x] 7.10 Implement `PublicAgenda` and `AgendaTopic` with normalised emergent relevance and decay
- [x] 7.11 Implement per-segment leader evaluation derived from popularity, charisma and fit
- [x] 7.12 Write the test asserting one stimulus moves six segments by six different amounts
- [x] 7.13 Write the test asserting content cannot target a perception, preference, turnout or seat

## 8. Polls, elections, parliament and government

- [x] 8.1 Implement `Parliament` owning seats and majority calculations for a 350-seat chamber
- [x] 8.2 Implement `GovernmentState` with lead party, coalition, external support, dates and stability
- [x] 8.3 Implement government formation with the first-vote absolute majority and second-vote simple
      majority rules
- [x] 8.4 Implement the repeat-election rule when no government forms within two months
- [x] 8.5 Implement parliamentary voting by rule for non-player parties, from ideological distance and trust
- [ ] 8.6 Implement bills arriving as government-proposed simulation events
- [x] 8.7 Implement `Poll`, `Pollster` and persistent house effects stored in state
- [x] 8.8 Implement monthly poll generation from turnout-weighted hidden preferences with sampling noise
      from the `polls` stream
- [x] 8.9 Implement `Election`, `ProvincialElectionResult` and the election-day pipeline
- [x] 8.10 Implement the `ElectionCampaign` phase of 15 days with raised volatility and media attention
- [x] 8.11 Implement the four-year election cycle and the forced repeat after a failed investiture
- [x] 8.12 Implement `GovernmentPerformanceCalculator` reading the `External` and `Policy` provenance tags
      with the per-indicator attribution shares
- [x] 8.13 Implement the `PollPublished`, `ElectionCompleted` and `GovernmentFormed` domain events and
      their history records
- [x] 8.14 Write the test asserting a full legislature produces a government

## 9. Survival, player view and balance

- [x] 9.1 Implement `PartySurvivalPolicy` with the configurable month-end dissolution conditions and the
      documented edge cases for zero seats, leaderless parties and mergers
- [x] 9.2 Implement `GameEndPolicy` and the `GameOver` phase
- [x] 9.3 Implement the `PartyDissolved` and `GameEnded` domain events
- [x] 9.4 Implement per-indicator visibility configuration: exact, noisy, delayed or hidden
- [x] 9.5 Implement the `PlayerView` projection with seed-derived reproducible noise
- [x] 9.6 Write the balance suite: 200 seeds over a full legislature, with bands held in configuration
- [x] 9.7 Write the test asserting no player-facing response carries raw `GameState`, a hidden
      preference, a perception record or a utility value

## 10. Application layer — mediator and CQRS

- [x] 10.1 Implement `ICommand`, `ICommand<TResponse>`, `IQuery<TResponse>` and the three handler interfaces
- [x] 10.2 Implement `ISender`, `IPublisher`, `IMediator` and `INotification`
- [x] 10.3 Implement the dispatcher: close the open generic handler interface over the runtime request type,
      resolve from the container, and invoke through a cached compiled delegate
- [x] 10.4 Implement handler registration by assembly scan, plus a registration validator that fails at
      startup on a missing or duplicate handler
- [x] 10.5 Implement `IPipelineBehavior<TRequest, TResponse>` and the behaviour chain composition
- [x] 10.6 Implement `LoggingBehavior` with a structured scope carrying request name, game id and correlation id
- [x] 10.7 Implement `ValidationBehavior` restricted to request shape
- [x] 10.8 Implement `IdempotencyBehavior` keyed on `ClientCommandId` per game
- [x] 10.9 Implement `UnitOfWorkBehavior` opening the transaction, committing, and handing collected domain
      events to the dispatcher after commit
- [x] 10.10 Implement `ConcurrencyRetryBehavior` with bounded retry for commands declared safe to retry
- [x] 10.11 Ensure queries bypass idempotency, unit of work and concurrency retry
- [x] 10.12 Write the dispatcher test suite, including open-generic resolution, delegate caching, and the
      startup validation failures

## 11. Application layer — domain event dispatch

- [x] 11.1 Implement `DomainEventNotification<TDomainEvent>` as the adapter from `IDomainEvent` to `INotification`
- [x] 11.2 Implement collection and clearing of `DomainEvents` from the aggregate inside the unit of work
- [x] 11.3 Implement post-commit publication, with no publication on rollback
- [x] 11.4 Implement `INotificationHandler<TNotification>` and handler discovery
- [x] 11.5 Implement handler isolation so that a throwing handler logs and does not fail the command
- [x] 11.6 Add the folder conventions that keep domain events, notifications and simulation events apart,
      and assert them in the architecture suite
- [x] 11.7 Write the ordering tests: commit precedes publication, and rollback publishes nothing

## 12. Application layer — use cases

- [x] 12.1 Implement the commands `CreateGame`, `StartGame`, `AdvanceUntil`, `ExecuteGameAction`,
      `ResolveEvent`, `PauseGame`, `ResumeGame` and `AbandonGame`
- [x] 12.2 Implement the queries `GetGameOverview`, `GetPlayerView`, `GetAvailableActions`,
      `GetActiveDecisions`, `GetPollHistory`, `GetElectionResults`, `GetNewsFeed` and `GetGameHistory`
- [x] 12.3 Implement `GetGameState` in an operator-only namespace, returning the complete `GameState`
      behind an operator-role authorisation check and unreachable from the player-facing surface
- [x] 12.4 Add the architecture test asserting no player-facing endpoint returns `GetGameState`'s result
- [ ] 12.5 Implement ownership authorisation in the Application layer
- [x] 12.6 Implement the command log append with type, parameters, client command id and resulting revision
- [ ] 12.7 Implement the DTOs and mapping from `PlayerView` to responses
- [ ] 12.8 Write the handler tests asserting handlers contain no business rule, using a stubbed domain
- [ ] 12.9 Write the replay test: replay a command log with the original seed and assert the same final state

## 13. Infrastructure and API scaffold

- [x] 13.1 Declare `IGameSessionRepository`, `IScenarioRepository`, `IHistoryStore`, `ICommandLog`,
      `IUnitOfWork` and `IClock` in the Application layer
- [x] 13.2 Scaffold `TheSpanishWay.Infrastructure` with EF Core and Npgsql package references and an empty
      `DbContext`
- [x] 13.3 Document the storage strategy in the project: relational header, JSONB snapshot, monthly
      snapshots, append-only history, append-only command log
- [x] 13.4 Scaffold the composition root in `TheSpanishWay.Api` registering Application and Infrastructure
      services, with no endpoints
- [x] 13.5 Confirm the deferred scope: no mappings, no migrations, no endpoints in this change
- [x] 13.6 Confirm the solution builds and the full test suite passes

## 14. Review gate

- [x] 14.1 Walk the eighteen capability specs against the two game-design documents and confirm every rule
      is either implemented, explicitly deferred, or recorded as a deviation in `design.md`
- [x] 14.2 Confirm every number in the model reads from configuration rather than a constant
- [x] 14.3 Confirm the seven high-severity review issues are each closed by a named requirement
- [ ] 14.4 Record any decision the user changes during review back into `design.md` before archiving
