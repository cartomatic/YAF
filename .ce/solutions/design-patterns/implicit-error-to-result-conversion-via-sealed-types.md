---
title: Implicit Error-to-Result conversion via sealed concrete types
category: design-patterns
tags: [result-pattern, implicit-operators, sealed-types, csharp-language-constraints, IError, Error]
date: 2026-04-02
applies_to: [Yaf.Domain]
related_prs: ["#22"]
---

# Implicit Error-to-Result Conversion via Sealed Concrete Types

## Problem

Domain methods returning `Result<T>` should support ergonomic failure returns:

```csharp
public Result<Order> PlaceOrder()
{
    if (cart.IsEmpty)
        return EmptyCart;  // implicit Error → Result<Order>
}
```

C# forbids user-defined implicit conversions from interface types ([CS0552](https://learn.microsoft.com/en-us/dotnet/csharp/misc/cs0552)). Since errors were originally typed as `IError`, defining `implicit operator Result<T>(IError error)` would not compile.

## Investigation

Three approaches were evaluated:

| Approach | Outcome |
|----------|---------|
| `Result<T, TError> where TError : IError` (union type) | Rejected — verbose signatures, cross-layer error propagation becomes painful |
| `implicit operator Result<T>(IError error)` | Rejected — C# language constraint, will not compile |
| Make `IError` internal, use concrete `Error` type | **Selected** — works because `Error` is a concrete sealed record |

## Solution

Three coordinated changes enable implicit error-to-result conversion:

### 1. Make `IError` internal

```csharp
// src/Yaf.Domain/Interfaces/IError.cs
internal interface IError
{
    string Code { get; }
    string Message { get; }
}
```

Above the domain layer, only the concrete `Error` type is visible. This simplifies the public API surface.

### 2. Change factory return types from `IError` to `Error`

```csharp
// src/Yaf.Domain/Error.cs
public static Error Create<T>(string message, [CallerMemberName] string memberName = "") =>
    new Error(BuildCode(typeof(T), memberName), message);

public static Error Unspecified<T>(string message) =>
    new Error(BuildCode(typeof(T), nameof(Unspecified)), message);
```

Previously these returned `IError`. Since `Error` is a sealed record, there is no subclassing concern.

### 3. Define implicit operators from `Error` (concrete) to `Result<T>`

```csharp
// src/Yaf.Domain/Result{T}.cs
public static implicit operator Result<T>(Error error)
{
    ArgumentNullException.ThrowIfNull(error);
    return new Result<T>([error]);
}

// src/Yaf.Domain/Result.cs
public static implicit operator Result(Error error)
{
    ArgumentNullException.ThrowIfNull(error);
    return new Result(false, [error]);
}
```

C# allows implicit conversions from concrete types, so this compiles and works.

## Usage

```csharp
public class Order : AggregateRoot<OrderId>, IErrorSource
{
    public static readonly Error EmptyCart = Error.Create<Order>("Cart cannot be empty.");

    public Result<Order> PlaceOrder()
    {
        if (Items.Count == 0)
            return EmptyCart;       // implicit Error → Result<Order>

        // ... business logic ...
        return this;                // implicit T → Result<T>
    }
}
```

## Trade-offs

**Positive:**
- Ergonomic `return error;` syntax in domain methods
- Simplified public API — consumers only see `Error`, not `IError`
- `Error` is sealed record with built-in value equality — no footguns

**Negative:**
- Variables typed as `IError` (from within the assembly) cannot use implicit conversion — must use `Result.Failure<T>(error)`
- `IError` can be made public again in a future phase if the error hierarchy (`IDomainError` / `IApplicationError`) is needed
- Binary-breaking change for the factory return types (acceptable pre-1.0)

## Gotcha: Default Struct Sentinel

A related pattern: `default(Result<T>)` is always possible for structs. The sentinel error uses the same auto-generated code pattern:

```csharp
// Defined once in Result (non-generic), shared by Result<T>
internal static readonly Error Uninitialized = Error.Create<Result>("Result was not properly initialized.");
```

`CallerMemberName` captures `Uninitialized`, producing code `Yaf.Domain.Result.Uninitialized`.

## Related

- [Auto-generated error codes with CallerMemberName](auto-generated-error-codes-with-callermembername.md)
- [ADR: Result and Error Pattern](../../adr/domain/20260324-1140-result-and-error-pattern.md)
- [Plan: Result\<T\> Pattern](../../plans/20260402-1808-feat-result-pattern-plan.md)
