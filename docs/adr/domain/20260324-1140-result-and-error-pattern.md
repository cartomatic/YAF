# Result and Error Pattern

- **Timestamp:** 2026-03-24 11:40
- **Status:** under review
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
public readonly struct Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure { get; }
    public T Value { get; }           // throws if IsFailure
    public IError Error { get; }      // throws if IsSuccess
}
```

**Usage in domain:**
```
public Result<Order> AddItem(Product product, int quantity)
{
    if (quantity <= 0)
        return Result.Failure<Order>(OrderErrors.InvalidQuantity);

    // ... business logic ...
    return Result.Success(this);
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

### Error Hierarchy

```
IError
├── IDomainError        (domain-level: invariant violations, business rule failures)
└── IApplicationError   (application-level: not found, unauthorized, validation)
```

| Interface | Layer | Examples |
|-----------|-------|---------|
| `IError` | Domain | Base — code, message, metadata |
| `IDomainError` | Domain | `OrderErrors.ExceedsLimit`, `CustomerErrors.Inactive` |
| `IApplicationError` | Application | `NotFoundError`, `UnauthorizedError`, `ValidationError` |

### Error Structure

Each error carries:

| Field | Purpose |
|-------|---------|
| `Code` | Machine-readable identifier (e.g., `"ORDER_EXCEEDS_LIMIT"`) |
| `Message` | Human-readable description |
| `Metadata` | Optional key-value pairs for additional context |

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

## More Information

- [YAF DDD Concepts Brainstorm](../../brainstorms/20260322-2036-yaf-ddd-concepts-brainstorm.md) — result pattern (section 11), error hierarchy (section 12)
- [ADR: Domain Building Blocks](20260324-1032-domain-building-blocks.md) — construction returns Result\<T\>
- [ADR: API Adapter — Controllers](../api/20260324-1325-api-adapter-controllers.md) — ProblemDetails mapping
