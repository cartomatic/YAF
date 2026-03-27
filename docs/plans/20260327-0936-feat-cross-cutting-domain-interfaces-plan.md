---
title: "feat: Cross-cutting domain and memento interfaces"
type: feat
status: completed
date: 2026-03-27
---

# feat: Cross-Cutting Domain and Memento Interfaces

## Overview

Implement opt-in cross-cutting interfaces in `Yaf.Domain` for accountability, timestamping, tenant context, soft-deletion, and versioning. Each typed-ID-bearing interface follows the established `IHasIdentity` pattern: non-generic base + generic derived with default interface methods (DIM), paired with a memento-side interface for automatic persistence bridging via `MementoHelper`.

**Six concerns:**

| Concern | Domain-side | Memento-side | Purpose |
|---------|-------------|-------------|---------|
| Accountability | `IAccountable<TActorId>` | `IHasAccountability<T>` | Who created/modified |
| Timestamping | `ITimestamped` | `IHasTimestamps` | When created/modified |
| Tenant context | `ITenantScoped<TTenantId>` | `IHasTenantId<T>` | Tenant isolation |
| Soft-deletion | `ISoftDeletable<TActorId>` | `IHasSoftDelete<T>` | DeletedAtUtc + DeletedBy; triggers soft-delete infrastructure |
| Optimistic concurrency | _(none)_ | `IHasVersionInfo` | Guid concurrency token |
| Versioning + graveyard | _(none)_ | `IHasVersionHistory` | Marker — activates snapshots + graveyard |

This also introduces the `TenantId` typed identifier and updates the base `Entity`/`AggregateRoot` classes to auto-handle these concerns during Snapshot/Restore/Hydrate.

## Problem Statement / Motivation

The [Cross-Cutting Infrastructure ADR](../adr/infrastructure/20260324-1249-cross-cutting-infrastructure.md) defines these contracts but none are implemented. Without them, consumers cannot opt into audit trails, timestamp tracking, multi-tenancy, soft-deletion, or versioning — all foundational for business applications. The existing `IHasIdentity` + `MementoHelper` pattern provides a proven template for automatic memento bridging.

## Proposed Solution

### Interface Hierarchy

```
Domain-side interfaces (on entities/aggregates):
─────────────────────────────────────────────────
IAccountable                          (non-generic marker for runtime discovery)
├── IAccountable<TActorId>            (generic — TActorId? CreatedBy, ModifiedBy?)
│   where TActorId : ITypedId

ITimestamped                          (DateTimeOffset? CreatedAtUtc, ModifiedAtUtc? — get-only)

ISoftDeletable                        (non-generic marker for runtime discovery)
├── ISoftDeletable<TActorId>          (generic — DateTimeOffset? DeletedAtUtc, TActorId? DeletedBy)
│   where TActorId : ITypedId

ITenantScoped                         (non-generic marker for runtime discovery)
├── ITenantScoped<TTenantId>          (generic — TTenantId TenantId)
│   where TTenantId : ITypedId


Memento-side interfaces (on persistence DTOs):
──────────────────────────────────────────────────
IHasAccountability                    (non-generic base — Type ActorIdType, boxed get/set, all nullable)
├── IHasAccountability<T>             (generic — T? CreatedBy, ModifiedBy? + DIM)
│   where T : IEquatable<T>

IHasTimestamps                        (DateTimeOffset? CreatedAtUtc, ModifiedAtUtc? — get/set)

IHasSoftDelete                        (non-generic base — DateTimeOffset? DeletedAtUtc, boxed DeletedBy get/set)
├── IHasSoftDelete<T>                 (generic — T? DeletedBy + DIM)
│   where T : IEquatable<T>

IHasTenantId                          (non-generic base — Type TenantIdType, boxed get/set)
├── IHasTenantId<T>                   (generic — T TenantId get/set + DIM)
│   where T : IEquatable<T>

IHasVersionInfo                       (Guid Version — get/set; memento-only concurrency token)

IHasVersionHistory                    (independent marker — activates snapshots + graveyard; memento-only)


Framework-provided typed ID:
────────────────────────────
TenantId(Guid Value) : TypedId<Guid>  (in Yaf.Domain, framework-provided default)
```

### Key Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Interface naming | ADR names: `IAccountable`, `ITimestamped`, `ITenantScoped`; new: `ISoftDeletable` | Consistency with ADR |
| Non-generic + generic hierarchy | Mirror `IHasIdentity` pattern for typed-ID-bearing interfaces | Proven pattern; enables runtime bridging |
| Accountability scope | CreatedBy + ModifiedBy only | Deletion tracking belongs to `ISoftDeletable`; graveyard tables have their own independent data model |
| Deletion fields on ISoftDeletable | ISoftDeletable owns `DeletedAtUtc` + `DeletedBy` | Clean separation: IAccountable tracks creation/modification, ISoftDeletable tracks deletion |
| ISoftDeletable is standalone | No inheritance from ITimestamped or IAccountable | Composition over inheritance — consumers combine interfaces as needed; more flexible than forced coupling |
| Nullable timestamps | `CreatedAtUtc?` and `ModifiedAtUtc?` are nullable | `null` clearly signals the entity has not yet completed a persistence round-trip; infrastructure sets timestamps on first save |
| ITimestamped / IHasTimestamps separation | Separate interfaces: domain (get-only) vs memento (get/set) | Domain entities expose read-only state via `{ get; private set; }`; mementos need public setters for infrastructure auto-population |
| IHasVersionInfo / IHasVersionHistory | Memento-side only | Versioning is purely an infrastructure concern |
| ITenantScoped generic | `where TTenantId : ITypedId` | Consumers may define their own tenant ID type |
| TenantId location | `Yaf.Domain` framework-provided default | Convenience; not mandatory |
| TActorId discovery | Runtime reflection with cached compiled factories | Avoids extra type parameter on Entity/AggregateRoot |
| ValueObject exclusion | These interfaces are for entities and aggregates only | ValueObjects have no identity or lifecycle |

### DIM Pattern: Domain-Side vs Memento-Side

The domain-side interfaces (`IAccountable<TActorId>`, `ISoftDeletable<TActorId>`, `ITenantScoped<TTenantId>`) use **typed IDs** as the type parameter (`where TActorId : ITypedId`), while the memento-side interfaces (`IHasAccountability<T>`, `IHasSoftDelete<T>`, `IHasTenantId<T>`) use **primitives** (`where T : IEquatable<T>`). This mirrors the domain/infrastructure boundary:

- **Domain side**: `TActorId? CreatedBy` — rich typed ID (e.g., `UserId`), nullable before first save
- **Memento side**: `Guid? CreatedBy` — flattened primitive, nullable
- **Bridging**: MementoHelper reads from entity's typed properties via compiled delegates, extracts primitives via `ITypedId.BoxedValue`, writes to memento's boxed properties; reverse on Restore

This diverges from `IHasIdentity<T>` which is memento-only and uses the primitive directly. The difference is intentional — domain interfaces need typed IDs for compile-time safety, mementos need primitives for ORM mapping.

### Property Access Convention

| Side | Pattern | Example |
|------|---------|---------|
| Domain interface | `{ get; }` — read-only contract | `DateTimeOffset? CreatedAtUtc { get; }` |
| Domain entity implementation | `{ get; private set; }` — settable internally | `public DateTimeOffset? CreatedAtUtc { get; private set; }` |
| Memento interface | `{ get; set; }` — fully mutable | `DateTimeOffset? CreatedAtUtc { get; set; }` |
| Memento implementation | `{ get; set; }` — public accessors | `public DateTimeOffset? CreatedAtUtc { get; set; }` |

During Restore/Hydrate, the base class sets domain entity properties (which have `private set`) via compiled expression-tree-based property setters, cached per type. This is the same approach used for identity restoration.

### Deletion Lifecycle

```
┌──────────────────────┬───────────────────────────────────────────────┐
│ Interfaces           │ Delete behavior                               │
├──────────────────────┼───────────────────────────────────────────────┤
│ Neither              │ Hard delete (row gone)                        │
│ ISoftDeletable       │ Soft delete (DeletedAtUtc set, row stays)     │
│ IHasVersionHistory         │ Graveyard (row archived to DeletedObjects)    │
│ Both                 │ Soft delete first; optional permanent delete  │
│                      │ moves to graveyard later                      │
└──────────────────────┴───────────────────────────────────────────────┘
```

- **ISoftDeletable** (standalone): `DeletedAtUtc` set, row stays in main table; `DeletedBy` set if generic variant used
- **IHasVersionHistory** (memento-only, without ISoftDeletable): row removed from main table, serialized to `DeletedObjects` graveyard (independent data model)
- **Both**: soft-delete first; permanent delete moves row to graveyard
- **Neither**: hard delete — row is gone
- EF Core global query filter `WHERE DeletedAtUtc IS NULL` auto-applied to all `ISoftDeletable` mementos (via `IHasSoftDelete`)
- Soft-deleted entities can be un-deleted by clearing `DeletedAtUtc` (and `DeletedBy`)

### ADR Updates Required

The [Cross-Cutting Infrastructure ADR](../adr/infrastructure/20260324-1249-cross-cutting-infrastructure.md) needs amending:

1. **Remove** `DeletedBy`/`DeletedAtUtc` from `IAccountable` and `ITimestamped` sections
2. **Add** `ISoftDeletable<TActorId>` as a new cross-cutting concern — explain why this opt-in marker approach differs from the rejected per-table soft-delete (opt-in via interface, global query filter, not forced on all entities)
3. **Update** `DeletedAtUtc` description: it lives on `ISoftDeletable`, not on graveyard records (graveyard has its own independent data model)
4. **Update** `IHasVersionInfo` description to clarify memento-only usage
5. **Reconcile** "non-versionable aggregates are hard-deleted" with the new `ISoftDeletable` path
6. **Update** activation summary table with all interfaces
7. **Update** `Activator.CreateInstance` reference to reflect compiled expression approach

## Technical Considerations

### IAccountable — Domain Side

```
IAccountable                              // Non-generic marker — enables runtime discovery
                                          // (no boxing, no properties)

IAccountable<TActorId> : IAccountable where TActorId : ITypedId
├── TActorId? CreatedBy { get; }          // Null before first persistence round-trip
├── TActorId? ModifiedBy { get; }         // Null until first modification
```

- **No boxing on domain-side interfaces** — typed IDs already implement `ITypedId.BoxedValue` for bridging; boxing lives on the memento side only (`IHasAccountability`)
- **Both `CreatedBy` and `ModifiedBy` are nullable** — `null` means the entity has not yet completed a persistence round-trip (same semantics as `CreatedAtUtc?`)
- Infrastructure auto-populates from `IIdentityContextProvider` during `SaveChanges`
- **Infrastructure must throw** if user context is not provided when saving an `IAccountable` entity — the actor identity is not optional at persistence time
- No deletion fields — those belong to `ISoftDeletable`
- MementoHelper discovers `IAccountable` marker at runtime, then uses compiled delegates to read typed `CreatedBy`/`ModifiedBy` properties and extract primitives via `ITypedId.BoxedValue`

### IAccountable — Memento Side

```
IHasAccountability (non-generic base)
├── Type ActorIdType { get; }
├── object? BoxedCreatedBy { get; set; }  // Nullable (null before first save)
├── object? BoxedModifiedBy { get; set; }

IHasAccountability<T> : IHasAccountability where T : IEquatable<T>
├── T? CreatedBy { get; set; }
├── T? ModifiedBy { get; set; }
├── DIM: ActorIdType => typeof(T)
├── DIM: BoxedCreatedBy
│   get => CreatedBy
│   set => CreatedBy = value is null ? default : value is T typed
│       ? typed
│       : throw new ArgumentException($"Expected {typeof(T).Name}, got {value?.GetType().Name ?? "null"}.")
├── DIM: BoxedModifiedBy
│   get => ModifiedBy
│   set => ModifiedBy = value is null ? default : value is T typed
│       ? typed
│       : throw new ArgumentException(...)
```

Mirrors `IHasIdentity<T>` DIM pattern (see `src/Yaf.Domain/Interfaces/IHasIdentity.cs:38-44`). Uses `value is T typed` pattern matching with actionable error messages. Both `BoxedCreatedBy` and `BoxedModifiedBy` are nullable — null before first persistence.

### ITimestamped — Domain Side (get-only)

```
ITimestamped
├── DateTimeOffset? CreatedAtUtc { get; }
├── DateTimeOffset? ModifiedAtUtc { get; }
```

- **Both properties are nullable** — `null` means the entity has not yet completed a persistence round-trip
- Infrastructure sets `CreatedAtUtc` on first save and `ModifiedAtUtc` on every subsequent save
- No deletion fields — those belong to `ISoftDeletable`
- Entity implements with `{ get; private set; }`

### IHasTimestamps — Memento Side (get/set)

```
IHasTimestamps
├── DateTimeOffset? CreatedAtUtc { get; set; }
├── DateTimeOffset? ModifiedAtUtc { get; set; }
```

- Settable for infrastructure auto-population during `SaveChanges`
- No type flattening needed — `DateTimeOffset` maps directly to EF Core

### ISoftDeletable — Domain Side

```
ISoftDeletable                            // Non-generic marker — pure marker, no properties
                                          // (consistent with IAccountable, ITenantScoped)

ISoftDeletable<TActorId> : ISoftDeletable where TActorId : ITypedId
├── DateTimeOffset? DeletedAtUtc { get; } // Null if not deleted; non-null = soft-deleted
├── TActorId? DeletedBy { get; }          // Who performed the deletion (null if not deleted)
```

- **Pure marker on non-generic base** — consistent with `IAccountable` and `ITenantScoped`
- **No boxing on domain-side** — typed IDs already implement `ITypedId.BoxedValue`; boxing lives on the memento side (`IHasSoftDelete`)
- `DeletedAtUtc != null` means the entity is soft-deleted
- Infrastructure checks `entity is ISoftDeletable` (marker) to activate soft-delete behavior
- `ISoftDeletable<TActorId>` adds domain-level access to deletion info (who and when)
- Entity implements with `{ get; private set; }`
- Typical composition: `class Order : ITimestamped, IAccountable<UserId>, ISoftDeletable<UserId>` — but any subset is valid

### ISoftDeletable — Memento Side

```
IHasSoftDelete (non-generic base)
├── DateTimeOffset? DeletedAtUtc { get; set; }
├── Type? ActorIdType { get; }
├── object? BoxedDeletedBy { get; set; }

IHasSoftDelete<T> : IHasSoftDelete where T : IEquatable<T>
├── T? DeletedBy { get; set; }
├── DIM: ActorIdType => typeof(T)
├── DIM: BoxedDeletedBy
│   get => DeletedBy
│   set => DeletedBy = value is null ? default : value is T typed
│       ? typed
│       : throw new ArgumentException($"Expected {typeof(T).Name}, got {value?.GetType().Name ?? "null"}.")
```

- EF Core global query filter: `WHERE DeletedAtUtc IS NULL` auto-applied
- Bypass mechanism for admin queries (similar to tenant filter bypass)

### ITenantScoped — Domain Side

```
ITenantScoped                             // Non-generic marker — enables runtime discovery

ITenantScoped<TTenantId> : ITenantScoped where TTenantId : ITypedId
├── TTenantId TenantId { get; }           // Typed tenant identifier
```

- **No boxing on domain-side** — `TenantId` already implements `ITypedId.BoxedValue`; boxing lives on the memento side (`IHasTenantId`)
- Get-only; set at creation time, immutable by convention
- Entity constructors/factory methods are responsible for accepting and setting `TenantId` at creation time
- The memento auto-handling only covers the persistence round-trip (Snapshot/Restore/Hydrate)
- Framework provides `TenantId : TypedId<Guid>` as a convenience default

### ITenantScoped — Memento Side

```
IHasTenantId (non-generic base)
├── Type TenantIdType { get; }
├── object BoxedTenantId { get; set; }

IHasTenantId<T> : IHasTenantId where T : IEquatable<T>
├── T TenantId { get; set; }
├── DIM: TenantIdType => typeof(T)
├── DIM: BoxedTenantId
│   get => TenantId!
│   set => TenantId = value is T typed
│       ? typed
│       : throw new ArgumentException($"Expected {typeof(T).Name}, got {value?.GetType().Name ?? "null"}.")
```

- Infrastructure auto-applies EF Core global query filter: `WHERE TenantId = @current`

### IHasVersionInfo — Memento Side Only

```
IHasVersionInfo
├── Guid Version { get; set; }
```

- Memento-only — domain objects do not access the version property
- Infrastructure auto-configures as EF Core concurrency token
- On save: infrastructure generates a new `Guid`
- On conflict: `DbUpdateConcurrencyException` thrown

### IHasVersionHistory — Memento Side Only

```
IHasVersionHistory
```

- Independent marker — does NOT extend `IHasVersionInfo`
- Can be used with or without `IHasVersionInfo` (concurrency and version history are separate concerns)
- Activates: memento snapshot storage on each UoW commit + graveyard on delete
- Graveyard has its own independent data model (not reusing ISoftDeletable fields)
- One snapshot per UoW commit (not intermediate saves)
- Snapshots are tenant-scoped if the aggregate is tenant-scoped

### TenantId Type

```csharp
/// <summary>
/// Framework-provided tenant identifier. Consumers may use this or define their own
/// tenant ID type by extending <see cref="TypedId{T}"/>.
/// </summary>
public record TenantId(Guid Value) : TypedId<Guid>(Value);
```

### MementoHelper Evolution

The existing `MementoHelper<TId, TSelf, TMemento>` handles identity bridging via runtime `is` checks. The same approach extends to the new interfaces:

1. **Snapshot direction** (entity -> memento): reads from entity's non-generic interface, writes to memento's interface. This is a **pure copy operation** — MementoHelper does not generate or compute values.
2. **Restore/Hydrate direction** (memento -> entity): reads from memento's interface, writes to entity via compiled property setters targeting `private set` properties (cached per type)

**Typed ID factories:** For `IAccountable`, `ISoftDeletable`, and `ITenantScoped`, the helper discovers the typed ID type at runtime from the non-generic interface's `ActorIdType`/`TenantIdType`, builds a compiled `Func<object, ITypedId>` factory, and caches it. Caching uses a `ConcurrentDictionary<Type, Delegate>` keyed on the concrete typed ID type, shared across all `MementoHelper` instantiations.

**Property setter compilation:** For setting read-only domain-side properties during Restore/Hydrate, the helper compiles expression-tree-based property setters targeting the entity's `private set` accessor. Properties are discovered by matching the interface property name on the concrete type. Entities must use **implicit** interface implementation (e.g., `public TActorId? CreatedBy { get; private set; }`) — explicit implementation (`TActorId? IAccountable<TActorId>.CreatedBy`) is not supported for auto-handling. If a property lacks a setter (e.g., `{ get; }` without `private set`), the helper throws with an actionable message (same pattern as the existing `_idFactory` null check).

**Consistent domain interface pattern:** All non-generic domain bases (`IAccountable`, `ITenantScoped`, `ISoftDeletable`) are pure markers with no properties. All typed properties live on the generic variants. MementoHelper discovers the marker at runtime, then uses reflection to find the generic variant and read typed properties via compiled delegates.

**Graceful degradation:** If an entity implements an interface but the memento does not implement the matching memento-side interface, auto-handling silently skips — exactly as identity handling does today.

### Base Class Changes (Entity & AggregateRoot)

The `Snapshot`, `Restore`, and `Hydrate` methods expand:

```
Snapshot(memento):
  1. WriteIdentity(memento, Id)            // existing
  2. WriteAccountability(memento, this)     // new — if IAccountable && IHasAccountability
  3. WriteTimestamps(memento, this)         // new — if ITimestamped && IHasTimestamps
  4. WriteSoftDelete(memento, this)         // new — if ISoftDeletable && IHasSoftDelete
  5. WriteTenantId(memento, this)           // new — if ITenantScoped && IHasTenantId
  6. SnapshotCore(memento)                 // existing — consumer's custom state

Restore(memento):
  1. ReadIdentity(memento) -> set Id        // existing
  2. ReadAccountability(memento)            // new
  3. ReadTimestamps(memento)                // new
  4. ReadSoftDelete(memento)                // new
  5. ReadTenantId(memento)                  // new
  6. RestoreCore(memento)                   // existing
  7. ThrowIfInvalid()                       // existing

Hydrate(memento):
  // Same order as Restore
```

Note: `IHasVersionInfo`/`IHasVersionHistory` are memento-only infrastructure concerns — no base class handling needed.

### File Organization

| File | Location | Description |
|------|----------|-------------|
| `IAccountable.cs` | `src/Yaf.Domain/Interfaces/` | Non-generic + generic domain interface (CreatedBy, ModifiedBy) |
| `ITimestamped.cs` | `src/Yaf.Domain/Interfaces/` | Domain interface (CreatedAtUtc?, ModifiedAtUtc?) |
| `ISoftDeletable.cs` | `src/Yaf.Domain/Interfaces/` | Non-generic + generic; standalone (DeletedAtUtc?, DeletedBy?) |
| `ITenantScoped.cs` | `src/Yaf.Domain/Interfaces/` | Non-generic + generic domain interface |
| `IHasAccountability.cs` | `src/Yaf.Domain/Interfaces/` | Non-generic + generic memento interface |
| `IHasTimestamps.cs` | `src/Yaf.Domain/Interfaces/` | Memento interface (get/set) |
| `IHasSoftDelete.cs` | `src/Yaf.Domain/Interfaces/` | Non-generic + generic memento interface |
| `IHasTenantId.cs` | `src/Yaf.Domain/Interfaces/` | Non-generic + generic memento interface |
| `IHasVersionInfo.cs` | `src/Yaf.Domain/Interfaces/` | Memento-only concurrency interface |
| `IHasVersionHistory.cs` | `src/Yaf.Domain/Interfaces/` | Memento-only independent marker (activates snapshots + graveyard) |
| `TenantId.cs` | `src/Yaf.Domain/` | Framework-provided typed ID |
| `MementoHelper.cs` | `src/Yaf.Domain/Helpers/` | Extended with new Write/Read methods |
| `Entity{TId,TSelf,TMemento}.cs` | `src/Yaf.Domain/` | Updated Snapshot/Restore/Hydrate |
| `AggregateRoot{TId,TSelf,TMemento}.cs` | `src/Yaf.Domain/` | Updated Snapshot/Restore/Hydrate |

## Acceptance Criteria

### Domain Interfaces

- [x] `IAccountable` non-generic marker (no properties, no boxing)
- [x] `IAccountable<TActorId>` generic with `CreatedBy?`, `ModifiedBy?`; `TActorId : ITypedId`
- [x] `ITimestamped` with `CreatedAtUtc?`, `ModifiedAtUtc?` (both nullable, get-only, no deletion fields)
- [x] `ISoftDeletable` non-generic marker (no properties, no boxing — consistent with IAccountable, ITenantScoped)
- [x] `ISoftDeletable<TActorId>` generic with `DeletedAtUtc?`, `TActorId? DeletedBy`; `TActorId : ITypedId`
- [x] `ITenantScoped` non-generic marker (no properties, no boxing)
- [x] `ITenantScoped<TTenantId>` generic with `TTenantId TenantId`; `TTenantId : ITypedId`

### Memento Interfaces

- [x] `IHasAccountability` / `IHasAccountability<T>` — CreatedBy?, ModifiedBy? (all nullable, no deletion fields)
- [x] `IHasTimestamps` — `CreatedAtUtc?`, `ModifiedAtUtc?` (both nullable, get/set)
- [x] `IHasSoftDelete` / `IHasSoftDelete<T>` — `DeletedAtUtc?`, `DeletedBy?`
- [x] `IHasTenantId` / `IHasTenantId<T>` — non-generic + generic with DIM
- [x] `IHasVersionInfo` with `Guid Version` get/set (memento-only)
- [x] `IHasVersionHistory` independent marker (memento-only, does not extend IHasVersionInfo)

### Framework Types

- [x] `TenantId` record: `public record TenantId(Guid Value) : TypedId<Guid>(Value)` with XML doc

### Auto-Handling in Base Classes

- [x] `Entity<TId, TSelf, TMemento>` auto-handles accountability, timestamps, soft-delete, and tenant in Snapshot/Restore/Hydrate
- [x] `AggregateRoot<TId, TSelf, TMemento>` same auto-handling
- [x] Auto-handling silently skips when entity implements interface but memento does not (graceful degradation)
- [x] Auto-handling silently skips when entity does not implement the interface (backward compatible)
- [x] Existing tests continue to pass without modification
- [x] `IHasVersionInfo` and `IHasVersionHistory` are memento-only — no auto-handling in base classes

### MementoHelper

- [x] `WriteAccountability` / `ReadAccountability` with compiled actor ID factory (runtime discovery, `ConcurrentDictionary` cache)
- [x] `WriteTimestamps` / `ReadTimestamps` — direct nullable DateTimeOffset copy via compiled property setters
- [x] `WriteSoftDelete` / `ReadSoftDelete` — DateTimeOffset? + actor ID bridging
- [x] `WriteTenantId` / `ReadTenantId` — typed ID <-> primitive bridging with compiled factory
- [x] Actionable error messages when entity property lacks required `private set` accessor

### Testing

- [x] Round-trip Snapshot/Restore for each domain interface individually
- [x] Round-trip Snapshot/Restore for all domain interfaces combined on a single entity
- [x] Graceful degradation: entity implements interface, memento does not — silent skip
- [x] Nullable timestamps: `CreatedAtUtc` null (not yet persisted), `ModifiedAtUtc` null
- [x] Non-null timestamps: both set after simulated persistence round-trip
- [x] `CreatedBy` null before persistence, non-null after Restore with populated memento
- [x] `ModifiedBy` null and non-null cases
- [x] ISoftDeletable: `DeletedAtUtc` + `DeletedBy` round-trip when soft-deleted
- [x] ISoftDeletable: `DeletedAtUtc` null when not deleted
- [x] ISoftDeletable works standalone (without ITimestamped or IAccountable)
- [x] ISoftDeletable composes with ITimestamped + IAccountable on same entity
- [x] `TenantId` round-trip: typed -> primitive -> typed
- [x] Actor ID round-trip: typed -> primitive -> typed (compiled factory)
- [x] Backward compatibility: existing entities without new interfaces work identically
- [x] Hydrate round-trip for each interface
- [x] `IHasVersionInfo` on memento: `Version` property round-trips
- [x] `IHasVersionHistory` marker: verifiable via `is IHasVersionHistory` check
- [x] Missing `private set` on entity property: actionable error thrown
- [x] All public API has XML doc comments (CS1591 enforced)

### ADR Updates

- [x] Amend cross-cutting infrastructure ADR:
  - Remove `DeletedBy`/`DeletedAtUtc` from `IAccountable` and `ITimestamped` sections
  - Add `ISoftDeletable<TActorId>` as a new standalone cross-cutting concern
  - Explain why opt-in `ISoftDeletable` differs from the rejected per-table soft-delete approach
  - Update `DeletedAtUtc` description: lives on `ISoftDeletable`, not on graveyard records
  - Reconcile "non-versionable aggregates are hard-deleted" with `ISoftDeletable` path
  - Update `IHasVersionInfo` to clarify memento-only usage
  - Update `Activator.CreateInstance` reference to compiled expression approach
  - Update activation summary table with all interfaces

## Dependencies & Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| Runtime typed ID discovery adds reflection overhead | Low — compiled factories cached in `ConcurrentDictionary`; one-time cost | Same pattern proven for identity |
| Compiled property setters for `private set` | Low — expression trees with `BindingFlags.NonPublic` | Actionable error if setter missing; .NET 10 supports this reliably |
| Code duplication between Entity and AggregateRoot | Low — MementoHelper centralizes logic | Consider shared base if duplication grows |
| ISoftDeletable without IAccountable | Consumer has `DeletedAtUtc` but no `DeletedBy` | Valid use case; document that `ISoftDeletable<TActorId>` adds actor tracking |
| Soft-delete query filter bypass | Deferred — infrastructure concern | Define interface here; implement in infrastructure layer |

## References & Research

### Internal References
- [Cross-Cutting Infrastructure ADR](../adr/infrastructure/20260324-1249-cross-cutting-infrastructure.md) — accountability, timestamping, encryption, versioning
- [Multi-Tenancy ADR](../adr/infrastructure/20260324-1323-multi-tenancy.md) — ITenantScoped, TenantId, query filters
- [Domain Building Blocks ADR](../adr/domain/20260324-1032-domain-building-blocks.md) — Entity, AggregateRoot, TypedId
- [State Management ADR](../adr/domain/20260324-1104-state-management-memento-pattern.md) — memento pattern, Snapshot/Restore/Hydrate
- [DDD Concepts Brainstorm](../brainstorms/20260322-2036-yaf-ddd-concepts-brainstorm.md) — accountability (14), timestamping (15), multi-tenancy (22)
- [Identity Bridging Solution](../solutions/design-patterns/ddd-building-blocks-identity-bridging-and-validation.md) — compiled factories, DIM patterns
- [Previous Plan (pattern reference)](20260325-2111-feat-typedid-entity-aggregateroot-building-blocks-plan.md) — TypedId, Entity, AggregateRoot implementation

### Existing Code (pattern reference)
- `src/Yaf.Domain/Interfaces/IHasIdentity.cs` — non-generic + generic DIM pattern
- `src/Yaf.Domain/Interfaces/ITypedId.cs` — ITypedId constraint pattern
- `src/Yaf.Domain/Helpers/MementoHelper.cs` — compiled factory + runtime interface bridging
- `src/Yaf.Domain/Entity{TId,TSelf,TMemento}.cs` — Snapshot/Restore/Hydrate orchestration
- `src/Yaf.Domain/AggregateRoot{TId,TSelf,TMemento}.cs` — same pattern for aggregates
