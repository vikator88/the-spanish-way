# Spec Delta

## Purpose

Defines political parties as state components: their ideology, cohesion, credibility, budget, membership,
territorial presence and inter-party relations — together with the parliament that owns seat counts, the
government formation rules, and the explicit policy that decides whether a party survives.

## ADDED Requirements

### Requirement: Party state

A party SHALL be a state component inside `GameState` carrying its declared ideology, bounded cohesion
and credibility, budget, resources, memberships and provincial presence.

#### Scenario: Bounded attributes
- **WHEN** cohesion or credibility is changed
- **THEN** the resulting value remains within its configured range, normally 0 to 100

#### Scenario: Ideology axes
- **WHEN** a party's declared ideology is read
- **THEN** it exposes six positions, each in the range −100 to +100, matching the segment axes

#### Scenario: No public setters
- **WHEN** the party component's surface is inspected
- **THEN** every attribute change happens through a named method such as `ChangeLeader`, `Expel`,
  `Promote`, `SpendBudget` or `AdjustCohesion`

#### Scenario: Losing an election does not dissolve a party
- **WHEN** a party loses every seat at an election
- **THEN** its status is unchanged unless the survival policy separately decides otherwise

### Requirement: Party membership

Membership SHALL link a character to a party with a role, a joining game date, bounded internal support
and influence, and an explicit status.

#### Scenario: No duplicate active membership
- **WHEN** a character who already holds an active membership in a party is added to it again
- **THEN** the operation fails

#### Scenario: Explicit transitions
- **WHEN** a character leaves a party
- **THEN** it happens through a named transition — resignation, expulsion or defection — and not by
  deleting a record silently

#### Scenario: Leader must be a member
- **WHEN** a character who is not an eligible member is set as leader
- **THEN** the operation fails unless the scenario explicitly permits an interim leader

#### Scenario: Leadership change records an event
- **WHEN** a party's leader changes
- **THEN** a `PartyLeaderChanged` domain event is recorded

#### Scenario: Internal actions have consequences
- **WHEN** a popular figure is expelled
- **THEN** the operation emits stimuli and may schedule follow-up events, rather than only mutating the
  membership record

### Requirement: Territorial presence

A party SHALL hold a per-province presence carrying organisational strength, local leadership strength
and territorial influence, which modifies utility and mobilisation only.

#### Scenario: Presence does not award votes
- **WHEN** territorial presence increases in a province
- **THEN** the change enters the electoral utility calculation and no vote share is added directly

#### Scenario: Presence is per province
- **WHEN** presence is read
- **THEN** it is defined per `(PartyId, ProvinceId)` pair

### Requirement: Party relationships

Relationships between parties SHALL be directed and may be asymmetric, carrying bounded affinity, trust
and respect.

#### Scenario: Asymmetry is permitted
- **WHEN** party A's relationship toward party B is set
- **THEN** party B's relationship toward party A is unchanged

#### Scenario: Ideology does not imply trust
- **WHEN** two parties hold nearly identical ideologies
- **THEN** no rule automatically raises their mutual trust or makes them coalition-compatible

### Requirement: Parliament owns seats

Seat counts SHALL be held by a `Parliament` component produced by elections, not by party attributes.

#### Scenario: Seats come from an election
- **WHEN** seats change
- **THEN** the change originates from a completed election's seat allocation

#### Scenario: Majority calculation
- **WHEN** a majority test is needed
- **THEN** `Parliament` answers it from its current seat distribution and the configured chamber size

#### Scenario: Default chamber
- **WHEN** the default scenario is loaded
- **THEN** the chamber has 350 seats and an absolute majority is 176

### Requirement: Government state and formation

The game SHALL model a national government with a lead party, coalition members, external parliamentary
supporters, a start date and bounded stability, formed through a validated domain operation.

#### Scenario: Coalition and support are distinct
- **WHEN** a government is formed
- **THEN** coalition membership and external parliamentary support are recorded separately

#### Scenario: Members must exist
- **WHEN** a government references a party absent from the game
- **THEN** formation fails

#### Scenario: Investiture first vote
- **WHEN** an investiture vote is held for the first time
- **THEN** it succeeds only with an absolute majority of the chamber

#### Scenario: Investiture second vote
- **WHEN** a first investiture vote has failed and a second is held
- **THEN** it succeeds with a simple majority of votes cast

#### Scenario: Failed formation forces an election
- **WHEN** no government is invested within the configured period of two months
- **THEN** a repeat election is scheduled and the phase remains `GovernmentFormation` until it is held

#### Scenario: Opposition is a valid position
- **WHEN** the player party is in neither government nor support
- **THEN** no electoral penalty is applied for being in opposition

#### Scenario: Government formation records an event
- **WHEN** a government is successfully formed
- **THEN** a `GovernmentFormed` domain event is recorded

### Requirement: Parliamentary voting

Bills SHALL arrive as simulation events, and each party SHALL vote by rule rather than by authored
outcome.

#### Scenario: Government parties support their bill
- **WHEN** a bill proposed by the government is voted
- **THEN** coalition parties vote in favour by default

#### Scenario: Others vote by distance and trust
- **WHEN** a party outside the government votes on a bill
- **THEN** its vote is derived from its ideological distance to the bill and its trust in the proposer

#### Scenario: The player chooses
- **WHEN** the player party is asked to vote
- **THEN** the vote is presented as a `ParliamentaryVote` action rather than decided automatically

### Requirement: Party survival policy

Party dissolution SHALL be decided by an explicit, configurable policy evaluated at month end, never by a
single election result.

#### Scenario: Default dissolution triggers
- **WHEN** the survival policy is evaluated at a month end
- **THEN** the party dissolves if any of the configured conditions hold: budget negative for six
  consecutive months, cohesion below 15, or no seat in two consecutive general elections

#### Scenario: Thresholds are configuration
- **WHEN** the thresholds are read
- **THEN** they come from the pinned scenario's configuration, not from constants in the rules

#### Scenario: Edge cases are explicit
- **WHEN** a party reaches zero seats, loses its leader, or merges with another party
- **THEN** the policy states the outcome for each case rather than leaving it undefined

#### Scenario: Player party dissolution ends the game
- **WHEN** the player party dissolves
- **THEN** a `PartyDissolved` domain event is recorded and the game-over policy is evaluated

#### Scenario: Survival is not a vote threshold
- **WHEN** a party's vote share falls below any particular figure
- **THEN** no dissolution follows from that fact alone
