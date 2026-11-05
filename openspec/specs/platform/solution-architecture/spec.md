# solution-architecture Specification

## Purpose

Defines the `api/` solution layout, the .NET 10 build configuration, and the Clean Architecture
dependency rules that keep business logic independent of persistence, HTTP and UI — together with the
automated checks that prevent those rules from eroding.

## Requirements

### Requirement: Solution tree

The API SHALL live under `api/` with four source projects under `api/src/` and all test projects under
`api/test/`, described by a single XML `.slnx` solution file at the `api/` root.

#### Scenario: Source projects exist
- **WHEN** the solution is opened
- **THEN** it contains exactly `TheSpanishWay.Domain`, `TheSpanishWay.Application`,
  `TheSpanishWay.Infrastructure` and `TheSpanishWay.Api` under `api/src/`

#### Scenario: Solution file format
- **WHEN** the solution file is inspected
- **THEN** it is `api/TheSpanishWay.slnx` in the XML `.slnx` format, sitting above both `src/` and `test/`
- **AND** no `.sln` file exists alongside it

#### Scenario: Test projects are in the solution
- **WHEN** the solution is built
- **THEN** every project under `api/test/` is included, referenced by relative path from `api/`

### Requirement: Target framework and build configuration

All projects SHALL target `net10.0` with nullable reference types enabled, implicit usings enabled,
warnings treated as errors, and package versions managed centrally.

#### Scenario: Shared build properties
- **WHEN** a project is built
- **THEN** `api/Directory.Build.props` supplies `net10.0`, `<Nullable>enable</Nullable>`,
  `<ImplicitUsings>enable</ImplicitUsings>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` and
  the latest language version
- **AND** no individual project overrides the target framework

#### Scenario: Central package management
- **WHEN** a package is added to any project
- **THEN** its version is declared only in `api/Directory.Packages.props`
- **AND** the `PackageReference` in the `.csproj` carries no `Version` attribute

### Requirement: Inward dependency direction

Project references SHALL point only inward: Api to Infrastructure and Application, Infrastructure to
Application and Domain, Application to Domain, and Domain to nothing.

#### Scenario: Domain has no project references
- **WHEN** `TheSpanishWay.Domain.csproj` is inspected
- **THEN** it declares no `ProjectReference` elements

#### Scenario: Application does not reference Infrastructure
- **WHEN** `TheSpanishWay.Application.csproj` is inspected
- **THEN** its only `ProjectReference` is `TheSpanishWay.Domain`

#### Scenario: Nothing references the Api project
- **WHEN** any project other than test projects is inspected
- **THEN** none of them reference `TheSpanishWay.Api`

### Requirement: Domain dependency isolation

The Domain project SHALL declare no NuGet package reference, so that no persistence, serialisation,
messaging, HTTP or dependency-injection concern can reach domain rules.

#### Scenario: Domain project has no packages
- **WHEN** `TheSpanishWay.Domain.csproj` is inspected
- **THEN** it contains zero `PackageReference` elements

#### Scenario: Banned namespaces
- **WHEN** Domain source is analysed
- **THEN** no type references `Microsoft.EntityFrameworkCore`, `Microsoft.AspNetCore`,
  `Microsoft.Extensions.DependencyInjection`, `System.Text.Json` or `Newtonsoft.Json`

### Requirement: Application layer dependency budget

The Application project SHALL depend only on abstraction packages, never on a concrete mediator,
ORM, validation or serialisation implementation.

#### Scenario: Permitted packages
- **WHEN** `TheSpanishWay.Application.csproj` is inspected
- **THEN** its package references are limited to
  `Microsoft.Extensions.DependencyInjection.Abstractions` and
  `Microsoft.Extensions.Logging.Abstractions`

#### Scenario: MediatR is absent
- **WHEN** the dependency graph of the solution is inspected
- **THEN** no project references `MediatR` or any third-party mediator package

### Requirement: Architecture rules are enforced by tests

The dependency and isolation rules above SHALL be asserted by an automated test project so that a
careless package or reference addition fails the build rather than the review.

#### Scenario: Layering test fails on an inward-rule violation
- **WHEN** a type in `TheSpanishWay.Domain` is made to depend on `TheSpanishWay.Application`
- **THEN** a test in `TheSpanishWay.Architecture.Tests` fails naming the offending type

#### Scenario: Randomness ban is asserted
- **WHEN** Domain source uses `System.Random` directly
- **THEN** an architecture test fails and names `IRandomSource` as the required abstraction

#### Scenario: Wall-clock ban is asserted
- **WHEN** Domain source references `DateTime.Now`, `DateTime.UtcNow` or `DateTimeOffset.UtcNow`
- **THEN** an architecture test fails and names `GameDate` as the required in-game time type
