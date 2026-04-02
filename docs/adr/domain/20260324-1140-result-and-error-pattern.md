# Result and Error Pattern

- **Timestamp:** 2026-03-24 11:40
- **Status:** accepted
- **Scope:** domain
- **Stakeholders:** Proposed by: Claude Code, Decided by: @cartomatic

---

## Summary

YAF ships its own `Result<T>` type in Yaf.Domain and a layered error hierarchy (`IError` → `IDomainError` / `IApplicationError`). Domain methods and application handlers return `Result<T>` instead of throwing exceptions for business rule violations. Errors are declared on domain objects and handlers via `IErrorSource`, enabling automatic discovery of a complete error catalog exposable via API.

## Drivers

1. **No exceptions for business logic** — Exceptions are for exceptional circumstances (infrastructure failures, programming errors), not for expected business outcomes like "order exceeds limit" or "customer not found."
2. **Explicit error paths** — Return types should make it obvious that an operation can fail. Callers must handle the failure case — it's not hidden in a catch block somewhere up the stack.
3. **Error discoverability** — Consumers should be able to expose a complete catalog of all possible errors to API clients, for documentation, client-side handling, and contract testing.
4. **Layered error semantics** — Domain errors (invariant violations, business rule failures) are semantically different from application errors (not found, unauthorized, validation failures). The error type should reflect where it originated.
5. **Zero dependencies** — `Result<T>` and the error hierarchy live in Yaf.Domain with no external packages.

## Options

### Result Type

| Option | Assessment |
|--------|------------|
| **Custom `Result<T>` in Yaf.Domain** | **Selected.** Lightweight, integrates with YAF's `IError` hierarchy, zero dependencies. Tailored to the framework's needs. |
| FluentResults | External dependency in Domain — violates zero-dependency rule. Also brings more API surface than needed. |
| OneOf / discriminated unions | External dependency. C# doesn't have native discriminated unions yet. When it does, `Result<T>` can evolve. |
| Exceptions for all errors | Violates driver #1. Exception-based control flow is expensive, hard to discover, and doesn't force callers to handle failures. |
| Nullable returns (`T?`) | No error information. Caller knows it failed but not why. |

### Error Hierarchy

| Option | Assessment |
|--------|------------|
| **`IError` → `IDomainError` / `IApplicationError` with `IErrorSource` discovery** | **Selected.** Two-level hierarchy distinguishes error origin. `IErrorSource` enables auto-discovery of all errors across the system. |
| Flat error type (no hierarchy) | Loses the semantic distinction between domain and application errors. API layer can't differentiate "business rule violated" from "entity not found." |
| Exception-based hierarchy | Same problems as using exceptions for control flow. Plus, exception hierarchies don't compose well with Result types. |
| String error codes only | No structure, no discoverability, no compile-time safety. |

## Recommendation

Custom `Result<T>` with a two-level error hierarchy and auto-discovery via `IErrorSource`. This gives explicit error handling, layered semantics, and a discoverable error catalog — all with zero external dependencies.

## Consequences

**Positive:**
- Method signatures communicate failure possibility — `Result<Order>` is self-documenting
- Callers must handle errors explicitly — no silent swallowing, no surprise exceptions
- Error catalog is auto-discoverable — exposable as an API endpoint for client documentation
- Domain and application errors are distinguishable — API layer can map them to appropriate HTTP status codes
- Zero external dependencies — lives entirely in Yaf.Domain

**Negative:**
- `Result<T>` adds verbosity to method signatures and call sites
- Developers must remember to use `Result<T>` instead of throwing exceptions for business logic
- Error catalog discovery requires scanning assemblies (reflection at startup — one-time cost)
- Two representations of failure (Result errors vs exceptions) — need clear guidance on when to use which

## Conclusion

### Result\<T\>

```
// Yaf.Domain
public readonly struct Result<T> : IEquatable<Result<T>>
    where T : notnull
{
    public bool IsSuccess { get; }
    public bool IsFailure { get; }
    public T Value { get; }                         // throws if IsFailure
    public Error Error { get; }                     // first error; throws if IsSuccess
    public IReadOnlyList<Error> Errors { get; }     // all errors; throws if IsSuccess

    public static implicit operator Result<T>(T value);      // T → success
    public static implicit operator Result<T>(Error error);  // Error → failure
}
```

Key design decisions:
- `where T : notnull` — prevents nullable success values at compile time
- `default(Result<T>)` is a failure with a sentinel error (`"Yaf.Domain.Result.Uninitialized"`)
- `.Error` returns first error (convenience); `.Errors` returns full collection
- `IError` interface is `internal` — only `Error` (concrete) is visible above the domain layer
- Multi-error `Failure` factories accept `IReadOnlyCollection<Error>` (matches updated `IValidatable`)
- `Error.Create<T>()` and `Error.Unspecified<T>()` return `Error` (concrete) to enable implicit conversion
- Equality via `IEquatable<Result<T>>` with explicit `operator ==`/`!=`

**Usage in domain (with implicit conversions):**
```
public Result<Order> AddItem(Product product, int quantity)
{
    if (quantity <= 0)
        return InvalidQuantity;    // implicit Error → Result<Order>

    // ... business logic ...
    return this;                   // implicit Order → Result<Order>
}
```

**Usage in application handlers:**
```
public Result<OrderDto> Handle(PlaceOrderCommand command)
{
    var order = Order.Create(command.CustomerId, command.Items);
    if (order.IsFailure)
        return Result.Failure<OrderDto>(order.Error);

    // ... persist, return DTO ...
}
```

### Result (Non-Generic)

```
// Yaf.Domain — also hosts static factories for Result<T>
public readonly struct Result : IEquatable<Result>
{
    public bool IsSuccess { get; }
    public bool IsFailure { get; }
    public Error Error { get; }
    public IReadOnlyList<Error> Errors { get; }
    // No .Value property

    public static Result Success();
    public static Result<T> Success<T>(T value) where T : notnull;
    public static Result Failure(Error error);
    public static Result Failure(IReadOnlyCollection<Error> errors);
    public static Result<T> Failure<T>(Error error) where T : notnull;
    public static Result<T> Failure<T>(IReadOnlyCollection<Error> errors) where T : notnull;
}
```

### Error Types

**Phase 1 (current):** `IError` is `internal`. The public API uses the concrete `Error` sealed record exclusively. All domain and application code works with `Error` directly.

**Phase 2 (future):** If layered error semantics are needed, `IError` can be made public and extended:

```
IError (made public)
├── IDomainError        (domain-level: invariant violations, business rule failures)
└── IApplicationError   (application-level: not found, unauthorized, validation)
```

| Interface | Layer | Examples | Status |
|-----------|-------|---------|--------|
| `IError` | Domain | Base — code, message | Internal (Phase 1) |
| `IDomainError` | Domain | `OrderErrors.ExceedsLimit`, `CustomerErrors.Inactive` | Future (Phase 2) |
| `IApplicationError` | Application | `NotFoundError`, `UnauthorizedError`, `ValidationError` | Future (Phase 2) |

### Error Structure

Each error carries:

| Field | Purpose |
|-------|---------|
| `Code` | Machine-readable identifier (e.g., `"MyApp.Domain.Orders.Order.ExceedsLimit"`) |
| `Message` | Human-readable description |

> **Note:** `Metadata` (optional key-value pairs) is planned for a future phase. The current `IError` interface has `Code` and `Message` only. Error codes are auto-generated via `Error.Create<T>()` using `CallerMemberName` + full type name.

### Error Declaration and Discovery

**Declaration** — domain objects and handlers implement `IErrorSource` and declare their possible errors:

```
public class Order : AggregateRoot<OrderId>, IErrorSource
{
    public static class Errors
    {
        public static readonly IDomainError ExceedsLimit = ...;
        public static readonly IDomainError InvalidQuantity = ...;
    }
}
```

**Discovery** — at application startup, the system scans all `IErrorSource` implementations and collects every declared error. This produces a complete error catalog that can be:
- Exposed via an API endpoint (for client documentation)
- Used in contract tests (verify all errors are handled)
- Included in OpenAPI documentation

### When to Use Result vs Exceptions

| Scenario | Use |
|----------|-----|
| Business rule violation (expected) | `Result.Failure(error)` |
| Entity not found (expected) | `Result.Failure(NotFoundError)` |
| Validation failure (expected) | `Result.Failure(ValidationError)` |
| Database connection failure (unexpected) | Exception |
| Null reference / programming error | Exception |
| External service timeout (unexpected) | Exception |

**Rule of thumb:** If the caller should handle it as part of normal flow → `Result`. If it's an infrastructure or programming failure → Exception. Exceptions are caught by the global exception handler and mapped to 500 ProblemDetails.

### API Layer Mapping

The API layer maps `Result<T>` errors to HTTP responses:

| Error Type | HTTP Status | Response |
|-----------|-------------|----------|
| `IDomainError` | 422 Unprocessable Entity | ProblemDetails with error code and message |
| `NotFoundError` | 404 Not Found | ProblemDetails |
| `UnauthorizedError` | 403 Forbidden | ProblemDetails |
| `ValidationError` | 400 Bad Request | ProblemDetails with validation details |
| Unhandled exception | 500 Internal Server Error | ProblemDetails (no internal details exposed) |

## Implementation Phasing

This ADR is implemented incrementally:

| Phase | Scope | Status |
|-------|-------|--------|
| **Phase 1** | `Result<T>`, `Result`, implicit conversions, `Error` factory return type change | Planned — [Plan](../../plans/20260402-1808-feat-result-pattern-plan.md) |
| **Phase 2** | `IDomainError` / `IApplicationError` hierarchy | Not started |
| **Phase 3** | `Metadata` on `IError`, monadic methods (`Map`, `Bind`, `Match`) | Not started |
| **Phase 4** | API layer mapping to ProblemDetails | Not started |

## More Information

- [YAF DDD Concepts Brainstorm](../../brainstorms/20260322-2036-yaf-ddd-concepts-brainstorm.md) — result pattern (section 11), error hierarchy (section 12)
- [ADR: Domain Building Blocks](20260324-1032-domain-building-blocks.md) — construction returns Result\<T\>
- [ADR: API Adapter — Controllers](../api/20260324-1325-api-adapter-controllers.md) — ProblemDetails mapping
- [Implementation Plan: Result\<T\> Pattern](../../plans/20260402-1808-feat-result-pattern-plan.md) — Phase 1 detailed plan
