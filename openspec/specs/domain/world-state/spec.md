# world-state Specification

## Purpose

Defines the autonomous simulated world: bounded world variables with explicit polarity, the economy,
society and demography that evolve on their own cadences, the provinces that act as electoral and social
units, and the public agenda that decides which issues voters are currently paying attention to.

## Requirements

### Requirement: World variables

A world indicator SHALL be represented by a generic `WorldVariable` carrying a key, base value, bounds,
polarity, optional target value and evolution rate — not by a dedicated class per indicator.

#### Scenario: No class per indicator
- **WHEN** the `World` module is inspected
- **THEN** no type named after a single indicator such as `Inflation` or `Unemployment` exists

#### Scenario: Polarity is explicit
- **WHEN** a variable is defined
- **THEN** it declares whether a high value is good or bad, so that no rule has to infer it from the key

#### Scenario: Bounds are enforced
- **WHEN** an effect would move a variable outside its configured bounds
- **THEN** the value is clamped and the truncation is reported as a diagnostic

#### Scenario: Variables evolve at different rates
- **WHEN** two variables with different evolution rates are advanced over the same period
- **THEN** they move by different amounts

#### Scenario: Trajectory versus value
- **WHEN** an effect is declared against `TargetValue` or `EvolutionRate`
- **THEN** it changes the variable's future trajectory rather than its current value

### Requirement: Economy, society and demography

`WorldState` SHALL group the economy, society, demography, public agenda and government, and SHALL NOT
accumulate unrelated game concepts.

#### Scenario: MVP economic indicators
- **WHEN** the economy is initialised from a scenario
- **THEN** it includes employment, inflation, economic growth, public debt and public deficit

#### Scenario: MVP social indicators
- **WHEN** society is initialised from a scenario
- **THEN** it includes education, inequality, security, welfare and institutional trust

#### Scenario: Differentiated cadence
- **WHEN** a simulated day is processed
- **THEN** the economy evolves on weekly boundaries, society on monthly boundaries, and demography on
  quarterly boundaries

#### Scenario: Structural indicators move slowly
- **WHEN** education and inflation are both advanced over one year with default parameters
- **THEN** education changes by a visibly smaller proportion of its range than inflation

#### Scenario: The world evolves without the player
- **WHEN** a game advances while the player party is in opposition and takes no action
- **THEN** economic, social and demographic indicators still change

### Requirement: Provinces as simulation units

The world SHALL model provinces as the unit of electoral and social simulation, each with a population,
a profile, a segment composition and electoral characteristics.

#### Scenario: Spanish constituency count
- **WHEN** the default scenario is loaded
- **THEN** it defines 52 constituencies, being 50 provinces plus Ceuta and Melilla

#### Scenario: Composition is non-negative
- **WHEN** a province's segment composition is validated
- **THEN** every segment weight is non-negative

#### Scenario: Composition reconciles to population
- **WHEN** a province's segment composition is validated
- **THEN** the weights normalise to the province's total population within a configured tolerance

#### Scenario: Demography changes composition gradually
- **WHEN** demographic evolution runs on a quarterly boundary
- **THEN** segment composition changes by a small configured increment rather than being reassigned

#### Scenario: No election-time adjustment
- **WHEN** an election is held
- **THEN** no code path alters provincial composition as part of computing the result

### Requirement: Per-province ideological offsets

Each population segment SHALL carry a per-province ideological offset, so that the same segment is not
politically identical in every constituency.

#### Scenario: Offsets exist
- **WHEN** a segment's ideology in a province is read
- **THEN** it is the segment's national profile combined with that province's offset

#### Scenario: Territorial axis is covered
- **WHEN** a scenario defines offsets
- **THEN** at minimum the territory axis can differ per province

#### Scenario: Regionalist vote becomes possible
- **WHEN** a party standing only in one region holds a strongly decentralising position
- **THEN** it can achieve a substantially higher share in provinces with a decentralising offset than
  elsewhere, with no per-province vote figure authored anywhere

### Requirement: Public agenda

The world SHALL maintain a public agenda of issue topics with relevance, trend and media attention, whose
dominance emerges from relevance rather than from a hard cap.

#### Scenario: Relevance is normalised
- **WHEN** the agenda is updated
- **THEN** topic relevance values are normalised across the catalogue

#### Scenario: Dominance is emergent
- **WHEN** the agenda is inspected
- **THEN** no rule enforces a fixed count of two to four dominant topics; dominance follows from relevance
  with decay

#### Scenario: Events and campaigns move the agenda
- **WHEN** a high-intensity stimulus on an issue is processed
- **THEN** that issue's relevance rises and others fall after normalisation

#### Scenario: Agenda gates attention
- **WHEN** a party acts on an issue whose relevance is near zero
- **THEN** the resulting perception change is substantially smaller than for a dominant issue

#### Scenario: Decay
- **WHEN** no further stimulus touches an issue for a configured period
- **THEN** its relevance decays toward its baseline
