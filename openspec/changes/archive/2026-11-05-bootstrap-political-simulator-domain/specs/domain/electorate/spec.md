# Spec Delta

## Purpose

Defines how votes are produced: population segments and their two distinct weight vectors, the perception
record, the centred utility function, the stable softmax that decides whom a voter chooses, the separate
turnout model that decides whether they vote at all, provincial aggregation, D'Hondt seat allocation, and
the polls that estimate — but never reveal — the hidden state.

## ADDED Requirements

### Requirement: Population segments

Segments SHALL be statistical aggregates carrying an ideological profile across six axes, an axis-weight
vector, an issue-weight vector, and behavioural parameters for turnout, loyalty, volatility, leader
sensitivity, media sensitivity and abstention propensity.

#### Scenario: MVP segments
- **WHEN** the default scenario is loaded
- **THEN** it defines six segments: young people, retired people, working class, self-employed and
  business owners, public-sector workers, and rural population

#### Scenario: Two weight vectors
- **WHEN** a segment is inspected
- **THEN** it carries an axis-weight vector used for ideological distance and a separate issue-weight
  vector used for issue salience

#### Scenario: Sensitivities are configured, not inferred
- **WHEN** a segment's sensitivity to an issue is read
- **THEN** it comes from configuration and is never derived from a left-or-right label

#### Scenario: Segments are not agents
- **WHEN** the model is inspected
- **THEN** no individual citizen is simulated

### Requirement: Ideological affinity

Affinity between a segment and a party SHALL be computed from the weighted absolute distance across the
six axes, using the party's perceived position.

#### Scenario: Identical profiles
- **WHEN** a segment and a party hold identical positions on every axis
- **THEN** affinity is 100

#### Scenario: Opposite profiles
- **WHEN** a segment and a party hold maximally opposed positions on every axis
- **THEN** affinity is 0

#### Scenario: Weights matter
- **WHEN** two segments with identical ideology but different axis weights evaluate the same party
- **THEN** their affinities differ

#### Scenario: Perceived, not declared
- **WHEN** affinity is computed
- **THEN** it uses the party's position as that segment perceives it, which is the declared position
  adjusted by the segment's `PositionDrift`

#### Scenario: Province offsets apply
- **WHEN** affinity is computed for a segment in a specific province
- **THEN** the segment's per-province ideological offset is applied first

### Requirement: Perception record

A segment-party perception SHALL store trust, respect and a position drift, and SHALL NOT store affinity.

#### Scenario: No stored affinity
- **WHEN** a perception record is inspected
- **THEN** it exposes `Trust`, `Respect` and `PositionDrift`, and no `Affinity` property

#### Scenario: Affinity counted once
- **WHEN** electoral utility is computed
- **THEN** ideological fit enters the sum exactly once

#### Scenario: Drift semantics
- **WHEN** a segment comes to see a party as more extreme than it declares
- **THEN** that is recorded as `PositionDrift` and changes the computed affinity

#### Scenario: Perception decays
- **WHEN** no stimulus touches a perception for a configured period
- **THEN** trust and respect decay exponentially toward their structural baselines, each with its own
  half-life and trust slower than respect

### Requirement: Electoral utility

Utility SHALL be a weighted sum of components each centred to the range −1 to +1, with segment weights
summing to 1.

#### Scenario: Centring
- **WHEN** a component expressed on a 0 to 100 scale enters the utility sum
- **THEN** it is transformed by `(x − 50) / 50` first

#### Scenario: Components
- **WHEN** utility is computed
- **THEN** it combines ideological fit, trust, respect, government or opposition performance, media
  perception, leader evaluation and provincial presence

#### Scenario: Weights normalise
- **WHEN** a segment's utility weights are loaded
- **THEN** they sum to 1, or are normalised before use, and the validator rejects a set that cannot be
  normalised

#### Scenario: Presence modifies utility
- **WHEN** provincial presence changes
- **THEN** only the utility changes; no vote figure is added anywhere

### Requirement: Choice probability by stable softmax

The probability a voting segment chooses a party SHALL be computed by a numerically stable softmax over
the parties standing in that constituency plus blank vote.

#### Scenario: Stability
- **WHEN** the softmax is computed
- **THEN** the maximum utility is subtracted before exponentiation

#### Scenario: Probabilities sum to one
- **WHEN** probabilities are computed for a segment in a province
- **THEN** they sum to 1 within the documented floating-point tolerance

#### Scenario: Only standing parties
- **WHEN** a party does not stand in a constituency
- **THEN** it is absent from that constituency's softmax denominator and receives no votes there

#### Scenario: Abstention is not in the softmax
- **WHEN** the softmax alternatives are inspected
- **THEN** they are the standing parties plus blank vote, and abstention is not among them

#### Scenario: Undecided is a polling concept
- **WHEN** the hidden state is inspected
- **THEN** no "undecided" category exists

### Requirement: Turnout

Turnout SHALL be computed separately from party choice and SHALL decide whether a segment votes.

#### Scenario: Separate calculation
- **WHEN** provincial votes are computed
- **THEN** turnout is evaluated before and independently of the choice probabilities

#### Scenario: Turnout inputs
- **WHEN** turnout is computed
- **THEN** it combines base turnout, segment turnout propensity, enthusiasm and mobilisation,
  disengagement, and campaign effects

#### Scenario: Turnout is bounded
- **WHEN** turnout is computed
- **THEN** the result lies between 0 and 1

### Requirement: Preference inertia

Preferences SHALL move toward their newly computed target by a volatility expressed per 30 days and
converted to the actual elapsed step.

#### Scenario: Blending
- **WHEN** preferences are recalculated after an elapsed interval
- **THEN** the new preference is `(1 − lambda) × previous + lambda × target`

#### Scenario: Step conversion
- **WHEN** the elapsed interval is `dt` days and the configured volatility is `lambda_30`
- **THEN** the applied lambda is `1 − (1 − lambda_30) ^ (dt / 30)`

#### Scenario: Cadence independence
- **WHEN** the same simulated period is advanced in weekly steps and in daily steps
- **THEN** the resulting preferences agree within the documented tolerance

#### Scenario: Renormalisation
- **WHEN** preferences have been blended
- **THEN** they are normalised to sum to 1

#### Scenario: No instant jumps
- **WHEN** a large perception change occurs
- **THEN** preferences move partway toward the new target rather than reaching it immediately

### Requirement: Provincial aggregation

Votes SHALL be aggregated per province from that province's own segment composition, never by
distributing a national share.

#### Scenario: Aggregation formula
- **WHEN** votes for a party in a province are computed
- **THEN** they are the sum over segments of population × turnout × choice probability

#### Scenario: Composition drives difference
- **WHEN** two provinces with different segment compositions are computed from the same national state
- **THEN** their vote shares differ

#### Scenario: No national distribution
- **WHEN** the calculators are inspected
- **THEN** no code path computes a national share and apportions it across provinces

### Requirement: Seat allocation

Seats SHALL be allocated per constituency using the pinned scenario's electoral system configuration.

#### Scenario: Default chamber and constituencies
- **WHEN** the default electoral system is loaded
- **THEN** it defines 350 seats across 52 constituencies

#### Scenario: Seat distribution across constituencies
- **WHEN** constituency seat counts are computed
- **THEN** each province receives a minimum of 2 seats, Ceuta and Melilla receive 1 each, and the
  remainder is distributed by population using largest remainder

#### Scenario: Threshold base
- **WHEN** the threshold is applied
- **THEN** it is 3 percent of valid votes in that constituency, where valid votes include blank votes

#### Scenario: D'Hondt
- **WHEN** seats are allocated among parties above the threshold
- **THEN** the D'Hondt divisor method is used

#### Scenario: Tie resolution
- **WHEN** two parties tie for the final seat under D'Hondt
- **THEN** the party with more total votes wins; if still tied, the winner is drawn from the `ties` random
  stream

#### Scenario: Configuration, not constants
- **WHEN** the allocation code is inspected
- **THEN** chamber size, constituency list, minimum seats and threshold come from configuration

#### Scenario: Pure and deterministic
- **WHEN** the allocator is called twice with the same votes and seed
- **THEN** it returns identical results and touches no state

### Requirement: Candidacies per constituency

A party SHALL stand only in the constituencies where it presents a candidacy, so that regional parties
are representable.

#### Scenario: Regional party
- **WHEN** a party declares candidacies in only four provinces
- **THEN** it appears in those four constituencies' calculations and nowhere else

#### Scenario: Votes elsewhere are impossible
- **WHEN** results are aggregated nationally
- **THEN** a party receives zero votes in every constituency where it does not stand

### Requirement: Polls estimate, never reveal

A poll SHALL be an estimate generated from turnout-weighted hidden preferences with sampling noise and a
persistent house effect, and SHALL be distinct from both the hidden state and the election result.

#### Scenario: Three distinct things
- **WHEN** the model is inspected
- **THEN** hidden preference, poll estimate and election result are separate values

#### Scenario: Sampling noise
- **WHEN** a poll is generated with sample size `n`
- **THEN** noise is derived from the standard error `sqrt(p(1 − p) / n)`

#### Scenario: Poll contents
- **WHEN** a poll is published
- **THEN** it records pollster, sample size, geographic coverage, margin of error, publication date and
  estimated shares

#### Scenario: Reproducibility
- **WHEN** the same game is replayed with the same seed
- **THEN** identical polls are produced, drawn from the `polls` stream

#### Scenario: Result may differ from the last poll
- **WHEN** an election follows a poll
- **THEN** the result may differ because of sampling error, turnout and late preference movement

#### Scenario: Default frequency
- **WHEN** the default scenario runs
- **THEN** polls are published monthly

### Requirement: Calibration tests precede content

Two calibration tests SHALL be written before any content exists, to pin the behaviour of the utility and
softmax pair.

#### Scenario: Identical parties
- **WHEN** two parties are identical in every respect
- **THEN** they receive identical shares

#### Scenario: Documented advantage
- **WHEN** one party holds a utility advantage of 0.1 at the default temperature
- **THEN** its share gain matches the documented expected value

#### Scenario: Temperature range
- **WHEN** the default segment temperature is read
- **THEN** it lies in the interpretable range of roughly 0.1 to 0.3
