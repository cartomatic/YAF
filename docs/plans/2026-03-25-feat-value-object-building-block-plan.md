---
title: "feat(domain): ValueObject building block"
type: feat
status: active
date: 2026-03-25
---

# ValueObject Building Block

## Overview

Implement the `ValueObject` base type in `Yaf.Domain` — the first domain building block. Two variants:

1. **`ValueObject`** — marker abstract record. Consumers inherit from it to declare value semantics. Relies on C# record equality (structural). No behavior beyond marking.
2. **`ValueObject<TSelf, TMemento>`** — extends the marker with memento support for snapshot/restore. Uses CRTP (Curiously Recurring Template Pattern) so that `Restore` is a static method on the concrete type, callable generically.

## Design Decision: Unified Memento Contract

The original ADR defines `IMemento<TMemento>` with `void Snapshot(TMemento)` / `void Hydrate(TMemento)` — instance methods that mutate either the memento or self. This doesn't work for immutable value objects (records can't be hydrated in place).

**Decision:** Replace the original single `IMemento<TMemento>` with two interfaces:

```csharp
// Core memento contract — implemented by all domain objects that support persistence
public interface IMemento<TSelf, TMemento>
    where TSelf : IMemento<TSelf, TMemento>
    where TMemento : class
{
    void Snapshot(TMemento memento);
    static abstract TSelf Restore(TMemento memento);
}

// Optional hydration contract — implemented by mutable domain objects (entities)
public interface IHydrateable<TMemento>
    where TMemento : class
{
    void Hydrate(TMemento memento);
}
```

**Key principle:** The domain defines the memento *contract*, not the implementation. Concrete memento types (DTOs) are owned by infrastructure. Infrastructure creates and provides memento instances — the domain only knows how to populate them (`Snapshot`) and restore/hydrate from them.

**Why two interfaces?**
- **Value objects** are immutable records — `Hydrate` (mutate self) is nonsensical. They should only implement `IMemento<TSelf, TMemento>` with `Snapshot` and `Restore`.
- **Entities** are mutable — they implement both `IMemento` and `IHydrateable`. `Hydrate` is useful for reloading state into an existing tracked instance (e.g., EF Core change tracker). `Restore` is used for initial materialization.
- No throwing `NotSupportedException` stubs — types only implement what they actually support.
- Infrastructure can check `is IHydrateable<TMemento>` to decide between hydrate-in-place vs restore-as-new.

**Changes from original ADR:**
- `void Snapshot(TMemento memento)` — **unchanged** from original ADR, now on `IMemento<TSelf, TMemento>`
- `void Hydrate(TMemento memento)` — **moved** to separate `IHydrateable<TMemento>` interface. Only implemented by mutable types.
- **Added** `static abstract TSelf Restore(TMemento memento)` on `IMemento<TSelf, TMemento>` — creates a new instance. Works for both value objects and entities.

**ADR impact:** The memento ADR (`docs/adr/domain/20260324-1104-state-management-memento-pattern.md`) needs updating to reflect this new contract shape. The save/load flow descriptions remain valid — only the interface signature changes.

## Proposed Solution

### Variant 1: Marker

```csharp
// src/Yaf.Domain/ValueObject.cs
namespace Yaf.Domain;

public abstract record ValueObject;
```

Consumer usage:
```csharp
public record Address(string Street, string City, string PostalCode) : ValueObject;
```

That's it. Equality, immutability, and `ToString()` come free from C# records.

### Variant 2: Memento-capable

```csharp
// src/Yaf.Domain/Interfaces/IMemento.cs
namespace Yaf.Domain.Interfaces;

public interface IMemento<TSelf, TMemento>
    where TSelf : IMemento<TSelf, TMemento>
    where TMemento : class
{
    void Snapshot(TMemento memento);
    static abstract TSelf Restore(TMemento memento);
}
```

```csharp
// src/Yaf.Domain/Interfaces/IHydrateable.cs
namespace Yaf.Domain.Interfaces;

public interface IHydrateable<TMemento>
    where TMemento : class
{
    void Hydrate(TMemento memento);
}
```

```csharp
// src/Yaf.Domain/ValueObject{TSelf,TMemento}.cs
namespace Yaf.Domain;

public abstract record ValueObject<TSelf, TMemento> : ValueObject
    where TSelf : ValueObject<TSelf, TMemento>
    where TMemento : class
{
    public abstract void Snapshot(TMemento memento);
}
```

**Note:** The base record cannot declare `: IMemento<TSelf, TMemento>` because C# doesn't allow abstract classes to defer `static abstract` interface members to derived types. Concrete types must explicitly implement the interface.

Consumer usage:
```csharp
public record Address : ValueObject<Address, AddressMemento>, IMemento<Address, AddressMemento>
{
    public string Street { get; init; }
    public string City { get; init; }
    public string PostalCode { get; init; }

    private Address(string street, string city, string postalCode)
    {
        Street = street;
        City = city;
        PostalCode = postalCode;
    }

    public static Address Create(string street, string city, string postalCode)
        => new(street, city, postalCode);

    public override void Snapshot(AddressMemento memento)
    {
        memento.Street = Street;
        memento.City = City;
        memento.PostalCode = PostalCode;
    }

    public static Address Restore(AddressMemento memento)
        => new(memento.Street, memento.City, memento.PostalCode);
}
```

The generic constraint chain means:
- `TSelf` constrains back to the concrete type — enables `static abstract Restore` to return the right type
- Infrastructure code can work generically: `TSelf restored = TSelf.Restore(memento);`
- `Snapshot` populates a memento provided by infrastructure — domain never creates memento instances

## File Layout

```
src/Yaf.Domain/
├── ValueObject.cs                              # Variant 1: marker (Yaf.Domain)
├── ValueObject{TSelf,TMemento}.cs              # Variant 2: memento-capable (Yaf.Domain)
└── Interfaces/
    ├── IMemento.cs                             # Core memento interface (Yaf.Domain.Interfaces)
    └── IHydrateable.cs                         # Optional hydration interface (Yaf.Domain.Interfaces)

tests/Yaf.Domain.Tests/
└── ValueObjectTests.cs                         # Tests for both variants
```

## Acceptance Criteria

- [x] `ValueObject` marker record exists, is abstract, in namespace `Yaf.Domain`
- [x] Consumer can inherit: `record Foo(int X) : ValueObject` and get structural equality
- [x] `IMemento<TSelf, TMemento>` interface exists in `Yaf.Domain.Interfaces` with `Snapshot()` and `static abstract Restore()`
- [x] `IHydrateable<TMemento>` interface exists in `Yaf.Domain.Interfaces` with `Hydrate()` (not implemented by value objects)
- [x] `ValueObject<TSelf, TMemento>` wires the marker to `IMemento` (not `IHydrateable`)
- [x] A concrete test value object demonstrates both variants
- [x] Memento round-trip test: create → snapshot → restore → assert equality
- [x] Tests verify record equality semantics (equal by value, not reference)
- [x] Tests verify inequality when values differ
- [x] Zero external dependencies — pure .NET only
- [x] All code compiles against net10.0
- [x] Memento ADR updated to reflect new `IMemento<TSelf, TMemento>` contract

## Test Plan

### ValueObject (marker) tests

| Test | Verifies |
|------|----------|
| `SimpleValueObject_WithSameValues_AreEqual` | Record structural equality works through inheritance |
| `SimpleValueObject_WithDifferentValues_AreNotEqual` | Inequality |
| `SimpleValueObject_IsValueObject` | Type hierarchy — `is ValueObject` check |

### ValueObject\<TSelf, TMemento\> tests

| Test | Verifies |
|------|----------|
| `MementoValueObject_Snapshot_PopulatesMemento` | Snapshot populates provided memento correctly |
| `MementoValueObject_DoesNotImplementIHydrateable` | Value objects don't implement IHydrateable |
| `MementoValueObject_Restore_CreatesEquivalentObject` | Static Restore round-trips correctly |
| `MementoValueObject_SnapshotThenRestore_RoundTrips` | Full round-trip: create → snapshot → restore → equals original |
| `MementoValueObject_Restore_CanBeCalledGenerically` | `TSelf.Restore(memento)` works in generic context |

## References

- [ADR: Domain Building Blocks](../adr/domain/20260324-1032-domain-building-blocks.md) — ValueObject as C# record with marker base
- [ADR: State Management — Memento Pattern](../adr/domain/20260324-1104-state-management-memento-pattern.md) — IMemento contract (to be updated)
- [DDD Concepts Brainstorm](../brainstorms/20260322-2036-yaf-ddd-concepts-brainstorm.md) — section 2: memento pattern
