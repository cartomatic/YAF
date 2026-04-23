---
title: "feat: Error static factory methods with automatic code generation"
type: feat
status: completed
date: 2026-03-30
---

# feat: Error Static Factory Methods with Automatic Code Generation

## Overview

Enhance the existing `Error` sealed record with two static factory methods that automatically construct error codes from the declaring type's full name and the calling member name:

1. **`Error.Create<T>(message)`** — generates code from `typeof(T).FullName + "." + memberName` (via `[CallerMemberName]`)
2. **`Error.Unspecified<T>(message)`** — generates code from `typeof(T).FullName + ".Unspecified"`

This eliminates manual error code construction and ensures consistent, namespace-qualified, discoverable error codes across the framework.

## Problem Statement / Motivation

Currently, consumers must manually construct error codes:

```csharp
public static readonly IError EmptyCart = new Error("MyApp.Domain.Orders.Order.EmptyCart", "Cannot create an order with an empty cart.");
```

This is verbose, error-prone (typos, inconsistent naming), and hard to maintain when types are renamed or moved. Auto-generating codes from the declaring type ensures:

- **Consistency** — all codes follow the same `Namespace.Type.Member` pattern
- **Refactor-safety** — renaming a type or member automatically updates the code
- **Discoverability** — codes are predictable and searchable by namespace/type
- **Translation support** — `IErrorSource` catalog extraction can rely on consistent code structure

## Proposed Solution

### `Error.Create<T>()` — Member-Named Error

```csharp
// src/Yaf.Domain/Error.cs (additions to existing sealed record)

/// <summary>
/// Creates an error with an automatically generated code composed of
/// the full name of <typeparamref name="T"/> and the declaring member name.
/// </summary>
/// <typeparam name="T">
/// The type declaring this error. Used to derive the namespace and class portion of the code.
/// </typeparam>
/// <param name="message">Human-readable error message. Must not be null or whitespace.</param>
/// <param name="memberName">
/// Automatically populated by <see cref="CallerMemberNameAttribute"/>.
/// Do not pass explicitly.
/// </param>
/// <returns>An <see cref="IError"/> with code in the format <c>Namespace.Type.MemberName</c>.</returns>
/// <example>
/// <code>
/// // Declared in MyApp.Domain.Orders.Order:
/// public static readonly IError EmptyCart = Error.Create&lt;Order&gt;("Cannot create an order with an empty cart.");
/// // Produces code: "MyApp.Domain.Orders.Order.EmptyCart"
/// </code>
/// </example>
public static IError Create<T>(string message, [CallerMemberName] string memberName = "") =>
    new Error(BuildCode(typeof(T), memberName), message);
```

### `Error.Unspecified<T>()` — Unspecified Error

```csharp
/// <summary>
/// Creates an error with an automatically generated code using "Unspecified" as the member suffix.
/// Useful as a catch-all error for a given type.
/// </summary>
/// <typeparam name="T">
/// The type declaring this error. Used to derive the namespace and class portion of the code.
/// </typeparam>
/// <param name="message">Human-readable error message. Must not be null or whitespace.</param>
/// <returns>An <see cref="IError"/> with code in the format <c>Namespace.Type.Unspecified</c>.</returns>
/// <example>
/// <code>
/// // Declared in MyApp.Domain.Orders.Order:
/// public static readonly IError Unknown = Error.Unspecified&lt;Order&gt;("An unexpected error occurred.");
/// // Produces code: "MyApp.Domain.Orders.Order.Unspecified"
/// </code>
/// </example>
public static IError Unspecified<T>(string message) =>
    new Error(BuildCode(typeof(T), "Unspecified"), message);
```

### `BuildCode()` — Internal Code Builder

```csharp
/// <summary>
/// Builds a dot-separated error code from a type's full name and a member name.
/// </summary>
private static string BuildCode(Type type, string memberName)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(memberName);

    var typeName = type.FullName ?? type.Name;

    // Replace nested type separator (+) with dot for consistency
    typeName = typeName.Replace('+', '.');

    // Strip generic arity suffixes (e.g., `1, `2) for cleaner codes
    typeName = StripGenericArity(typeName);

    return $"{typeName}.{memberName}";
}

private static string StripGenericArity(string typeName)
{
    // Remove backtick + digits (e.g., "Handler`1" → "Handler")
    // and bracket-enclosed type args (e.g., "Handler`1[[...]]" → "Handler")
    var backtickIndex = typeName.IndexOf('`');
    if (backtickIndex >= 0)
    {
        typeName = typeName[..backtickIndex];
    }
    return typeName;
}
```

## Technical Considerations

### Generic Types

`typeof(MyHandler<>).FullName` returns `"MyApp.Handlers.MyHandler`1"`. The backtick and arity suffix are stripped to produce `"MyApp.Handlers.MyHandler"`. Closed generics like `typeof(MyHandler<Order>).FullName` return the full type argument metadata — the backtick stripping handles this cleanly.

### Nested Types

`typeof(Order.LineItem).FullName` returns `"MyApp.Domain.Orders.Order+LineItem"`. The `+` is replaced with `.` to produce `"MyApp.Domain.Orders.Order.LineItem"`.

### Null FullName

`Type.FullName` is null for open generic type parameters (e.g., `typeof(T)` when `T` is unresolved at runtime — not applicable here since `T` is always a concrete type at the call site). As a safety net, falls back to `Type.Name`.

### CallerMemberName Behavior

`[CallerMemberName]` resolves to the member name at compile time:
- For a static field: the field name (e.g., `"EmptyCart"`)
- For a property: the property name
- For a method: the method name
- For a constructor: `.ctor` — unlikely but handled (validates non-whitespace)

### Thread Safety

Both `Create<T>` and `Unspecified<T>` are pure functions returning new `Error` instances. No shared mutable state — inherently thread-safe.

### Existing Constructor Unchanged

The existing `Error(string code, string message)` constructor remains for consumers who want manual control over error codes. The factory methods are additive.

## Acceptance Criteria

- [x] `Error.Create<T>(message)` returns an `IError` with code `Namespace.Type.MemberName`
- [x] `Error.Unspecified<T>(message)` returns an `IError` with code `Namespace.Type.Unspecified`
- [x] Generic types produce clean codes without backtick/arity suffixes
- [x] Nested types produce dot-separated codes (no `+` separator)
- [x] `Create<T>` throws `ArgumentException` for null/empty/whitespace message
- [x] `Unspecified<T>` throws `ArgumentException` for null/empty/whitespace message
- [x] `CallerMemberName` correctly resolves the field/property name at the call site
- [x] Existing `Error(code, message)` constructor still works unchanged
- [x] All public members have XML documentation
- [x] Solution builds with zero warnings
- [x] Tests cover:
  - [x] `Create<T>` with a simple type — correct code format
  - [x] `Create<T>` with a nested type — `+` replaced with `.`
  - [x] `Create<T>` with a generic type — backtick/arity stripped
  - [x] `Unspecified<T>` — code ends with `.Unspecified`
  - [x] `Create<T>` with invalid message — throws `ArgumentException`
  - [x] `Unspecified<T>` with invalid message — throws `ArgumentException`
  - [x] Record equality — two identical `Create<T>` calls produce equal errors
  - [x] `CallerMemberName` resolves correctly for static fields

## Design Decisions

### Why `IError` return type, not `Error`?

The factory methods return `IError` to match the convention established by the brainstorm: error fields are declared as `IError`, allowing future substitution with specialised error types (e.g., `IDomainError`, `IApplicationError`).

### Why strip generic arity?

`Handler`1` is a CLR artefact, not a meaningful identifier. `Handler` is what developers recognise and what appears in translations. The arity suffix adds noise without information.

### Why replace `+` with `.` for nested types?

Dot-separated paths are the universal convention for error codes, configuration keys, and resource identifiers. The `+` separator is a CLR internal detail that would confuse consumers and break code-based lookups.

### Why validate `memberName`?

Although `[CallerMemberName]` always provides a value at compile time, a consumer could explicitly pass an empty string: `Error.Create<Order>("msg", "")`. Validating prevents silent creation of malformed codes.

## Dependencies & Risks

**Dependencies:**
- Existing `Error` sealed record (modified, not replaced)
- `System.Runtime.CompilerServices.CallerMemberNameAttribute` (built-in, no external dependency)

**Risks:**
- **Low:** `CallerMemberName` in a lambda or anonymous method resolves to the enclosing member — documented, not a bug.
- **Low:** `FullName` for compiler-generated types (anonymous, closures) may be messy — these types should not declare errors.

## References & Research

### Internal References
- Existing `Error` record: `src/Yaf.Domain/Error.cs`
- `IError` interface: `src/Yaf.Domain/Interfaces/IError.cs`
- `IErrorSource` marker: `src/Yaf.Domain/Interfaces/IErrorSource.cs`
- Brainstorm: [YAF DDD Concepts](../brainstorms/20260322-2036-yaf-ddd-concepts-brainstorm.md) — section 12 (Error Hierarchy)
- Previous plan: `docs/plans/20260330-1353-feat-error-encryption-base-memento-plan.md`
