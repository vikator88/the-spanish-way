# mediator Specification

## Purpose

Defines the hand-written CQRS mediator that the Application layer owns outright: the request and handler
contracts, how a handler is resolved and invoked without per-call reflection, the pipeline behaviour
chain that wraps every command, and the startup validation that turns a misregistered handler into a boot
failure instead of a runtime surprise.

## Requirements

### Requirement: No third-party mediator

The Application layer SHALL implement its own mediator and SHALL NOT take a dependency on MediatR or any
other mediator package.

#### Scenario: No mediator package
- **WHEN** the solution's dependency graph is inspected
- **THEN** no project references MediatR or an equivalent library

#### Scenario: Contracts live in the Application layer
- **WHEN** the messaging abstractions are located
- **THEN** they are in `TheSpanishWay.Application/Abstractions/Messaging/`

### Requirement: Request and handler contracts

The mediator SHALL define separate marker interfaces for commands and queries, and matching handler
interfaces that return `Result` or `Result<T>`.

#### Scenario: Command without a response
- **WHEN** a command returns no value
- **THEN** it implements `ICommand` and its handler implements `ICommandHandler<TCommand>` returning
  `Task<Result>`

#### Scenario: Command with a response
- **WHEN** a command returns a value
- **THEN** it implements `ICommand<TResponse>` and its handler implements
  `ICommandHandler<TCommand, TResponse>` returning `Task<Result<TResponse>>`

#### Scenario: Query
- **WHEN** a query is defined
- **THEN** it implements `IQuery<TResponse>` and its handler implements `IQueryHandler<TQuery, TResponse>`
  returning `Task<Result<TResponse>>`

#### Scenario: Commands and queries are distinguishable
- **WHEN** the pipeline inspects a request
- **THEN** it can tell a command from a query without consulting a naming convention

#### Scenario: Cancellation is propagated
- **WHEN** a handler is invoked
- **THEN** it receives the caller's `CancellationToken`

### Requirement: Dispatch surface

The mediator SHALL expose `ISender` for requests and `IPublisher` for notifications, with `IMediator`
combining both.

#### Scenario: Sending
- **WHEN** a caller sends a command or query
- **THEN** it does so through `ISender.Send` and receives a `Result` or `Result<TResponse>`

#### Scenario: Publishing
- **WHEN** a notification is published
- **THEN** it goes through `IPublisher.Publish`

#### Scenario: Callers depend on the narrow interface
- **WHEN** an API endpoint needs to issue a command
- **THEN** it depends on `ISender`, not on `IMediator`

### Requirement: Handler resolution without per-call reflection

The dispatcher SHALL resolve a handler from the container by closing the open generic handler interface
over the runtime request type, and SHALL invoke it through a cached delegate.

#### Scenario: Resolution
- **WHEN** a request arrives
- **THEN** the dispatcher closes the handler interface over the concrete request and response types and
  resolves it from the service provider

#### Scenario: Invocation is cached
- **WHEN** the same request type is dispatched repeatedly
- **THEN** the invoker is built once and reused, and `MethodInfo.Invoke` is not called per request

#### Scenario: Registration by assembly scan
- **WHEN** the Application services are registered
- **THEN** handlers are discovered by scanning the Application assembly

### Requirement: Startup validation of handler registration

Handler registration SHALL be validated at startup, so that a missing or duplicated handler fails the
application's boot rather than the first request that needs it.

#### Scenario: Missing handler
- **WHEN** a request type has no registered handler
- **THEN** startup fails naming that request type

#### Scenario: Duplicate handler
- **WHEN** a request type has more than one registered handler
- **THEN** startup fails naming the request type and every competing handler

#### Scenario: Validation is testable
- **WHEN** the registration validator runs in a test
- **THEN** it can be executed against the Application assembly without starting a web host

### Requirement: Pipeline behaviours

The mediator SHALL support a behaviour chain around handler execution, applied in a fixed order.

#### Scenario: Behaviour contract
- **WHEN** a behaviour is written
- **THEN** it implements `IPipelineBehavior<TRequest, TResponse>` and may call or skip `next`

#### Scenario: Command order
- **WHEN** a command is dispatched
- **THEN** behaviours execute outermost-first as logging, validation, idempotency, unit of work, then
  concurrency retry, before reaching the handler

#### Scenario: Queries skip write-path behaviours
- **WHEN** a query is dispatched
- **THEN** idempotency, unit of work and concurrency retry are not applied

#### Scenario: Logging scope
- **WHEN** a request is dispatched
- **THEN** a structured logging scope carries the request name, the game identifier where present, and a
  correlation identifier

#### Scenario: Validation is shape-only
- **WHEN** the validation behaviour runs
- **THEN** it checks the request's shape and required fields only, and contains no business rule

#### Scenario: Short-circuiting returns a Result
- **WHEN** a behaviour rejects a request
- **THEN** it returns a failed `Result` rather than throwing

### Requirement: Errors are values

Business rule violations SHALL be returned as failed `Result` values, and exceptions SHALL be reserved
for programmer error and infrastructure failure.

#### Scenario: Unavailable action
- **WHEN** a player attempts an action whose availability conditions are not met
- **THEN** the handler returns a failed `Result` and no exception is thrown

#### Scenario: Concurrency conflict
- **WHEN** a revision conflict occurs and retry is exhausted
- **THEN** a failed `Result` carrying a conflict error code is returned

#### Scenario: Infrastructure failure
- **WHEN** the database is unreachable
- **THEN** an exception propagates, because it is not a business outcome
