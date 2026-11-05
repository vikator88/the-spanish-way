# Spec Delta

## Purpose

Defines the data-driven content model — scenarios, event and action definitions, conditions, effect
declarations, role bindings and the issue catalogue — together with the attribute registry and validator
that make invalid content fail at load instead of silently misbehaving at runtime.

## ADDED Requirements

### Requirement: Definitions are data, not classes

Events and actions SHALL be expressed as reusable definitions interpreted at runtime, with no C# class
per named event, news item, issue, indicator or action instance.

#### Scenario: No class per event
- **WHEN** the `Content` module is inspected
- **THEN** no type is named after an individual event such as `CorruptionScandal`

#### Scenario: Handler per category, not per action
- **WHEN** actions are implemented
- **THEN** there is one handler per action category — public positioning, parliamentary voting, internal
  party management — and the specifics come from data

#### Scenario: MVP content target
- **WHEN** the default scenario is loaded
- **THEN** it can carry roughly 30 event definitions without any code change

### Requirement: Scenario definition is an immutable published aggregate

A scenario SHALL be a separate aggregate that becomes immutable once published, and SHALL be referenced
by games through a version.

#### Scenario: Publishing freezes content
- **WHEN** a scenario version is published
- **THEN** no further modification to that version is possible

#### Scenario: Editing creates a new version
- **WHEN** a published scenario is edited
- **THEN** the edit produces a new version and leaves the published one untouched

#### Scenario: Scenario contents
- **WHEN** a scenario is loaded
- **THEN** it provides initial parties, characters and roles, segments, provinces and compositions, media
  outlets, pollsters, initial economy and society values, the issue catalogue, the event and action
  catalogues, simulation parameters and the electoral system

#### Scenario: Scenario is not game state
- **WHEN** a game advances
- **THEN** no scenario value is mutated

### Requirement: Issue catalogue

The content model SHALL define an issue catalogue in which each issue is either positional, loading onto
one or more ideological axes, or valence, linked to world variables with no stance.

#### Scenario: Positional issue
- **WHEN** a positional issue such as pensions is defined
- **THEN** it declares its loadings onto one or more of the six axes

#### Scenario: Valence issue
- **WHEN** a valence issue such as corruption or unemployment is defined
- **THEN** it declares no stance and instead links to the world variables that measure it

#### Scenario: Ideology applies only to positional issues
- **WHEN** a reaction to a valence stimulus is computed
- **THEN** ideological fit does not enter the calculation; competence and blame do

#### Scenario: MVP catalogue size
- **WHEN** the default scenario is loaded
- **THEN** it defines roughly 12 issues

### Requirement: Role bindings

An event or action definition SHALL declare roles with data-driven selection rules, and the occurrence
SHALL store the bound identifiers; conditions, effects, templates and stimuli refer to roles only.

#### Scenario: Roles are declared
- **WHEN** a definition needs to refer to a particular party or character
- **THEN** it declares a role such as `$accused` with a selection rule, rather than naming an identifier

#### Scenario: Binding at trigger
- **WHEN** an event fires
- **THEN** each declared role is bound to a concrete identifier, chosen from the candidates using the
  `bindings` random stream, and the bindings are stored on the occurrence

#### Scenario: Dependent roles
- **WHEN** `$figure` is declared as a member of `$accused` whose role is Leader or Spokesperson
- **THEN** it is bound only from the members of the party already bound to `$accused`

#### Scenario: No candidates means not eligible
- **WHEN** a required role has no candidate satisfying its selection rule
- **THEN** the event is not eligible to fire

#### Scenario: Built-in roles
- **WHEN** a definition references `$player`, `$government.lead` or `$self`
- **THEN** those resolve without a declared selection rule

#### Scenario: Literal identifiers are rejected
- **WHEN** content references a concrete party or character identifier in a reusable definition
- **THEN** the validator rejects it unless the definition is explicitly marked as scenario-specific

### Requirement: Conditions

Conditions SHALL be data-driven predicates composed with AND and OR, able to inspect game state and role
bindings, and unable to execute arbitrary expressions supplied by content.

#### Scenario: Condition shape
- **WHEN** a condition is defined
- **THEN** it carries a target type and reference, an attribute key, an operator and an expected value

#### Scenario: Supported operators
- **WHEN** the operator set is inspected
- **THEN** it includes at least `Equal`, `NotEqual`, `GreaterThan`, `LessThan`, `InRange` and `Exists`

#### Scenario: Grouping
- **WHEN** conditions are composed
- **THEN** a `ConditionGroup` combines them with AND or OR and may nest

#### Scenario: No arbitrary code
- **WHEN** content supplies a value
- **THEN** it is interpreted from a restricted grammar and never compiled or evaluated as code

#### Scenario: Roles are addressable
- **WHEN** a condition needs the credibility of the accused party
- **THEN** it targets the role `$accused` rather than an identifier

### Requirement: Attribute registry

A code-side registry SHALL map every `(target type, attribute key)` pair to its type, bounds, polarity
and permitted operations, and SHALL be the single authority against which content is validated.

#### Scenario: Unknown key fails validation
- **WHEN** content references an attribute key absent from the registry
- **THEN** content validation fails and names the offending key and definition

#### Scenario: No silent truth
- **WHEN** a condition names an unknown attribute
- **THEN** validation fails rather than the condition evaluating as true or false at runtime

#### Scenario: Operation whitelist
- **WHEN** an effect declares an operation the registry does not permit for that attribute
- **THEN** validation fails

#### Scenario: Type mismatch
- **WHEN** content supplies a value whose type does not match the registry entry
- **THEN** validation fails

### Requirement: Content validation runs at load and in CI

Scenario content SHALL be validated against a schema and against the attribute registry both at
application startup and as part of the build.

#### Scenario: Startup validation
- **WHEN** the application starts with invalid content
- **THEN** startup fails with a report naming every violation, rather than starting and failing later

#### Scenario: Build-time validation
- **WHEN** content is changed
- **THEN** a test in the build validates every scenario in the repository

#### Scenario: Authoring format
- **WHEN** content is authored
- **THEN** it is JSON files in the repository validated against a published JSON schema

#### Scenario: Normalisation checks
- **WHEN** segment weights, provincial compositions or utility weights are validated
- **THEN** sets that cannot be normalised are rejected

### Requirement: Event definitions

An event definition SHALL carry a name, description template, category, base probability, trigger
conditions, role declarations, effects, available actions, follow-up events, a blocking flag, optional
duration, cooldown and expiry, and tags.

#### Scenario: Categories
- **WHEN** an event category is read
- **THEN** it is one of random, conditional or chained, expressed as data rather than as a subclass

#### Scenario: Blocking flag
- **WHEN** an event is defined
- **THEN** it declares whether it blocks the simulation pending a player decision

#### Scenario: Default option
- **WHEN** a non-blocking event is defined
- **THEN** it declares a default option used if it expires unresolved

#### Scenario: Follow-ups are eligible, not automatic
- **WHEN** an occurrence resolves
- **THEN** its follow-up events become eligible subject to their own conditions, rather than firing
  unconditionally

### Requirement: Action definitions

An action definition SHALL carry a name, description template, category, availability conditions, cost,
effects, emitted stimuli, duration, cooldown, tags and an optional parameter schema.

#### Scenario: Three MVP categories
- **WHEN** the catalogue is inspected
- **THEN** every action belongs to `PublicPosition`, `ParliamentaryVote` or `InternalParty`

#### Scenario: Parameters are schema-validated
- **WHEN** a player submits parameters for an action
- **THEN** they are validated against the definition's parameter schema before anything executes

#### Scenario: Costs are declared
- **WHEN** an action has a cost
- **THEN** the cost is declared as data and validated against the attribute registry
