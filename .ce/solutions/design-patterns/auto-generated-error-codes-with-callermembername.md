---
title: Auto-generated error codes with CallerMemberName and typeof(T).FullName
category: design-patterns
tags: [error-handling, CallerMemberName, factory-methods, type-name-normalization, IError, IErrorSource]
date: 2026-03-31
applies_to: [Yaf.Domain]
related_prs: ["#20"]
---

# Auto-Generated Error Codes with CallerMemberName

## Problem

Domain types need to declare error constants with unique, predictable codes. Manual string construction is verbose, error-prone, and fragile during refactoring:

```csharp
// Manual — typo-prone, not refactor-safe
public static readonly IError EmptyCart = new Error("MyApp.Domain.Orders.Order.EmptyCart", "Cart is empty.");
```

## Solution

Use `[CallerMemberName]` to capture the declaring member name at compile time, and `typeof(T).FullName` to derive the namespace + class prefix. The error code is assembled automatically:

```csharp
public static IError Create<T>(string message, [CallerMemberName] string memberName = "") =>
    new Error(BuildCode(typeof(T), memberName), message);
```

**Usage:**

```csharp
public class Order : AggregateRoot<OrderId, Order, OrderMemento>, IErrorSource
{
    // Code = "MyApp.Domain.Orders.Order.EmptyCart"
    public static readonly IError EmptyCart = Error.Create<Order>("Cannot create an order with an empty cart.");

    // Code = "MyApp.Domain.Orders.Order.Unspecified"
    public static readonly IError Unknown = Error.Unspecified<Order>("An unexpected error occurred.");
}
```

## Type Name Normalization

`Type.FullName` produces CLR-internal formats that need cleaning:

| Scenario | Raw `FullName` | Normalized |
|----------|---------------|------------|
| Simple type | `MyApp.Domain.Order` | `MyApp.Domain.Order` (unchanged) |
| Nested type | `MyApp.Domain.Order+LineItem` | `MyApp.Domain.Order.LineItem` |
| Generic (1 param) | `` MyApp.Handlers.Handler`1[[...]] `` | `MyApp.Handlers.Handler` |
| Generic (2 params) | `` MyApp.Collections.Pair`2[[...]] `` | `MyApp.Collections.Pair` |
| Null FullName | `null` (open generic params) | Falls back to `Type.Name` |

Implementation:

```csharp
private static string BuildCode(Type type, string memberName)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(memberName);
    var typeName = type.FullName ?? type.Name;
    typeName = typeName.Replace('+', '.');           // Nested type separator
    var backtickIndex = typeName.IndexOf('`');        // Generic arity suffix
    if (backtickIndex >= 0)
        typeName = typeName[..backtickIndex];
    return $"{typeName}.{memberName}";
}
```

## Key Design Decisions

### Why `[CallerMemberName]` and not reflection?

`CallerMemberName` resolves at compile time — zero runtime cost. Reflection would require scanning static fields, which is slower and less predictable.

### Why `IError` return type, not `Error`?

Error fields are declared as `IError` (the interface), allowing future substitution with specialised error types (`IDomainError`, `IApplicationError`).

### Why `nameof(Unspecified)` in `Error.Unspecified<T>`?

Refactor-safe: if the method is ever renamed, the suffix updates automatically.

### Why strip generic arity?

`` Handler`1 `` is a CLR artefact. `Handler` is what developers recognise in translations and API docs. The arity suffix adds noise without information.

## Gotchas

1. **CallerMemberName from a method body** captures the method name, not a field name. Intended use is `static readonly` field initializers. Calling from a method produces a code with the method name — usually unintended.

2. **Explicit memberName override** is possible (`Error.Create<T>("msg", "CustomName")`). The parameter is validated — empty/whitespace throws `ArgumentException`.

3. **Namespace exposure** — error codes contain full type names. If codes reach API responses, they reveal internal namespace structure. The API layer should remap codes before external serialization if this is a concern.

4. **Namespace rename = code change** — refactoring a namespace silently changes all error codes. This is refactor-safe for internal use but can be a breaking change if codes are part of an external API contract.

## Prevention / Best Practices

- Always declare errors as `static readonly` fields on the type they belong to
- Always use `Error.Create<T>` or `Error.Unspecified<T>` — avoid manual code strings
- Implement `IErrorSource` on types declaring errors, enabling catalog discovery
- Plan an API-layer code remapping strategy before exposing codes externally

## Cross-References

- `src/Yaf.Domain/Error.cs` — implementation
- `src/Yaf.Domain/Interfaces/IError.cs` — error interface
- `src/Yaf.Domain/Interfaces/IErrorSource.cs` — catalog discovery marker
- [Brainstorm: Error Hierarchy](../../brainstorms/20260322-2036-yaf-ddd-concepts-brainstorm.md) — section 12
- [Plan: Error factory methods](../../plans/20260330-1508-feat-error-static-factory-methods-plan.md)
