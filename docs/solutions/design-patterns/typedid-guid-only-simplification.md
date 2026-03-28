---
title: "TypedId Guid-Only Simplification: Removing Speculative Genericity"
date: 2026-03-28
severity: medium
tags:
  - speculative-genericity
  - over-engineering
  - DDD
  - TypedId
  - memento-pattern
  - simplification
  - YAGNI
  - identity
affected_files:
  - src/Yaf.Domain/TypedId.cs
  - src/Yaf.Domain/Interfaces/ITypedId.cs
  - src/Yaf.Domain/Interfaces/IHasIdentity.cs
  - src/Yaf.Domain/Interfaces/IHasAccountability.cs
  - src/Yaf.Domain/Interfaces/IHasTenantId.cs
  - src/Yaf.Domain/Interfaces/IHasSoftDelete.cs
  - src/Yaf.Domain/Helpers/TypedIdBridge.cs
  - src/Yaf.Domain/Helpers/MementoHelper.cs
  - src/Yaf.Domain/Helpers/BoxingHelper.cs
  - src/Yaf.Domain/Entity{TId,TSelf,TMemento}.cs
  - src/Yaf.Domain/AggregateRoot{TId,TSelf,TMemento}.cs
---

## Problem

`TypedId<T> where T : IEquatable<T>` was designed to support arbitrary backing types (Guid, int, string, long). In practice, every concrete ID in the framework and all tests used `Guid`. The speculative genericity created cascading complexity through the entire domain layer:

- **Two-tier memento interface hierarchies** — a non-generic base with `Type` properties and boxed `object?` accessors, plus a generic variant with typed properties and DIM (Default Interface Method) implementations bridging between them.
- **BoxingHelper** — a standalone class solely for unboxing unknown backing types from `object?`.
- **Runtime type discovery** — `TypedIdBridge` used reflection to discover the backing type, find constructors, and build compiled expression-tree factories.
- **IdentityType compatibility checks** — `MementoHelper` runtime-checked that the memento's `IdentityType` matched the entity's typed ID backing type before bridging.

The generic `T` parameter delivered zero value while forcing every layer that touched identity to carry and propagate it.

## Solution

### What Changed

**ITypedId — from two-tier generic to single interface:**

```csharp
// Before
public interface ITypedId {
    static abstract Type IdentityType { get; }
    object BoxedValue { get; }
}
public interface ITypedId<out T> : ITypedId where T : IEquatable<T> {
    T Value { get; }
}

// After
public interface ITypedId {
    Guid Value { get; }
}
```

**TypedId — from generic to non-generic:**

```csharp
// Before
public abstract record TypedId<T> : ITypedId<T> where T : IEquatable<T> { ... }

// After
public abstract record TypedId : ITypedId { ... }
```

**Memento interfaces — collapsed from two-tier to single-tier** (IHasAccountability as example):

```csharp
// Before: non-generic base + generic with DIM boxing
public interface IHasAccountability {
    Type ActorIdType { get; }
    object? BoxedCreatedBy { get; set; }
    object? BoxedModifiedBy { get; set; }
}
public interface IHasAccountability<T> : IHasAccountability where T : struct, IEquatable<T> {
    T? CreatedBy { get; set; }
    T? ModifiedBy { get; set; }
    // DIM implementations bridging typed -> boxed...
}

// After: single interface with direct Guid? properties
public interface IHasAccountability {
    Guid? CreatedBy { get; set; }
    Guid? ModifiedBy { get; set; }
}
```

Same collapse applied to `IHasIdentity`, `IHasTenantId`, and `IHasSoftDelete`.

**Entity auto-mapping** — uses `Guid?` directly instead of boxed accessors:

```csharp
// Before
ha.BoxedCreatedBy = _createdByBridge.Read((TSelf)this);

// After
ha.CreatedBy = _createdByBridge.Read((TSelf)this);
```

### What Was Removed

| Removed | Reason |
|---------|--------|
| `ITypedId<out T>` | Single `ITypedId` with `Guid Value` suffices |
| `IHasAccountability<T>`, `IHasIdentity<T>`, `IHasTenantId<T>`, `IHasSoftDelete<T>` | Generic parameter was always `Guid` |
| `BoxingHelper` (entire file) | Boxing only existed because backing type was unknown |
| `IdentityType` / `BoxedValue` on `ITypedId` | Type is always `Guid`, no runtime discovery needed |
| `BoxedCreatedBy` / `BoxedModifiedBy` / `BoxedDeletedBy` etc. | DIM boxing bridge pattern unnecessary |
| Non-Guid TypedId tests (int, string, long) | No non-Guid backing types supported |

### What Stayed Generic (and Why)

Domain-side interfaces kept their type parameters because they serve a *different* purpose — distinguishing *which* typed ID, not *which backing type*:

- `IAccountable<TActorId>` — prevents assigning `EmployeeId` where `UserId` is expected
- `ITenantScoped<TTenantId>` — allows custom tenant ID types
- `ISoftDeletable<TActorId>` — same actor ID safety as accountability
- `Entity<TId>`, `AggregateRoot<TId>` — distinguishes `OrderId` from `InvoiceId`

These generics provide compile-time type safety at zero complexity cost.

### Consumer Migration

```csharp
// Typed ID: remove <Guid>
public record OrderId(Guid Value) : TypedId<Guid>(Value);  // Before
public record OrderId(Guid Value) : TypedId(Value);         // After

// Memento interface: remove <Guid>
class OrderMemento : IHasIdentity<Guid>, IHasAccountability<Guid> { ... }  // Before
class OrderMemento : IHasIdentity, IHasAccountability { ... }              // After

// Domain interfaces: unchanged
class Order : Entity<OrderId>, IAccountable<UserId> { ... }  // Same
```

## Prevention Strategies

### How to Avoid Speculative Genericity

1. **Require two concrete use cases before introducing a generic type parameter.** If only one type is ever instantiated, use the concrete type directly.
2. **Audit generic propagation at review time.** If a new generic parameter forces signature changes in 3+ other types, treat it as a code smell requiring justification.
3. **Add a "Why generic?" comment** whenever introducing a generic type parameter. If the answer is "we might need it someday," that is insufficient.
4. **Periodically review wrapper/bridge/helper types.** These are where speculative genericity accumulates silently.

### Decision Framework: When Are Generics Justified?

**Use generics when:**
- Two or more concrete types exist *today* that share the same behavior
- The generic parameter represents a *core variability axis*, not a speculative one
- The generic *reduces existing duplication*, not hypothetical duplication
- The generic *stays local* — does not cascade through 3+ layers

**Use concrete types when:**
- Only one instantiation exists or is planned
- The "what if?" scenario is hypothetical with no concrete requirement
- Adding the generic would propagate through 3+ layers
- The domain has a strong natural constraint (e.g., "IDs are always Guids" is a policy)

**Rule of thumb:** If you cannot name two different concrete types that will fill the generic parameter within the current milestone, use the concrete type. Refactoring from concrete to generic is straightforward; removing unnecessary generics from a deeply entangled type hierarchy is painful.

### Warning Signs

- A generic parameter with only one concrete instantiation across the codebase
- Generic parameters propagating through 4+ layers
- Bridge/adapter types that exist solely to manage generic complexity
- Test fixtures harder to write than production code due to type parameters
- Justifications that include "in case we ever need..."

## Related Documentation

### Plans
- `docs/plans/20260328-0738-refactor-typedid-guid-only-simplification-plan.md` — the plan for this work (completed)
- `docs/plans/20260325-2111-feat-typedid-entity-aggregateroot-building-blocks-plan.md` — original plan that introduced generic `TypedId<T>`
- `docs/plans/20260327-0936-feat-cross-cutting-domain-interfaces-plan.md` — established the two-tier memento pattern being collapsed
- `docs/plans/20260328-0738-feat-domain-event-interfaces-plan.md` — depends on this simplification

### ADRs (updated)
- `docs/adr/domain/20260324-1032-domain-building-blocks.md` — TypedId section updated to Guid-only
- `docs/adr/infrastructure/20260324-1249-cross-cutting-infrastructure.md` — memento interfaces updated to single-tier

### Superseded Solutions
- `docs/solutions/design-patterns/cross-cutting-interfaces-dim-boxing-and-typed-id-bridging.md` — documents the DIM boxing pattern eliminated by this work (marked as superseded)
