# Spec Delta

## Purpose

Defines media outlets as independent actors that decide for themselves whether and how to cover what
happens, the coverage facts they produce, the pollster house effects that persist across months, and the
rule that exposure is not the same thing as approval.

## ADDED Requirements

### Requirement: Media outlets

A media outlet SHALL carry an editorial orientation, bounded audience, credibility, sensationalism and
independence, plus per-party affinities and per-issue interests.

#### Scenario: Outlet attributes
- **WHEN** an outlet is loaded from a scenario
- **THEN** it exposes orientation, audience, credibility, sensationalism, independence, party affinity and
  issue interest

#### Scenario: MVP outlet count
- **WHEN** the default scenario is loaded
- **THEN** it defines at least three outlets with materially different orientations

### Requirement: Independent coverage decisions

Each outlet SHALL decide independently whether to cover a stimulus, and SHALL derive its framing and tone
rather than having them authored.

#### Scenario: Pick-up probability
- **WHEN** an outlet evaluates a stimulus
- **THEN** its pick-up chance is computed from newsworthiness, its interest in the issue, and its
  sensationalism

#### Scenario: Different outlets, different decisions
- **WHEN** the same stimulus is offered to three outlets with different profiles
- **THEN** they can differ in whether they cover it, how prominently, and with what tone

#### Scenario: Orientation does not force tone
- **WHEN** an outlet with a strong editorial orientation covers an actor it usually opposes
- **THEN** tone is derived from editorial alignment plus noise and is not fixed by orientation alone

#### Scenario: Visibility gates pick-up
- **WHEN** a stimulus is marked `Internal`
- **THEN** its pick-up probability is substantially lower than for a `Public` stimulus

#### Scenario: Deterministic
- **WHEN** coverage is evaluated twice from the same state and seed
- **THEN** the same outlets make the same decisions, drawing from the `media` stream

### Requirement: Coverage facts

Coverage SHALL be recorded as a structured fact linking an outlet to an occurrence or stimulus with a
level, framing, tone, reach and publication date.

#### Scenario: Coverage is structured
- **WHEN** coverage is produced
- **THEN** it records outlet, source, level, framing, tone, reach and the game date of publication

#### Scenario: News text is not a domain concern
- **WHEN** the Domain produces coverage
- **THEN** it produces structured facts, and headline and body text are rendered outside the Domain

### Requirement: Exposure is not approval

Media impact on perception SHALL combine reach, credibility, tone and segment media sensitivity, so that
more coverage can be harmful.

#### Scenario: Negative coverage harms
- **WHEN** an actor receives wide coverage with a hostile tone
- **THEN** affected segments' trust falls rather than rising with exposure

#### Scenario: Segment sensitivity matters
- **WHEN** the same coverage reaches two segments with different media sensitivity
- **THEN** the segment with higher sensitivity shows the larger perception change

#### Scenario: Credibility weights impact
- **WHEN** two outlets with equal reach but different credibility publish the same tone
- **THEN** the more credible outlet produces the larger perception change

### Requirement: Outlet memory

An outlet SHALL retain a record of how it has recently framed an actor, so that its coverage is not
independent from one day to the next.

#### Scenario: Past framing biases future framing
- **WHEN** an outlet has framed an actor hostilely several times recently
- **THEN** its next framing of that actor is biased toward the same direction before noise is applied

#### Scenario: Memory decays
- **WHEN** no further coverage of an actor occurs for a configured period
- **THEN** the retained framing bias decays toward neutral

### Requirement: Pollster house effects

Each pollster SHALL carry a persistent house effect stored in state, so that its bias remains stable
across months.

#### Scenario: House effect persists
- **WHEN** the same pollster publishes in two consecutive months
- **THEN** the direction of its bias is consistent between the two polls

#### Scenario: House effect is state, not a draw
- **WHEN** a game is saved and reloaded
- **THEN** each pollster's house effect is unchanged
