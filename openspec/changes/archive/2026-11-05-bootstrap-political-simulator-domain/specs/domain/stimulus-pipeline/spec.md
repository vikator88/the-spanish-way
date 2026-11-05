# Spec Delta

## Purpose

Defines the stage that stands between what content authors write and what voters think: actions and
events emit typed stimuli, and only the reaction pipeline writes perceptions. This is the mechanism that
keeps the product's central promise — that election results emerge from the simulation rather than being
placed by hand.

## ADDED Requirements

### Requirement: Stimulus value object

A stimulus SHALL be a typed value object carrying an actor reference, an issue, a kind, an optional
stance, an intensity and a visibility.

#### Scenario: Stimulus fields
- **WHEN** a stimulus is created
- **THEN** it carries `Actor`, `Issue`, `Kind`, `Stance`, `Intensity` and `Visibility`

#### Scenario: Kind
- **WHEN** a stimulus kind is read
- **THEN** it is `Positional` or `Valence`

#### Scenario: Stance applies only to positional stimuli
- **WHEN** a `Valence` stimulus is created with a stance
- **THEN** creation fails

#### Scenario: Stance range
- **WHEN** a `Positional` stimulus carries a stance
- **THEN** the stance lies between −100 and +100 on the issue's axis

#### Scenario: Intensity range
- **WHEN** a stimulus is created
- **THEN** its intensity lies between 0 and 1

#### Scenario: Actor may be a role
- **WHEN** an event emits a stimulus
- **THEN** its actor may be a bound role such as `$accused`, resolved to a concrete identifier at emission

### Requirement: Authoring surface is small

Content SHALL express political consequence by emitting stimuli, not by writing per-segment numbers.

#### Scenario: One authored line, six reactions
- **WHEN** a definition emits a single stimulus such as a hard pro-pension stance at intensity 0.7
- **THEN** all six segments react, each by a different amount, with no per-segment value authored

#### Scenario: No per-segment authoring
- **WHEN** content is validated
- **THEN** no construct exists that lets an author address a specific segment's perception

### Requirement: Reaction pipeline

Stimuli SHALL be converted to perception changes by a pipeline that accounts for media pick-up, agenda
salience, segment issue sensitivity, ideological fit and media sensitivity.

#### Scenario: Pipeline order
- **WHEN** a stimulus is processed
- **THEN** media pick-up and framing are evaluated first, then per-segment reaction, then the perception
  delta is applied

#### Scenario: Agenda gates magnitude
- **WHEN** the same stimulus is processed with its issue dominant and with its issue irrelevant
- **THEN** the dominant case produces a substantially larger perception change

#### Scenario: Ideology applies to positional stimuli
- **WHEN** a `Positional` stimulus is processed
- **THEN** the sign and magnitude of each segment's reaction depend on the distance between the stance and
  that segment's position on the issue's axes

#### Scenario: Competence applies to valence stimuli
- **WHEN** a `Valence` stimulus is processed
- **THEN** reactions depend on blame attribution and competence rather than on ideological distance

#### Scenario: Unpicked stimuli still have internal effect
- **WHEN** no outlet picks up a stimulus
- **THEN** public perception is largely unaffected while internal and relational effects may still apply

#### Scenario: Deterministic
- **WHEN** the pipeline runs twice from the same state and seed
- **THEN** it produces identical perception changes, drawing from the `reactions` stream

### Requirement: Perceptions are write-protected

Segment perceptions SHALL be writable only by the reaction pipeline.

#### Scenario: Single writer
- **WHEN** the Domain is inspected
- **THEN** the only code path that mutates a perception record is the reaction pipeline

#### Scenario: Actions do not write perceptions
- **WHEN** an action executes
- **THEN** it may change world variables, party and character attributes and relations directly, and may
  queue stimuli, but writes no perception

#### Scenario: No vote awarded
- **WHEN** any action or event is executed
- **THEN** no voting-intention percentage is modified by it directly

### Requirement: Stimulus queue and timing

Stimuli SHALL be queued and processed at one fixed point in the daily loop.

#### Scenario: Queued from actions
- **WHEN** a player action executes on a given date
- **THEN** its immediate effects apply at once and its stimuli are queued for the next day's processing

#### Scenario: Single processing point
- **WHEN** the day loop runs
- **THEN** all queued stimuli are processed in step 8 and nowhere else

#### Scenario: Stable order
- **WHEN** multiple stimuli are queued
- **THEN** they are processed in a deterministic order independent of insertion order
