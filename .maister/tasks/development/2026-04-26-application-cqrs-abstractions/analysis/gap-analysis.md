# Gap Analysis: Application Layer CQRS and Abstractions

## Summary
- **Risk Level**: Low
- **Estimated Effort**: Medium (interface count is ~15-18 files, but all are declarative with no logic)
- **Detected Characteristics**: creates_new_entities

## Task Characteristics
- Has reproducible defect: no
- Modifies existing code: no
- Creates new entities: yes
- Involves data operations: no
- UI heavy: no

## Gaps Identified

### Missing Features (Everything is Greenfield)

Yaf.Application contains zero source files. Every interface listed below must be created from scratch.

**CQRS Core (specified in ADR 20260324-1144)**:
1. `ICommand<TResult>` -- generic command marker, returns `Result<TResult>` via handler
2. `ICommandHandler<TCommand, TResult>` -- single handler per command
3. `IQuery<TResult>` -- query marker
4. `IQueryHandler<TQuery, TResult>` -- single handler per query
5. `INotification` -- fan-out marker
6. `INotificationHandler<TNotification>` -- zero-or-more handlers per notification

**Two-Level Command Hierarchy (task requirement, NOT in ADR)**:
7. `ICommand` (non-generic) -- void commands returning `Result` (not `Result<TResult>`)
8. `ICommandHandler<TCommand>` (single type parameter) -- handles non-generic `ICommand`, returns `Task<Result>`

The ADR only defines `ICommand<TResult>`. The task description adds a non-generic `ICommand` for void operations. This is a reasonable extension (mirrors `Result` vs `Result<T>` duality already in Domain) but represents scope beyond the ADR.

**Validation (specified in ADR 20260324-1141)**:
9. `IValidator<T>` -- pipeline validator, returns `Result`

**Context Providers (specified in ADR 20260324-1146)**:
10. `ITenantContextProvider` -- resolves current tenant
11. `IIdentityContextProvider` -- resolves current user identity
12. `ICorrelationIdProvider` -- resolves correlation ID
13. `IActivityIdProvider` -- resolves activity/trace ID

**Sanitization (specified in ADR 20260324-1146)**:
14. `ISanitizable` -- marker on types requiring sanitization
15. `ISanitizer` -- sanitization logic interface

**Business Event Log (specified in ADR 20260324-1146)**:
16. `IBusinessEventLog` -- append-only audit log interface
17. `BusinessLogEntry` -- entry type with structured fields (What, AggregateType, AggregateId, TenantId, IdentityId, CorrelationId, ActivityId, OccurredAtUtc, Metadata)

### ADR vs Task Scope Discrepancies

| Item | ADR Status | Task Description | Gap |
|------|-----------|-----------------|-----|
| Non-generic `ICommand` | Not specified | Explicitly requested | Needs design decision |
| Non-generic `ICommandHandler<TCommand>` | Not specified | Implied by two-level hierarchy | Needs design decision |
| `ISanitizable` layer placement | ADR says Domain | Task lists under Application | Needs clarification |
| `BusinessLogEntry` | ADR specifies fields | Task just says `IBusinessEventLog` | Entry type also needed |

## New Capability Analysis

### Integration Points

1. **Yaf.Domain dependency**: Handlers return `Result<TResult>` / `Result`, reference `Error` type. Application depends on Domain (already configured in .csproj).
2. **Future adapter packages**: `Yaf.Application.Wolverine` (and others) will bridge these interfaces to mediator implementations. Adapters reference Application.
3. **Future API layer**: Controllers will create commands/queries and dispatch them. API references Application.
4. **Consumer handler implementations**: Application developers implement `ICommandHandler<T,R>` and `IQueryHandler<T,R>` in their own assemblies.

### Patterns to Follow

All patterns derived from existing Domain layer code:

| Pattern | Source Template | Application |
|---------|----------------|-------------|
| Marker interface with semicolon body | `IEncryptable.cs` | `ICommand<TResult>`, `IQuery<TResult>`, `INotification`, `ISanitizable` |
| Interface with single method | `IValidatable.cs` | Handlers, `IValidator<T>`, `ISanitizer`, `IBusinessEventLog` |
| Comprehensive XML docs | `IDomainEvent.cs` | All interfaces |
| File-scoped namespaces | All Domain files | All Application files |
| Generic file naming with braces | `Entity{TId,TSelf,TMemento}.cs` | `ICommand{TResult}.cs`, etc. |
| Contravariant type parameters | N/A (new pattern) | Handler `in TCommand` / `in TQuery` |
| Composable context interfaces | `ICorrelated`, `ITenantScoped` | Context providers follow similar naming |

### Architectural Impact: Low

- 15-18 new files in `src/Yaf.Application/`
- No changes to existing files
- No new project references needed (Yaf.Domain reference already exists)
- Namespace: `Yaf.Application` for core CQRS, sub-namespaces TBD for context/sanitization/audit

### Namespace Organization Decision

The Domain layer uses a flat structure with an `Interfaces/` folder for interfaces and root namespace for implementations. Application could follow the same pattern or use sub-namespaces for grouping. Options:

1. **Flat `Yaf.Application` namespace** with optional folders for organization (mirrors Domain)
2. **Sub-namespaces**: `Yaf.Application.Cqrs`, `Yaf.Application.Context`, `Yaf.Application.Sanitization`, `Yaf.Application.Audit`

## Issues Requiring Decisions

### Critical (Must Decide Before Proceeding)

None -- this is greenfield with no blocking issues.

### Important (Should Decide)

1. **Two-Level Command Hierarchy Design**
   - **Issue**: The ADR specifies only `ICommand<TResult>`. The task adds non-generic `ICommand` for void operations. How should the hierarchy relate?
   - **Options**:
     - A) `ICommand` (non-generic, void) as base, `ICommand<TResult>` extends it: `public interface ICommand<TResult> : ICommand;`
     - B) `ICommand` and `ICommand<TResult>` as independent marker interfaces (no inheritance)
     - C) Only `ICommand<TResult>` exists; void commands use `ICommand<Unit>` or similar
   - **Default**: Option A -- inheritance mirrors `Result`/`Result<T>` pattern and enables pipeline behaviors to target all commands via `ICommand`
   - **Rationale**: Option A allows pipeline behaviors (validation, logging) to constrain on `ICommand` to match ALL commands regardless of return type, which is the primary motivation for the two-level hierarchy

2. **ISanitizable Layer Placement**
   - **Issue**: ADR 20260324-1146 places `ISanitizable` in Domain and `ISanitizer` in Application. The task description lists both under Application scope.
   - **Options**:
     - A) Follow ADR: `ISanitizable` in Domain, `ISanitizer` in Application
     - B) Follow task: both in Application
   - **Default**: Option A -- follow ADR (higher priority per CLAUDE.md)
   - **Rationale**: ADR explicitly states "`ISanitizable` | Domain | Marker interface on types/properties requiring sanitization". Domain types need to be markable as sanitizable without depending on Application.

3. **Namespace Organization**
   - **Issue**: With ~17 interfaces spanning 5 concerns (CQRS, notifications, validation, context, sanitization/audit), a flat namespace may become crowded.
   - **Options**:
     - A) Flat `Yaf.Application` namespace, file folders for IDE organization only
     - B) Sub-namespaces per concern (`Yaf.Application.Context`, `Yaf.Application.Sanitization`)
   - **Default**: Option A -- flat namespace mirrors Domain's approach (`Yaf.Domain.Interfaces` uses one namespace for all interfaces)
   - **Rationale**: Domain uses `Yaf.Domain.Interfaces` as a single namespace for all interfaces. Application can use `Yaf.Application` with a similar approach. Consumers benefit from fewer `using` statements.

4. **Context Provider Return Types**
   - **Issue**: ADR 20260324-1146 specifies what context providers do but not their exact method signatures. Need to determine return types.
   - **Options**:
     - A) Synchronous properties: `TenantId TenantId { get; }` (like domain interfaces)
     - B) Synchronous methods: `TenantId GetTenantId()`
     - C) Async methods: `Task<TenantId> GetTenantIdAsync()` (allows async resolution from HTTP context, JWT, etc.)
   - **Default**: Option A -- synchronous properties (context is resolved once at request start, then available synchronously)
   - **Rationale**: Context providers are populated by infrastructure at request entry. By the time application code reads them, the value is already resolved. Properties match the domain interface pattern (`ICorrelated.CorrelationId`, `ITenantScoped.TenantId`).

5. **BusinessLogEntry: Class vs Record**
   - **Issue**: ADR specifies `BusinessLogEntry` with specific fields but not the type design. It's a data carrier for audit entries.
   - **Options**:
     - A) Immutable record with required properties
     - B) Mutable class with settable properties (as shown in ADR code sample)
     - C) Record with constructor (fully immutable)
   - **Default**: Option A -- record with required/init properties
   - **Rationale**: Audit log entries are append-only and should be immutable once created. Records provide value semantics. The ADR sample uses object initializer syntax which works with `init` properties.

6. **IBusinessEventLog Method Signature**
   - **Issue**: ADR shows `AppendAsync(BusinessLogEntry)` but context fields (TenantId, IdentityId, CorrelationId, ActivityId) are described as "auto-populated from context providers." Should the interface accept a full entry or a partial one?
   - **Options**:
     - A) Accept full `BusinessLogEntry` -- infrastructure implementation auto-populates context fields before persisting
     - B) Accept only user-supplied fields (What, AggregateType, AggregateId, Metadata) -- implementation adds context
   - **Default**: Option A -- matches ADR code sample, simpler interface
   - **Rationale**: The ADR shows `new BusinessLogEntry { What = ..., AggregateType = ..., AggregateId = ... }` with context auto-populated. Option A keeps the interface simple; the implementation enriches the entry.

## Recommendations

1. **Follow ADR signatures exactly** for CQRS core (ICommand<TResult>, IQuery<TResult>, handlers, notifications)
2. **Add non-generic ICommand as base** for the two-level hierarchy (task requirement)
3. **Place ISanitizable in Domain** per ADR unless overridden (flag for orchestrator)
4. **Use IDomainEvent.cs as documentation template** -- it demonstrates the project's XML doc depth expectations
5. **Create all interfaces in a single implementation phase** -- they have no interdependencies beyond the command hierarchy
6. **Defer test project creation** unless specifically requested (marker interfaces have limited testable surface)

## Risk Assessment

- **Complexity Risk**: Low -- all interfaces are declarative with no logic
- **Integration Risk**: Low -- greenfield, no existing consumers
- **Regression Risk**: None -- no existing code is modified
- **ADR Alignment Risk**: Low-Medium -- task adds non-generic ICommand not in ADR, and ISanitizable placement needs clarification
