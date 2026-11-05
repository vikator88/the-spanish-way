# simulation-test-strategy Specification

## Purpose

Defines how this system is proven correct: the test projects and their boundaries, the deterministic test
doubles that make a simulation assertable, and the six suites — invariants, effects, events, electoral,
determinism and balance — that together cover rules the spec states and behaviour the spec only implies.

## Requirements

### Requirement: Test projects and boundaries

Tests SHALL live under `api/test/` in projects whose boundaries match the layers they exercise.

#### Scenario: Project set
- **WHEN** the test tree is inspected
- **THEN** it contains `TheSpanishWay.TestKit`, `TheSpanishWay.Domain.UnitTests`,
  `TheSpanishWay.Application.UnitTests`, `TheSpanishWay.Simulation.Tests`,
  `TheSpanishWay.Architecture.Tests`, `TheSpanishWay.Infrastructure.IntegrationTests` and
  `TheSpanishWay.Api.FunctionalTests`

#### Scenario: Domain tests need no infrastructure
- **WHEN** the domain test suite runs
- **THEN** it requires no database, no web host, no dependency-injection container and no network

#### Scenario: Deferred suites are scaffolded only
- **WHEN** this change is implemented
- **THEN** the infrastructure and functional test projects exist and compile but contain no tests yet

#### Scenario: Toolchain
- **WHEN** a test project is inspected
- **THEN** it uses xUnit v3 with Shouldly for assertions, NSubstitute for fakes where needed, Verify for
  snapshot comparison, and NetArchTest in the architecture project

### Requirement: Deterministic test doubles

The test kit SHALL provide the doubles that make simulation outcomes assertable.

#### Scenario: Scripted random source
- **WHEN** a test needs a specific random outcome
- **THEN** it uses a scripted `IRandomSource` that returns a prepared sequence per stream

#### Scenario: Builders
- **WHEN** a test needs a game in a particular state
- **THEN** it constructs one through fluent builders rather than by assembling raw state by hand

#### Scenario: Minimal scenario fixture
- **WHEN** a test needs content
- **THEN** a minimal valid scenario fixture is available that is small enough to reason about

### Requirement: Invariant suite

Tests SHALL cover every invariant the domain specification states.

#### Scenario: Invariants covered
- **WHEN** the invariant suite runs
- **THEN** it asserts that a finished game cannot advance, the date cannot move backwards, the player
  party must belong to the game, relationships must reference existing actors, bounded values stay in
  range, a resolved or expired occurrence cannot be resolved again, an unavailable action cannot execute,
  action costs and effects are atomic, and a party cannot dissolve without satisfying the policy

#### Scenario: Status machine is covered
- **WHEN** the suite runs
- **THEN** every permitted transition is asserted and every forbidden transition is asserted to fail

### Requirement: Effect engine suite

Tests SHALL cover the effect engine's timing, layering, stacking and atomicity behaviour.

#### Scenario: Timing behaviours
- **WHEN** the effect suite runs
- **THEN** it covers immediate, delayed, temporary with expiry, permanent and recurring effects

#### Scenario: Exact reversal
- **WHEN** the clamped-expiry case is tested
- **THEN** a variable at 9.5 with an upper bound of 10 that receives a temporary `+2` returns to exactly
  9.5 on expiry

#### Scenario: Stacking policies
- **WHEN** the suite runs
- **THEN** each of `Stack`, `RefreshDuration`, `Replace` and `IgnoreDuplicate` is covered, including the
  refresh-on-expiry-day case

#### Scenario: Invalid targets
- **WHEN** an effect names an invalid target or attribute
- **THEN** a test asserts the whole change set is rejected and nothing is written

#### Scenario: Byte-identical rollback
- **WHEN** an action fails during effect application
- **THEN** a test asserts the serialised snapshot is unchanged

### Requirement: Event suite

Tests SHALL cover event eligibility, binding, cooldown, expiry and chaining.

#### Scenario: Eligibility and selection
- **WHEN** the suite runs
- **THEN** it covers random selection under a scripted random source, conditional triggers, cooldown,
  expiry, follow-up scheduling, and the prevention of a duplicate occurrence where not permitted

#### Scenario: Role binding
- **WHEN** a scandal definition fires
- **THEN** a test asserts a party and a character are bound according to the selection rules

#### Scenario: No candidate means not eligible
- **WHEN** no party satisfies a role's selection rule
- **THEN** a test asserts the definition is not eligible

#### Scenario: Content validation failures
- **WHEN** content names an attribute absent from the registry
- **THEN** a test asserts load fails naming the definition and the key

### Requirement: Electoral suite

Tests SHALL cover the electoral calculators as pure functions, including the two calibration tests that
precede content.

#### Scenario: Affinity extremes
- **WHEN** the suite runs
- **THEN** identical profiles yield affinity 100 and maximally opposed profiles yield 0

#### Scenario: Softmax properties
- **WHEN** probabilities are computed
- **THEN** a test asserts they sum to 1 and that the stable form is used

#### Scenario: Calibration
- **WHEN** the calibration tests run
- **THEN** identical parties receive identical shares, and a 0.1 utility advantage produces the documented
  share gain

#### Scenario: Turnout and inertia
- **WHEN** the suite runs
- **THEN** it covers turnout calculation, preference inertia, renormalisation, and the equivalence of
  weekly and daily stepping under the per-30-day lambda conversion

#### Scenario: Provincial difference
- **WHEN** two provinces with different compositions are computed from the same national state
- **THEN** a test asserts their results differ

#### Scenario: Presence does not add votes
- **WHEN** territorial presence increases
- **THEN** a test asserts utility changed and no fixed vote quantity was added

#### Scenario: Seat allocation
- **WHEN** the suite runs
- **THEN** it covers a hand-checked D'Hondt case, the 3 percent threshold on valid votes including blanks,
  minimum seats per province, and tie resolution by total votes then by seeded lot

#### Scenario: Polls differ from truth
- **WHEN** a poll is generated
- **THEN** a test asserts it differs from the hidden preference while remaining reproducible under the
  same seed

### Requirement: Simulation and determinism suite

Tests SHALL prove that the loop runs in order and that a game is reproducible.

#### Scenario: Step order
- **WHEN** a day is simulated with instrumentation
- **THEN** a test asserts the twelve steps executed in the specified order

#### Scenario: Cadence gating
- **WHEN** a non-boundary day is simulated
- **THEN** a test asserts the weekly, monthly and quarterly subsystems did not run

#### Scenario: Replay equality
- **WHEN** the same scenario version, simulation version, seed and ordered commands are replayed
- **THEN** a test asserts the resulting state is identical

#### Scenario: Save and resume
- **WHEN** a game is advanced, serialised, reloaded in a fresh context and advanced further
- **THEN** a test asserts the result matches an uninterrupted run

#### Scenario: Stream isolation
- **WHEN** an extra draw is added to one stream
- **THEN** a test asserts the other streams' sequences are unchanged

#### Scenario: Determinism runs on the deployment platform
- **WHEN** the determinism test executes in CI
- **THEN** it runs on the deployment platform's architecture, and the guarantee is documented as
  per-platform because `Math.Exp` is not bit-exact across architectures

### Requirement: Balance suite

Tests SHALL cover distributions of outcomes, not only individual rules.

#### Scenario: Seed sweep
- **WHEN** the balance suite runs
- **THEN** it simulates a full legislature across 200 seeds and asserts that outcome distributions fall
  within agreed bands

#### Scenario: Bands are configuration
- **WHEN** a band is adjusted
- **THEN** it changes in a configuration file, not in a test's assertions

#### Scenario: Sweeps are excluded from the fast suite
- **WHEN** the default test run executes
- **THEN** the seed sweep is in a separate, longer-running category

### Requirement: Architecture suite

Tests SHALL assert the structural rules that reviews cannot reliably catch.

#### Scenario: Layering
- **WHEN** the architecture suite runs
- **THEN** it asserts the inward dependency rule, the Domain's zero package references, and the absence of
  any mediator package

#### Scenario: Banned APIs
- **WHEN** the architecture suite runs
- **THEN** it asserts the Domain uses no `System.Random`, no wall-clock API and no serialisation,
  persistence or HTTP namespace

#### Scenario: Domain purity conventions
- **WHEN** the architecture suite runs
- **THEN** it asserts domain events are sealed records under `Events/` folders and that state components
  expose no public property setters

#### Scenario: Player-facing surface leaks no hidden state
- **WHEN** the architecture suite runs
- **THEN** it asserts that no player-facing endpoint or response type exposes `GameState`, a hidden
  preference, a perception record or a utility value, and that `GetGameState` is reachable only from the
  operator namespace
