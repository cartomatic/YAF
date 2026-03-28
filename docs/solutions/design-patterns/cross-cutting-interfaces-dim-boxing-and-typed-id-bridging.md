---
title: "Cross-Cutting Interfaces: DIM Boxing and Typed ID Bridging"
date: 2026-03-28
severity: medium
tags:
  - domain-driven-design
  - cross-cutting-concerns
  - memento-pattern
  - default-interface-methods
  - compiled-delegates
  - typed-ids
  - accountability
  - timestamps
  - soft-delete
  - multi-tenancy
  - versioning
affected_files:
  - src/Yaf.Domain/Entity{TId,TSelf,TMemento}.cs
  - src/Yaf.Domain/AggregateRoot{TId,TSelf,TMemento}.cs
  - src/Yaf.Domain/ValueObject{TSelf,TMemento}.cs
  - src/Yaf.Domain/Helpers/TypedIdBridge.cs
  - src/Yaf.Domain/Helpers/MementoHelper.cs
  - src/Yaf.Domain/Helpers/ReflectionHelper.cs
  - src/Yaf.Domain/Helpers/BoxingHelper.cs
  - src/Yaf.Domain/Interfaces/IHasAccountability.cs
  - src/Yaf.Domain/Interfaces/IHasSoftDelete.cs
  - src/Yaf.Domain/Interfaces/IHasTenantId.cs
  - src/Yaf.Domain/Interfaces/IHasTimestamps.cs
  - src/Yaf.Domain/Interfaces/IHasIdentity.cs
---

## Problem

The YAF framework needed opt-in cross-cutting concern interfaces (accountability, timestamps, soft-delete, tenant, versioning) that work across a memento-based persistence boundary. The challenge: base classes (`Entity`, `AggregateRoot`) must auto-map properties between domain entities (typed IDs like `UserId`) and mementos (primitives like `Guid`) without knowing the concrete generic type arguments of the opt-in interfaces.

## Solution

### Architecture: Four-Layer Helper Decomposition

| Layer | File | Responsibility |
|-------|------|----------------|
| **BoxingHelper** | `Helpers/BoxingHelper.cs` | Generic `Unbox<T>()` — shared by all DIM implementations |
| **ReflectionHelper** | `Helpers/ReflectionHelper.cs` | Generic compiled property readers/writers — zero domain knowledge |
| **MementoHelper** | `Helpers/MementoHelper.cs` | Conditional builders that check entity/memento interface compatibility, identity bridging |
| **TypedIdBridge** | `Helpers/TypedIdBridge.cs` | Compiled typed ID boxing/unboxing via `ITypedId.BoxedValue` + factory reconstruction |

### Pattern 1: DIM Boxing Bridge

Non-generic / generic interface pairs with Default Interface Methods that bridge typed values to `object?`:

```csharp
// Non-generic base (consumed by base class via runtime is-checks)
public interface IHasAccountability
{
    Type ActorIdType { get; }
    object? BoxedCreatedBy { get; set; }
    object? BoxedModifiedBy { get; set; }
}

// Generic derived (consumed by memento implementations)
public interface IHasAccountability<T> : IHasAccountability
    where T : struct, IEquatable<T>
{
    T? CreatedBy { get; set; }
    T? ModifiedBy { get; set; }

    // DIM auto-bridges typed → boxed using shared helper
    object? IHasAccountability.BoxedCreatedBy
    {
        get => CreatedBy;
        set => CreatedBy = Helpers.BoxingHelper.Unbox<T>(value);
    }
}
```

**Key insight**: The `struct` constraint makes `T?` a `Nullable<T>`, so `null` naturally represents "not yet set" — no sentinel values or `IsDefaultOrNull` hacks needed.

### Pattern 2: TypedIdBridge — Per-Property Compiled Delegates

Each typed ID property gets its own bridge instance:

```csharp
// Static fields compiled once per closed generic type, null if concern not applicable
private static readonly TypedIdBridge<TSelf>? _createdByBridge =
    MementoHelper<TId, TSelf, TMemento>.BuildBridge(
        typeof(IAccountable<>), typeof(IHasAccountability),
        nameof(IHasAccountability<Guid>.CreatedBy));

// Read: entity → primitive (extracts ITypedId.BoxedValue)
ha.BoxedCreatedBy = _createdByBridge.Read((TSelf)this);

// Write: primitive → entity (reconstructs typed ID via cached factory)
_createdByBridge.Write((TSelf)this, ha.BoxedCreatedBy);
```

### Pattern 3: Conditional Null-Returning Builders

`MementoHelper.BuildWriter/BuildReader/BuildBridge` return `null` when the entity or memento doesn't implement the required interfaces. The Snapshot/Hydrate methods use null-checks to skip inapplicable concerns — zero reflection cost at runtime for interfaces not implemented.

### Pattern 4: Restore Delegates to Hydrate

`Restore` creates an uninitialized instance and calls `Hydrate`. Consumers implement only `HydrateCore` — no separate `RestoreCore`. Consistent naming across all base classes (Entity, AggregateRoot, ValueObject).

## What Consumers Implement

```csharp
public class Order : Entity<OrderId, Order, IOrderMemento>,
    IAccountable<UserId>, ISoftDeletable<UserId>, ITimestamped, ITenantScoped<TenantId>
{
    // Auto-mapped: identity, timestamps, accountability, soft-delete
    // Manual: tenant + business properties

    protected override void SnapshotCore(IOrderMemento memento)
    {
        memento.TenantId = TenantId?.Value;
        memento.CustomerName = CustomerName;
    }

    protected override void HydrateCore(IOrderMemento memento)
    {
        TenantId = memento.TenantId is { } t ? new TenantId(t) : null!;
        CustomerName = memento.CustomerName;
    }
}
```

## Iterative Simplification Journey

The design went through 21 commits of refinement:

| Iteration | What changed | Why |
|-----------|-------------|-----|
| Initial | Non-generic markers (`IAccountable`, `ISoftDeletable`, `ITenantScoped`) | Existed for runtime `is` checks |
| Simplified | Removed non-generic markers | `FindGenericInterface` alone suffices — markers were redundant |
| Initial | `RestoreCore` + `HydrateCore` as separate abstract methods | Assumed Restore and Hydrate differ |
| Simplified | Restore delegates to Hydrate, `RestoreCore` eliminated | Consumer code was always identical in both |
| Initial | Full reflection-based MementoBridge with Lazy init | Auto-handled all concerns via compiled delegates |
| Simplified | Removed MementoBridge (KISS) | Pragmatic review: too much infrastructure for zero consumers |
| Restored | Re-added auto-mapping for timestamps, accountability, soft-delete | Targeted auto-mapping for concerns that genuinely save boilerplate |
| Initial | `TypedIdBridge.ReadPair`/`WritePair` operating on 1-2 properties | Pair abstraction for accountability (2 props) |
| Simplified | Single-property `Read`/`Write` on TypedIdBridge | Each property gets its own bridge — cleaner for soft-delete (1 prop) |
| Initial | `IHasAccountability<T>` used `T` (non-nullable) for Guid | C# generics quirk: unconstrained `T?` doesn't mean `Nullable<T>` for structs |
| Fixed | Added `struct` constraint, made properties `T?` | Proper `Nullable<T>` semantics — null = not set |
| Initial | Each DIM had inline switch expression for boxing | Duplicated across 4 interfaces |
| Simplified | Extracted `BoxingHelper.Unbox<T>` | Single implementation shared by all DIMs |

## Gotchas

1. **`FindGenericInterface` returns null, not an exception.** Every call site must null-check. Forgetting this produces silent no-ops where a concern appears "not enabled."

2. **Properties must use `{ get; private set; }`.** The compiled property writers access non-public setters via `GetSetMethod(nonPublic: true)`. Properties with only `{ get; }` (no setter at all) throw an actionable `InvalidOperationException`.

3. **TypedIdBridge boxing breaks reference equality.** After boxing and unboxing, two `UserId` values that were `ReferenceEquals` before will not be after. Always use `.Equals()` or `==`, never reference equality.

4. **DIM implementations are not virtual.** If a class implements `IHasTimestamps` and you want to override the DIM behavior in a derived class, you must explicitly re-declare the method.

5. **Struct constraint excludes string-backed IDs from memento generics.** `IHasAccountability<string>` is not possible. String IDs would need to use the non-generic boxed interface path directly.

## Prevention Strategies

- **Start with manual mapping, add auto-mapping when the pattern is proven.** The first iteration built full auto-mapping; the pragmatic review correctly flagged this as premature. Add auto-mapping once the interfaces have real consumers.
- **Use pragmatic reviews after multi-agent code reviews.** The architecture/security/performance reviews found no critical issues, but the pragmatic review identified the most impactful simplification (removing the MementoBridge).
- **Design interfaces before implementation.** The spec audit caught naming inconsistencies and stale text that would have been harder to fix after implementation.

## Related

- [DDD Building Blocks: Identity Bridging and Validation](ddd-building-blocks-identity-bridging-and-validation.md) — the `IHasIdentity` DIM pattern that this work extends
- [C# Static Abstract CRTP Memento Pattern](../logic-errors/csharp-static-abstract-crtp-memento-pattern.md) — CRTP and `GetUninitializedObject` gotchas
- [Cross-Cutting Infrastructure ADR](../../adr/infrastructure/20260324-1249-cross-cutting-infrastructure.md) — architectural decisions for all six concerns
- [Implementation Plan](../../plans/20260327-0936-feat-cross-cutting-domain-interfaces-plan.md) — original plan with acceptance criteria
