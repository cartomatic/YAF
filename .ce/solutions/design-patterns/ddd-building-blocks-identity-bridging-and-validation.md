---
title: "DDD Building Blocks: Identity Bridging, Compiled Factories, and Validation Patterns"
date: 2026-03-26
category: design-patterns
tags: [ddd, strongly-typed-ids, memento-pattern, validation, compiled-expressions, static-abstract, net10]
component: Yaf.Domain
severity: medium
---

# DDD Building Blocks: Identity Bridging, Compiled Factories, and Validation

## Problem Summary

Implementing `TypedId<T>`, `Entity<TId>`, and `AggregateRoot<TId>` for a DDD framework required solving several interconnected design problems: transferring identity values between domain objects and mementos without extra type parameters, avoiding per-call reflection costs, separating validation responsibility from memento orchestration, and eliminating code duplication across the Entity/AggregateRoot inheritance split.

## Key Patterns

### 1. Interface-Based Identity Bridging (No Extra Type Parameters)

**Problem:** Entity memento variants need to read/write identity values between `ITypedId<T>` (domain) and `IHasIdentity<T>` (memento), but adding `T` as a type parameter to Entity bloats the consumer API (`Entity<TId, T, TSelf, TMemento>` — 4 params).

**Solution:** Non-generic base interfaces with runtime type checking via `IdentityType` property.

```csharp
// ITypedId — static abstract for compile-time generic dispatch
public interface ITypedId
{
    static abstract Type IdentityType { get; }
    object BoxedValue { get; }
}

// IHasIdentity — non-generic base with DIM on generic variant
public interface IHasIdentity
{
    Type IdentityType { get; }
    object BoxedId { get; set; }
}

public interface IHasIdentity<T> : IHasIdentity where T : IEquatable<T>
{
    T Id { get; set; }
    Type IHasIdentity.IdentityType => typeof(T);     // DIM
    object IHasIdentity.BoxedId
    {
        get => Id!;
        set => Id = value is T typed ? typed
            : throw new ArgumentException(...);       // Type guard
    }
}
```

**Usage in memento orchestration:**
```csharp
// Type compatibility check — no reflection, static abstract dispatch
if (memento is IHasIdentity hasIdentity && hasIdentity.IdentityType == TId.IdentityType)
{
    hasIdentity.BoxedId = id.BoxedValue;  // Snapshot
    // or
    id = _idFactory(hasIdentity.BoxedId); // Restore
}
```

**Key insight:** `static abstract Type IdentityType` on `ITypedId` enables `TId.IdentityType` in generic context without reflection. The non-generic `IHasIdentity` base allows `memento is IHasIdentity` pattern matching. Default interface methods (DIM) on `IHasIdentity<T>` eliminate boilerplate for consumers — they only implement `T Id { get; set; }`.

### 2. Compiled Expression Delegate Factory

**Problem:** `Activator.CreateInstance(typeof(TId), boxedValue)` on every Restore/Hydrate has ~5-10x overhead vs direct constructor call due to runtime constructor lookup + `object[]` allocation.

**Solution:** Compile an expression tree once per generic instantiation, cache as static field.

```csharp
private static readonly Func<object, TId>? _idFactory = BuildIdFactory();

private static Func<object, TId>? BuildIdFactory()
{
    var backingType = TId.IdentityType;
    var constructor = typeof(TId).GetConstructor([backingType]);
    if (constructor is null) return null;

    var param = Expression.Parameter(typeof(object), "value");
    var body = Expression.New(constructor, Expression.Convert(param, backingType));
    return Expression.Lambda<Func<object, TId>>(body, param).Compile();
}
```

**Fail-fast on missing constructor:** If the memento has `IHasIdentity` with matching types but `_idFactory` is null, throw `InvalidOperationException` with a diagnostic message including the required record syntax. Never silently return null.

### 3. IValidatable Trio (GetValidationErrors / IsValid / ThrowIfInvalid)

**Problem:** Validation logic was coupled to memento orchestration (`MementoHelper.ThrowIfInvalid` decided whether entity state was valid). Also, `ThrowIfInvalid` was calling `GetValidationErrors()` twice on the failure path.

**Solution:** Interface + extensions separating concerns.

```csharp
public interface IValidatable
{
    IReadOnlyCollection<IError> GetValidationErrors();
}

public static class ValidatableExtensions
{
    public static bool IsValid(this IValidatable v) =>
        v.GetValidationErrors().Count == 0;

    public static void ThrowIfInvalid(this IValidatable v)
    {
        var errors = v.GetValidationErrors();  // Collect ONCE
        if (errors.Count > 0)
            throw new ValidationException(v.GetType(), errors);
    }
}
```

**Key insight:** Naming matters. `GetValidationErrors()` makes the return type obvious. `IsValid()` is the quick boolean. `ThrowIfInvalid()` is the guard. Collect errors in a local variable — never call the method twice.

### 4. Static Abstract on Abstract Classes

**Confirmed behavior:** A concrete static method on an abstract class DOES satisfy a `static abstract` interface member. This enables `TId.IdentityType` via generic dispatch:

```csharp
public abstract record TypedId<T> : ITypedId<T>
{
    static Type ITypedId.IdentityType => typeof(T);  // Satisfies static abstract
}
```

This was initially assumed impossible (see [CRTP + Memento solution](../logic-errors/csharp-static-abstract-crtp-memento-pattern.md)). The same pattern is used for `IMemento<TSelf, TMemento>.Restore` on `ValueObject<TSelf, TMemento>`, `Entity<TId, TSelf, TMemento>`, and `AggregateRoot<TId, TSelf, TMemento>`.

## Prevention Strategies

### Double Method Calls on Expensive Operations
- Capture results in a local variable before branching. Never call a consumer-implemented method twice in the same code path.
- Review extension methods that compose other extensions (e.g., `ThrowIfInvalid` calling `IsValid`).

### Silent Reflection Failures
- Throw with actionable error messages at the detection point, not where symptoms manifest.
- Include remediation guidance in the message (e.g., "Use positional record syntax: `record OrderId(Guid Value)`").

### Hydrate Mutate-Before-Validate
- Document that callers must discard entity instances on `ValidationException` — the entity is partially mutated.
- Consider validate-before-mutate patterns in future iterations if this causes issues.

### Code Duplication Across Inheritance Hierarchies
- When C# single inheritance prevents sharing (Entity vs AggregateRoot memento variants), extract to internal static helpers with typed generic constraints.
- The `MementoHelper<TId, TSelf, TMemento>` pattern eliminates ~70 lines of duplication.

## Related Files

- `src/Yaf.Domain/Interfaces/ITypedId.cs` — static abstract IdentityType + BoxedValue
- `src/Yaf.Domain/Interfaces/IHasIdentity.cs` — non-generic base + generic with DIM
- `src/Yaf.Domain/Interfaces/IValidatable.cs` — validation interface
- `src/Yaf.Domain/Extensions/ValidatableExtensions.cs` — IsValid + ThrowIfInvalid
- `src/Yaf.Domain/Helpers/MementoHelper.cs` — compiled factory + identity bridging
- `src/Yaf.Domain/Entity{TId,TSelf,TMemento}.cs` — memento entity base
- `src/Yaf.Domain/AggregateRoot{TId,TSelf,TMemento}.cs` — memento aggregate base
- `src/Yaf.Domain/TypedId.cs` — abstract record with explicit constructor
- `docs/solutions/logic-errors/csharp-static-abstract-crtp-memento-pattern.md` — prior solution on CRTP + static abstract
- `docs/adr/domain/20260324-1032-domain-building-blocks.md` — type catalog and design decisions
- `docs/adr/domain/20260324-1104-state-management-memento-pattern.md` — memento contract
