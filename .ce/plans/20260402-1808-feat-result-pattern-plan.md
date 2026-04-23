---
title: "feat: Result<T> pattern with IError integration"
type: feat
status: completed
date: 2026-04-02
---

# ✨ feat: Result\<T\> Pattern with IError Integration

## Overview

Add `Result<T>` (generic) and `Result` (non-generic) readonly structs to `Yaf.Domain` that wrap operation outcomes — success with a value or failure with one or more `IError` instances. This is the foundation for explicit error handling throughout the framework, replacing exception-based control flow for business logic failures.

## Problem Statement / Motivation

Currently, YAF handles business logic failures in two ways:
- **Validation errors** via `IValidatable.GetValidationErrors()` → `IReadOnlyCollection<IError>` (checked manually)
- **Validation exceptions** via `ValidatableExtensions.ThrowIfInvalid()` → throws `ValidationException`

Neither approach forces callers to handle failures. Exceptions are invisible in method signatures, and `IValidatable` only covers validation — not general domain operation outcomes like "order exceeds limit" or "customer is inactive."

`Result<T>` makes failure a first-class part of the return type. Callers **must** handle it — it's not hidden in a catch block or silently ignored.

**Drivers** (from [ADR: Result and Error Pattern](../adr/domain/20260324-1140-result-and-error-pattern.md)):
1. No exceptions for business logic — exceptions are for infrastructure failures
2. Explicit error paths — return types communicate that an operation can fail
3. Error discoverability — errors declared via `IErrorSource` compose into a catalog
4. Zero dependencies — lives entirely in `Yaf.Domain`

## Proposed Solution

### Type Design

Two readonly structs in `Yaf.Domain`:

- **`Result<T>`** — wraps `T` on success or `IError[]` on failure
- **`Result`** — non-generic, for void-equivalent operations (no `.Value`)

Both use the concrete `Error` record in their public API. `IError` becomes `internal` — only visible within `Yaf.Domain`. Above the domain layer, consumers work exclusively with `Error`.

### `Result<T>` API Surface

```csharp
// Yaf.Domain/Result{T}.cs
public readonly struct Result<T> : IEquatable<Result<T>>
    where T : notnull
{
    // State
    public bool IsSuccess { get; }
    public bool IsFailure { get; }

    // Value access (throws InvalidOperationException on failure/default)
    public T Value { get; }

    // Error access (throws InvalidOperationException on success)
    // On default(Result<T>): returns sentinel error
    public Error Error { get; }                     // First error (convenience)
    public IReadOnlyList<Error> Errors { get; }     // All errors

    // Implicit conversions
    public static implicit operator Result<T>(T value);        // T → success
    public static implicit operator Result<T>(Error error);    // Error → single-error failure

    // Equality operators (required — not auto-generated for non-record structs)
    public static bool operator ==(Result<T> left, Result<T> right);
    public static bool operator !=(Result<T> left, Result<T> right);

    // Equality: success compares by EqualityComparer<T>.Default
    // Equality: failure compares errors via object.Equals (record Error has value equality)
    // ToString: "Success(value)" or "Failure(code1, code2, ...)"
    // ToString (default): "Result<T>(Uninitialized)"
}
```

### `Result` (Non-Generic) API Surface

```csharp
// Yaf.Domain/Result.cs
public readonly struct Result : IEquatable<Result>
{
    // State
    public bool IsSuccess { get; }
    public bool IsFailure { get; }

    // Error access (throws InvalidOperationException on success)
    // On default(Result): returns sentinel error
    public Error Error { get; }
    public IReadOnlyList<Error> Errors { get; }

    // No .Value property

    // Implicit conversion
    public static implicit operator Result(Error error);    // Error → single-error failure

    // Equality operators
    public static bool operator ==(Result left, Result right);
    public static bool operator !=(Result left, Result right);

    // Static factories (for both Result and Result<T>)
    public static Result Success();
    public static Result<T> Success<T>(T value) where T : notnull;
    public static Result Failure(Error error);
    public static Result Failure(IReadOnlyCollection<Error> errors);
    public static Result<T> Failure<T>(Error error) where T : notnull;
    public static Result<T> Failure<T>(IReadOnlyCollection<Error> errors) where T : notnull;
}
```

### Key Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| **`default(Result<T>)` behavior** | Failure with sentinel error (`IsFailure=true`, `.Error`/`.Errors` return built-in uninitialized error) | Safest — prevents treating uninitialized as success. Sentinel avoids trap where `IsFailure` is true but errors throw |
| **`.Error` with multiple errors** | Returns first error | Simple, always works. `.Errors` gives full list |
| **`.Errors` with single error** | Single-element collection | Consistent — `.Errors` always works on failure |
| **Implicit conversion `T → Result<T>`** | Yes | Enables `return order;` for ergonomic success returns |
| **Implicit conversion `Error → Result<T>`** | Yes | Enables `return MyErrors.NotFound;` for ergonomic failure returns |
| **`IError` visibility** | `internal` | Only `Error` (concrete) is visible above the domain layer. Simplifies public API. Can be made public again in Phase 2 if error hierarchy is needed |
| **`Error.Create<T>()` return type change** | Returns `Error` instead of `IError` | Enables implicit conversion usage. `Error` is sealed — no subclassing concern |
| **Multi-error factory parameter** | `IReadOnlyCollection<Error>` | Matches updated `IValidatable.GetValidationErrors()` — no `.ToList()` needed |
| **Internal error storage** | `Error[]` array, exposed as `IReadOnlyList<Error>` | Immutable, no external mutation, supports indexing |
| **Null value on success** | `ArgumentNullException` for reference types | Success with null defeats the purpose of explicit modeling |
| **Empty error collection** | `ArgumentException` on `Failure()` | Failure with zero errors is semantically invalid |
| **Null elements in error collection** | `ArgumentException` if any null found | Every error must be a valid `Error` instance |
| **`where T : notnull` constraint** | Yes | Prevents `Result<string?>` at compile time; reinforces non-null success semantics |
| **`operator ==` / `!=`** | Explicitly declared | Required for non-record structs — not compiler-generated |
| **Failure equality** | Record value equality on each `Error` | `Error` is a sealed record — value equality is built-in. No custom `IError` implementations to worry about |
| **Static factories location** | On non-generic `Result` class | `Result.Success(value)`, `Result.Failure<T>(error)` — clean API |

### Interaction with Existing Types

These existing types require changes to align with `IError` becoming internal:

- **`IError` interface**: Changed from `public` to `internal`. Only visible within `Yaf.Domain` assembly
- **`IValidatable.GetValidationErrors()`**: Return type changes from `IReadOnlyCollection<IError>` to `IReadOnlyCollection<Error>`. Errors can be passed directly to `Result.Failure<T>(errors)`
- **`ValidationException.Errors`**: Changes from `IReadOnlyCollection<IError>` to `IReadOnlyCollection<Error>`. Remains for memento hydration (infrastructure concern)
- **`ValidatableExtensions`**: Updated to use `Error` instead of `IError` in signatures
- **`IErrorSource`**: Unchanged — marker interface with no `IError` references in its signature. Consumers declare `static readonly Error` fields
- **`Error` record**: Factory methods `Create<T>()` and `Unspecified<T>()` change return type from `IError` to `Error`. Constructor and internal implementation unchanged

### What This Does NOT Include

- No `IDomainError` / `IApplicationError` hierarchy (future plan — `IError` can be made public again when needed)
- No `Metadata` property on errors (future plan)
- No monadic methods (`Map`, `Bind`, `Match`) — deferred to a later phase
- No `ToResult<T>()` extension on `IValidatable` — can be added separately

## Technical Considerations

### Struct Semantics
- `readonly struct` ensures no defensive copies and communicates immutability
- `where T : notnull` constraint prevents nullable type parameters at compile time
- Internal `Error[]` array is never exposed directly — the array itself implements `IReadOnlyList<T>`, so cast directly (no `Array.AsReadOnly()` wrapper needed)
- `default(Result<T>)` is handled by checking if the internal array is null — null array = uninitialized = returns a lazily-created sentinel error (`"Yaf.Domain.Result.Uninitialized"`, `"Result was not properly initialized"`)

### Thread Safety
- Readonly struct is inherently thread-safe (copied on assignment)
- Internal `Error[]` is set once at construction and never mutated
- No mutable state anywhere

### Performance
- Zero allocations for success path (struct + stores `T` directly)
- Single array allocation for failure path (the `Error[]`)
- Single-error failure: allocates a 1-element array
- No boxing if used directly (not via interface)

### Equality
- `IEquatable<Result<T>>` implementation required for correct struct equality
- `operator ==` / `!=` explicitly declared (not auto-generated for non-record structs)
- Override `Equals(object?)` and `GetHashCode()` accordingly
- Success: delegates to `EqualityComparer<T>.Default.Equals`
- Failure: sequence equality on the errors array using `Error` record equality (built-in value equality — same Code + Message = equal)
- Default (uninitialized): two defaults are equal to each other

## Acceptance Criteria

### Functional Requirements

- [ ] `Result<T>` readonly struct with `IsSuccess`, `IsFailure`, `Value`, `Error`, `Errors` properties
- [ ] `Result` non-generic readonly struct with `IsSuccess`, `IsFailure`, `Error`, `Errors` properties
- [ ] Static factories: `Result.Success()`, `Result.Success<T>(value)`, `Result.Failure(error)`, `Result.Failure(errors)`, `Result.Failure<T>(error)`, `Result.Failure<T>(errors)`
- [ ] `IError` interface changed to `internal` visibility
- [ ] `IValidatable.GetValidationErrors()` returns `IReadOnlyCollection<Error>` (changed from `IError`)
- [ ] `ValidationException.Errors` uses `IReadOnlyCollection<Error>` (changed from `IError`)
- [ ] `Failure` factories accept `IReadOnlyCollection<Error>` for multi-error overloads
- [ ] `.Error` returns first error on failure (single or multiple)
- [ ] `.Errors` returns full collection on any failure
- [ ] `.Value` throws `InvalidOperationException` on failure
- [ ] `.Error` / `.Errors` throw `InvalidOperationException` on success
- [ ] `default(Result<T>)` behaves as failure (`IsFailure=true`, `.Error`/`.Errors` return sentinel uninitialized error, `.Value` throws)
- [ ] `default(Result)` behaves as failure (same sentinel pattern)
- [ ] Implicit conversion from `T` to `Result<T>` (success)
- [ ] Implicit conversion from `Error` to `Result<T>` (single-error failure)
- [ ] Implicit conversion from `Error` to `Result` (single-error failure)
- [ ] `Error.Create<T>()` and `Error.Unspecified<T>()` return `Error` instead of `IError`
- [ ] `where T : notnull` constraint on `Result<T>`
- [ ] Null value rejected on `Result.Success<T>()` — throws `ArgumentNullException`
- [ ] Empty error collection rejected on `Result.Failure<T>()` — throws `ArgumentException`
- [ ] Null error rejected on `Result.Failure<T>()` — throws `ArgumentNullException`
- [ ] Null error collection rejected on `Result.Failure<T>()` — throws `ArgumentNullException`
- [ ] Null elements in error collection rejected — throws `ArgumentException`
- [ ] Implicit conversions apply the same guard clauses as explicit factories
- [ ] Existing tests still pass after `IError` → `internal` and signature changes
- [ ] `IEquatable<Result<T>>` and `IEquatable<Result>` implemented
- [ ] `operator ==` / `!=` explicitly declared on both structs
- [ ] `Equals(object?)` and `GetHashCode()` overridden
- [ ] `ToString()` overridden: `"Success(value)"` / `"Failure(code1, code2, ...)"` / `"Result<T>(Uninitialized)"`

### Non-Functional Requirements

- [ ] All public API has XML doc comments (CS1591 enforced)
- [ ] Zero external dependencies
- [ ] File naming follows project conventions (`Result{T}.cs`, `Result.cs`)
- [ ] Namespace: `Yaf.Domain`
- [ ] Test coverage for all public members and edge cases

### Testing Requirements

Test file: `tests/Yaf.Domain.Tests/ResultTests.cs`

Test classes following `{Type}{Concern}Tests` convention:

**`ResultSuccessTests`**
- [ ] Success with value — `IsSuccess=true`, `IsFailure=false`, `Value` returns value
- [ ] Success with value type (int, struct) — works correctly
- [ ] Success rejects null reference type — throws `ArgumentNullException`
- [ ] Non-generic `Result.Success()` — `IsSuccess=true`, `IsFailure=false`

**`ResultFailureTests`**
- [ ] Single error failure — `IsFailure=true`, `IsSuccess=false`
- [ ] Multi-error failure — `IsFailure=true`, errors accessible
- [ ] `.Error` on single-error failure — returns the error
- [ ] `.Error` on multi-error failure — returns first error
- [ ] `.Errors` on single-error failure — single-element collection
- [ ] `.Errors` on multi-error failure — all errors in order
- [ ] Failure rejects null error — throws `ArgumentNullException`
- [ ] Failure rejects empty collection — throws `ArgumentException`
- [ ] Failure rejects null collection — throws `ArgumentNullException`
- [ ] Failure rejects collection with null elements — throws `ArgumentException`
- [ ] Non-generic `Result.Failure(error)` — works correctly

**`ResultAccessTests`**
- [ ] `.Value` on failure — throws `InvalidOperationException`
- [ ] `.Error` on success — throws `InvalidOperationException`
- [ ] `.Errors` on success — throws `InvalidOperationException`

**`ResultDefaultTests`**
- [ ] `default(Result<T>)` — `IsFailure=true`, `IsSuccess=false`
- [ ] `default(Result<T>)` — `.Value` throws `InvalidOperationException`
- [ ] `default(Result<T>)` — `.Error` returns sentinel uninitialized error
- [ ] `default(Result<T>)` — `.Errors` returns single-element collection with sentinel error
- [ ] `default(Result)` — `IsFailure=true`, `.Error`/`.Errors` return sentinel error

**`ResultImplicitConversionTests`**
- [ ] `T` implicitly converts to `Result<T>` success
- [ ] Value type implicitly converts to `Result<T>` success
- [ ] `Error` implicitly converts to `Result<T>` single-error failure
- [ ] `Error` implicitly converts to `Result` single-error failure
- [ ] Null `T` via implicit conversion — throws `ArgumentNullException` (same guard as `Success<T>`)
- [ ] Null `Error` via implicit conversion — throws `ArgumentNullException` (same guard as `Failure<T>`)

**`ResultEqualityTests`**
- [ ] Two success results with same value — equal
- [ ] Two success results with different values — not equal
- [ ] Two failure results with same errors — equal
- [ ] Two failure results with different errors — not equal
- [ ] Success and failure — not equal
- [ ] `default` results — equal to each other
- [ ] `GetHashCode` consistent with equality
- [ ] `operator ==` returns true for equal results
- [ ] `operator !=` returns true for different results
- [ ] `Equals(object)` works correctly

**`ResultToStringTests`**
- [ ] Generic success — `"Success(value)"`
- [ ] Non-generic success — `"Success"`
- [ ] Failure single error — `"Failure(CODE)"`
- [ ] Failure multiple errors — `"Failure(CODE1, CODE2)"`
- [ ] Generic default — `"Result<T>(Uninitialized)"`
- [ ] Non-generic default — `"Result(Uninitialized)"`

**`ResultValidatableIntegrationTests`**
- [ ] `IValidatable.GetValidationErrors()` result passes directly to `Result.Failure<T>()`

## File Structure

```
src/Yaf.Domain/
├── Result{T}.cs              # Result<T> readonly struct
├── Result.cs                 # Result non-generic struct + static factories
tests/Yaf.Domain.Tests/
├── ResultTests.cs            # All Result test classes
```

## Success Metrics

- All acceptance criteria pass
- Zero warnings (TreatWarningsAsErrors is enabled)
- Tests cover success, failure, default, edge cases, and IValidatable integration

## Dependencies & Risks

**Dependencies:** None — uses only existing `IError` interface and `Error` record.

**Pre-implementation:** Update the ADR status from "under review" to "accepted" before starting implementation.

**Risks:**
- `default(Result<T>)` as failure means uninitialized struct fields silently become failures. Mitigated by sentinel error that clearly communicates the issue. XML docs should call this out.
- Implicit `T → Result<T>` conversion could cause confusion in overload resolution if methods accept both `T` and `Result<T>`. Unlikely in practice since Result is a return type, not a parameter type.
- `where T : notnull` prevents `Result<int?>` — acceptable since a nullable success value is semantically questionable. If needed, consumers can wrap in a value object.

## References & Research

### Internal References
- [ADR: Result and Error Pattern](../adr/domain/20260324-1140-result-and-error-pattern.md) — architectural decision (accept as part of this work)
- [ADR: Validation Strategy](../adr/domain/20260324-1141-validation-strategy.md) — three-level validation using Result
- [ADR: Domain Building Blocks](../adr/domain/20260324-1032-domain-building-blocks.md) — construction returns Result\<T\>
- [Brainstorm: DDD Concepts](../brainstorms/20260322-2036-yaf-ddd-concepts-brainstorm.md) — sections 11-12
- [Solution: Auto-generated Error Codes](../solutions/design-patterns/auto-generated-error-codes-with-callermembername.md)
- Existing implementation: `src/Yaf.Domain/Error.cs`, `src/Yaf.Domain/Interfaces/IError.cs`

### Existing Patterns to Follow
- `Error.cs` — sealed record pattern, guard clauses, XML docs, factory methods
- `TypedId.cs` — readonly struct with equality, `IEquatable<T>`
- `ErrorTests.cs` — test class naming: `{Type}{Concern}Tests`

## Audit Trail

- **Spec audit:** [20260402-1808-feat-result-pattern-plan-spec-audit.md](../verification/20260402-1808-feat-result-pattern-plan-spec-audit.md) — pre-implementation audit, mostly compliant. All high-severity findings resolved and incorporated into this plan.
