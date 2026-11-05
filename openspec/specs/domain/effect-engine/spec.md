# effect-engine Specification

## Purpose

Defines how declared effects change state: the base-value and modifier layering that makes temporary
effects exactly reversible, the pending-effect queue for delayed and recurring changes, explicit stacking
policy, change-set atomicity, clamp diagnostics, and the provenance that every mutation carries.

## Requirements

### Requirement: Three-layer attribute values

Every numeric attribute SHALL have a base value, a set of active modifiers, and an effective value
computed on read; modifiers SHALL never be folded into the base.

#### Scenario: Base changes only permanently
- **WHEN** a permanent effect or natural evolution applies
- **THEN** the base value changes and is clamped on write

#### Scenario: Temporary effects become modifiers
- **WHEN** a temporary effect applies
- **THEN** a modifier is added carrying its source, expiry and stack key, and the base is untouched

#### Scenario: Fixed computation order
- **WHEN** the effective value is read
- **THEN** it is computed as base, then `Set` overrides, then the sum of `Add` modifiers, then the product
  of `Multiply` modifiers, then clamped to bounds

#### Scenario: Exact expiry after clamping
- **WHEN** a variable at 9.5 with an upper bound of 10 receives a temporary `+2` and that modifier expires
- **THEN** the effective value returns to exactly 9.5

#### Scenario: Expiry is removal
- **WHEN** a modifier expires
- **THEN** it is removed from the active set and nothing is subtracted

### Requirement: Clamping is a diagnostic

When a clamp truncates a change, the engine SHALL report the truncation rather than discarding it
silently.

#### Scenario: Truncation reported
- **WHEN** an effect is truncated by 1.5 at an upper bound
- **THEN** the result carries a diagnostic naming the attribute, the attempted change and the truncated
  amount

#### Scenario: Diagnostics do not fail the command
- **WHEN** a truncation occurs
- **THEN** the command still succeeds and the diagnostic is recorded alongside the applied change

### Requirement: Effect timing behaviours

The engine SHALL support immediate permanent, immediate temporary, delayed, gradual or recurring, and
trajectory-changing effects.

#### Scenario: Immediate permanent
- **WHEN** an effect declares no delay and no duration
- **THEN** it mutates the base value at once

#### Scenario: Immediate temporary
- **WHEN** an effect declares a duration but no delay
- **THEN** a modifier with that expiry is created at once

#### Scenario: Delayed
- **WHEN** an effect declares a delay
- **THEN** a `PendingEffect` is queued and becomes a base mutation or a modifier on its `ApplyAt` date

#### Scenario: Recurring
- **WHEN** an effect declares a step value and a count
- **THEN** a recurring `PendingEffect` applies that step on each due date until the count is exhausted

#### Scenario: Trajectory change
- **WHEN** an effect targets `TargetValue` or `EvolutionRate`
- **THEN** the variable's future path changes and its current value is not directly altered

### Requirement: Randomness resolved at instantiation

A random value in an effect declaration SHALL be rolled when the effect is instantiated and the rolled
value stored, so that reloading a save cannot reroll it.

#### Scenario: Roll once
- **WHEN** an effect declaring a range of +1 to +3 is instantiated
- **THEN** a single value is drawn and stored on the pending effect or modifier

#### Scenario: Reload does not reroll
- **WHEN** a game is saved while a rolled pending effect is queued and then reloaded
- **THEN** the stored value is applied unchanged

#### Scenario: Saves explain themselves
- **WHEN** a queued effect is inspected
- **THEN** its concrete value is visible without recomputing any random draw

### Requirement: Stacking policy

Every effect SHALL carry a stack key and a stacking policy, with a documented default.

#### Scenario: Default stack key
- **WHEN** an effect does not declare a stack key
- **THEN** the key is `(source definition, effect index, target, attribute)`

#### Scenario: Supported policies
- **WHEN** a policy is declared
- **THEN** it is one of `Stack`, `RefreshDuration`, `Replace` or `IgnoreDuplicate`

#### Scenario: Default policy
- **WHEN** no policy is declared
- **THEN** `RefreshDuration` applies

#### Scenario: Refresh on the expiry day
- **WHEN** a `RefreshDuration` modifier is reapplied on the same day it would expire
- **THEN** it survives, because expiry is processed before pending effects

#### Scenario: Replace
- **WHEN** a `Replace` modifier is applied over an existing one with the same stack key
- **THEN** the existing modifier is removed and the new one takes effect

### Requirement: Change-set atomicity

Effects SHALL first produce a list of proposed changes, which the engine validates in full before
applying any of them.

#### Scenario: All-or-nothing
- **WHEN** one effect in a group targets an invalid attribute
- **THEN** no change from any effect in that group is applied and the command is rejected

#### Scenario: Failed action leaves state untouched
- **WHEN** an action fails during effect application
- **THEN** the resulting snapshot is byte-identical to the snapshot before the attempt

#### Scenario: Cost and effect are atomic
- **WHEN** an action charges a cost and applies effects
- **THEN** either both happen or neither does

#### Scenario: Validation precedes application
- **WHEN** the engine processes a change set
- **THEN** every proposed change is validated for target existence, attribute validity and type before the
  first mutation occurs

### Requirement: Provenance on every change

Every base mutation and every modifier SHALL carry the source that produced it and an origin tag.

#### Scenario: Source reference
- **WHEN** a change is applied
- **THEN** it records the originating action execution or event occurrence identifier and the effect index

#### Scenario: Origin tag
- **WHEN** a change is applied
- **THEN** it is tagged as either `External` or `Policy`

#### Scenario: Attribution reads the tag
- **WHEN** government performance attribution is computed
- **THEN** it distinguishes changes tagged `External` from those tagged `Policy`

#### Scenario: Player explanation
- **WHEN** the player asks why support fell
- **THEN** the provenance records are sufficient to construct the explanation without replaying the game

### Requirement: Content cannot write computed state

The engine SHALL reject any effect targeting segment perceptions, electoral preferences, turnout or
seats, except through an explicitly flagged scenario escape hatch.

#### Scenario: Perception target rejected
- **WHEN** content declares an effect writing a segment's trust in a party
- **THEN** content validation fails

#### Scenario: Preference, turnout and seat targets rejected
- **WHEN** content declares an effect writing a preference, a turnout figure or a seat count
- **THEN** content validation fails

#### Scenario: Permitted targets
- **WHEN** content declares an effect
- **THEN** it may target world variables, party attributes, character attributes, party relations,
  character relations, agenda topic relevance, party resources and provincial presence

#### Scenario: Scripted override is opt-in
- **WHEN** a scenario needs a scripted opening shock
- **THEN** it uses the distinct `ScriptedPerceptionOverride` effect kind, which the default validator
  rejects unless the scenario explicitly opts in
