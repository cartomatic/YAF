# Specification: Application Layer CQRS and Cross-Cutting Abstractions

## Goal

Define the complete set of mediator-agnostic CQRS interfaces, notification contracts, validation abstractions, context provider interfaces, sanitization contracts, and business event log types in Yaf.Application — enabling adapter packages and consumer code to build against stable, framework-owned contracts with no external dependencies.

## User Stories

- As a framework consumer, I want to implement command and query handlers against YAF-owned interfaces so that I can swap mediator libraries (Wolverine, MediatR) without changing application code.
- As a framework consumer, I want notification interfaces for fan-out dispatch so that domain event handling is wired through a common contract.
- As a framework consumer, I want pipeline validation abstractions so that business preconditions are checked before handler execution.
- As a framework consumer, I want context provider interfaces so that tenant, identity, correlation, and activity context are available throughout the application layer.
- As a framework consumer, I want sanitization markers and contracts so that input cleaning happens automatically in the pipeline before validation.
- As a framework consumer, I want a business event log abstraction so that human-readable audit entries are captured with full context.

## Core Requirements

1. **CQRS Core** — Two-level command hierarchy with `ICommand<out TResult> : ICommand` (covariant) inheritance; `IQuery<out TResult>` (covariant) for reads; corresponding handler interfaces returning `Task<Result>` or `Task<Result<TResult>>`
2. **Notifications** — `INotification` marker and `INotificationHandler<TNotification>` for fan-out dispatch returning `Task`
3. **Validation** — `IValidator<T>` returning `Task<Result>` for async pipeline validation
4. **Context Providers** — Four synchronous provider interfaces (`ITenantContextProvider`, `IIdentityContextProvider`, `ICorrelationIdProvider`, `IActivityIdProvider`) with read-only properties
5. **Sanitization** — `ISanitizable` marker in Application (user override of ADR Domain placement) and `ISanitizer` contract
6. **Business Event Log** — `IBusinessEventLog` with `AppendAsync` method and `BusinessLogEntry` record type with structured fields
7. **Sub-namespaces** — One sub-namespace per concern: `Cqrs`, `Notifications`, `Validation`, `Context`, `Sanitization`, `Audit`
8. **XML Documentation** — All public APIs must have comprehensive XML docs (CS1591 enforced as error); follow IDomainEvent.cs documentation depth and style
9. **No External Dependencies** — Only reference Yaf.Domain; no NuGet packages
10. **One Type Per File** — PascalCase filenames with braces for generics (e.g., `ICommand{TResult}.cs`)
11. **File-scoped Namespaces** — `namespace Yaf.Application.Cqrs;` style
12. **Semicolon Body Markers** — Marker interfaces use `public interface IFoo;` syntax

## Reusable Components

### Existing Code to Leverage

| Component | File Path | How It's Used |
|-----------|-----------|---------------|
| `Result` (non-generic) | `src/Yaf.Domain/Result.cs` | Return type for void command handlers and validators |
| `Result<T>` (generic) | `src/Yaf.Domain/Result{T}.cs` | Return type for typed command and query handlers |
| `Error` | `src/Yaf.Domain/Error.cs` | Error type within Result failures |
| `TenantId` | `src/Yaf.Domain/TenantId.cs` | Return type for `ITenantContextProvider.TenantId` property |
| `ActorId` | `src/Yaf.Domain/ActorId.cs` | Return type for `IIdentityContextProvider.ActorId` property |
| `ITenantScoped` | `src/Yaf.Domain/Interfaces/ITenantScoped.cs` | Pattern template for context provider property style |
| `ICorrelated` | `src/Yaf.Domain/Interfaces/ICorrelated.cs` | Pattern template; `CorrelationId` property mirrors provider |
| `IActorScoped` | `src/Yaf.Domain/Interfaces/IActorScoped.cs` | Pattern template; `ActorId` property mirrors provider |
| `IActivityScoped` | `src/Yaf.Domain/Interfaces/IActivityScoped.cs` | Pattern template; `ActivityId` as `string?` mirrors provider |
| `IDomainEvent` | `src/Yaf.Domain/Interfaces/IDomainEvent.cs` | XML documentation style template (summary, remarks with para, typeparam, see cref) |
| `IEncryptable` | `src/Yaf.Domain/Interfaces/IEncryptable.cs` | Marker interface with semicolon body template |
| `IValidatable` | `src/Yaf.Domain/Interfaces/IValidatable.cs` | Domain-level validation pattern; application `IValidator<T>` complements this |
| Project file | `src/Yaf.Application/Yaf.Application.csproj` | Already configured with Yaf.Domain reference and InternalsVisibleTo |

### New Components Required

All 17 types are new because Yaf.Application has zero source files. Each is justified by the ADRs and requirements:

| New Type | Why Not Reusable | Justification |
|----------|-----------------|---------------|
| CQRS interfaces (6 types) | No application-layer dispatch contracts exist | ADR 20260324-1144 mandates YAF-owned CQRS abstractions |
| Notification interfaces (2 types) | No fan-out dispatch contracts exist | ADR 20260324-1144 mandates notification support |
| `IValidator<T>` | Domain has `IValidatable` (self-validation) but no pipeline validator | ADR 20260324-1141 requires pipeline validation as separate concern |
| Context providers (4 types) | Domain has scoped interfaces (read markers) but no providers | ADR 20260324-1146 requires provider interfaces that infrastructure implements |
| Sanitization (2 types) | Nothing exists | ADR 20260324-1146 mandates sanitization contracts |
| Business event log (2 types) | Nothing exists | ADR 20260324-1146 mandates audit abstraction |

## Technical Approach

### File Structure

```
src/Yaf.Application/
  Cqrs/
    ICommand.cs                              # Non-generic void command marker
    ICommand{TResult}.cs                     # Generic command marker : ICommand
    ICommandHandler{TCommand}.cs             # Handler for void commands -> Task<Result>
    ICommandHandler{TCommand,TResult}.cs     # Handler for typed commands -> Task<Result<TResult>>
    IQuery{TResult}.cs                       # Query marker
    IQueryHandler{TQuery,TResult}.cs         # Query handler -> Task<Result<TResult>>
  Notifications/
    INotification.cs                         # Fan-out marker
    INotificationHandler{TNotification}.cs   # Notification handler -> Task
  Validation/
    IValidator{T}.cs                         # Pipeline validator -> Task<Result>
  Context/
    ITenantContextProvider.cs                # Synchronous TenantId property
    IIdentityContextProvider.cs              # Synchronous ActorId property
    ICorrelationIdProvider.cs                # Synchronous Guid CorrelationId property
    IActivityIdProvider.cs                   # Synchronous string? ActivityId property
  Sanitization/
    ISanitizable.cs                          # Marker interface
    ISanitizer.cs                            # Sanitization logic contract
  Audit/
    IBusinessEventLog.cs                     # Append-only log interface
    BusinessLogEntry.cs                      # Record with structured audit fields
```

### Namespace Mapping

| Folder | Namespace |
|--------|-----------|
| `Cqrs/` | `Yaf.Application.Cqrs` |
| `Notifications/` | `Yaf.Application.Notifications` |
| `Validation/` | `Yaf.Application.Validation` |
| `Context/` | `Yaf.Application.Context` |
| `Sanitization/` | `Yaf.Application.Sanitization` |
| `Audit/` | `Yaf.Application.Audit` |

### Interface Design Details

#### CQRS (Yaf.Application.Cqrs)

**ICommand** — Non-generic marker for void commands (no return value beyond success/failure).

**ICommand{TResult}** — Generic marker inheriting from `ICommand`. Covariant `out TResult` (marker has no members, so `TResult` appears in no position; covariance permits `ICommand<Derived>` to be treated as `ICommand<Base>`, matching `IDomainEvent<out T>` pattern in Domain). The inheritance enables pipeline behaviors to target all commands via `ICommand` constraint. Mirrors the `Result`/`Result<T>` duality in Domain.

**ICommandHandler{TCommand}** — Handles non-generic `ICommand` commands. Constraint: `where TCommand : ICommand`. Returns `Task<Result>`. Single method: `Handle(TCommand command, CancellationToken cancellationToken)`.

**ICommandHandler{TCommand,TResult}** — Handles generic `ICommand<TResult>` commands. Constraint: `where TCommand : ICommand<TResult>`. Returns `Task<Result<TResult>>`. Single method: `Handle(TCommand command, CancellationToken cancellationToken)`. Contravariant `in TCommand`.

**IQuery{TResult}** — Marker for read operations. Covariant `out TResult` (marker has no members; matches `ICommand<out TResult>` and `IDomainEvent<out T>` convention). Queries always return typed results.

**IQueryHandler{TQuery,TResult}** — Handles queries. Constraint: `where TQuery : IQuery<TResult>`. Returns `Task<Result<TResult>>`. Single method: `Handle(TQuery query, CancellationToken cancellationToken)`. Contravariant `in TQuery`.

#### Notifications (Yaf.Application.Notifications)

**INotification** — Marker for fan-out messages. Used for domain event handler wiring in the application layer.

**INotificationHandler{TNotification}** — Handles notifications. Constraint: `where TNotification : INotification`. Returns `Task`. Single method: `Handle(TNotification notification, CancellationToken cancellationToken)`. Contravariant `in TNotification`. Returns `Task` (not `Task<Result>`) because fan-out dispatch produces no aggregated result — failures surface via exceptions or handler-internal logging.

#### Validation (Yaf.Application.Validation)

**IValidator{T}** — Pipeline validator for business preconditions. Contravariant `in T`. Single method: `Task<Result> ValidateAsync(T instance, CancellationToken cancellationToken)`. Async because application-level validation may require repository lookups or other I/O. Returns `Result` (not `Result<T>`) since validation produces pass/fail with errors, not a transformed value.

#### Context Providers (Yaf.Application.Context)

All providers use synchronous read-only properties — context is resolved once at request entry by infrastructure, then available synchronously throughout the pipeline.

**ITenantContextProvider** — Property: `TenantId? TenantId { get; }`. Nullable because not all operations are tenant-scoped (e.g., system operations, tenant management).

**IIdentityContextProvider** — Property: `ActorId ActorId { get; }`. Non-nullable because all operations should have an identified actor (system actor for background jobs).

**ICorrelationIdProvider** — Property: `Guid CorrelationId { get; }`. Non-nullable because infrastructure generates one if absent.

**IActivityIdProvider** — Property: `string? ActivityId { get; }`. Nullable because not all execution contexts have an active `System.Diagnostics.Activity` (matches `IActivityScoped` in Domain).

#### Sanitization (Yaf.Application.Sanitization)

**SanitizeAttribute** — Attribute (`[AttributeUsage(Property | Class | Struct)]`) marking properties or whole types for sanitization. Per-property usage opts in a single field; per-type usage auto-applies to `string`, `string[]`, `List<string>`, `IList<string>`, `IReadOnlyList<string>` properties. Replaces an earlier `ISanitizable` marker-interface design.

**ISanitizer** — Contract for sanitization logic. Single method: `T Sanitize<T>(T instance)` (unconstrained — opt-in is via `[Sanitize]` rather than a marker interface). Synchronous because sanitization is an in-memory transformation. Returns a sanitized copy — works with record (immutable) and class types. Pipeline replaces the input with the returned sanitized value before validation.

#### Business Event Log (Yaf.Application.Audit)

**BusinessLogEntry** — Record type with required/init properties matching ADR structure:
- `What` (string) — human-readable description
- `AggregateType` (string) — type name of the aggregate
- `AggregateId` (Guid) — ID of the aggregate (YAF typed IDs are Guid-backed)
- `TenantId` (Guid?) — from context provider (nullable for non-tenant operations)
- `ActorId` (Guid) — from context provider
- `CorrelationId` (Guid) — from context provider
- `ActivityId` (string?) — from context provider
- `OccurredAtUtc` (DateTimeOffset) — system clock
- `Metadata` (IReadOnlyDictionary{string, object}?) — optional additional context

Note: Context fields use primitive types (`Guid`, `Guid?`, `string?`) rather than domain typed IDs (`TenantId`, `ActorId`) because `BusinessLogEntry` is a persistence-oriented DTO — infrastructure populates it from context providers and stores it directly. Using primitives avoids requiring domain type reconstruction when reading entries back.

**IBusinessEventLog** — Append-only interface. Single method:
```csharp
Task AppendAsync(
    string what,
    string aggregateType,
    string aggregateId,
    IReadOnlyDictionary<string, object>? metadata,
    CancellationToken cancellationToken);
```
The implementation constructs the `BusinessLogEntry` from these parameters plus context fields (TenantId, ActorId, CorrelationId, ActivityId, OccurredAtUtc) sourced from context providers. Callers supply only what, aggregateType, aggregateId, and optional metadata. Returns `Task` (not `Task<Result>`) — audit failures throw exceptions for fail-fast semantics; non-blocking write semantics belong in implementation, not the contract.

### Data Flow

```
API Controller creates ICommand<T> / IQuery<T>
  -> Adapter dispatches through pipeline
    -> [Sanitization (ISanitizer on ISanitizable)]
      -> [Validation (IValidator<T>, short-circuits on failure)]
        -> ICommandHandler<T,R>.Handle() / IQueryHandler<T,R>.Handle()
          -> Returns Result / Result<TResult>
```

Context providers are injected where needed throughout the pipeline. Business event log is called explicitly from handlers or domain event handlers.

## Implementation Guidance

### Testing Approach

- 2-8 focused tests per implementation step group
- Tests verify: generic constraints compile correctly, handler return types are correct, marker interfaces have no members, record properties are accessible
- Test project: `Yaf.Application.Tests` (InternalsVisibleTo already configured in .csproj)
- Compilation tests are sufficient for marker interfaces — verify that valid types compile and invalid types do not
- For handler interfaces: verify method signature contracts via mock implementations in tests
- Test verification runs only new tests, not the entire suite

### Standards Compliance

| Standard | Location | Application |
|----------|----------|-------------|
| C# Conventions | `.maister/docs/standards/backend/csharp-conventions.md` | File-scoped namespaces, PascalCase files, semicolon markers, records for value semantics |
| Coding Style | `.maister/docs/standards/global/coding-style.md` | Descriptive names, no dead code, DRY |
| Minimal Implementation | `.maister/docs/standards/global/minimal-implementation.md` | Only interfaces specified by ADRs, no speculative abstractions |
| Models | `.maister/docs/standards/backend/models.md` | XML documentation required (CS1591), `<summary>`, `<remarks>`, `<see cref>` |
| API Design | `.maister/docs/standards/backend/api.md` | CQRS interface ownership, sanitization-before-validation ordering |
| Domain Patterns | `.maister/docs/standards/backend/domain-patterns.md` | Result pattern for business errors |
| Test Writing | `.maister/docs/standards/testing/test-writing.md` | xUnit + AwesomeAssertions, `Method_Condition_ExpectedBehavior` naming |

### Key ADRs

| ADR | Scope | Relevance |
|-----|-------|-----------|
| `docs/adr/architecture/20260324-1144-cqrs-and-mediator-abstraction.md` | CQRS interfaces, notifications, pipeline | Primary specification for CQRS types |
| `docs/adr/architecture/20260324-1146-application-layer-patterns.md` | Context, sanitization, audit | Primary specification for non-CQRS types |
| `docs/adr/domain/20260324-1141-validation-strategy.md` | Validation pipeline | Defines IValidator<T> role and pipeline position |

### ADR Drift / User Overrides

The following decisions deviate from the ADRs by user clarification. Update ADRs to match after implementation lands.

| Decision | ADR Source | Override | Rationale |
|----------|------------|----------|-----------|
| `IValidator<T>` is async (`Task<Result> ValidateAsync`) | ADR-1141 (sync `Result Validate`) | Async | Application-level validators may need repository lookups |
| `[Sanitize]` attribute replaces `ISanitizable` marker (lives in `Yaf.Application.Sanitization`) | ADR-1146 (Domain marker interface) | Application + attribute model | User decision; attribute expresses opt-in per-field or per-type, sanitization concern is application pipeline |
| Non-generic `ICommand` marker exists | ADR-1144 (only `ICommand<TResult>`) | Added | Enables void commands returning `Result` and uniform pipeline targeting |
| `BusinessLogEntry.IdentityId` → `ActorId` | ADR-1146 (`IdentityId`) | Renamed | Aligns with `ActorId` typed ID and `IIdentityContextProvider.ActorId` |
| `BusinessLogEntry.ActivityId` is `string?` | ADR-1146 (`Guid`) | Type changed | Matches W3C trace context format used by `IActivityScoped` |
| `IBusinessEventLog.AppendAsync` parameter-based | ADR-1146 (entry-based example) | Parameter-based | Matches ADR usage example, avoids init-overwrite pattern |

### Using Directives

All interface files in `Yaf.Application.*` sub-namespaces declare `using Yaf.Domain;` at the top to access `Result`, `Result<T>`, `Error`, `TenantId`, and `ActorId` without fully qualifying. No global usings file — explicit per-file usings preserve readability and IDE navigation.

### XML Documentation Templates

- **Marker-only interfaces** (`ICommand`, `IQuery<TResult>`, `INotification`, `ISanitizable`): follow `IEncryptable.cs` style — `<summary>` + `<remarks>` with `<para>` blocks, semicolon body
- **Member-bearing interfaces** (handlers, providers, `IValidator<T>`, `ISanitizer`, `IBusinessEventLog`): follow `IDomainEvent.cs` style — `<summary>`, full `<remarks>`, `<typeparam>` for each generic, `<see cref>` cross-references, optional `<example>` for non-trivial usage
- **Records** (`BusinessLogEntry`): summary + remarks + per-property `<summary>` on init properties

### Variance Defaults

For all generic interfaces in this spec:
- **Marker interfaces** (`ICommand<TResult>`, `IQuery<TResult>`): `out TResult` (covariant) — markers have no members, so `TResult` appears in no constraining position; covariance permits substitution along inheritance hierarchies. Matches existing `IDomainEvent<out T>` convention.
- **Handler input parameters**: `in` (contravariant) — applies to `TCommand`, `TQuery`, `TNotification`, `T` in `IValidator<T>`
- **Handler result type parameters** appearing within `Result<T>` / `Task<Result<T>>`: invariant (no annotation) — `Result<T>` is invariant in `T`, so handlers cannot annotate `TResult` as `out`

## Out of Scope

- Adapter implementations (Wolverine, MediatR) — separate task
- Pipeline behavior implementations (logging, authorization) — infrastructure concern
- Concrete context provider implementations — infrastructure provides these
- Concrete sanitizer implementations (e.g., HtmlSanitizer) — infrastructure concern
- IBusinessEventLog persistence implementation — infrastructure concern
- IApplicationService interface — not specified in current ADRs with enough detail
- FluentValidation integration — API layer concern
- Domain-layer ISanitizable placement — user explicitly chose Application layer

## Success Criteria

1. All 17 types compile without errors in Yaf.Application
2. No external NuGet dependencies added (only Yaf.Domain reference)
3. All public APIs have XML documentation (CS1591 passes)
4. `ICommand<TResult> : ICommand` inheritance hierarchy works correctly
5. Handler methods accept `CancellationToken` and return appropriate `Task<Result>` / `Task<Result<TResult>>` / `Task`
6. Context provider properties use correct domain types (`TenantId?`, `ActorId`, `Guid`, `string?`)
7. `BusinessLogEntry` is a record with the 9 fields specified in the ADR
8. All interfaces follow coding conventions: file-scoped namespaces, semicolon markers, contravariance where appropriate
9. Tests verify constraint correctness and interface contracts
10. Existing domain tests continue to pass (no regressions)
