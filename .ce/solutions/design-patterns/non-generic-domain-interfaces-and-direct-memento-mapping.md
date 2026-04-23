---
title: "Non-Generic Domain Interfaces and Direct Memento Mapping"
date: 2026-03-28
severity: medium
tags:
  - simplification
  - DDD
  - memento-pattern
  - cross-cutting-concerns
  - interfaces
  - encapsulation
  - YAGNI
affected_files:
  - src/Yaf.Domain/ActorId.cs
  - src/Yaf.Domain/Interfaces/ITenantScoped.cs
  - src/Yaf.Domain/Interfaces/IAccountable.cs
  - src/Yaf.Domain/Interfaces/ISoftDeletable.cs
  - src/Yaf.Domain/Interfaces/ITimestamped.cs
  - src/Yaf.Domain/Interfaces/IActorScoped.cs
  - src/Yaf.Domain/Interfaces/IDomainEvent.cs
  - src/Yaf.Domain/Helpers/MementoHelper.cs
  - src/Yaf.Domain/Entity{TId,TSelf,TMemento}.cs
  - src/Yaf.Domain/AggregateRoot{TId,TSelf,TMemento}.cs
---

## Problem

After simplifying `TypedId<T>` to Guid-only (see `typedid-guid-only-simplification.md`), domain-side interfaces still carried generic type parameters: `IAccountable<TActorId>`, `ISoftDeletable<TActorId>`, `ITenantScoped<TTenantId>`. This had two consequences:

1. **Unnecessary generics** — with Guid-only backing, the distinction between `IAccountable<UserId>` and `IAccountable<EmployeeId>` was speculative. A framework-provided `ActorId` type (parallel to `TenantId`) serves all actor identity needs.
2. **Complex memento mapping** — Entity and AggregateRoot used `TypedIdBridge` (reflection + compiled expression-tree factories) to convert between typed IDs and Guid values. `ReflectionHelper` provided compiled property writers for hydration because entity properties had `{ get; private set; }` setters inaccessible through interfaces.

The infrastructure consisted of: `TypedIdBridge`, `TypedIdFactoryCache`, `ReflectionHelper`, `MementoHelper.BuildWriter`/`BuildReader`/`BuildBridge` — all to handle generic type discovery and property access at runtime.

## Solution

### 1. Introduce framework `ActorId` and remove all generic type parameters

```csharp
// Framework type (parallel to TenantId)
public record ActorId(Guid Value) : TypedId(Value);

// Before: generic
public interface IAccountable<TActorId> where TActorId : ITypedId
{
    TActorId? CreatedBy { get; }
}

// After: non-generic, uses framework ActorId
public interface IAccountable
{
    ActorId? CreatedBy { get; }
}
```

Same pattern for `ITenantScoped` (uses `TenantId`), `ISoftDeletable` (uses `ActorId`).

### 2. Read-only domain interfaces with internal writer complements

Domain interfaces are read-only (`{ get; }`) for encapsulation. Internal writer interfaces provide `{ get; set; }` for infrastructure hydration:

```csharp
// Public, domain-side, read-only
public interface IAccountable
{
    ActorId? CreatedBy { get; }
    ActorId? ModifiedBy { get; }
}

// Internal, for infrastructure hydration
internal interface IAccountableWriter
{
    ActorId? CreatedBy { get; set; }
    ActorId? ModifiedBy { get; set; }
}
```

Entities implement both — a single `public ActorId? CreatedBy { get; set; }` satisfies both contracts. Domain consumers see only the read-only view through `IAccountable`; MementoHelper writes through `IAccountableWriter`.

### 3. Direct interface reads/writes replace compiled expressions

With non-generic interfaces and framework types, memento mapping uses direct interface casts — no reflection:

```csharp
// Snapshot: read from entity via domain interface, write to memento
internal static void SnapshotCrossCutting(TSelf entity, TMemento memento)
{
    if (entity is IAccountable acc && memento is IHasAccountability ha)
    {
        ha.CreatedBy = acc.CreatedBy?.Value;   // ActorId → Guid
        ha.ModifiedBy = acc.ModifiedBy?.Value;
    }
}

// Hydrate: read from memento, write to entity via internal writer
internal static void HydrateCrossCutting(TSelf entity, TMemento memento)
{
    if (entity is IAccountableWriter acc && memento is IHasAccountability ha)
    {
        acc.CreatedBy = ha.CreatedBy.HasValue ? new ActorId(ha.CreatedBy.Value) : null;
        acc.ModifiedBy = ha.ModifiedBy.HasValue ? new ActorId(ha.ModifiedBy.Value) : null;
    }
}
```

### 4. Eliminated infrastructure

| Deleted | Reason |
|---------|--------|
| `TypedIdBridge<TSelf>` | Open-generic reflection no longer needed — types are known |
| `TypedIdFactoryCache` | No more dynamic constructor lookup — `new ActorId(guid)` directly |
| `ReflectionHelper` | Compiled property writers replaced by interface writes |
| `MementoHelper.BuildWriter/BuildReader/BuildBridge` | All builder overloads removed |
| `System.Linq.Expressions` dependency | Zero compiled expressions remain in cross-cutting mapping |

Identity reconstruction (`TId` from `Guid`) still uses `Activator.CreateInstance` because `TId` is a generic type parameter on `Entity<TId>`. This is the one remaining reflection call — a single `Activator.CreateInstance(typeof(TId), guid)` per Restore/Hydrate.

### 5. Cross-cutting mapping centralized in MementoHelper

Previously, 6 private mapping methods were duplicated between `Entity{TId,TSelf,TMemento}` and `AggregateRoot{TId,TSelf,TMemento}`. Now `MementoHelper.SnapshotCrossCutting` and `HydrateCrossCutting` are the single source of truth. Entity and AggregateRoot are thin orchestrators:

```csharp
public void Snapshot(TMemento memento)
{
    MementoHelper<TId, TSelf, TMemento>.WriteIdentity(memento, Id);
    MementoHelper<TId, TSelf, TMemento>.SnapshotCrossCutting((TSelf)this, memento);
    SnapshotCore(memento);  // consumer-specific
}
```

## Key Design Decisions

1. **Framework provides `ActorId` and `TenantId`** — consumers don't define their own actor/tenant ID types. Different actor types (human user, service account, bot) share `ActorId`. This is a policy decision, not a limitation.

2. **Domain interfaces read-only, writer interfaces internal** — domain consumers interact through `IAccountable` (read-only). Infrastructure writes through `IAccountableWriter` (internal). Entities implement both with a single `{ get; set; }` property.

3. **Direct construction over factories** — `new ActorId(guid)` replaces compiled factory delegates. The concrete type is known at compile time for all cross-cutting concerns. Only `TId` (entity identity) needs runtime construction via `Activator.CreateInstance`.

## Prevention Strategies

- **When adding cross-cutting interfaces**: follow the read-only domain + internal writer pattern. Add both interfaces to the same file.
- **When a generic type parameter has only one instantiation**: use a concrete framework type instead. Generic → concrete is easy; concrete → generic is a well-understood refactoring.
- **When memento mapping needs property writes**: check if an internal writer interface exists. Never re-introduce compiled expression writers.

## Related Documentation

- `docs/solutions/design-patterns/typedid-guid-only-simplification.md` — prerequisite simplification
- `docs/solutions/design-patterns/cross-cutting-interfaces-dim-boxing-and-typed-id-bridging.md` — superseded pattern (historical)
- `docs/adr/domain/20260324-1032-domain-building-blocks.md` — TypedId and entity design
- `docs/adr/infrastructure/20260324-1249-cross-cutting-infrastructure.md` — cross-cutting concern contracts
