# Spec Delta

## Purpose

Defines the game loop: `AdvanceUntil` running many days in memory for one persisted revision, the fixed
twelve-step order inside a day, the cadence boundaries that decide which subsystems run, the decision
points at which the simulation must stop and wait for the player, and the determinism guarantees the
whole loop must honour.

## ADDED Requirements

### Requirement: AdvanceUntil replaces per-day advancement

The simulation SHALL be advanced by `AdvanceUntil(stop, maxDays)`, which runs days in memory and
persists once per command.

#### Scenario: Many days, one write
- **WHEN** `AdvanceUntil` simulates 400 days
- **THEN** the state is persisted once and the revision increases by exactly 1

#### Scenario: No per-day command
- **WHEN** the command surface is inspected
- **THEN** no `AdvanceGameDay` command exists

#### Scenario: maxDays bounds the call
- **WHEN** no other stop condition triggers
- **THEN** the loop halts after `maxDays` days

#### Scenario: Stop conditions
- **WHEN** the caller specifies a stop condition
- **THEN** it may be the next decision, a specific date, a phase change, or a number of days

### Requirement: Stopping at decision points

The loop SHALL stop when a blocking event is raised and SHALL NOT advance past a decision point.

#### Scenario: Blocking event halts the loop
- **WHEN** a blocking event fires on day 37 of a 400-day advance
- **THEN** the loop stops on day 37, the status becomes `AwaitingDecision`, and days 38 onward are not
  simulated

#### Scenario: Decision required event
- **WHEN** the loop stops for a decision
- **THEN** a `DecisionRequired` domain event is recorded naming the occurrence

#### Scenario: Resuming after a decision
- **WHEN** the player resolves the blocking occurrence and advances again
- **THEN** the loop continues from the day after the decision point

#### Scenario: Non-blocking events do not stop the loop
- **WHEN** a non-blocking event fires
- **THEN** the loop continues and the event auto-resolves with its default option at expiry

#### Scenario: Election stops the loop
- **WHEN** an election day is reached
- **THEN** the loop stops so the player sees the result before the game continues

### Requirement: Fixed order inside one day

Each simulated day SHALL execute the following steps in this exact order.

#### Scenario: Step order
- **WHEN** one day is simulated
- **THEN** the steps run as: 1 advance the date and compute cadence flags; 2 expire modifiers and events;
  3 apply due pending effects; 4 world evolution when due; 5 autonomous actors act; 6 event generation;
  7 auto-resolve non-blocking events; 8 stimuli to coverage to reactions to perceptions; 9 agenda,
  preferences and polls when due; 10 election when due; 11 survival and game-over policy; 12 stop-condition
  check

#### Scenario: Expiry precedes pending effects
- **WHEN** a `RefreshDuration` modifier would expire on the same day its source effect reapplies
- **THEN** expiry runs first, the effect reapplies, and the modifier survives

#### Scenario: Pending effect ordering
- **WHEN** several pending effects are due on the same day
- **THEN** they are applied ordered by `ApplyAt`, then priority, then sequence number

#### Scenario: Player actions are not a step
- **WHEN** the day loop is inspected
- **THEN** no step executes a player action; actions are separate commands that run between days

#### Scenario: Order is tested
- **WHEN** a day is simulated with instrumentation
- **THEN** a test asserts the observed step order matches the specification

### Requirement: Cadence definitions

Weekly, monthly and quarterly boundaries SHALL be defined unambiguously and SHALL gate the subsystems
that run on them.

#### Scenario: Weekly
- **WHEN** the days elapsed since the game's start date is a multiple of 7
- **THEN** the weekly flag is set and economic evolution and preference recalculation run

#### Scenario: Monthly
- **WHEN** the date is the first day of a calendar month
- **THEN** the monthly flag is set and social evolution, agenda update, polls and the survival policy run

#### Scenario: Quarterly
- **WHEN** the date is 1 January, 1 April, 1 July or 1 October
- **THEN** the quarterly flag is set and demographic evolution runs

#### Scenario: Subsystems run only when due
- **WHEN** a day is simulated that is not a monthly boundary
- **THEN** no poll is generated and social indicators do not evolve

#### Scenario: Preferences recalculate weekly
- **WHEN** preferences are recalculated
- **THEN** it happens on weekly boundaries, with the elapsed interval passed to the inertia conversion

### Requirement: Autonomous world

The world SHALL continue to evolve without player input, including the behaviour of rival parties.

#### Scenario: World advances in opposition
- **WHEN** the player takes no action for a year
- **THEN** indicators evolve, events fire, media covers them, perceptions move and polls are published

#### Scenario: Rival behaviour phase one
- **WHEN** a rival party acts in the MVP
- **THEN** it acts through an event whose `$actor` role binds a rival, with no separate AI implementation

#### Scenario: Autonomous actors draw deterministically
- **WHEN** autonomous actors act
- **THEN** their choices are drawn from the `actors` stream

### Requirement: Player actions execute between days

An action SHALL execute on the current date after that day's tick, applying its effects immediately and
queueing its stimuli.

#### Scenario: Timing
- **WHEN** a player executes an action
- **THEN** it applies on the current date and its stimuli are processed in step 8 of the next day

#### Scenario: Allowed statuses
- **WHEN** an action is submitted while the game is `Running` or `AwaitingDecision`
- **THEN** it is permitted

#### Scenario: Refused statuses
- **WHEN** an action is submitted while the game is `Created`, `Paused`, `Finished` or `Abandoned`
- **THEN** it is refused with a failed `Result`

#### Scenario: Availability is validated first
- **WHEN** an action's availability conditions are not satisfied
- **THEN** execution fails before any state is changed

#### Scenario: Action economy
- **WHEN** a player exceeds the configured weekly allowance of voluntary actions or a definition's
  cooldown
- **THEN** the action is refused

#### Scenario: Execution is recorded
- **WHEN** an action executes successfully
- **THEN** an `ActionExecuted` domain event is recorded and an execution history record is appended with
  its provenance

### Requirement: Event occurrence lifecycle

An event occurrence SHALL be distinct from its definition and SHALL have a single-resolution lifecycle.

#### Scenario: Multiple occurrences
- **WHEN** a definition's cooldown has elapsed and its conditions hold again
- **THEN** it may occur a second time as a new occurrence

#### Scenario: Cooldown blocks re-firing
- **WHEN** a definition's cooldown has not elapsed
- **THEN** it is not eligible

#### Scenario: Resolved once
- **WHEN** a resolved occurrence is resolved again
- **THEN** the operation fails

#### Scenario: Expired occurrences refuse decisions
- **WHEN** a player submits a decision for an expired occurrence
- **THEN** the operation fails

#### Scenario: Occurrence carries context
- **WHEN** an occurrence is inspected
- **THEN** it carries its definition, role bindings, trigger date, status, expiry and the context needed
  to explain it

#### Scenario: Occurrence records an event
- **WHEN** an event occurs
- **THEN** an `EventOccurred` domain event is recorded

### Requirement: Determinism of the loop

Given the same scenario version, simulation version, seed and ordered commands, the loop SHALL produce
the same result on the same platform.

#### Scenario: Replay equality
- **WHEN** a game is replayed from its command log with the same seed and scenario version
- **THEN** the resulting state is identical

#### Scenario: Save, reload, continue
- **WHEN** a game is advanced 700 days, saved, reloaded in a new process and advanced a further 700 days
- **THEN** the final state matches an uninterrupted 1,400-day advance

#### Scenario: Stream isolation
- **WHEN** an extra random draw is introduced into the media subsystem
- **THEN** the sequence of event rolls is unchanged

#### Scenario: Platform caveat is explicit
- **WHEN** the determinism guarantee is documented
- **THEN** it states that it holds within one simulation version on one platform, because `Math.Exp` is
  not bit-exact across architectures

### Requirement: Game-over evaluation

The loop SHALL evaluate the terminal condition each time the survival policy runs, and SHALL end the game
when it is met.

#### Scenario: Terminal condition
- **WHEN** the player party is dissolved or another configured terminal condition is met
- **THEN** the status becomes `Finished`, the phase becomes `GameOver`, and a `GameEnded` domain event is
  recorded

#### Scenario: Losing an election is not terminal
- **WHEN** the player party loses an election
- **THEN** the game continues

#### Scenario: No advancement after finishing
- **WHEN** `AdvanceUntil` is called on a finished game
- **THEN** it is refused with a failed `Result`
