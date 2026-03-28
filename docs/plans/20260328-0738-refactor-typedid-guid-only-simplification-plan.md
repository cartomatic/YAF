---
title: "Simplify TypedId to Guid-only backing type"
type: refactor
status: completed
date: 2026-03-28
depends-on: none
blocks: 20260328-XXXX-feat-domain-event-interfaces-plan
---

# refactor: Simplify TypedId to Guid-only backing type

## Overview

Remove the generic backing type parameter from `TypedId<T>` and fix it to `Guid`. This eliminates the generic type parameter cascade through memento interfaces, collapses the two-tier (non-generic base + generic variant) memento interface hierarchy, and dramatically simplifies the helper/bridge machinery. External systems with non-Guid identifiers remap at the anti-corruption layer boundary.

## Problem Statement / Motivation

`TypedId<T> where T : IEquatable<T>` was designed to support arbitrary backing types (Guid, int, string, long). In practice:

1. **Every concrete ID in the framework and tests uses `Guid`** — `TenantId(Guid Value) : TypedId<Guid>(Value)` is the only shipped type; all test IDs are Guid-backed.
2. **The generic backing type creates a cascade of complexity:**
   - Memento interfaces need a two-tier hierarchy (non-generic base with `Type` property + boxed accessors, generic variant with typed properties + DIM implementations) because the backing type is unknown at compile time.
   - `TypedIdBridge` uses reflection + compiled expressions to discover backing types and build factories at runtime.
   - `BoxingHelper` exists solely to handle the unboxing of unknown backing types.
   - `MementoHelper` must runtime-check `IdentityType` compatibility.
3. **The speculative genericity adds complexity without delivering value.** Any real-world system using int/string IDs externally would map them to Guid at the boundary anyway — standard anti-corruption layer practice.

Fixing the backing type to `Guid` removes all this machinery while keeping the type safety of distinct ID types (`OrderId` vs `CustomerId` are still different types).

## Proposed Solution

Fix `TypedId` to always wrap `Guid`. Collapse memento interfaces to single-tier. Simplify helpers. Keep domain-side interface generics for compile-time type safety.

## Technical Considerations

### What stays generic (and why)

Domain-side interfaces keep their type parameters because they serve a **different purpose** — distinguishing which typed ID, not which backing type:

- `IAccountable<TActorId>` — `CreatedBy` is `UserId?`, not `EmployeeId?`
- `ITenantScoped<TTenantId>` — allows custom tenant ID types
- `ISoftDeletable<TActorId>` — same as accountability
- `Entity<TId>`, `AggregateRoot<TId>` — `TId` distinguishes `OrderId` from `InvoiceId`

These generics remain valuable because they prevent accidentally mixing ID types at compile time.

### What loses its generic (and why)

The **backing type** generic `T` in `TypedId<T>` and all memento-side interfaces, because `T` is always `Guid`:

| Before | After | Reason |
|--------|-------|--------|
| `TypedId<T> where T : IEquatable<T>` | `TypedId` (Guid) | Backing type always Guid |
| `ITypedId<out T> : ITypedId` | removed | Single `ITypedId` suffices |
| `ITypedId { static abstract Type IdentityType; object BoxedValue; }` | `ITypedId { Guid Value; }` | Type is known, no boxing needed |
| `IHasIdentity` + `IHasIdentity<T>` | `IHasIdentity { Guid? Id; }` | No type ambiguity |
| `IHasAccountability` + `IHasAccountability<T>` | `IHasAccountability { Guid? CreatedBy; Guid? ModifiedBy; }` | No type ambiguity |
| `IHasTenantId` + `IHasTenantId<T>` | `IHasTenantId { Guid? TenantId; }` | No type ambiguity |
| `IHasSoftDelete` + `IHasSoftDelete<T>` | `IHasSoftDelete { DateTimeOffset? DeletedAtUtc; Guid? DeletedBy; }` | No type ambiguity |

### Helper simplification

| Helper | Before | After |
|--------|--------|-------|
| `BoxingHelper` | Generic unboxing for unknown `T` | **Remove entirely** — no boxing needed |
| `TypedIdBridge<TSelf>` | Reflection-based factory to discover backing type, build constructor delegate, box/unbox via `ITypedId.BoxedValue` | Simplified — factory always takes `Guid`, reads `.Value` directly. Still needed to bridge typed IDs to plain Guids. |
| `TypedIdFactoryCache` | Discovers `ITypedId<T>`, resolves backing type, finds matching constructor | Simplified — always looks for `ctor(Guid)` |
| `MementoHelper<TId,TSelf,TMemento>` | Runtime `IdentityType` checks, bridge-based mapping | Direct `Guid` mapping for identity; simplified bridge for typed ID properties |
| `ReflectionHelper` | Compiled property readers/writers | **Unchanged** — still needed for non-public setters |

### ADR impact

- **Domain Building Blocks ADR** (`20260324-1032`): Update TypedId section — remove `T` parameter, note Guid-only.
- **Cross-Cutting Infrastructure ADR** (`20260324-1249`): Update memento interface descriptions — single-tier, no boxing.
- **Domain Events ADR** (`20260324-1113`): No change needed — this is a prerequisite simplification.

### Entity memento auto-mapping changes

`Entity<TId, TSelf, TMemento>` currently uses:
- `TypedIdBridge` for accountability (`CreatedBy`, `ModifiedBy`), soft-delete (`DeletedBy`), and tenant
- Boxed accessors (`ha.BoxedCreatedBy`, `hsd.BoxedDeletedBy`) on memento interfaces

After simplification:
- Memento interfaces expose `Guid?` directly — no boxing layer
- `TypedIdBridge` reads `typedId.Value` (Guid) and writes via `new ConcreteId(guid)` factory
- Auto-mapping methods become more straightforward

## Acceptance Criteria

### Functional Requirements

- [x] `TypedId` is a non-generic abstract record wrapping `Guid`
- [x] `ITypedId` has `Guid Value { get; }` — no `IdentityType`, no `BoxedValue`
- [x] `ITypedId<out T>` is removed
- [x] Each memento interface is a single tier with `Guid?` properties — no boxing accessors, no `Type` properties
- [x] `BoxingHelper` is removed
- [x] `TypedIdBridge` is simplified (reads `.Value`, factory always takes `Guid`)
- [x] `TypedIdFactoryCache` simplified (always looks for `ctor(Guid)`)
- [x] `MementoHelper` simplified (no `IdentityType` checks)
- [x] Domain-side interfaces keep generics: `IAccountable<TActorId>`, `ITenantScoped<TTenantId>`, `ISoftDeletable<TActorId>` with `where T : ITypedId`
- [x] `TenantId` updated: `TenantId(Guid Value) : TypedId(Value)`
- [x] `Entity<TId, TSelf, TMemento>` auto-mapping works correctly with simplified interfaces
- [x] All 81 tests pass (updated for new signatures; 9 removed with BoxingHelper and non-Guid TypedId tests)
- [x] Consumer usage pattern: `public record OrderId(Guid Value) : TypedId(Value);`

### Non-Functional Requirements

- [x] No new NuGet dependencies introduced
- [x] Public API surface is smaller (fewer types, fewer members)
- [x] Reflection/compiled expression usage reduced

## Success Metrics

- Zero generic backing type parameters remain in the TypedId/memento layer
- Memento interface count reduced (from ~10 interfaces to ~5)
- `BoxingHelper` deleted
- All 90 existing tests updated and passing
- No runtime behavior changes for consumers (API shape changes, behavior identical)

## Dependencies & Risks

**Dependencies:** None — this is a foundational refactor with no external dependencies.

**Risks:**

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Missed boxing usage in a test or helper | Low | Low | Compiler will catch — removing `BoxedValue` etc. causes build errors at all call sites |
| `Entity<TId, TSelf, TMemento>` auto-mapping regression | Medium | High | Existing memento round-trip tests cover this; run full suite after each change |
| Consumers using non-Guid TypedId (speculative) | Very low | N/A | No shipped consumers exist yet; only framework + tests |

## Implementation Notes

### Suggested order

1. **ITypedId + TypedId** — change the root types, let the compiler guide everything else
2. **Memento interfaces** — collapse two-tier → single-tier (IHasIdentity, IHasAccountability, IHasTenantId, IHasSoftDelete)
3. **Remove BoxingHelper** — delete the file
4. **Simplify TypedIdBridge + TypedIdFactoryCache** — always Guid backing
5. **Simplify MementoHelper** — remove IdentityType checks, direct Guid mapping
6. **Update Entity{TId,TSelf,TMemento}** — use simplified interfaces in auto-mapping
7. **Update TenantId** — `TypedId<Guid>` → `TypedId`
8. **Update tests** — all test ID types, memento types, and assertions
9. **Update ADRs** — reflect the simplification decision

### Files affected

**Source (modify):**
- `src/Yaf.Domain/Interfaces/ITypedId.cs`
- `src/Yaf.Domain/TypedId.cs`
- `src/Yaf.Domain/Interfaces/IHasIdentity.cs`
- `src/Yaf.Domain/Interfaces/IHasAccountability.cs`
- `src/Yaf.Domain/Interfaces/IHasTenantId.cs`
- `src/Yaf.Domain/Interfaces/IHasSoftDelete.cs`
- `src/Yaf.Domain/Helpers/TypedIdBridge.cs`
- `src/Yaf.Domain/Helpers/MementoHelper.cs`
- `src/Yaf.Domain/Entity{TId,TSelf,TMemento}.cs`
- `src/Yaf.Domain/TenantId.cs`

**Source (delete):**
- `src/Yaf.Domain/Helpers/BoxingHelper.cs`

**Source (unchanged):**
- `src/Yaf.Domain/Entity.cs` — `Entity<TId> where TId : ITypedId` still valid
- `src/Yaf.Domain/AggregateRoot.cs` — unchanged
- `src/Yaf.Domain/Interfaces/IAccountable.cs` — keeps `<TActorId>` generic
- `src/Yaf.Domain/Interfaces/ITenantScoped.cs` — keeps `<TTenantId>` generic
- `src/Yaf.Domain/Interfaces/ISoftDeletable.cs` — keeps `<TActorId>` generic
- `src/Yaf.Domain/Interfaces/ITimestamped.cs` — no typed IDs involved
- `src/Yaf.Domain/Interfaces/IHasTimestamps.cs` — no typed IDs involved
- `src/Yaf.Domain/Interfaces/IHasVersionInfo.cs` — no typed IDs involved
- `src/Yaf.Domain/Interfaces/IHasVersionHistory.cs` — marker only
- `src/Yaf.Domain/Helpers/ReflectionHelper.cs` — still needed for compiled property access

**Tests (modify all):**
- `tests/Yaf.Domain.Tests/*.cs` — update all test ID types from `TypedId<Guid>` to `TypedId`

**Docs (modify):**
- `docs/adr/domain/20260324-1032-domain-building-blocks.md`
- `docs/adr/infrastructure/20260324-1249-cross-cutting-infrastructure.md`

## References & Research

### Internal References

- `src/Yaf.Domain/TypedId.cs` — current generic implementation
- `src/Yaf.Domain/Interfaces/ITypedId.cs` — current two-tier interface
- `src/Yaf.Domain/Helpers/TypedIdBridge.cs` — boxing/unboxing bridge
- `src/Yaf.Domain/Helpers/BoxingHelper.cs` — generic unboxing (to be removed)
- `src/Yaf.Domain/Entity{TId,TSelf,TMemento}.cs` — auto-mapping consumer
- `docs/solutions/design-patterns/cross-cutting-interfaces-dim-boxing-and-typed-id-bridging.md` — documents current DIM/boxing pattern

### Related Work

- Previous plan: `docs/plans/20260327-0936-feat-cross-cutting-domain-interfaces-plan.md` — established the pattern being simplified
- Previous plan: `docs/plans/20260325-2111-feat-typedid-entity-aggregateroot-building-blocks-plan.md` — original TypedId design
- Blocked plan: domain event interfaces (to be written as Plan 2) — depends on this simplification
