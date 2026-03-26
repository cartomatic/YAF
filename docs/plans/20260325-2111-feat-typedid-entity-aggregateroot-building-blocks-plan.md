---
title: "feat: TypedId, Entity, and AggregateRoot DDD building blocks"
type: feat
status: completed
date: 2026-03-25
---

# feat: TypedId, Entity, and AggregateRoot DDD Building Blocks

## Overview

Implement the three core identity and lifecycle building blocks for Yaf.Domain: `TypedId<T>` (strongly-typed identifiers), `Entity<TId>` (identity-based domain objects), and `AggregateRoot<TId>` (consistency boundaries with domain events). These extend the existing `ValueObject` foundation and follow the same patterns (memento support, XML docs, zero dependencies).

This also introduces `ITypedId` (constraint interface), `IDomainEvent` (marker interface), and memento-integrated variants `Entity<TId, TSelf, TMemento>` and `AggregateRoot<TId, TSelf, TMemento>` with template methods mirroring `ValueObject<TSelf, TMemento>`.

## Problem Statement / Motivation

The [Domain Building Blocks ADR](../adr/domain/20260324-1032-domain-building-blocks.md) defines these types but none are implemented yet. Without them, consumers cannot build domain models — every entity and aggregate root depends on these base types. TypedId prevents accidental ID mix-ups at compile time. Entity provides identity-based equality. AggregateRoot defines the consistency boundary and owns domain events.

The existing `ValueObject` + `IMemento` + `IHydratable` implementations establish the patterns these types must follow.

## Proposed Solution

### Type Hierarchy

```
ITypedId (interface — constraint marker)
├── ITypedId<T> : ITypedId (interface — typed value access)
│   └── TypedId<T> : ITypedId<T> (abstract record class)
│       └── OrderId(Guid Value) : TypedId<Guid> (consumer-defined)

IDomainEvent (marker interface)

Entity<TId> where TId : ITypedId (abstract class — identity + equality)
├── Entity<TId, TSelf, TMemento> : Entity<TId>, IMemento<TSelf, TMemento>, IHydratable<TMemento>
│   (abstract class — adds memento template methods)

AggregateRoot<TId> : Entity<TId> (abstract class — adds domain events)
├── AggregateRoot<TId, TSelf, TMemento> : AggregateRoot<TId>, IMemento<TSelf, TMemento>, IHydratable<TMemento>
│   (abstract class — adds memento template methods + events)
```

### Key Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| TypedId base type | Abstract record **class** (not struct) | Record structs cannot be inherited. Record class allows `OrderId : TypedId<Guid>` inheritance, provides value equality and ToString via record semantics. |
| TId constraint | `where TId : ITypedId` | Interface constraint rather than concrete `TypedId<T>` base class. Constraining to `TypedId<T>` would require a second type parameter on Entity (`Entity<TId, T>`) because the compiler needs to know `T`. `ITypedId` avoids this while still preventing raw primitives like `Entity<Guid>`. |
| Entity equality | By ID + runtime type check | `Order(id=1) != OrderLine(id=1)` even if both inherit `Entity<SomeId>`. Prevents cross-type equality bugs in mixed collections. |
| Transient entity equality | Application-generated IDs only | ID is always present at construction time (`Guid.NewGuid()` in factory). No `default(TId)` edge case. Database-generated sequential IDs are not a first-class pattern. |
| AddDomainEvent | **Protected** | Only the aggregate raises events through domain methods. Tests assert via the public read-only `DomainEvents` collection. |
| ClearDomainEvents | **Public** | Infrastructure needs to clear events after dispatch. Convenience outweighs misuse risk. |
| IDomainEvent | Marker interface only | Context envelope (TenantId, IdentityId, CorrelationId, etc.) is attached by infrastructure at dispatch time, not by the event. Per [Domain Events ADR](../adr/domain/20260324-1113-domain-events-and-integration-events.md). |
| Memento base for entities | Included in this work item | `Entity<TId, TSelf, TMemento>` and `AggregateRoot<TId, TSelf, TMemento>` follow the `ValueObject<TSelf, TMemento>` pattern with `SnapshotCore`/`RestoreCore`/`HydrateCore`/`Validate` template methods. |
| IIntegrationEvent | Deferred | Out of scope. ADR defines it as contract-only; implement when messaging module is built. |

## Technical Considerations

### C# Pattern: Static Abstract on Abstract Classes

A concrete static method on an abstract class **does** satisfy a `static abstract` interface member. This was [confirmed during ValueObject implementation](../solutions/logic-errors/csharp-static-abstract-crtp-memento-pattern.md). Both `Entity<TId, TSelf, TMemento>` and `AggregateRoot<TId, TSelf, TMemento>` implement `IMemento<TSelf, TMemento>` directly — exactly like `ValueObject<TSelf, TMemento>` does. Concrete types inherit the implementation for free.

### Single Inheritance

`AggregateRoot<TId, TSelf, TMemento>` cannot inherit from both `AggregateRoot<TId>` and `Entity<TId, TSelf, TMemento>`. It inherits from `AggregateRoot<TId>` (for domain events) and implements `IMemento<TSelf, TMemento>` + `IHydratable<TMemento>` directly, repeating the same template method pattern. The shared logic (`Snapshot`/`Restore`/`Hydrate` orchestration) is small (~15 lines).

### TypedId Design

- `ITypedId` — non-generic marker interface for use as a generic constraint (`where TId : ITypedId`)
- `ITypedId<T>` — generic interface exposing `T Value` for infrastructure access (e.g., EF Core converters). Constrained: `where T : IEquatable<T>` to ensure backing types have proper equality semantics
- `TypedId<T>` — abstract record class with `where T : IEquatable<T>`. Consumer inherits: `public record OrderId(Guid Value) : TypedId<Guid>(Value)`
- No guard against default/empty values on TypedId itself — guard in Entity constructors/factory methods instead
- No `IComparable` in this iteration
- No implicit/explicit conversion operators — consumers use `id.Value` or `new OrderId(guid)`
- JSON converters and EF Core value converters are out of scope (infrastructure concern)

### Entity Equality Implementation

```
// Pseudocode — actual implementation in C#
Entity<TId>.Equals(other):
  if other is null → false
  if ReferenceEquals(this, other) → true
  if GetType() != other.GetType() → false
  return Id.Equals(other.Id)

Entity<TId>.GetHashCode():
  return HashCode.Combine(GetType(), Id)
```

- Implements `IEquatable<Entity<TId>>`
- Overrides `object.Equals(object?)` and `object.GetHashCode()`
- Overloads `==` and `!=` operators with nullable parameters, delegating to `Equals(object?, object?)` to avoid null-reference bugs and infinite recursion:
  ```
  public static bool operator ==(Entity<TId>? left, Entity<TId>? right)
      => Equals(left, right);
  public static bool operator !=(Entity<TId>? left, Entity<TId>? right)
      => !Equals(left, right);
  ```
- Type check prevents cross-type equality (`Order` vs `OrderLine` with same ID type)

### Memento-Capable Entity Pattern

Mirrors `ValueObject<TSelf, TMemento>`:

```
Entity<TId, TSelf, TMemento> : Entity<TId>, IMemento<TSelf, TMemento>, IHydratable<TMemento>
  - Snapshot(TMemento) — base handles Id, then calls SnapshotCore for subclass state
  - static Restore(TMemento) — RuntimeHelpers.GetUninitializedObject + base restores Id + RestoreCore + Validate
  - Hydrate(TMemento) — base handles Id, calls HydrateCore, then Validate (throws on invalid state)
  - abstract SnapshotCore(TMemento) — subclass snapshots its own properties (not Id)
  - abstract RestoreCore(TMemento) — subclass restores its own properties (not Id)
  - abstract HydrateCore(TMemento) — subclass hydrates its own properties (not Id)
  - abstract Validate() → IReadOnlyCollection<IError>
```

**Id handling:** The base class (`Entity<TId, TSelf, TMemento>`) handles snapshotting, restoring, and hydrating the `Id` property. The `*Core` template methods are for subclass-specific state only. This requires TMemento to expose an Id-compatible property — the base class needs a way to read/write the Id on the memento. This is achieved via an abstract method or a convention (e.g., the memento must have a property that the subclass maps in its Core methods). **Decision: the base class handles Id via abstract methods `GetIdFromMemento(TMemento)` and `SetIdOnMemento(TMemento)` that subclasses implement.**

**Hydrate validates:** Both `Restore()` and `Hydrate()` call `Validate()` after populating state and throw `ValidationException` on invalid state. We don't trust input in either path — data from EF Core could be corrupted or from an incompatible schema migration.

Key difference from ValueObject: Entity has `HydrateCore` for in-place updates (mutable entities tracked by EF Core). `RestoreCore` is for initial materialization; `HydrateCore` is for reloading state into an existing tracked instance.

### Domain Events Collection

```
AggregateRoot<TId>:
  - private List<IDomainEvent>? _domainEvents (nullable, lazy-initialized with ??=)
  - public IReadOnlyCollection<IDomainEvent> DomainEvents →
      _domainEvents is not null ? _domainEvents.AsReadOnly() : Array.Empty<IDomainEvent>()
  - protected void AddDomainEvent(IDomainEvent domainEvent) — lazy-inits _domainEvents via ??=
  - public void ClearDomainEvents() → clears list, returns void. Infrastructure reads DomainEvents first.
```

- `_domainEvents` is nullable and lazy-initialized via `??=` because `RuntimeHelpers.GetUninitializedObject` bypasses field initializers
- `DomainEvents` returns `Array.Empty<IDomainEvent>()` when no events have been added (avoids allocating a list just to read an empty collection)
- Events preserve insertion order (List<T>)

## Acceptance Criteria

### TypedId

- [x]`ITypedId` marker interface in `Yaf.Domain.Interfaces`
- [x]`ITypedId<T>` generic interface with `T Value` property and `where T : IEquatable<T>` constraint in `Yaf.Domain.Interfaces`
- [x]`TypedId<T>` abstract record class implementing `ITypedId<T>` in `Yaf.Domain`
- [x]Consumer can declare `public record OrderId(Guid Value) : TypedId<Guid>(Value)`
- [x]Equality works correctly: `new OrderId(guid1) == new OrderId(guid1)` is true
- [x]Different values are not equal: `new OrderId(guid1) != new OrderId(guid2)`
- [x]Different typed ID types with same value are not equal: `new OrderId(guid) != new CustomerId(guid)`
- [x]`ToString()` returns a meaningful representation
- [x]Works with `int`, `long`, `string` backing types (not just Guid)
- [x]XML doc comments on all public API (CS1591 compliance)

### IDomainEvent

- [x]`IDomainEvent` marker interface in `Yaf.Domain.Interfaces`
- [x]Consumer can declare `public record OrderPlaced(OrderId OrderId) : IDomainEvent`
- [x]XML doc comments

### Entity

- [x]`Entity<TId>` abstract class with `where TId : ITypedId` constraint in `Yaf.Domain`
- [x]`Id` property with public getter, protected setter
- [x]Protected constructor accepting `TId id`
- [x]Identity-based equality: same type + same ID = equal
- [x]Cross-type inequality: `Order(id) != OrderLine(id)` even with same ID value
- [x]Implements `IEquatable<Entity<TId>>`, overrides `Equals`/`GetHashCode`, overloads `==`/`!=`
- [x]`Entity<TId, TSelf, TMemento>` memento variant with `SnapshotCore`/`RestoreCore`/`HydrateCore`/`Validate` template methods
- [x]Base class handles `Id` in Snapshot/Restore/Hydrate — `*Core` methods are for subclass-specific state only
- [x]`Restore()` uses `RuntimeHelpers.GetUninitializedObject`, base restores Id, calls `RestoreCore`, then `Validate`
- [x]`Hydrate()` base handles Id, calls `HydrateCore`, then `Validate` — throws `ValidationException` on invalid state
- [x]Validation errors on both restore and hydrate throw `ValidationException`
- [x]XML doc comments on all public API

### AggregateRoot

- [x]`AggregateRoot<TId>` abstract class extending `Entity<TId>` in `Yaf.Domain`
- [x]`DomainEvents` public read-only collection
- [x]`AddDomainEvent(IDomainEvent)` protected method
- [x]`ClearDomainEvents()` public method
- [x]Events preserve insertion order
- [x]`AggregateRoot<TId, TSelf, TMemento>` memento variant with same template methods as Entity
- [x]Domain events collection survives memento restore (lazy `??=` initialization)
- [x]XML doc comments on all public API

### Testing

- [x]TypedId: equality, inequality, cross-type inequality, ToString, different backing types
- [x]Entity: identity equality, cross-type inequality, GetHashCode consistency, null handling
- [x]Entity memento: round-trip (create → snapshot → restore → verify equality), hydrate round-trip, validation on both restore and hydrate
- [x]AggregateRoot: event accumulation, event ordering, ClearDomainEvents, events survive restore
- [x]AggregateRoot memento: round-trip with events
- [x]All tests use xUnit + AwesomeAssertions, following patterns in `ValueObjectTests.cs`

## Success Metrics

- All acceptance criteria pass
- Zero CS1591 warnings (XML docs on all public API)
- `dotnet build` and `dotnet test` pass with zero warnings
- Round-trip memento tests prove snapshot/restore/hydrate fidelity

## Dependencies & Risks

**Dependencies:**
- Existing `IMemento<TSelf, TMemento>`, `IHydratable<TMemento>`, `IError`, `ValidationException` — all already implemented
- No external package dependencies (pure .NET)

**Risks:**
- **TypedId as record class (heap allocation):** This is a deliberate tradeoff for inheritance support. TypedIds are typically short-lived method parameters or entity properties — allocation pressure is minimal. If profiling later shows issues, the hierarchy can be preserved while optimizing allocation (e.g., object pooling in infrastructure).
- **Single inheritance:** `AggregateRoot<TId, TSelf, TMemento>` inherits from `AggregateRoot<TId>` (not `Entity<TId, TSelf, TMemento>`), so it implements `IMemento` + `IHydratable` independently. The shared orchestration logic (Snapshot/Restore/Hydrate methods calling template methods) is repeated but small and well-tested.

## File Plan

### New Source Files (`src/Yaf.Domain/`)

| File | Type | Description |
|------|------|-------------|
| `Interfaces/ITypedId.cs` | Interface | Non-generic marker + generic `ITypedId<T>` with `Value` |
| `Interfaces/IDomainEvent.cs` | Interface | Marker interface for domain events |
| `TypedId.cs` | Abstract record | `TypedId<T>` base for all strongly-typed IDs |
| `Entity.cs` | Abstract class | `Entity<TId>` with identity, equality, protected constructor |
| `Entity{TId,TSelf,TMemento}.cs` | Abstract class | Memento-capable entity with template methods |
| `AggregateRoot.cs` | Abstract class | `AggregateRoot<TId>` with domain event collection |
| `AggregateRoot{TId,TSelf,TMemento}.cs` | Abstract class | Memento-capable aggregate root with template methods |

### New Test Files (`tests/Yaf.Domain.Tests/`)

| File | Description |
|------|-------------|
| `TypedIdTests.cs` | Equality, hashing, ToString, different backing types, cross-type |
| `EntityTests.cs` | Identity equality, cross-type inequality, null handling, memento round-trip, hydrate, validation |
| `AggregateRootTests.cs` | Event accumulation, ordering, clearing, memento round-trip with events |

## Implementation Order

0. **Update ADRs** (prerequisite — must happen before or atomically with implementation, see "ADR Updates Needed" below)
1. `ITypedId` + `ITypedId<T>` (interfaces, no dependencies)
2. `TypedId<T>` (depends on interfaces)
3. `IDomainEvent` (marker interface, no dependencies)
4. `Entity<TId>` (depends on `ITypedId`)
5. `Entity<TId, TSelf, TMemento>` (depends on Entity, IMemento, IHydratable)
6. `AggregateRoot<TId>` (depends on Entity, IDomainEvent)
7. `AggregateRoot<TId, TSelf, TMemento>` (depends on AggregateRoot, IMemento, IHydratable)
8. Tests for all types

## References & Research

### Internal References

- [ADR: Domain Building Blocks](../adr/domain/20260324-1032-domain-building-blocks.md) — type specifications
- [ADR: State Management — Memento Pattern](../adr/domain/20260324-1104-state-management-memento-pattern.md) — IMemento/IHydratable contract
- [ADR: Domain Events](../adr/domain/20260324-1113-domain-events-and-integration-events.md) — IDomainEvent, context envelope
- [ADR: Cross-Cutting Infrastructure](../adr/infrastructure/20260324-1249-cross-cutting-infrastructure.md) — IHasVersionInfo (not in scope, but informs why concurrency is NOT on AggregateRoot)
- [Brainstorm: DDD Concepts](../brainstorms/20260322-2036-yaf-ddd-concepts-brainstorm.md) — design rationale
- [Solution: CRTP + Memento Pattern](../solutions/logic-errors/csharp-static-abstract-crtp-memento-pattern.md) — gotchas with `GetUninitializedObject`, `{ get; private set; }`, `??=` lazy init
- Existing implementation: `src/Yaf.Domain/ValueObject{TSelf,TMemento}.cs` — pattern to follow
- Existing tests: `tests/Yaf.Domain.Tests/ValueObjectTests.cs` — test conventions

### ADR Updates Needed (prerequisite — update before or alongside implementation)

**[Domain Building Blocks ADR](../adr/domain/20260324-1032-domain-building-blocks.md):**
- TypedId is an abstract record class (not record struct) with `ITypedId`/`ITypedId<T>` interface hierarchy
- `ITypedId<T>` constrains `T : IEquatable<T>`
- TId constraint on Entity is `where TId : ITypedId` (not `IEquatable<TId>`) — rationale: `ITypedId` enforces that consumers use strongly-typed IDs rather than raw primitives like `Entity<Guid>`
- Application-generated IDs are the mandated pattern (no database-generated ID support)

**[Domain Building Blocks ADR](../adr/domain/20260324-1032-domain-building-blocks.md) (continued):**
- Remove implicit/explicit conversion operators from TypedId construction column — consumers use `id.Value` or `new OrderId(guid)` instead

**[State Management — Memento Pattern ADR](../adr/domain/20260324-1104-state-management-memento-pattern.md):**
- Remove incorrect claim (line 123) that abstract classes cannot implement `static abstract` interfaces — the existing `ValueObject<TSelf, TMemento>` proves otherwise
- Add: `Hydrate()` also calls `Validate()` and throws `ValidationException` on invalid state (same as `Restore()`)
