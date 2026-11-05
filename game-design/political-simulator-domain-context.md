# Political Simulator --- Domain & Application Context

**Document type:** Engineering context / domain specification\
**Version:** 0.1\
**Status:** Initial MVP baseline\
**Language:** English identifiers in code; Spanish content/UI is
acceptable\
**Primary stack:** .NET / C# backend, React + TypeScript frontend,
PostgreSQL persistence

------------------------------------------------------------------------

## 1. Purpose of this document

This document is the source of truth for implementing the **Domain** and
an initial, deliberately thin **Application** layer for the political
simulation game.

The game is a political and social systems simulator. The player leads a
political party and tries to keep it alive for as long as possible.
Winning elections is useful but is not the ultimate objective: a party
can survive without governing, and it can win an election while still
being at risk of internal collapse.

The implementation must prioritize:

1.  Explicit business rules and invariants in the Domain.
2.  A coherent, understandable Domain object model.
3.  A simulation model whose rules and content can be extended without
    creating a class for every event or action.
4.  A thin Application layer that coordinates use cases but does not own
    business rules.
5.  A simulation engine that can be tested independently of HTTP, EF
    Core, and the UI.

This is an MVP specification, not a requirement to implement every
future feature immediately. Where a rule is not fully specified, prefer
a simple, explicit, configurable implementation over speculative
complexity.

------------------------------------------------------------------------

# 2. Product and simulation principles

## 2.1 Core gameplay

-   A game represents one political world and one player's campaign to
    keep their party alive.
-   The game advances on a daily calendar.
-   Elections normally occur in four-year cycles.
-   The player can be in government, in a coalition, provide
    parliamentary support, or remain in opposition.
-   The world continues evolving even when the player is not governing.
-   The player makes decisions in response to events and can also
    initiate actions voluntarily.
-   Decisions do not directly add or subtract voting-intention
    percentages.
-   Decisions affect the world, perceptions, relationships, public
    agenda, media coverage, party organization, and other intermediate
    state. The electoral model converts those changes into voter
    preferences.
-   The game should produce understandable but not entirely predictable
    outcomes.

## 2.2 Design principles

1.  **Emergent outcomes:** election results emerge from the simulation;
    actions must not directly award votes.
2.  **World autonomy:** society, economy, media, rival parties, and
    external events continue evolving independently of the player.
3.  **Imperfect information:** the player sees estimates and selected
    indicators, not every internal value or formula.
4.  **Heterogeneous reactions:** population segments and political
    actors react differently according to their profiles, interests,
    relationships, and context.
5.  **Memory:** perceptions and relationships persist and may leave
    long-term effects.
6.  **Data-driven content:** events, conditions, effects, and most
    action definitions are data/configuration, not dedicated classes.
7.  **Controlled randomness:** random outcomes must be reproducible from
    a game seed and a known sequence of simulation operations.
8.  **Pragmatic DDD:** use aggregates to protect meaningful invariants;
    do not force every concept into an aggregate or create one giant
    `Game` aggregate containing the entire simulation.
9.  **Separation of concerns:** Domain rules are independent of
    persistence, HTTP, UI, and external services.
10. **Incremental MVP:** implement a small set of reusable mechanics
    first; expand content and sophistication later.

------------------------------------------------------------------------

# 3. Bounded contexts / domain areas

The initial model can be organized into these logical areas. They do not
need to become separate deployable services.

-   **Game Management:** game identity, lifecycle, date, player party,
    scenario/configuration references.
-   **World Simulation:** economy, society, demographics, provinces,
    public agenda.
-   **Political Organizations:** parties, party membership, resources,
    government and party relationships.
-   **Political Actors:** individual characters and their relationships.
-   **Media:** media outlets, coverage and published news.
-   **Events and Actions:** event definitions, occurrences, action
    definitions, executions, effects and conditions.
-   **Electoral Simulation:** population segments, perceptions,
    electoral preferences, polls, elections and seat allocation.

A modular monolith is the intended deployment architecture.

------------------------------------------------------------------------

# 4. Domain model: entities, value objects and services

The following is the proposed conceptual class model. Names are
suggestions, but the concepts and responsibilities should be preserved.

## 4.1 Game and world state

### `Game` --- Aggregate Root

Represents one running game.

Suggested attributes:

-   `GameId Id`
-   `string Name`
-   `GameStatus Status`
-   `GameDate CurrentDate`
-   `PartyId PlayerPartyId`
-   `ScenarioId ScenarioId`
-   `int ElectoralCycle`
-   `int Revision`
-   `int StateVersion`
-   `int SimulationVersion`
-   `RandomSeed RandomSeed`
-   `DateTimeOffset CreatedAt`
-   `DateTimeOffset? FinishedAt`

Responsibilities and invariants:

-   A game has exactly one player-controlled party.
-   The player party must exist in the game.
-   A game can only advance while in a playable status.
-   The game date moves forward; it must never move backwards.
-   A game cannot execute an action after it has finished.
-   A game must not be advanced twice concurrently from the same
    revision.
-   The game ends when its configured terminal condition is reached (for
    the MVP, party disappearance or another explicitly configured end
    condition).

`Game` should not directly contain every party, character, province,
media outlet, and historical record as child entities. The simulation
state may be represented separately and loaded by the simulation
coordinator.

### `GameState`

Represents the mutable simulation state at a particular point in time.
It may be persisted as a serialized snapshot (e.g. JSONB), but it is a
Domain-level concept and must not depend on EF Core or JSON
implementation details.

Suggested contents:

-   Current date and game phase.
-   Current government.
-   Party states.
-   Character states.
-   Economy state.
-   Society state.
-   Demographic state.
-   Province state.
-   Public agenda.
-   Active event occurrences.
-   Pending effects.
-   Current electoral preferences/perceptions.
-   Random generator state or deterministic random sequence position.

The state must be sufficiently complete to resume a game without relying
on hidden in-memory process state.

### `GameDate` --- Value Object

-   Year
-   Month
-   Day

Must support safe date arithmetic and comparison. The simulation uses
the game's calendar, not the server's wall-clock time.

### `GameStatus` --- Enum

Suggested values:

-   `Created`
-   `InProgress`
-   `Paused`
-   `Finished`
-   `Abandoned`

### `GamePhase` --- Enum

Suggested values:

-   `Normal`
-   `ElectionCampaign`
-   `ElectionDay`
-   `GovernmentFormation`
-   `GameOver`

### `GameConfiguration` / `ScenarioDefinition`

Immutable configuration used to initialize a game.

Contains references or definitions for:

-   Initial parties.
-   Initial characters and party roles.
-   Population segments and profiles.
-   Provinces and provincial compositions.
-   Media outlets.
-   Initial economy and society values.
-   Event/action catalogues.
-   Simulation parameters.
-   Electoral system.

A scenario definition is not the mutable state of a running game.
Multiple games can be created from the same scenario.

------------------------------------------------------------------------

## 4.2 World, economy, society and demographics

### `WorldState`

Logical container for the state of the simulated world:

-   `EconomyState Economy`
-   `SocietyState Society`
-   `DemographicState Demographics`
-   `PublicAgenda PublicAgenda`
-   `GovernmentState Government`

Do not make `WorldState` a dumping ground for every game concept. It
groups global world-level state only.

### `EconomyState`

Contains economic indicators and their historical/trend information.

MVP indicators:

-   Employment.
-   Inflation.
-   Economic growth.
-   Public debt.
-   Public deficit.

### `SocietyState`

MVP indicators:

-   Education.
-   Inequality.
-   Security.
-   Welfare.
-   Institutional trust.

### `WorldVariable` --- Value Object or small entity

A configurable numeric indicator.

Suggested attributes:

-   `string Key`
-   `decimal CurrentValue`
-   `decimal MinimumValue`
-   `decimal MaximumValue`
-   `decimal? TargetValue`
-   `decimal EvolutionRate`
-   `VariableDirection Direction`
-   `VariableMetadata Metadata`

Rules:

-   Values must remain within configured bounds.
-   The meaning of a high value must be explicit. For example, high
    employment is good, while high inflation is generally bad.
-   Variables must not all evolve at the same rate.
-   An effect may change a variable immediately or alter its future
    trajectory.
-   A variable's current value is not automatically a measure of
    government performance; attribution is a separate calculation.

Potential implementation: use `EconomicVariable` and `SocialVariable`
wrappers or typed collections if that improves safety. Do not create a
separate class for each individual indicator unless it has genuinely
different behavior.

### `DemographicState`

Contains population segments and their weights by province.

Demographic changes are generally slower than economic or perception
changes. The MVP can use quarterly updates.

### `Province`

Suggested attributes:

-   `ProvinceId Id`
-   `string Name`
-   `PopulationSize Population`
-   `ProvinceProfile Profile`
-   Segment population composition.
-   Economic/social characteristics.
-   Electoral characteristics.

A province is an electoral and social simulation unit. For the Spanish
Congress approximation, use 52 constituencies (50 provinces plus Ceuta
and Melilla).

### `PopulationComposition`

Maps each population segment to a provincial population count or
normalized weight.

Rules:

-   Segment weights within a province must be non-negative.
-   Their total must be normalized or reconcilable to the province's
    population.
-   Demographic evolution changes composition gradually, not by
    arbitrary election-time adjustments.

------------------------------------------------------------------------

## 4.3 Population segments and ideological profiles

### `PopulationSegment`

A statistical aggregate, not an individual citizen.

Suggested attributes:

-   `PopulationSegmentId Id`
-   `string Name`
-   `IdeologicalProfile Ideology`
-   `IssueSensitivity[] IssueSensitivities`
-   `decimal BaseTurnout`
-   `decimal ElectoralLoyalty`
-   `decimal ElectoralVolatility`
-   `decimal LeaderSensitivity`
-   `decimal MediaSensitivity`
-   `decimal AbstentionPropensity`

MVP segments:

1.  Young people.
2.  Retired people.
3.  Working class.
4.  Self-employed / business owners.
5.  Public-sector workers.
6.  Rural population.

Segments are not stereotypes. They represent average distributions. A
segment's sensitivity to an issue must be configurable and should not be
inferred from a simplistic left/right label.

### `IdeologicalProfile` --- Value Object

Six axes, each in the range `[-100, +100]`:

-   Economy: interventionism ↔ liberalism.
-   Social values: progressivism ↔ conservatism.
-   Territory: centralism ↔ decentralization.
-   Immigration: openness ↔ restriction.
-   Institutions: regeneration ↔ continuity.
-   Security: civil-liberties orientation ↔ authority.

The exact labels are descriptive; the numeric direction must remain
consistent across parties and segments.

### `IssueSensitivity` --- Value Object

-   `IssueType Issue`
-   `decimal Weight`

Weights represent the importance of a topic to a segment. Normalize
weights during calculation or configuration validation.

------------------------------------------------------------------------

## 4.4 Political parties and government

### `PoliticalParty` --- Aggregate Root (or independently managed aggregate)

Suggested attributes:

-   `PartyId Id`
-   `string Name`
-   `PartyIdeology Ideology`
-   `PartyStatus Status`
-   `decimal Budget`
-   `decimal Cohesion`
-   `decimal Credibility`
-   `int Seats`
-   `CharacterId? LeaderId`
-   `PartyResource[] Resources`
-   Territorial organization/presence.
-   Membership and leadership references.

Rules:

-   Cohesion and credibility remain in their configured ranges (normally
    0--100).
-   A party cannot have a leader who is not a valid member/eligible
    character, unless the scenario explicitly permits an interim leader.
-   Party membership and leadership changes must be validated.
-   A party can become inactive or disappear only through explicit
    domain rules.
-   Loss of an election does not automatically mean party disappearance.
-   A party in opposition is not automatically penalized electorally.
-   Party survival is determined by a separate
    survival/terminal-condition policy, not by a single election result.

Avoid putting all game state inside the `PoliticalParty` aggregate.
Party-level invariants belong here; cross-world calculations belong in
domain services.

### `PartyIdeology`

Six positions matching `IdeologicalProfile`, each in `[-100, +100]`.

### `PartyMembership`

Suggested attributes:

-   `PartyId PartyId`
-   `CharacterId CharacterId`
-   `PartyRole Role`
-   `DateTimeOffset JoinedAt`
-   `decimal Loyalty`
-   `decimal Influence`
-   `MembershipStatus Status`

Rules:

-   A character cannot have duplicate active membership in the same
    party.
-   Expulsion, resignation, promotion and appointment are explicit state
    transitions.
-   Internal actions may trigger reactions and follow-up events.

### `PartyProvincePresence`

-   `PartyId PartyId`
-   `ProvinceId ProvinceId`
-   `decimal OrganizationalStrength`
-   `decimal LocalLeadershipStrength`
-   `decimal TerritorialInfluence`

Territorial presence modifies local political utility or mobilization;
it must not directly award a fixed vote percentage.

### `PartyRelationship`

Directed/bilateral relationship between two parties:

-   `PartyId SourcePartyId`
-   `PartyId TargetPartyId`
-   `decimal Affinity`
-   `decimal Trust`
-   `decimal Respect`

Relationship values are normally 0--100. Relationships need not be
symmetrical. Shared ideology does not imply friendship or coalition
compatibility.

### `GovernmentState`

Represents the current national government.

-   `PartyId LeadPartyId`
-   Coalition party IDs.
-   Parliamentary support party IDs.
-   `GameDate StartDate`
-   `GameDate? EndDate`
-   `decimal Stability`

Rules:

-   Government composition must refer to parties in the game.
-   Coalition membership and external parliamentary support are
    distinct.
-   A party may be in opposition, in government, or supporting
    government without holding ministries.
-   Government formation should be a domain operation that validates
    parliamentary support and political constraints.

For the MVP, a simplified national government is sufficient. Detailed
regional governments can be deferred.

### `PartyResource`

-   `ResourceType Type`
-   `decimal Amount`

MVP may initially use budget and organizational strength only. Do not
introduce a large resource economy until the gameplay requires it.

------------------------------------------------------------------------

## 4.5 Political characters

### `PoliticalCharacter`

Suggested attributes:

-   `CharacterId Id`
-   `string Name`
-   `PartyId? PartyId`
-   `PoliticalPosition CurrentPosition`
-   `decimal Popularity`
-   `decimal Charisma`
-   `decimal Loyalty`
-   `decimal Ambition`
-   `decimal NegotiationSkill`
-   `IdeologicalProfile Ideology`
-   `CharacterStatus Status`

Rules:

-   Popularity, charisma, loyalty, ambition and negotiation skill use
    documented ranges (normally 0--100).
-   A character may be popular with the public but unpopular within
    their party.
-   Personal ambition and loyalty can influence internal behavior and
    event conditions.
-   A character's party affiliation and role can change through explicit
    actions.

### `CharacterRelationship`

-   `CharacterId SourceCharacterId`
-   `CharacterId TargetCharacterId`
-   `decimal Affinity`
-   `decimal Trust`
-   `decimal Respect`

Do not assume these relationships are symmetrical.

------------------------------------------------------------------------

## 4.6 Media and public agenda

### `MediaOutlet`

Suggested attributes:

-   `MediaOutletId Id`
-   `string Name`
-   `EditorialOrientation EditorialOrientation`
-   `decimal Audience`
-   `decimal Credibility`
-   `decimal Sensationalism`
-   `decimal Independence`
-   Party affinity profile.
-   Issue interests.

Rules:

-   Media outlets decide independently whether to cover an event.
-   A media outlet may frame the same event differently from another
    outlet.
-   Exposure is not equivalent to positive approval.
-   Aggregate media impact must account for audience, credibility,
    tone/framing and segment sensitivity.
-   A media outlet's editorial orientation must not force every article
    to have the same tone.

### `MediaCoverage`

A particular outlet's coverage of a particular occurrence.

-   `MediaCoverageId Id`
-   `MediaOutletId OutletId`
-   `EventOccurrenceId EventOccurrenceId`
-   `CoverageLevel Level`
-   `MediaFraming Framing`
-   `MediaTone Tone`
-   `decimal Reach`
-   `GameDate PublishedAt`

### `NewsArticle`

A published item, not the event itself.

-   `NewsArticleId Id`
-   `string Headline`
-   `string Content`
-   `MediaOutletId OutletId`
-   `EventOccurrenceId? EventOccurrenceId`
-   Topic.
-   Related party/character IDs.
-   Publication date.

News content generation is not a core Domain responsibility. The Domain
should provide facts and structured outcomes; an Application or
infrastructure component may render those facts into text.

### `PublicAgenda`

Contains the currently dominant public topics.

### `AgendaTopic`

-   `AgendaTopicId Id`
-   `IssueType Issue`
-   `decimal Relevance`
-   `decimal Trend`
-   `decimal MediaAttention`
-   Segment importance profile.
-   `GameDate LastUpdatedAt`

Rules:

-   Normally 2--4 topics dominate the agenda at a given time.
-   Events and campaigns can increase or decrease topic relevance.
-   Agenda relevance influences attention to party positions and
    actions.
-   A policy proposal can have little electoral impact if its topic has
    little public attention.

------------------------------------------------------------------------

# 5. Events, actions, conditions and effects

This area must be data-driven. Do not create a C# class for every named
event or every possible decision.

## 5.1 Event definition vs event occurrence

### `GameEventDefinition`

Immutable/configurable definition:

-   `EventDefinitionId Id`
-   `string Name`
-   `string DescriptionTemplate`
-   `EventCategory Category`
-   `decimal BaseProbability`
-   `ConditionGroup TriggerConditions`
-   `EffectDefinition[] Effects`
-   `ActionDefinitionId[] AvailableActions`
-   `EventDefinitionId[] FollowUpEvents`
-   Optional duration/cooldown/expiry.
-   Tags.

Event categories:

-   Random.
-   Conditional.
-   Chain/follow-up.

These can be enum values or configuration metadata; dedicated subclasses
are not required for the MVP.

### `EventOccurrence`

A concrete occurrence in a game:

-   `EventOccurrenceId Id`
-   `EventDefinitionId DefinitionId`
-   `GameDate TriggeredAt`
-   `EventOccurrenceStatus Status`
-   `GameDate? ExpiresAt`
-   `ActionExecutionId? SelectedActionExecutionId`
-   Applied effect references.
-   Context/snapshot data required to explain the occurrence.

Rules:

-   One event definition can occur multiple times, subject to cooldown
    and trigger rules.
-   An occurrence is distinct from its definition.
-   A resolved occurrence cannot be resolved again.
-   An expired occurrence cannot accept a player decision.
-   Follow-up events are scheduled/eligible from the outcome of an
    occurrence, not automatically assumed to happen.

## 5.2 Action definition vs action execution

### `ActionDefinition`

Configurable description of an available action:

-   `ActionDefinitionId Id`
-   `string Name`
-   `string DescriptionTemplate`
-   `ActionCategory Category`
-   `ConditionGroup AvailabilityConditions`
-   `EffectDefinition[] Effects`
-   Cost definition.
-   Duration/cooldown.
-   Tags.
-   Optional parameter schema.

Three primary action categories:

1.  `PublicPosition`: public statements, support/condemnation, reactions
    to events, introducing topics.
2.  `ParliamentaryVote`: votes on laws, budgets, motions, investitures
    and proposals.
3.  `InternalParty`: hire/fire, promote, expel, reorganize, replace
    leadership, appoint spokespersons.

### `ActionExecution`

A concrete action performed in a game:

-   `ActionExecutionId Id`
-   `ActionDefinitionId DefinitionId`
-   `ActorId ActorId`
-   `GameDate ExecutedAt`
-   Parameters selected by the player.
-   `ActionExecutionStatus Status`
-   Applied effects.
-   Result/summary.

Rules:

-   An action can only execute if its availability conditions are
    satisfied.
-   Costs must be validated and paid atomically with the action.
-   An action cannot be executed twice if its definition/occurrence is
    single-use.
-   The action's effects are applied only after validation succeeds.
-   A failed action must not partially mutate the game state.
-   Some actions are event responses; others are voluntary actions.
-   The same action definition can produce different outcomes in
    different contexts.

The Domain should expose meaningful operations such as `Execute`,
`Resolve`, `Apply`, or domain-specific methods rather than allowing
arbitrary property mutation.

## 5.3 Conditions

### `ConditionGroup`

Represents AND/OR composition of conditions.

### `Condition`

Generic data-driven predicate:

-   Target type/reference.
-   Attribute/key.
-   Operator (`Equal`, `NotEqual`, `GreaterThan`, `LessThan`, `InRange`,
    `Exists`, etc.).
-   Expected value.
-   Optional context/tag.

Conditions can inspect game state but must not execute arbitrary code or
expressions supplied by content data.

Validate condition definitions when loading scenario content.
Unsupported attributes/operators should fail configuration validation,
not silently evaluate as true.

## 5.4 Effects

### `EffectDefinition`

A data-driven instruction to change state:

-   Target type.
-   Target selector/filter.
-   Attribute/key.
-   Operation (`Add`, `Subtract`, `Multiply`, `Set`, `MoveTowards`,
    etc.).
-   Value or value expression from a restricted supported set.
-   Duration.
-   Delay.
-   Conditions.
-   Optional stacking policy.
-   Optional source/reason metadata.

Effect targets may include:

-   World/economic/social variables.
-   A party.
-   A character.
-   A party relationship.
-   A character relationship.
-   A population segment's perception of a party.
-   A media outlet/coverage.
-   Public agenda topics.
-   Party resources.
-   Provincial presence.

Effects must not target an arbitrary object path or execute arbitrary
C#.

### `PendingEffect`

A scheduled effect:

-   `PendingEffectId Id`
-   `EffectDefinitionId DefinitionId` or immutable effect payload.
-   Target reference.
-   `GameDate ApplyAt`
-   Optional `GameDate ExpiresAt`
-   Remaining duration/recurrence.
-   Stacking metadata.
-   Status.

Rules:

-   Delayed effects are applied at the appropriate simulation date.
-   Temporary effects expire or decay according to their configured
    policy.
-   Permanent effects remain until changed by another rule.
-   Stacking behavior must be explicit (`Stack`, `RefreshDuration`,
    `Replace`, `IgnoreDuplicate`).
-   Effect application must validate target and attribute types.
-   An effect that cannot be applied must produce a diagnostic/result;
    it must not silently corrupt state.

------------------------------------------------------------------------

# 6. Core business rules

This section is the concise rule catalogue. These rules take precedence
over implementation convenience.

## 6.1 Game lifecycle

-   A game has one player party.
-   A game starts from a valid scenario.
-   The game date advances monotonically.
-   Only an active game can be advanced or accept actions.
-   A game may end when the player party is dissolved/disappears or
    another configured terminal condition is met.
-   Losing an election does not by itself end the game.
-   A party's survival is evaluated by explicit rules, not by a direct
    vote threshold alone.
-   Every state-changing operation must use optimistic concurrency (game
    revision/version).

## 6.2 Society and economy

-   Social and economic indicators evolve at different cadences and
    speeds.
-   External events and government policies can affect them.
-   The world evolves even if the player is in opposition.
-   Indicators have explicit ranges and direction/meaning.
-   No effect may push a variable outside its bounds.
-   Long-term structural indicators (e.g. education/demographics) should
    generally change more slowly than short-term indicators
    (e.g. inflation, confidence).
-   Changes may be immediate, delayed, gradual, temporary or permanent.
-   Government performance is evaluated separately from the raw state of
    the country; the incumbent is not automatically responsible for
    every external shock.

## 6.3 Population and ideological reactions

-   Population segments are statistical aggregates, not individual
    agents.
-   Segment weights vary by province and can evolve demographically.
-   Each segment has a profile and issue sensitivities.
-   Ideological affinity is calculated from weighted distance across six
    axes.
-   A segment's reaction to an action/event depends on issue
    sensitivity, ideology, current perceptions and context.
-   No universal rule such as "all right-wing voters ignore corruption"
    is allowed. Such differences must be represented as configurable
    sensitivities and response parameters.
-   Segment reactions affect perception/preferences indirectly; they do
    not directly award vote percentages.

## 6.4 Party and character behavior

-   Parties have ideology, credibility, cohesion, resources, territorial
    presence and leadership.
-   Party relationships are directed and may be asymmetric.
-   Ideological proximity does not guarantee trust or coalition
    compatibility.
-   A party in opposition can improve its electoral position.
-   Characters have independent popularity, loyalty, ambition and
    skills.
-   Public popularity and internal party support are separate concepts.
-   Internal actions may trigger immediate and delayed reactions,
    including factional conflict, resignations, defections or leadership
    challenges.

## 6.5 Media and agenda

-   Media outlets are individual actors with audience, credibility,
    editorial orientation, sensationalism, independence, party
    affinities and issue interests.
-   Each outlet independently evaluates whether and how to cover an
    event.
-   Media exposure may be positive, neutral or negative.
-   More exposure does not necessarily mean more support.
-   The public agenda normally contains 2--4 dominant topics.
-   Agenda relevance influences which political actions and proposals
    receive attention.
-   Agenda changes over time due to events, media and political
    campaigns.

## 6.6 Events and decisions

-   Event definitions are reusable configuration; occurrences are
    per-game instances.
-   Events may be random, conditional or chained.
-   Event generation must respect trigger conditions, probability,
    cooldown and expiry.
-   Actions may be reactive or voluntary.
-   The three MVP action categories are public positioning,
    parliamentary voting and internal party management.
-   Action availability and costs are validated before execution.
-   Effects are declarative, typed and data-driven.
-   Effects support immediate, delayed, temporary, recurring/gradual and
    permanent behavior.
-   Event/action execution must be atomic from the perspective of game
    state.
-   Every execution should record provenance: which event/action caused
    which effects.

## 6.7 Electoral rules

-   No action directly modifies voting-intention percentages.
-   Electoral preference is derived from structural affinity,
    perception, context, media and leadership.
-   Perception has affinity, trust and respect dimensions, each normally
    0--100.
-   Electoral utility combines normalized components with
    segment-specific weights.
-   Utility is transformed into probabilities, including
    abstention/undecided options where applicable.
-   Turnout is calculated separately from party choice.
-   Preferences evolve with segment-specific loyalty/volatility; they do
    not instantly jump to the newly calculated ideal distribution.
-   Provincial vote is calculated from provincial segment composition
    and provincial modifiers.
-   Territorial presence modifies utility/mobilization, not raw vote
    percentages.
-   Seat allocation occurs per constituency using the configured
    electoral system.
-   Polls are estimates with sample/error/bias; they are not the hidden
    true electoral state.
-   Election results may differ from the latest poll because of sampling
    error, turnout and late changes.

------------------------------------------------------------------------

# 7. Electoral model

The electoral engine is a Domain service/calculation subsystem. It must
not be embedded in API controllers or frontend code.

## 7.1 Ideological affinity

For segment `s` and party `p`:

`D(s,p) = sum(weight(s,axis) * abs(segmentPosition - partyPosition)) / sum(weights)`

`Affinity(s,p) = 100 - D(s,p) / 2`

Each axis is in `[-100, +100]`, so the resulting affinity is in
`[0, 100]`.

## 7.2 Segment-party perception

`SegmentPartyPerception` stores:

-   Affinity.
-   Trust.
-   Respect.

All are normally in `[0, 100]`.

Affinity can include the structural ideological fit and its evolution;
keep the exact distinction between structural affinity and perceived
affinity explicit in implementation. Do not accidentally count the same
ideological component twice in the utility calculation.

## 7.3 Electoral utility

For each segment, party and province, calculate a utility from
normalized components:

-   Structural/experienced affinity.
-   Trust.
-   Respect.
-   Government performance or opposition performance.
-   Media perception.
-   Leadership evaluation.
-   Provincial presence/context.

Weights are segment-specific and should sum to 1 (or be normalized
before use).

Important: all utility components must be on compatible scales. Do not
feed raw 0--100 values into softmax with an arbitrary temperature
without calibration. Define a normalization/centering strategy and test
its behavior.

## 7.4 Probabilities and turnout

Use a softmax-like probability conversion:

`Pr(p | segment, province) = exp(U(p)/T) / sum_j exp(U(j)/T)`

Include abstention/undecided as appropriate.

Numerical implementation should use a stable softmax (subtract maximum
utility before exponentiation).

Calculate turnout separately, using:

-   Base turnout.
-   Segment turnout propensity.
-   Enthusiasm/mobilization.
-   Disengagement/abstention.
-   Campaign effects.

## 7.5 Preference inertia

A simple initial transition model:

`P(t) = (1 - lambda) * P(t-1) + lambda * P_target(t)`

Where `lambda` is segment volatility.

After blending, normalize probabilities to sum to 1. Consider whether
extraordinary events/campaign periods temporarily change lambda.

## 7.6 Provincial aggregation

For province `k`, party `p`:

`Votes(p,k) = sum_s Population(s,k) * Turnout(s,k) * Probability(p,s,k)`

Normalize by total votes cast in the province to produce vote share.

Use actual provincial population composition and territorial modifiers.
Do not distribute national vote share uniformly across provinces.

## 7.7 Seat allocation

MVP approximation:

-   350 Congress seats.
-   52 constituencies (50 provinces, Ceuta and Melilla).
-   D'Hondt allocation.
-   3% provincial threshold, subject to the chosen scenario's electoral
    configuration.
-   Constituency seat allocation and minimum seat rules are
    configuration, not hardcoded assumptions throughout the Domain.

Implement seat allocation as a pure domain calculation with
deterministic tests.

## 7.8 Polls

Keep separate:

-   Hidden internal electoral preference.
-   Poll estimate.
-   Actual election result.

A poll contains polling company, sample size, geographic coverage,
margin of error, publication date and estimated results. Bias and noise
are configurable and reproducible.

------------------------------------------------------------------------

# 8. Simulation cadence and daily processing

The simulation advances by game days. Different subsystems update at
different cadences.

  -----------------------------------------------------------------------
  Cadence                             Processes
  ----------------------------------- -----------------------------------
  Daily                               Pending effects, event checks,
                                      event occurrences, player actions,
                                      actor reactions, media coverage,
                                      news facts

  Weekly                              Economic evolution and selected
                                      political indicators

  Monthly                             Social evolution, public agenda,
                                      polls, aggregate perception updates

  Quarterly                           Demographic trends and structural
                                      changes

  Election                            Vote calculation, turnout,
                                      provincial aggregation, seat
                                      allocation, government formation
  -----------------------------------------------------------------------

The exact order should be explicit and tested. Proposed daily order:

1.  Validate game status and expected revision.
2.  Advance game date by one day.
3.  Apply pending effects due on this date.
4.  Expire effects/events whose expiry date has been reached.
5.  Evaluate event triggers and generate eligible events.
6.  Expose event-driven decisions to the player.
7.  Execute any submitted player action (as a separate command if the
    game pauses for player input).
8.  Process relevant reactions.
9.  Evaluate media coverage and create structured news facts.
10. Update daily world/party/character state.
11. Run weekly/monthly/quarterly processes when due.
12. Recalculate hidden electoral preferences when relevant.
13. Evaluate party survival and game-over conditions.
14. Increment game revision and persist the new state.

Important: if the game pauses awaiting player input, do not silently
advance beyond the decision point. Model the game as awaiting a decision
or as having an active event requiring a response.

The daily simulation should be deterministic given: - Initial
scenario. - Initial state. - Simulation version. - Random seed/state. -
Ordered player actions.

------------------------------------------------------------------------

# 9. Domain services and policies

Suggested Domain services (names can change):

-   `IdeologicalAffinityCalculator`
-   `PoliticalUtilityCalculator`
-   `ElectoralPreferenceCalculator`
-   `TurnoutCalculator`
-   `ProvincialVoteCalculator`
-   `SeatAllocationCalculator`
-   `EventEligibilityPolicy`
-   `EffectApplicationService`
-   `ActionAvailabilityPolicy`
-   `ActionExecutionService`
-   `ReactionCalculator`
-   `MediaCoverageCalculator`
-   `PublicAgendaCalculator`
-   `GovernmentPerformanceCalculator`
-   `PartySurvivalPolicy`
-   `GameEndPolicy`
-   `DailySimulationService` / `SimulationEngine`

Keep these services pure where possible: input state + parameters -\>
result. If a service needs randomness, inject a Domain abstraction such
as `IRandomSource` rather than using static/global random APIs.

Do not make all of these separate projects or interfaces automatically.
Create boundaries where they improve testability and maintainability.

------------------------------------------------------------------------

# 10. Application layer guidance

The Application layer is intentionally less prescriptive than the
Domain. It will evolve as the interaction model and observer/event
mechanism are implemented.

Its responsibilities are:

-   Load the game and required configuration/state.
-   Authorize the player to access and modify the game.
-   Invoke Domain operations/services.
-   Persist the resulting state atomically.
-   Publish domain events/notifications after successful persistence.
-   Return DTOs/read models suitable for the frontend.
-   Handle concurrency and idempotency.

It must not contain electoral formulas, event business rules, perception
rules, effect semantics, or party-survival rules.

## 10.1 Initial use cases

Possible commands:

-   `CreateGame`
-   `AdvanceGameDay`
-   `ExecuteGameAction`
-   `ResolveEvent`
-   `PauseGame`
-   `ResumeGame`
-   `AbandonGame`
-   `RunElection` (may be invoked by the simulation engine rather than
    directly by the player)

Possible queries:

-   `GetGameOverview`
-   `GetGameState`
-   `GetAvailableActions`
-   `GetActiveEvents`
-   `GetPollHistory`
-   `GetElectionResults`
-   `GetNewsFeed`
-   `GetGameHistory`

These are suggestions, not mandatory endpoint names.

## 10.2 Observer pattern and events

The implementation may use an observer pattern, in-process
notifications, MediatR notifications, or another mechanism. Keep the
Domain independent of the chosen library.

Distinguish carefully:

-   **Domain event:** a fact that has happened in the Domain,
    e.g. `ActionExecuted`, `EventOccurred`, `PartyLeaderChanged`,
    `ElectionCompleted`, `PartyDissolved`.
-   **Application notification/integration event:** a message used to
    notify other components, update read models, send analytics, or
    trigger external work.
-   **Simulation event:** a configurable in-game occurrence,
    e.g. corruption scandal or natural disaster. This is game content,
    not the same thing as a C# domain event.

Do not conflate these three meanings of "event".

Recommended handling:

1.  Domain operation changes state and records domain events.
2.  Application persists the changed state and event/outbox records in
    one transaction where practical.
3.  After commit, notifications are dispatched.
4.  Handlers update read models, create news projections, analytics,
    etc.
5.  A notification handler must not be required for the core simulation
    state to remain valid.

For the MVP, in-process observers are sufficient. If reliable external
delivery becomes necessary, introduce an outbox. Do not add distributed
messaging infrastructure pre-emptively.

## 10.3 Transaction and concurrency

-   A state-changing game command should load a known game revision.
-   Persist using optimistic concurrency (e.g. `Revision`).
-   If the revision has changed, reject or retry safely; never overwrite
    a newer state.
-   Applying an action, charging its cost, updating the game state and
    recording its execution must be atomic.
-   Retrying a request must not execute a non-idempotent action twice.
    Use an idempotency key or execution ID where appropriate.

------------------------------------------------------------------------

# 11. Persistence and serialization guidance

The chosen persistence approach may use PostgreSQL and EF Core, with a
hybrid relational + JSONB strategy.

Suggested high-level storage:

-   Relational metadata for users, games, ownership, status, date,
    revision, scenario and versions.
-   Serialized `GameState` snapshots for the complex simulation state.
-   Relational history tables or append-only records for important
    occurrences/executions/results.
-   Versioned scenario/configuration definitions.

Do not let EF Core entities become the Domain model by default.
Persistence mapping belongs in Infrastructure.

Persist at least:

-   `StateVersion`: serialized state schema version.
-   `SimulationVersion`: rules/calculation version used by the game.
-   `RandomSeed` and, if needed, current PRNG state/sequence position.
-   `Revision`: concurrency version.
-   Current full state or a reliable snapshot + event history.

A state migration strategy is required when serialized state changes. A
simulation rules version is separate from a state schema version:
changing the serialization shape is not the same as changing the game
rules.

For reproducibility, a saved game must not depend on process-local
memory or server wall-clock time.

------------------------------------------------------------------------

# 12. Randomness and deterministic simulation

All randomness must go through an injected abstraction, e.g.:

``` csharp
public interface IRandomSource
{
    int NextInt(int minInclusive, int maxExclusive);
    decimal NextDecimal();
    bool Chance(decimal probability);
}
```

The exact API can differ.

Rules:

-   Never use `new Random()` or static random calls inside Domain logic.
-   A game has a stable seed.
-   Random calls must occur in a deterministic order for
    reproducibility.
-   Tests should use a deterministic/fake random source.
-   Polling noise and random event selection must be reproducible.
-   If future parallel simulation is introduced, define independent
    deterministic random streams per subsystem or task.

The Domain must not depend on server time. Use the game calendar.

------------------------------------------------------------------------

# 13. MVP scope and initial content targets

Suggested initial content:

  Content                       Initial target
  --------------------------- ----------------
  Political parties                          5
  Population segments                        6
  Media outlets                              3
  Political characters                      10
  Social indicators                          5
  Economic indicators                        5
  Electoral constituencies                  52
  Event definitions                         30
  Primary action categories                  3
  Election cycle                       4 years
  Poll frequency                       Monthly

These are balancing/content targets, not hardcoded domain limits.

MVP must support:

-   Daily calendar.
-   Game creation and persistence.
-   World/economic/social evolution.
-   Random, conditional and chained events.
-   Public positioning, parliamentary voting and internal party actions.
-   Reactions by segments, parties, characters and media.
-   Public agenda.
-   Party and character relationships.
-   Provincial voting intention.
-   Monthly polls.
-   Election and seat allocation.
-   Party survival and game-over evaluation.

Defer unless essential:

-   Individual citizen simulation.
-   Complex autonomous character AI.
-   Detailed regional/municipal parliaments.
-   Advanced campaign resource allocation.
-   Sector-by-sector economic model.
-   Complex geopolitical simulation.
-   Sophisticated polling-house methodology.
-   Dynamic natural-language generation inside the Domain.

------------------------------------------------------------------------

# 14. Suggested C# project structure

This is a suggested starting structure, not a requirement to create an
interface and project for every item.

``` text
src/
  PoliticalSimulator.Domain/
    Common/
      Entity.cs
      AggregateRoot.cs
      ValueObject.cs
      DomainEvent.cs
      IRandomSource.cs
    Games/
      Game.cs
      GameState.cs
      GameDate.cs
      GameStatus.cs
      GameConfiguration.cs
    World/
      WorldState.cs
      EconomyState.cs
      SocietyState.cs
      DemographicState.cs
      WorldVariable.cs
      Province.cs
    Population/
      PopulationSegment.cs
      IdeologicalProfile.cs
      IssueSensitivity.cs
      SegmentPartyPerception.cs
      ElectoralPreference.cs
    Politics/
      PoliticalParty.cs
      PartyIdeology.cs
      PartyMembership.cs
      PartyRelationship.cs
      PartyProvincePresence.cs
      GovernmentState.cs
      PoliticalCharacter.cs
      CharacterRelationship.cs
    Media/
      MediaOutlet.cs
      MediaCoverage.cs
      NewsArticle.cs
      PublicAgenda.cs
      AgendaTopic.cs
    Events/
      GameEventDefinition.cs
      EventOccurrence.cs
      Condition.cs
      ConditionGroup.cs
      EffectDefinition.cs
      PendingEffect.cs
    Actions/
      ActionDefinition.cs
      ActionExecution.cs
      PublicPositionAction.cs
      ParliamentaryVoteAction.cs
      InternalPartyAction.cs
    Elections/
      ElectoralSystem.cs
      Poll.cs
      Election.cs
      ProvincialElectionResult.cs
    Services/
      SimulationEngine.cs
      EventEligibilityPolicy.cs
      EffectApplicationService.cs
      ReactionCalculator.cs
      Electoral/
        IdeologicalAffinityCalculator.cs
        PoliticalUtilityCalculator.cs
        ElectoralPreferenceCalculator.cs
        TurnoutCalculator.cs
        ProvincialVoteCalculator.cs
        SeatAllocationCalculator.cs

  PoliticalSimulator.Application/
    Games/
      Commands/
      Queries/
      Handlers/
      DTOs/
    Abstractions/
      IGameRepository.cs
      IGameStateRepository.cs
      IUnitOfWork.cs
      IClock.cs (only if needed outside game-time rules)
      IEventDispatcher.cs
```

Do not create empty classes just to match this tree. Implement the
smallest coherent model and evolve it.

------------------------------------------------------------------------

# 15. Testing requirements

The Domain should be testable without a database, web server, or
frontend.

Minimum test categories:

## 15.1 Invariant tests

-   Game cannot advance after finishing.
-   Game date cannot move backwards.
-   Player party must belong to the game.
-   Party/character relationships must reference valid actors.
-   Values remain within configured bounds.
-   An expired/resolved event cannot be resolved again.
-   An unavailable action cannot execute.
-   Action costs and effects are atomic.
-   A party cannot be dissolved without satisfying the configured
    policy.

## 15.2 Effect engine tests

-   Immediate effect.
-   Delayed effect.
-   Temporary effect and expiry.
-   Permanent effect.
-   Stacking policies.
-   Invalid target/attribute.
-   Multiple effects applied atomically.
-   Effects conditioned on current state.

## 15.3 Event tests

-   Random event selection with deterministic random source.
-   Conditional trigger.
-   Cooldown.
-   Expiry.
-   Follow-up event scheduling.
-   No duplicate occurrence when not allowed.

## 15.4 Electoral tests

-   Ideological affinity at identical and opposite profiles.
-   Segment-specific issue weights.
-   Stable softmax and probabilities summing to one.
-   Abstention/turnout calculation.
-   Preference inertia and normalization.
-   Different provincial compositions produce different outcomes.
-   Territorial presence changes utility rather than adding fixed votes.
-   D'Hondt allocation, thresholds and tie handling.
-   Poll estimates differ from internal preference but remain
    reproducible.

## 15.5 Simulation tests

-   A full day executes processes in the specified order.
-   Weekly/monthly/quarterly systems run only on the correct dates.
-   Same initial state, simulation version, seed and actions produce the
    same result.
-   A game can be saved, loaded and resumed with identical subsequent
    results.

------------------------------------------------------------------------

# 16. Open design decisions

Do not invent complex rules for these without product-level discussion.
Implement extension points and keep the MVP simple.

1.  **Party survival:** exact thresholds and combination of electoral,
    financial, organizational and internal factors that dissolve a
    party.
2.  **Rival party AI:** which decisions rival parties can make and how
    often.
3.  **Government attribution:** how much responsibility the incumbent
    receives for external economic/social changes.
4.  **Action economy:** budget, influence, staff and other costs.
5.  **Campaign mechanics:** whether campaigning has a distinct phase and
    special actions.
6.  **Parliamentary detail:** exact proposals, votes, confidence motions
    and government formation.
7.  **Election law configuration:** constituency seat allocation,
    minimum seats and thresholds for each scenario.
8.  **Perception decay:** exact recovery/decay curves and historical
    memory.
9.  **Media model:** how outlet reach, credibility and framing combine
    into segment-level effects.
10. **Variable calibration:** initial values, evolution speeds and
    effect magnitudes.
11. **Content authoring:** JSON/YAML/database format and validation
    tooling for event/action definitions.
12. **Simulation pause model:** whether each event pauses the calendar
    until the player chooses, or whether some events resolve
    automatically.

------------------------------------------------------------------------

# 17. Instructions for the coding agent

When implementing from this document:

1.  Start by proposing the Domain aggregates, entities, value objects,
    enums and domain services. Explain aggregate boundaries and
    invariants before generating large amounts of code.
2.  Implement the Domain independently of ASP.NET Core, EF Core,
    MediatR, HTTP and UI packages.
3.  Keep the Application layer thin and use-case oriented. Its shape can
    evolve; the Domain rules must not migrate into handlers.
4.  Do not create one class per event, news item, issue, economic
    indicator or action instance. Use definitions, occurrences,
    conditions and effects.
5.  Do not implement the entire simulation in one giant `Game` aggregate
    or one giant service.
6.  Do not expose setters that allow invalid state. Use
    constructors/factories and explicit domain methods.
7.  Do not use `double` for money or values where decimal precision
    matters. Use `decimal` for game numeric values unless a measured
    reason supports another type.
8.  Use strongly typed IDs where this adds clarity and prevents
    accidental reference mixing.
9.  Keep game time separate from server time.
10. Inject deterministic randomness.
11. Make calculations pure where possible and cover them with unit
    tests.
12. Prefer explicit, simple MVP behavior over speculative abstractions.
13. When a rule is underspecified, record the assumption and keep it
    configurable; do not silently invent a complex mechanic.
14. Implement in vertical slices: first game/state foundations, then
    world variables/effects, then events/actions, then
    perception/electoral calculations, then election results.
15. Before implementing persistence mappings, ensure the Domain model
    and state serialization boundaries are clear.

**First requested implementation milestone:** produce a proposed
aggregate-boundary/class diagram and a list of unresolved domain
decisions. Do not immediately generate the complete application. Once
the model is reviewed, implement the Domain skeleton and focused tests.
