# Spec Delta

## Purpose

Defines political characters as state components with independent public and internal standing, their
personality traits, their directed relationships with one another, and the per-segment leader evaluation
that feeds the electoral model.

## ADDED Requirements

### Requirement: Character state

A political character SHALL be a state component carrying a name, current position, an ideological
profile and bounded popularity, charisma, loyalty, ambition and negotiation skill.

#### Scenario: Bounded traits
- **WHEN** any character trait is changed
- **THEN** the resulting value remains in its documented range, normally 0 to 100

#### Scenario: Party affiliation is derived
- **WHEN** a character's party is needed
- **THEN** it is read from active memberships rather than from a property on the character

#### Scenario: Ideology uses the same axes
- **WHEN** a character's ideology is read
- **THEN** it exposes the same six axes as segments and parties, each in −100 to +100

#### Scenario: Status transitions are explicit
- **WHEN** a character retires, dies or is otherwise removed from play
- **THEN** it happens through a named transition that records the reason

### Requirement: Public popularity and internal standing are separate

A character's standing with the public SHALL be distinct from their standing inside their party.

#### Scenario: Popular outside, weak inside
- **WHEN** a character has high `Popularity` and low membership `InternalSupport`
- **THEN** both values are valid simultaneously and no rule reconciles them

#### Scenario: Different consumers
- **WHEN** the electoral model evaluates leadership
- **THEN** it reads public popularity; when the internal reaction calculator evaluates a leadership
  challenge, it reads internal support

### Requirement: Character relationships

Relationships between characters SHALL be directed and may be asymmetric, carrying bounded affinity,
trust and respect.

#### Scenario: Asymmetry is permitted
- **WHEN** character A's relationship toward character B is set
- **THEN** character B's relationship toward A is unchanged

#### Scenario: Valid endpoints
- **WHEN** a relationship references a character absent from the game
- **THEN** the operation fails

### Requirement: Ambition and loyalty drive internal behaviour

Character traits SHALL be readable by event conditions and by the internal reaction calculator, so that
internal politics emerges from traits rather than from authored outcomes.

#### Scenario: Ambition gates a leadership challenge
- **WHEN** a leadership-challenge event's trigger conditions are evaluated
- **THEN** they can reference a candidate's ambition, internal support and relationship with the leader

#### Scenario: Internal reactions follow an internal action
- **WHEN** a member is expelled or demoted
- **THEN** the internal reaction calculator may produce loyalty changes, resignations, defections or
  follow-up events depending on traits and relationships

#### Scenario: No universal rule
- **WHEN** the calculator is inspected
- **THEN** it contains no rule of the form "ambitious characters always defect"; behaviour is governed by
  configured parameters

### Requirement: Per-segment leader evaluation

Leadership evaluation entering electoral utility SHALL be derived per segment rather than taken as one
national popularity number.

#### Scenario: Derived per segment
- **WHEN** the leader evaluation for a segment is computed
- **THEN** it combines national popularity, charisma and the ideological fit between the leader and that
  segment

#### Scenario: Segments disagree
- **WHEN** two segments with opposing ideological profiles evaluate the same leader
- **THEN** they can produce materially different evaluations

#### Scenario: Not authored
- **WHEN** content is validated
- **THEN** no effect may write a per-segment leader evaluation directly
