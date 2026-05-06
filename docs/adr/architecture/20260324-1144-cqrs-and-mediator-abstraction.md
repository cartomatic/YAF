# CQRS and Mediator Abstraction

- **Timestamp:** 2026-03-24 11:44
- **Status:** accepted
- **Scope:** architecture
- **Stakeholders:** Proposed by: Claude Code, Decided by: @cartomatic

---

## Summary

YAF defines its own CQRS abstractions (`ICommand<TResult>`, `IQuery<TResult>`, handlers, `INotification`) in Yaf.Application. These are mediator-agnostic — they define the contract without coupling to any dispatch library. Concrete adapter packages (starting with Wolverine) bridge YAF's interfaces to specific mediator implementations. Consumers can swap mediators without touching application code.

## Drivers

1. **Mediator independence** — MediatR's move to commercial licensing demonstrates the risk of coupling directly to a third-party mediator. YAF must own its abstractions.
2. **Adapter swappability** — Consumers should be able to choose Wolverine, MediatR, or a future mediator without changing their command/query handlers.
3. **Clean command/query separation** — Commands mutate state. Queries read state. The type system should enforce this distinction.
4. **Pipeline extensibility** — Cross-cutting behaviors (validation, logging, authorization) should plug into the dispatch pipeline without modifying handlers.
5. **Future messaging alignment** — Wolverine combines mediator + messaging + outbox. Owning the abstractions means the in-process CQRS model can extend to distributed messaging when Yaf.Messaging is built.

## Options

### Abstraction Ownership

| Option | Assessment |
|--------|------------|
| **YAF owns CQRS interfaces in Yaf.Application** | **Selected.** Framework-owned contracts, adapter packages bridge to implementations. Consumers code against YAF's interfaces. |
| Use Wolverine's interfaces directly | Couples all application code to Wolverine. Swapping mediators requires rewriting handlers. |
| Use MediatR's `IRequest`/`IRequestHandler` | Same coupling problem, plus commercial licensing risk. |
| No abstractions — consumers pick their own | YAF can't provide pipeline behaviors (validation, logging) if there's no common contract. |

### First Adapter

| Option | Assessment |
|--------|------------|
| **Wolverine** | **Selected.** MIT-licensed, convention-based, combines mediator + messaging + outbox. Best long-term value. |
| MediatR | Commercial license. Well-known but licensing direction is uncertain. Can be supported as a future adapter. |
| Custom in-process dispatch | Unnecessary engineering. Wolverine and MediatR are mature, tested libraries. |

### Notification Model

| Option | Assessment |
|--------|------------|
| **`INotification` / `INotificationHandler<T>` in Yaf.Application** | **Selected.** Fan-out dispatch — one notification, multiple handlers. Used for domain event handler wiring in the application layer. |
| Domain events only (no notification abstraction) | Domain events need a dispatch mechanism in the application layer. Without `INotification`, consumers must wire their own fan-out. |

## Recommendation

YAF-owned CQRS abstractions with Wolverine as the first adapter. The abstractions are minimal — just enough to define the contract without reimplementing a mediator.

## Consequences

**Positive:**
- Application code is mediator-agnostic — handlers implement YAF interfaces, not library-specific ones
- Swapping from Wolverine to MediatR (or vice versa) only changes the adapter package reference and DI registration
- Pipeline behaviors (validation, logging, authorization) plug into YAF's pipeline contract, not a library-specific one
- Wolverine's messaging capabilities provide a natural path from in-process CQRS to distributed messaging
- MIT licensing across the stack — no commercial risk

**Negative:**
- Thin abstraction layer between YAF and the mediator adds a small amount of indirection
- Adapter packages must be maintained for each supported mediator
- Wolverine is less widely known than MediatR — fewer community resources and examples
- Consumers familiar with MediatR need to learn YAF's interface names (mitigated: nearly identical patterns)

## Conclusion

### Abstractions (Yaf.Application.Cqrs / .Notifications)

| Interface | Purpose | Dispatch |
|-----------|---------|----------|
| `ICommand` | Represents a state-mutating operation with no return value | Single handler, returns `Task<Result>` |
| `ICommand<out TResult>` | Represents a state-mutating operation that produces a typed value (covariant marker, inherits `ICommand`) | Single handler, returns `Task<Result<TResult>>` |
| `ICommandHandler<in TCommand>` | Handles a void command | One handler per command |
| `ICommandHandler<in TCommand, TResult>` | Handles a typed-result command | One handler per command |
| `IQuery<out TResult>` | Represents a read operation (covariant marker) | Single handler, returns `Task<Result<TResult>>` |
| `IQueryHandler<in TQuery, TResult>` | Handles a specific query | One handler per query |
| `INotification` | Represents a fan-out message | Multiple handlers |
| `INotificationHandler<in TNotification>` | Handles a specific notification (returns `Task` — fan-out has no aggregated outcome) | Zero or more per notification |

### Variance and Constraints

- **Marker interfaces** (`ICommand<TResult>`, `IQuery<TResult>`) are covariant in `TResult` (`out`). Markers have no members, so `TResult` appears in no constraining position; covariance permits `ICommand<Derived>` references to be assigned where `ICommand<Base>` is expected. Mirrors the existing `IDomainEvent<out T>` convention.
- **Handler input parameters** are contravariant (`in TCommand`, `in TQuery`, `in TNotification`) so that a base-typed handler can stand in where a derived-typed handler is required.
- **Handler `TResult` is invariant.** `Task<Result<TResult>>` nests `TResult` inside two invariant generics: `Task<T>` (BCL) and `Result<T>` (a `readonly struct`; structs cannot have variance annotations in C#). Declaring `out TResult` on the handler is rejected by the compiler with CS1961.
- All `TResult` parameters that flow through `Result<T>` are constrained to `where TResult : notnull` to match `Result<T>`'s own constraint.

### Two-Level Command Hierarchy

The non-generic `ICommand` is a YAF-specific addition not present in MediatR or Wolverine. It exists so that:

1. Void commands return `Result` (success/failure only) without an artificial `Unit` payload.
2. Pipeline behaviors that target every command — logging, sanitization, validation — can constrain on `ICommand` uniformly regardless of whether the command produces a typed value.

`ICommand<out TResult>` inherits from `ICommand`, so any code reasoning about "any command" can reference the non-generic base.

### Command vs Query

| Aspect | Command | Query |
|--------|---------|-------|
| **Purpose** | Mutate state | Read state |
| **Return** | `Result<TResult>` (confirmation, created ID, etc.) | `Result<TResult>` (data) |
| **Side effects** | Yes — persists changes, raises events | No — read-only |
| **Handlers** | Exactly one per command | Exactly one per query |
| **Caching** | Never | Possible (future concern) |

### Pipeline Behaviors

Cross-cutting concerns plug into the dispatch pipeline as behaviors that wrap handler execution:

```
Request arrives
  → Logging behavior (log entry)
    → Sanitization behavior (clean inputs marked with [Sanitize])
      → Validation behavior (run IValidator<T>, short-circuit on failure)
        → Authorization behavior (future — check permissions)
          → Handler executes
        ← Authorization
      ← Validation
    ← Sanitization
  ← Logging (log exit + duration)
Response returned
```

Sanitization runs **before** validation — inputs are cleaned first, then validated against the sanitized values. This ensures validation rules operate on safe data.

Pipeline behaviors are defined in Yaf.Application as interfaces. Adapter packages wire them into the specific mediator's pipeline mechanism (e.g., Wolverine middleware, MediatR pipeline behaviors).

### Adapter Package Contract

Each adapter package must:

1. **Register YAF handler types** with the mediator's DI container
2. **Bridge dispatch** — route `ICommand<T>` / `IQuery<T>` to the correct `ICommandHandler` / `IQueryHandler`
3. **Wire pipeline behaviors** — map YAF's validation, logging, and future behaviors to the mediator's pipeline
4. **Bridge notifications** — route `INotification` to all registered `INotificationHandler<T>` implementations
5. **Provide a DI extension** — `AddYafWolverine()`, `AddYafMediatR()`, etc.

### Wolverine Adapter (Yaf.Application.Wolverine)

The first adapter bridges YAF's interfaces to Wolverine:

| YAF Concept | Wolverine Concept |
|-------------|------------------|
| `ICommand<T>` dispatch | Wolverine `InvokeAsync<T>()` |
| `IQuery<T>` dispatch | Wolverine `InvokeAsync<T>()` |
| `INotification` dispatch | Wolverine `PublishAsync()` |
| Pipeline behaviors | Wolverine middleware chain |
| Handler discovery | Wolverine's assembly scanning, bridged to YAF handler interfaces |

**Registration:**
```
builder.AddYaf(options =>
{
    options.UseWolverine();  // registers Yaf.Application.Wolverine adapter
});
```

### Future: MediatR Adapter

A `Yaf.Application.MediatR` package would bridge to MediatR's `IMediator.Send()` / `Publish()` and `IPipelineBehavior<,>`. Same application code, different dispatch library.

## More Information

- [YAF Library Design Brainstorm](../../brainstorms/20260322-1755-yaf-library-design-brainstorm.md) — CQRS decisions (section "Key Decisions" #1)
- [ADR: Technology Stack](20260324-0948-technology-stack.md) — Wolverine selection
- [ADR: Solution Structure](20260324-0953-solution-structure-and-package-layering.md) — adapter package conventions
- [ADR: Validation Strategy](../domain/20260324-1141-validation-strategy.md) — validation pipeline behavior
