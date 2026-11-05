# Design

## Context

Two input documents govern this change and they do not fully agree:

- `game-design/political-simulator-domain-context.md` (v0.1) — the domain specification.
- `game-design/Political Simulator — Domain Design Review.md` — an audit of v0.1 listing 24 issues
  (7 high severity) and proposing fixes.

**Where the two conflict, the review wins.** v0.1 remains the source of truth for everything the review
does not contest (gameplay principles, indicator lists, MVP content targets, the general shape of the
electoral maths). Every deviation is listed in "Deviations from the v0.1 spec" below so a reader of v0.1
is never surprised.

`api/` is empty. There is no legacy code, no database, no deployed consumer, and no migration burden.
The only hard external constraints are the ones the user set for this change:

- .NET 10, Clean Architecture, four projects under `api/src/`, an `.slnx` solution, a `api/test/` tree.
- DDD tactical patterns scaffolded explicitly (`Entity`, `AggregateRoot`, `ValueObject`, …).
- CQRS in the Application layer over a **custom, hand-written mediator** (not MediatR).
- Domain events raised in the Domain and dispatched through that mediator in the Application layer.
- EF Core against PostgreSQL as the ORM; infrastructure detail deliberately out of scope for now.

## Goals / Non-Goals

**Goals:**

- A Domain project that compiles against the BCL alone and is fully unit-testable with no database,
  no HTTP, no DI container and no wall clock.
- Aggregate boundaries that match the real transactional boundary (one snapshot, one revision) instead
  of matching the vocabulary of the domain.
- A model where **no content author can place a vote by hand** — perceptions, preferences, turnout and
  seats are computed, never authored.
- A simulation that is bit-reproducible from `(scenario version, simulation version, seed, ordered
  commands)` on a fixed platform, and whose reproducibility does not break when an unrelated subsystem
  adds a random draw.
- A game loop that advances a whole legislature in one command, with one write and one revision bump.
- An Application layer thin enough that moving a rule into a handler is visibly wrong.
- A structure the user can review and adapt file-by-file, with every underspecified rule exposed as a
  named configuration value rather than buried in a formula.

**Non-Goals:**

- EF Core mappings, migrations, the physical schema, connection management or any Npgsql detail. The
  Infrastructure project is scaffolded with its dependency wiring and left otherwise empty.
- HTTP endpoints, request/response contracts, authentication, authorisation or OpenAPI.
- The content JSON itself (30 event definitions, 5 parties, 52 provinces, …). This change defines the
  schema, the validator and the attribute registry; populating them is a content task.
- Rival-party AI beyond "phase 1: rivals act through events whose `$actor` role binds a rival".
- Regional and municipal parliaments, individual-citizen simulation, sector-level economics, natural
  language generation, and everything else v0.1 §13 defers.
- Balance. Every number in this design is a placeholder behind a named configuration key.

## Decisions

### D1. Solution layout and project boundaries

```text
api/
  Directory.Build.props          # net10.0, nullable, warnings-as-errors, LangVersion latest
  Directory.Packages.props       # central package management, all versions pinned here
  .editorconfig                  # analyzer severities, naming rules
  TheSpanishWay.slnx             # XML solution, references both src/ and test/ projects
  src/
    TheSpanishWay.Domain/        # no package refs beyond the BCL
    TheSpanishWay.Application/   # -> Domain
    TheSpanishWay.Infrastructure/# -> Application, Domain
    TheSpanishWay.Api/           # -> Infrastructure, Application (composition root only)
  test/
    TheSpanishWay.TestKit/                   # builders, fakes, deterministic sources
    TheSpanishWay.Domain.UnitTests/
    TheSpanishWay.Application.UnitTests/
    TheSpanishWay.Simulation.Tests/          # determinism, replay, seed sweeps
    TheSpanishWay.Architecture.Tests/        # NetArchTest layering rules
    TheSpanishWay.Infrastructure.IntegrationTests/  # scaffolded, deferred
    TheSpanishWay.Api.FunctionalTests/              # scaffolded, deferred
```

The `.slnx` sits at the `api/` root, above both `src/` and `test/`, so that one solution file sits
beside the shared build configuration and references both trees by a symmetric relative path.

`.slnx` is the XML solution format; it is the default for `dotnet new sln --format slnx` on .NET 10 and
needs no `.sln` alongside it.

**Dependency rule.** References point inward only: `Api -> Infrastructure -> Application -> Domain`.
`Api` may also reference `Application` directly (it builds command objects). Nothing references `Api`.
The Domain's `.csproj` carries zero `PackageReference` elements, which is the cheapest possible proof
that no infrastructure concern leaked in. `TheSpanishWay.Architecture.Tests` asserts this with
NetArchTest so the rule survives a careless `dotnet add package`.

### D2. One mutable aggregate, not one aggregate per noun

v0.1 §4.4 proposes `PoliticalParty` as its own aggregate root. The review's issue #1 rejects this, and
the rejection is correct for a concrete reason: v0.1 §6.1 requires *every* state-changing operation to
be atomic under one game `Revision`, and §11 persists one `GameState` snapshot. A daily tick mutates
parties, characters, world variables, perceptions and queues together. Two aggregates under one
transaction is the exact anti-pattern aggregates exist to prevent.

So the running world is **one** aggregate:

| Unit | Kind | Owns | Persisted as |
| --- | --- | --- | --- |
| `GameSession` | Aggregate root | Lifecycle, status, revision, versions, seed, owner | Relational row |
| `GameState` | Owned by `GameSession`, always loaded with it | All mutable simulation state; the game date lives here and only here | One JSONB snapshot |
| `WorldState`, `PartyState`, `CharacterState`, `ElectorateState`, `MediaState`, `Parliament`, `GovernmentState` | State components — entity-like, not aggregates | Their own local invariants (bounds, membership, leadership) | Inside the snapshot |
| `ScenarioDefinition` | Separate aggregate, immutable once published | Catalogues, initial values, parameters, electoral law | Versioned rows / JSON |
| `ElectionRecord`, `PollRecord`, `ExecutionRecord`, `OccurrenceRecord` | Append-only history | Facts that never change after creation | Relational, insert-only |

v0.1's real fear — one god-class `Game` with every rule in it — is answered by **internal modularity**,
not by more aggregates. `GameSession` is a thin façade: it validates status and revision, delegates to
pure domain services, and records domain events. The simulation logic lives in `Simulation/` services
that take state components as parameters.

Consequences:

- Only `GameSession` exposes commands (`Start`, `AdvanceUntil`, `ExecuteAction`, `ResolveEvent`,
  `Pause`, `Resume`, `Abandon`). State components expose intention-revealing methods
  (`party.ChangeLeader(...)`, `party.Expel(...)`) that the session or a domain service calls — never
  public setters.
- `GameSession.CurrentDate` and `GameSession.Phase` **hold nothing of their own**. Both the date and the
  phase live in `GameState`, because both are mutable simulation state that the tick advances. The session
  exposes them as read-through properties and projects them onto the relational header for querying, so
  there is exactly one owner and no pair of fields that can disagree. v0.1 declares the date in both
  places (review issue #8); an earlier draft of this document did the same with the phase.
- A game pins a **published scenario version**, not a scenario id. Editing a scenario can never change a
  running game (review issue #15).
- History records are written in the same transaction as the snapshot but are **never read back to
  compute the next state**. The snapshot alone must be sufficient to resume.

### D3. Eight domain modules inside one project

Folders in `TheSpanishWay.Domain`, each owning its state types and its pure calculators. Dependencies
point inward to `Common` and `Shared`, and to `Content` for authored definitions.

| Module | Owns (state) | Pure services |
| --- | --- | --- |
| `Common` | DDD tactical patterns only: `Entity<TId>`, `AggregateRoot<TId>`, `ValueObject`, `IDomainEvent`, `Result`, `Error` | — |
| `Shared` | The game's shared kernel, classified by kind: `ValueObjects/` (`GameDate`, `Bounded<T>`, `RandomSeed`), `Identifiers/`, `Enumerations/`, `Services/` (`IRandomSource`) | Calendar arithmetic, cadence boundaries, counter-based draws |
| `Content` | Definitions: scenario, events, actions, conditions, effects, issues, electoral law | Content validator, condition evaluator, attribute registry |
| `World` | Variables, provinces, composition, public agenda | Variable evolution, agenda calculator |
| `Politics` | Parties, memberships, presence, relations, government, parliament | Government formation, survival policy |
| `Actors` | Characters, character relations | Internal reaction calculator |
| `Media` | Outlets, coverage facts, pollster house effects | Coverage calculator |
| `Electorate` | Segments, perceptions, preferences, polls, elections | Affinity, utility, turnout, preference, provincial vote, D'Hondt |
| `Simulation` | Tick pipeline, effect engine, modifier store, queues | `SimulationEngine`, `EffectEngine`, `EventScheduler`, stimulus pipeline |

Two renames adopted from the review:

- `PoliticalParty.Seats` → a `Parliament` component. Seats are an election outcome, not a party attribute.
- `PartyMembership.Loyalty` → `InternalSupport`, so it is not confused with `PoliticalCharacter.Loyalty`,
  which is a personality trait.

### D3a. `Common` is pattern, `Shared` is vocabulary

The foundation splits in two, by whether a type would read the same in a completely
different domain:

- **`Common`** holds the DDD tactical patterns alone — `Entity<TId>`, `AggregateRoot<TId>`,
  `ValueObject`, `IDomainEvent`, `Result`, `Error`. Lifted into a banking system they would
  need no edit.
- **`Shared`** holds this game's shared kernel, and classifies each type by *what it is*:
  `ValueObjects/`, `Identifiers/`, `Enumerations/`, `Services/`. `GameDate` is a value object
  of this game, not a DDD concept, so it lives there.

Both halves are enforced by `DomainStructureTests`: a whitelist pins `Common`'s contents, and
every `Shared` type must sit in a classified folder whose kind it actually satisfies.

One deliberate seam: `IDomainEvent` stays in `Common` but its `OccurredOn` is a
`Shared.ValueObjects.GameDate`. Recording *when* a fact happened is a DDD concern; that this
game measures time on its own calendar rather than a wall clock is game vocabulary. Rather than
split the interface in two, `Common` takes that single dependency on `Shared`.

### D4. The authored/computed boundary — stimuli

This is the decision that protects the product's central promise.

v0.1 §5.4 lists "a population segment's perception of a party" as a legal effect target. Perception feeds
electoral utility almost linearly, so an author writing `trust(retirees, PartyX) -= 15` is placing votes
by hand — and is also deciding *who* reacts, which destroys the heterogeneous-reaction principle of
§2.2.4.

One stage is inserted. Actions and events emit **stimuli**; only the reaction pipeline writes perceptions.

```text
authored:  ActionDefinition / GameEventDefinition
                     |
                     v  emits
              Stimulus (typed record)
                     |
  computed:          v
   media pick-up -> framing -> reach
                     |
                     v
   per-segment reaction (ideology, issue sensitivity, agenda salience, media sensitivity)
                     |
                     v
          perception delta (trust, respect, position drift)
                     |
                     v
   electoral utility -> softmax -> turnout -> provincial votes -> D'Hondt -> seats
```

A stimulus is a small typed value object:

| Field | Type | Example | Used by |
| --- | --- | --- | --- |
| `Actor` | `ActorRef` | the party bound to role `$accused` | who is judged |
| `Issue` | `IssueId` | `corruption`, `pensions`, `housing` | agenda salience, segment sensitivity |
| `Kind` | `StimulusKind` | `Positional` (takes a stance) or `Valence` (competence/blame) | whether ideology matters |
| `Stance` | `AxisPosition?` | −100..+100 on the issue's axis; null for valence | ideological fit |
| `Intensity` | `UnitInterval` | 0..1 | magnitude before filtering |
| `Visibility` | `Visibility` | `Public`, `Parliamentary`, `Internal` | media pick-up probability |

Effect targets split in two:

- **Content may write:** world variables, party attributes, character attributes, party relations,
  character relations, agenda topic relevance, party resources, provincial presence.
- **Content may not write:** segment perceptions, electoral preferences, turnout, seats. These are
  reachable only through stimuli.

A scenario-level escape hatch exists for a scripted opening shock. It is a distinct effect kind,
`ScriptedPerceptionOverride`, and the **default content validator rejects it**; a scenario must opt in
explicitly. This keeps it available for authored prologues without making it the path of least resistance.

### D5. Effect engine — base values plus a modifier layer

v0.1 §5.4 says temporary effects "expire or decay" but never says how. Subtracting on expiry is wrong
whenever a bound, another modifier or a multiplication intervened. The worked case: inflation 9.5,
ceiling 10, a temporary `+2` clamps to `+0.5`; subtracting 2 on expiry leaves 8.0 instead of 9.5.

Every numeric attribute therefore has three layers:

- **Base value** — changed only by permanent effects and by natural evolution. Clamped on write.
- **Active modifiers** — temporary effects, each carrying source, expiry and stack key. **Never folded
  into the base.**
- **Effective value** — computed on read in a fixed, documented order, because `Add` and `Multiply` do
  not commute:

  ```text
  effective = clamp( ( base  applied-with  Set-overrides
                            then  sum of Add modifiers
                            then  product of Multiply modifiers ), bounds )
  ```

Expiry is then just removing the modifier — exact by construction. Clamping becomes a reportable
diagnostic ("effect truncated by 1.5 at upper bound") instead of silent loss.

Mapping v0.1's required behaviours onto the mechanism:

| v0.1 behaviour | Mechanism |
| --- | --- |
| Immediate, permanent | Base mutation now |
| Immediate, temporary | Modifier now, with expiry |
| Delayed | `PendingEffect` that becomes one of the above on its `ApplyAt` date |
| Gradual / recurring | Recurring `PendingEffect` with a step value and a count |
| Trajectory change | Modifier on `TargetValue` or `EvolutionRate` — never on `CurrentValue` |

Engine rules:

- **Attribute registry.** One code-side whitelist maps each `(target type, attribute key)` to its CLR
  type, bounds, polarity and allowed operations. Conditions and effects validate against it at content
  load. An unknown key fails validation; it never silently evaluates as true or no-ops.
- **Randomness resolved at instantiation.** A "+1 to +3" value is rolled when the effect is *created*
  and the rolled value is stored on the `PendingEffect`. Saved games then explain themselves, and
  reloading cannot reroll.
- **Default stack key** is `(source definition, effect index, target, attribute)`. Policy is per effect:
  `Stack`, `RefreshDuration` (the default), `Replace`, `IgnoreDuplicate`.
- **Atomicity via change set.** Effects first produce a list of proposed `StateChange` records; the
  engine validates all of them, then applies all of them. A failure anywhere rejects the whole command
  with nothing written. This is how v0.1 §5.2's "a failed action must not partially mutate the game
  state" is actually achieved.
- **Provenance on every change.** Each base mutation and each modifier carries its source (execution or
  occurrence id, effect index) and an `External` / `Policy` origin tag. Both the player-facing
  explanation ("why did my support fall?") and government attribution read that tag.

### D6. Role bindings make 30 definitions reusable

A definition declares **roles** with selection rules; the occurrence stores the bound ids; conditions,
effects, text templates and stimuli refer to roles, never to literal ids.

| Role | Selection rule (data) | Bound at |
| --- | --- | --- |
| `$accused` | a party with credibility below 60 | trigger, chosen on the `bindings` random stream |
| `$figure` | a member of `$accused` whose role is Leader or Spokesperson | trigger |
| `$player`, `$government.lead`, `$self` | built-in references | always available |

If a required role has no candidate, **the event is not eligible**. That one rule replaces a large number
of hand-written trigger conditions.

### D7. The game loop — `AdvanceUntil`, not `AdvanceGameDay`

A legislature is 1,461 days. One command per day is unplayable and writes 1,461 snapshots. The loop runs
days **in memory** and persists once per command with one revision increment.

`AdvanceUntil(StopCondition stop, int maxDays)` stops when: a blocking event needs a decision, an
election or phase change is reached, the game ends, or `maxDays` is exhausted.

v0.1 §8 step 7 ("execute any submitted player action") sits inside the day loop. It is moved out: player
actions are separate commands that run **between** days.

**Order inside one day** (v0.1 §8 applies pending effects before expiring old ones; this inverts that,
because an effect with `RefreshDuration` reapplied on its expiry day must then stay alive):

1. Date + 1; compute cadence flags (weekly / monthly / quarterly / election).
2. Expire modifiers and events.
3. Apply due pending effects, ordered by `(ApplyAt, Priority, Sequence)`.
4. World evolution when due — economy weekly, society monthly, demography quarterly.
5. Autonomous actors act — government and rival parties, from the same action catalogue.
6. Event generation — eligible definitions, cooldowns, bind roles, roll.
7. Auto-resolve non-blocking events.
8. Stimuli → media coverage → reactions → perceptions.
9. Agenda, preferences and polls when due.
10. Election day: votes, turnout, provincial aggregation, seats, phase → `GovernmentFormation`.
11. Survival and game-over policy.
12. Stop condition met? If not, loop. If yes, persist once and `Revision + 1`.

Every loop iterates in a **stable order (by id)**, never in dictionary order.

Player action timing: an action executes on the current date *after* that day's tick. Its immediate
effects apply at once; its stimuli are queued and processed in step 8 of the next day, so media and
reactions always run in exactly one place.

Cadence definitions v0.1 leaves open: **"weekly" means every 7 days counted from the game's start date**
(not ISO Monday — it keeps `AdvanceUntil` independent of the scenario's start weekday). Monthly means the
first day of a new calendar month; quarterly means the first day of January, April, July or October.
Preferences recalculate **weekly**; they are not visible daily anyway.

### D8. Status machine — `Paused` and `AwaitingDecision` are different

v0.1 §4.1 offers `Created / InProgress / Paused / Finished / Abandoned`, which conflates "the player
chose to stop" with "the simulation cannot continue". They need different values because they permit
different commands.

```text
Created --Start--> Running
Running --blocking event raised--> AwaitingDecision
AwaitingDecision --ResolveEvent (all blocking resolved)--> Running
Running --Pause--> Paused
AwaitingDecision --Pause--> Paused
Paused --Resume (no blocking events)--> Running
Paused --Resume (blocking events open)--> AwaitingDecision
Running | AwaitingDecision --terminal condition--> Finished
Running | AwaitingDecision | Paused --Abandon--> Abandoned
```

`GamePhase` (`Normal`, `ElectionCampaign`, `ElectionDay`, `GovernmentFormation`, `GameOver`) is
**orthogonal** to status.

Voluntary actions are allowed in `Running` and `AwaitingDecision` — answering a scandal with a press
conference is a voluntary action. They are refused in `Created`, `Paused`, `Finished` and `Abandoned`.

### D9. Electoral model corrections

The formulas in v0.1 §7 are a sound skeleton. Four of them conflict with each other or with the Spanish
system and are decided here.

**1. Issues and axes are two different weight vectors.** v0.1's affinity formula weights six *axes*;
`IssueSensitivity` weights `IssueType`, which v0.1 never defines. An **issue catalogue** is introduced:
each issue is either *positional* (loads onto one or more axes; pensions → economy) or *valence* (no
stance, only competence or blame; corruption, unemployment). Segments carry **both** an axis-weight
vector and an issue-weight vector. Target: about 12 issues.

**2. Affinity must not be counted twice.** v0.1 §7.2 stores affinity in the perception record *and*
computes it in §7.1. Decision: ideological fit is computed from the party's **perceived** position and is
read exactly once by the utility function. The perception record stores `Trust`, `Respect` and a
per-segment-per-party `PositionDrift` (seen as more extreme or more moderate than declared) — not affinity.

**3. Abstention appears twice.** v0.1 §7.4 puts abstention inside the softmax *and* computes turnout
separately. Decision: **turnout decides whether a segment votes; the softmax decides whom**, over the
parties standing in that constituency plus blank vote. "Undecided" is a polling concept and never appears
in the hidden state.

**4. Inertia is time-step dependent.** If `lambda` is applied on every recalculation, changing the cadence
from weekly to daily silently changes the whole game. Volatility is expressed **per 30 days** and converted:

```text
lambda(dt) = 1 - (1 - lambda_30) ^ (dt / 30)
```

**Scales.** Every utility component is centred to `[-1, 1]` before weighting (a 0–100 value `x` becomes
`(x - 50) / 50`), with segment weights summing to 1. Temperature then has an interpretable range of
roughly 0.1–0.3. Softmax is the numerically stable form (subtract the maximum before exponentiating).

```text
U(s,k,p)     = sum_c  w(s,c) * xhat(c, s, k, p)
Pr(p | s, k) = exp( (U(p) - Umax) / T(s) ) / sum over parties standing in k
```

Two calibration tests are written **before any content**: identical parties get identical shares, and a
0.1 utility advantage produces a documented share gain.

**Spanish rules v0.1 simplifies**, all moved into per-scenario `ElectoralSystem` configuration:

| Rule | v0.1 | Configured rule |
| --- | --- | --- |
| Seat distribution | "configurable" | 2 seats minimum per province, 1 each for Ceuta and Melilla, the remaining 248 by population via largest remainder |
| Threshold base | 3% | 3% of **valid** votes in the constituency, which includes blank votes |
| D'Hondt ties | "tie handling" | more total votes wins; then by lot on the seeded `ties` stream |
| Candidacies | not modelled | a party stands only in some constituencies; regional parties must be possible |

**Regional realism.** Six national segments with identical ideology everywhere makes provinces differ only
by composition, which cannot produce Basque, Catalan or Canarian regionalist vote — and that vote decides
governments under this electoral system. Minimum fix adopted: a **per-province ideology offset per
segment** (at least on the territory axis), plus per-constituency candidacies.

**Polls** are generated from turnout-weighted hidden preferences, with sampling noise from the sample
size (standard error `sqrt(p(1-p)/n)`) and a **persistent house effect per pollster stored in state** so
it stays stable across months. Hidden preference, poll estimate and election result are three distinct
things and are never conflated.

### D10. Determinism — counter-based streams, not one sequence

v0.1 §12 asks for determinism but specifies one global random sequence. With one sequence, adding a single
draw in the media model shifts every later event roll, so any content tweak or bug fix changes the whole
game and replay tests break for unrelated reasons.

- **Counter-based generator.** Each draw is a hash of `(seed, stream, game day, index)`. The platform
  `Random` is not used at all — its seeded algorithm is a .NET implementation detail, not a contract.
- **One named stream per subsystem:** `events`, `bindings`, `media`, `reactions`, `actors`, `polls`, `ties`.
- **Persist only the seed and each stream's index for the current day.** Resuming is then exact and
  subsystems cannot disturb each other.
- **Iterate collections in id order everywhere.** Dictionary and hash-set enumeration order is not a contract.

`IRandomSource` is a Domain abstraction; `new Random()` and static random APIs are banned in Domain code
and the ban is asserted by an architecture test.

### D11. `decimal` for state, `double` inside calculators

v0.1 §17.7 says "use `decimal` for game numeric values". But `decimal` has no `Exp`, `Log` or `Sqrt`, and
softmax, a sigmoid turnout curve and poll noise all need them.

Policy: **`decimal` for stored state, money and bounded attributes; `double` inside the electoral
calculators; round back to `decimal` at a fixed precision (6 decimal places) at the calculator boundary.**

Bit-exact `Math.Exp` is not guaranteed across CPU architectures and runtime versions, so the determinism
test runs in CI **on the deployment platform**, and the replay guarantee is stated as "same platform, same
simulation version" rather than universally.

### D12. Versioning — three versions, three policies

| Version | Changes when | Policy |
| --- | --- | --- |
| `SchemaVersion` | the snapshot JSON shape changes | a chain of upcasters (v1 → v2 → v3) runs on load |
| `SimulationVersion` | any rule or formula changes | replay equality is guaranteed only *within* one version; on upgrade the game moves forward and history records the date |
| Scenario version | content is edited | games pin a published version; edits never reach running games |

v0.1 calls the first one `StateVersion`, which sits next to `Revision` and reads like a concurrency token.
Renamed to `SchemaVersion` (review issue #22). `Revision` is the only concurrency token.

Keeping every historical rule set alive in order to replay old games is unrealistic for a small team.
This table makes that trade-off explicit rather than implied.

### D13. The custom mediator

No MediatR. A hand-written mediator in `TheSpanishWay.Application/Abstractions/Messaging/`:

```csharp
// Markers. A command may or may not return a value; a query always does.
public interface ICommand;
public interface ICommand<out TResponse>;
public interface IQuery<out TResponse>;

public interface ICommandHandler<in TCommand> where TCommand : ICommand
{
    Task<Result> Handle(TCommand command, CancellationToken ct);
}

public interface ICommandHandler<in TCommand, TResponse> where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> Handle(TCommand command, CancellationToken ct);
}

public interface IQueryHandler<in TQuery, TResponse> where TQuery : IQuery<TResponse>
{
    Task<Result<TResponse>> Handle(TQuery query, CancellationToken ct);
}

// Dispatch.
public interface ISender
{
    Task<Result>            Send(ICommand command, CancellationToken ct = default);
    Task<Result<TResponse>> Send<TResponse>(ICommand<TResponse> command, CancellationToken ct = default);
    Task<Result<TResponse>> Send<TResponse>(IQuery<TResponse> query, CancellationToken ct = default);
}

public interface IPublisher
{
    Task Publish<TNotification>(TNotification notification, CancellationToken ct = default)
        where TNotification : INotification;
}

public interface IMediator : ISender, IPublisher;
```

**Why hand-written is reasonable here:** the surface above is the whole contract, dispatch is one
`IServiceProvider.GetRequiredService` on a closed generic type, and avoiding the dependency keeps the
Application layer's own abstractions in the repository where they can be read and changed.

**Handler resolution.** `Send` receives a `TResponse`-typed request whose concrete type is only known at
runtime. The dispatcher closes `ICommandHandler<,>` over `(request.GetType(), typeof(TResponse))`,
resolves it from the container, and invokes it through a **cached compiled delegate** keyed by request
type — built once with `Expression.Lambda` (or a cached `MethodInfo` + open delegate), never via
`MethodInfo.Invoke` per call. Handlers are registered by assembly scan at startup; a request type with
zero or more than one handler fails **at startup**, not on first use.

**Pipeline behaviours**, applied in this order (outermost first):

```csharp
public interface IPipelineBehavior<in TRequest, TResponse>
{
    Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct);
}
```

1. `LoggingBehavior` — structured scope with request name, game id, correlation id.
2. `ValidationBehavior` — shape-level validation of the request only. Never business rules.
3. `IdempotencyBehavior` — for commands carrying a `ClientCommandId`; replays the stored result instead
   of executing twice.
4. `UnitOfWorkBehavior` — opens the transaction, commits on success, rolls back on failure, and hands the
   collected domain events to the dispatcher **after** commit.
5. `ConcurrencyRetryBehavior` — on a revision conflict, reloads and retries a bounded number of times for
   commands declared safe to retry; otherwise returns a conflict `Result`.

Queries bypass behaviours 3–5.

**Errors are returned, not thrown.** Handlers return `Result`/`Result<T>`; exceptions are reserved for
programmer error and infrastructure failure. This keeps "the action was unavailable" out of the exception
path, where it would be both slow and easy to swallow.

### D14. Domain events and the three meanings of "event"

v0.1 §10.2 warns about this and the warning is worth repeating in code structure:

| Concept | Lives in | Example | Dispatched by |
| --- | --- | --- | --- |
| **Domain event** | Domain | `PartyLeaderChanged`, `ElectionCompleted` | nobody — it is *recorded*, not sent |
| **Application notification** | Application | `PartyLeaderChangedNotification` | the mediator, after commit |
| **Simulation event** | Domain content | a corruption scandal, a natural disaster | the simulation loop — it is game content |

Naming rules that keep them apart: simulation events are always `GameEventDefinition` /
`EventOccurrence` / `ActiveEvent`; C# domain events are past-tense records in `Domain/**/Events/`;
application notifications live in `Application/**/Notifications/`.

**Flow:**

1. A domain operation changes state and calls `RecordEvent(...)` on the aggregate root. `AggregateRoot`
   exposes `IReadOnlyList<IDomainEvent> DomainEvents` and `ClearDomainEvents()`.
2. `UnitOfWorkBehavior` persists the changed state. Where the storage supports it, history/outbox rows go
   in the same transaction.
3. **After commit**, the collected events are wrapped — `DomainEventNotification<TDomainEvent>` — and
   published through `IPublisher`. The wrapper is what keeps `IDomainEvent` free of any Application type;
   the Domain never references `INotification`.
4. Handlers update read models, build news projections, write analytics.
5. **A notification handler must never be required for the core simulation state to be valid.** If a
   handler throws, the command still succeeded; the failure is logged and, for the handlers that matter,
   retried from an outbox.

For the MVP, in-process publication is sufficient. An outbox is introduced only when reliable external
delivery is actually needed. No distributed messaging infrastructure pre-emptively.

### D15. Hidden information — `PlayerView` for the player, `GetGameState` for the operator

Anything sent to the browser can be read by the player, and imperfect information is the game's third
design principle. The constraint that actually matters is therefore about the **player-facing surface**,
not about whether a full-state query exists at all. The review's issue #17 is satisfied by separating the
two surfaces rather than by deleting the query.

- **Player-facing.** The game client receives a `PlayerView` projection only: polls with their noise and
  house effects, visible indicators at their configured visibility (exact / noisy / delayed / hidden), the
  news feed, active decisions and available actions. No player-facing response carries a hidden
  preference, a perception record, a utility value or effect provenance.
- **Operator-facing.** `GetGameState` exists and returns the complete `GameState`. It is what makes the
  game debuggable: reproducing a player's bug report, inspecting where a replay diverged, asserting state
  from the test kit, and whatever support or admin tooling follows. It is authorised by an operator role
  rather than by game ownership, and it is never wired to a player-facing endpoint.

The separation is structural rather than a matter of discipline: the two live in different query
namespaces, `GetGameState` returns a type the player-facing mapping layer has no path to produce, and an
architecture test asserts that no player-facing endpoint returns it.

Per-indicator visibility is a scenario configuration value, not a code constant.

### D16. Storage strategy (contracts now, implementation later)

- **Current snapshot** as JSONB plus a relational header: status, phase, date, revision, versions, owner.
- **Command log**, append-only: command type, parameters, `ClientCommandId` (unique per game, which is
  what gives idempotency), resulting revision. With the seed and the scenario version this log replays the
  whole game — which turns any player bug report into a reproducible test.
- **Monthly snapshots** for history charts and fast replay from a midpoint.
- **History records** (polls, elections, executions with provenance) for the read side.

The Application owns the interfaces — `IGameSessionRepository`, `IScenarioRepository`,
`IHistoryStore`, `ICommandLog`, `IUnitOfWork` — and Infrastructure implements them. EF Core entities are
**not** the domain model; mapping lives in Infrastructure and is written against the snapshot, not against
`PartyState` and friends directly.

### D17. Open decisions — MVP defaults

v0.1's twelve open decisions plus the review's three new ones. Each default is the simplest rule that
unblocks the MVP. **Every number is a configuration placeholder, not a design claim.**

| # | Decision | Blocks | MVP default |
| --- | --- | --- | --- |
| 1 | Party survival | game over | dissolution if, at a month end, any of: budget negative for 6 straight months; cohesion below 15; no seat in 2 straight general elections |
| 2 | Rival party AI | world autonomy | phase 1: rival behaviour arrives as events whose `$actor` role binds a rival — no AI code. Phase 2: a weekly utility pick from the same action catalogue |
| 3 | Government attribution | utility input | per-indicator share (inflation 0.5, employment 0.6, …) of the change since the government began; changes tagged `External` get one third of that share |
| 4 | Action economy | action spam | budget, plus a weekly allowance of voluntary actions, plus per-definition cooldowns |
| 5 | Campaign mechanics | phase handling | a 15-day campaign phase (the legal Spanish length): higher volatility and media attention, no special actions yet |
| 6 | Parliamentary detail | votes, formation | bills arrive as events from the government; each party votes by rule (government parties yes, others by ideological distance and trust); investiture needs an absolute majority (176) in the first vote and a simple majority in the second; no government within 2 months forces a repeat election |
| 7 | Election law | seats | per-scenario `ElectoralSystem` config (see D9) |
| 8 | Perception decay | memory | exponential decay toward the structural baseline, one half-life per dimension (trust slower than respect) |
| 9 | Media model | reactions | pick-up chance = newsworthiness × issue interest × sensationalism; tone from editorial alignment with the actor plus noise; impact = reach × credibility × segment media sensitivity × tone |
| 10 | Variable calibration | balance | placeholder values; seed-sweep tests decide |
| 11 | Content authoring | content | JSON files in the repository, validated against a JSON schema **and** the attribute registry at startup and in CI |
| 12 | Pause model | loop | events are flagged `blocking` or not; non-blocking events auto-resolve with their default option at expiry |
| 13 | Issue catalogue (new) | reactions, affinity | about 12 issues, each positional (axis loadings) or valence (linked variables) |
| 14 | Visibility rules (new) | player view | per indicator: exact, noisy, delayed or hidden |
| 15 | Early elections (new) | calendar | fixed 4-year date plus a forced repeat after a failed investiture; voluntary dissolution deferred |

### D18. Deviations from the v0.1 spec

A reader of v0.1 should be able to find every place this design departs from it:

| v0.1 says | This design does | Why |
| --- | --- | --- |
| §4.4 `PoliticalParty` is an aggregate root | `PartyState` is a state component inside `GameState` | one snapshot, one revision, atomic ticks (D2) |
| §4.1 `Game.CurrentDate` and §4.1 `GameState` both hold the date | the date lives only in `GameState` | one owner per fact |
| §4.4 `PoliticalParty.Seats` | a `Parliament` component | seats are an election outcome |
| §4.4 `PartyMembership.Loyalty` | `InternalSupport` | distinct from `PoliticalCharacter.Loyalty` |
| §4.4 `PartyMembership.JoinedAt` is `DateTimeOffset` | `GameDate` | in-game time is never wall-clock time |
| §4.1 `StateVersion` | `SchemaVersion` | it is not a concurrency token |
| §4.1 status `InProgress` | `Running` + a separate `AwaitingDecision` | the player pausing and the engine blocking are different |
| §5.4 effects may target segment perceptions | perceptions reachable only through stimuli | content must not place votes (D4) |
| §5.4 temporary effects "expire or decay" | base + modifier layering | subtraction after clamping is wrong (D5) |
| §7.2 perception stores affinity | perception stores trust, respect, position drift | affinity must be counted once (D9) |
| §7.4 abstention inside the softmax | turnout decides whether, softmax decides whom | abstention appeared twice (D9) |
| §7.5 `lambda` per recalculation | `lambda` per 30 days, converted per step | cadence must not change the game (D9) |
| §8 pending effects before expiry | expiry before pending effects | `RefreshDuration` on its expiry day must survive |
| §8 player action inside the day loop | player actions are commands between days | actions are not a tick phase |
| §10.1 `AdvanceGameDay` | `AdvanceUntil(stop, maxDays)` | 1,461 clicks and 1,461 writes per legislature |
| §10.1 `GetGameState` | retained, but operator-only; the client gets `PlayerView` | hidden information is a design principle (D15) |
| §12 one global random sequence | counter-based named streams | a new draw must not shift every later roll (D10) |
| §17.7 `decimal` everywhere | `decimal` state, `double` in calculators | `decimal` has no `Exp` (D11) |
| §14 `PublicPositionAction.cs` etc. as classes | one handler per *category*, data per instance | consistent with §17.4's own "no class per action" |
| §4.6 "2–4 dominant agenda topics" | emergent: normalised relevance with decay | it reads like an invariant but is an observation |

### D19. Build order

Nine slices, each ending in something testable. Slice 5 depends only on slice 1 and can run in parallel
from the start; it carries the most formula risk and the least feedback.

1. **Foundations** — `GameDate` and cadence boundaries, `Bounded<T>`, typed ids, counter-based streams.
   *Done when* the same seed and stream produce the same draws across processes.
2. **Session and loop** — `GameSession`, the status machine, `AdvanceUntil` over an empty world, snapshot
   save/load. *Done when* a game advances 1,461 days, reloads mid-way, and finishes identical.
3. **Variables and effect engine** — base values, modifiers, pending effects, stacking, change-set
   atomicity, provenance. *Done when* the effect test list in v0.1 §15.2 passes.
4. **Content and events** — JSON loader, schema and attribute-registry validation, roles, cooldowns,
   blocking events, `AwaitingDecision`. *Done when* an invalid attribute fails at load and a scandal binds
   a party and a character.
5. **Electoral calculators (pure)** — affinity, utility, softmax, turnout, provincial aggregation, seat
   distribution, D'Hondt with ties. *Done when* the calibration tests and a hand-checked D'Hondt case pass.
6. **Actions** — the three categories as handlers, costs, allowance, cooldowns. *Done when* a failed action
   leaves the snapshot byte-identical.
7. **Stimulus pipeline** — media coverage, segment reactions, perception updates, agenda. *Done when* one
   stimulus moves six segments by different amounts.
8. **Polls, elections, parliament** — monthly polls with house effects, election day, investiture,
   government formation. *Done when* a full legislature produces a government.
9. **Survival, player view, balance** — survival policy, game over, `PlayerView`, seed sweeps. *Done when*
   200 seeds produce outcome distributions inside agreed bands.

## Risks / Trade-offs

**The single-aggregate decision concentrates write contention.** Every command on a game serialises on one
row and one snapshot. This is correct for a single-player game — there is exactly one writer per game — and
it would be wrong for anything multiplayer. If multiplayer ever appears, the boundary must be revisited;
nothing else in this design assumes it.

**The snapshot will get large.** A full `GameState` with 52 provinces × 6 segments × 5 parties of
preferences is on the order of a few hundred kilobytes of JSON. Rewriting all of it per command is
acceptable at `AdvanceUntil` granularity (one write per player interaction, not per day) and unacceptable
at per-day granularity — which is one more reason D7 is not optional. If it becomes a problem, the
mitigation is to split the preference matrix into its own column, not to split the aggregate.

**The stimulus pipeline raises the cost of authoring content.** An author can no longer write "trust −10";
they must choose an issue, a kind, a stance and an intensity, and then trust the model. That is exactly the
intent, but it means content is not tunable by direct manipulation and the reaction calculator becomes the
single point where balance lives. Mitigation: the seed-sweep balance tests in slice 9, and the
`ScriptedPerceptionOverride` escape hatch for genuinely scripted moments.

**The hand-written mediator is code we own and MediatR is code we don't.** The reflection-plus-cached-
delegate dispatch is the one genuinely fiddly part, and getting the open-generic resolution wrong produces
confusing runtime errors. Mitigation: startup-time validation that every request type has exactly one
handler, and a dedicated `Application.UnitTests` suite for the dispatcher itself.

**`double` inside the calculators makes determinism platform-dependent.** `Math.Exp` is not bit-exact across
architectures. The guarantee offered is therefore "same platform, same simulation version", and the
determinism test must run in CI on the deployment platform. An x64 developer machine and an arm64 CI runner
can legitimately disagree in the last decimal places, and the test must not be written as if they cannot.

**Replay is only guaranteed within one `SimulationVersion`.** Fixing a formula bug invalidates replay for
every existing save. For a team of this size, keeping every historical rule set alive is not realistic. The
cost is accepted explicitly (D12) rather than discovered later.

**The electoral calculators are the long pole and have the weakest feedback loop.** They cannot be judged
by "does it feel right" until slices 7 and 8 land. Mitigation: they are pure functions, slice 5 runs in
parallel from the start, and the two calibration tests are written before any content exists.

**Seven of the review's high-severity fixes are adopted wholesale without an independent second opinion.**
This design treats the review as correct because its reasoning is checkable and the worked counter-examples
(the clamped-inflation case, the 1,461-day case) hold up. If any of them is wrong, the cheapest moment to
find out is this proposal review — not after slice 3.
