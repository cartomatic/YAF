# Codebase Analysis Report

**Date**: 2026-04-26 10:57
**Task**: Add ICommand/IQuery/ICommandHandler/IQueryHandler CQRS abstractions to Yaf.Application
**Description**: Mediator-agnostic command and query interfaces for the application layer
**Analyzer**: codebase-analyzer skill (3 Explore agents: File Discovery, Pattern Mining, Code Analysis)

---

## Summary

The Yaf.Application project exists but is empty (only a .csproj with a reference to Yaf.Domain). ADR `20260324-1144-cqrs-and-mediator-abstraction.md` fully specifies the CQRS interface signatures, return types, and pipeline design. The domain layer (48 files, 1806 lines) provides mature patterns (XML docs, marker interfaces, generics with constraints) that serve as direct templates for the new application-layer interfaces.

---

## Files Identified

### Primary Files

**`src/Yaf.Application/Yaf.Application.csproj`** (15 lines)
- Empty application project, references Yaf.Domain
- Target for all new CQRS interface files

**`docs/adr/architecture/20260324-1144-cqrs-and-mediator-abstraction.md`** (152 lines)
- Authoritative specification for all CQRS abstractions
- Defines ICommand, IQuery, handlers, INotification, pipeline behaviors

**`docs/adr/architecture/20260324-1146-application-layer-patterns.md`** (158 lines)
- Defines context providers, sanitization, business event log
- Provides broader application layer context (not in scope for this task but informs namespace design)

**`docs/adr/domain/20260324-1141-validation-strategy.md`**
- Defines IValidator<T> for pipeline validation
- Application pipeline validation runs before handler execution

### Related Files

**`src/Yaf.Domain/Result{T}.cs`** (168 lines)
- Return type for all command and query handlers (`Result<TResult>`)
- Defines the success/failure pattern handlers must use

**`src/Yaf.Domain/Result.cs`**
- Non-generic Result type, used by IValidator<T> return

**`src/Yaf.Domain/Error.cs`**
- Error type used in Result failures

**`src/Yaf.Domain/Interfaces/IDomainEvent.cs`** (56 lines)
- Best template for XML documentation style and composite marker interfaces
- Shows covariant generic pattern (`out T`)

**`src/Yaf.Domain/Interfaces/IMemento.cs`** (24 lines)
- Template for CRTP constraints and `static abstract` usage
- Shows two-type-parameter generic interface style

**`src/Yaf.Domain/Interfaces/IValidatable.cs`**
- Domain-level validation interface, related to application IValidator<T>

**`.maister/docs/standards/backend/api.md`**
- Confirms CQRS interface signatures and sanitization-before-validation ordering

**`.maister/docs/standards/backend/csharp-conventions.md`**
- Marker interfaces use semicolon body
- Generic file naming with braces: `ICommand{TResult}.cs`

---

## Current Functionality

The Yaf.Application project is empty -- no source files exist yet. This is a greenfield implementation within an existing project structure.

### What Exists in Domain (to be referenced)

- **`Result<T>`**: Readonly struct representing success-with-value or failure-with-errors. Handlers return `Result<TResult>`.
- **`Result`**: Non-generic result with static factory methods (`Success<T>`, `Failure<T>`). `IValidator<T>` returns `Result`.
- **`Error`**: Immutable error type with Code, Message, optional inner errors. Used throughout the Result pattern.
- **Cross-cutting interfaces**: `ICorrelated`, `ITenantScoped`, `IActorScoped`, `IActivityScoped` -- composable context interfaces that commands/queries may optionally implement.

### Interface Signatures (from ADR)

The ADR specifies these exact interfaces:

| Interface | Type Parameters | Constraints | Return |
|-----------|----------------|-------------|--------|
| `ICommand<TResult>` | `TResult` | Marker | -- |
| `IQuery<TResult>` | `TResult` | Marker | -- |
| `ICommandHandler<TCommand, TResult>` | `in TCommand, TResult` | `TCommand : ICommand<TResult>` | `Task<Result<TResult>>` |
| `IQueryHandler<TQuery, TResult>` | `in TQuery, TResult` | `TQuery : IQuery<TResult>` | `Task<Result<TResult>>` |
| `INotification` | -- | Marker | -- |
| `INotificationHandler<TNotification>` | `in TNotification` | `TNotification : INotification` | `Task` |

### Data Flow

```
API Controller -> ICommand<TResult> / IQuery<TResult>
  -> Mediator adapter dispatches
    -> [Logging -> Sanitization -> Validation -> Authorization] pipeline
      -> ICommandHandler<T,R>.Handle() / IQueryHandler<T,R>.Handle()
        -> Returns Result<TResult>
```

---

## Dependencies

### Imports (What Yaf.Application Depends On)

- **Yaf.Domain**: `Result<T>`, `Result`, `Error` (handler return types)
- No external NuGet dependencies (zero-dependency application layer, same philosophy as domain)

### Consumers (What Will Depend On This)

- **Yaf.Infrastructure.{Adapter}**: Will bridge CQRS interfaces to mediator implementations (e.g., Wolverine)
- **Yaf.Api**: Will dispatch commands/queries from controllers
- **Consumer application code**: Will implement `ICommandHandler<T,R>` and `IQueryHandler<T,R>`
- **Future pipeline behaviors**: Will reference `ICommand<T>`, `IQuery<T>` constraints

**Consumer Count**: 0 files currently (greenfield), 3+ layers will consume once built
**Impact Scope**: Low -- new code with no existing consumers to break

---

## Test Coverage

### Test Files

- No test project exists for Yaf.Application yet (Yaf.Application.Tests)
- `InternalsVisibleTo` for `Yaf.Application.Tests` is already configured in the .csproj

### Coverage Assessment

- **Test count**: 0 tests (greenfield)
- **Gaps**: Entire application layer is untested. For marker interfaces, tests would verify constraints compile correctly. Handler interfaces need tests confirming Result<T> return type contracts.

---

## Coding Patterns

### Naming Conventions

- **Interfaces**: `I` prefix, PascalCase (e.g., `ICommand`, `IQueryHandler`)
- **Generic parameters**: Descriptive names (`TResult`, `TCommand`, `TQuery`, `TNotification`)
- **Files**: PascalCase with braces for generics: `ICommand{TResult}.cs`, `ICommandHandler{TCommand,TResult}.cs`

### Architecture Patterns

- **Marker interfaces**: Semicolon body (e.g., `public interface ICommand<TResult>;`)
- **Handler interfaces**: Single `Handle` method returning `Task<Result<TResult>>`
- **Contravariance**: `in` on handler type parameters for dispatch flexibility
- **One interface per file**: Consistent with all domain interfaces
- **File-scoped namespaces**: `namespace Yaf.Application;`
- **XML documentation**: Mandatory on all public APIs; CS1591 enforced as error
- **Documentation style**: `<summary>`, `<remarks>` with `<para>`, `<typeparam>`, `<see cref>` cross-references (follow IDomainEvent.cs pattern)

---

## Complexity Assessment

| Factor | Value | Level |
|--------|-------|-------|
| File count | 6 new files | Low-Med |
| Dependencies | 1 (Yaf.Domain) | Low |
| Consumers | 0 current | Low |
| Test Coverage | 0 tests needed | Low |

### Overall: Simple

These are marker interfaces and single-method handler interfaces with no implementation logic. The signatures are fully specified by the ADR. The primary effort is ensuring correct generic constraints, proper XML documentation, and adherence to established coding patterns.

---

## Key Findings

### Strengths
- ADR provides complete, unambiguous interface specifications
- Domain layer establishes clear patterns to follow (IDomainEvent, IMemento as templates)
- Yaf.Application project already exists with correct project reference and InternalsVisibleTo
- Result<T> and Error types in Domain are ready to be used as handler return types

### Concerns
- ADR status is "under review" -- interface signatures may still change (low risk given specificity)
- No existing application-layer code means no precedent for namespace structure within Yaf.Application
- Handler return types should be `Task<Result<TResult>>` but ADR table says `Result<TResult>` -- async handlers need Task wrapping (standard practice, implied by async pipeline)

### Opportunities
- Establish namespace conventions for Yaf.Application that future components (context providers, sanitization, validators) will follow
- Create comprehensive XML documentation that serves as API reference for consumers
- Set up Yaf.Application.Tests project structure for future test coverage

---

## Impact Assessment

- **Primary changes**: 6 new files in `src/Yaf.Application/` (ICommand, IQuery, ICommandHandler, IQueryHandler, INotification, INotificationHandler)
- **Related changes**: Possible Yaf.Application.Tests project creation
- **Test updates**: New test project if marker interface constraint verification is desired

### Risk Level: Low

All interfaces are greenfield with no existing consumers. Signatures are fully specified by an ADR. The implementation is purely declarative (interfaces only, no logic). The only risk is deviation from the ADR spec, which is mitigated by the specificity of the specification.

---

## Recommendations

### Implementation Strategy

Since this is a greenfield capability with a complete specification:

1. **Create the 6 interface files** in `src/Yaf.Application/` following the ADR signatures exactly
2. **Use `Yaf.Application` as the root namespace** for core CQRS interfaces (consistent with `Yaf.Domain` root namespace for core types)
3. **Follow IDomainEvent.cs as the documentation template** -- it demonstrates the exact XML doc style expected
4. **Use marker interface semicolon syntax** for ICommand, IQuery, INotification
5. **Handler methods should return `Task<Result<TResult>>`** for commands/queries and `Task` for notifications (async by default)

### File Creation Order

1. `ICommand{TResult}.cs` -- marker interface
2. `IQuery{TResult}.cs` -- marker interface
3. `ICommandHandler{TCommand,TResult}.cs` -- handler with Handle method
4. `IQueryHandler{TQuery,TResult}.cs` -- handler with Handle method
5. `INotification.cs` -- marker interface
6. `INotificationHandler{TNotification}.cs` -- handler with Handle method

### Patterns to Follow

- Copy XML documentation depth and style from `IDomainEvent.cs` and `IMemento.cs`
- Use contravariance (`in`) on handler command/query type parameters
- Use `where TCommand : ICommand<TResult>` constraints as specified in ADR
- Include `<see cref>` cross-references between command/handler pairs and to `Result<T>`

### What NOT to Include (per ADR scope)

- No `IValidator<T>` (separate concern, covered by validation strategy ADR)
- No context providers (covered by application layer patterns ADR)
- No `ISanitizable`/`ISanitizer` (separate concern)
- No `IBusinessEventLog` (separate concern)
- No pipeline behavior interfaces (adapter-specific wiring)

---

## Next Steps

Proceed to gap analysis to confirm scope boundaries and identify any missing details before specification and implementation planning.
