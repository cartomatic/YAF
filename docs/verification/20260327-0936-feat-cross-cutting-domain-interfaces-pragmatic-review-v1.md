# Pragmatic Code Review: Cross-Cutting Domain Interfaces

**Date:** 2026-03-27
**Scope:** `src/Yaf.Domain/` -- feat/cross-cutting-domain-interfaces branch
**Status:** Warning -- Over-Engineered for Current Scale

---

## Executive Summary

The cross-cutting interfaces PR adds 2,711 lines across 28 files to a framework library with **zero consumers**. The design is technically sound and internally consistent, but the implementation exhibits clear over-engineering for the project's maturity. The core issue: a reflection-heavy, expression-tree-compiled memento bridge is solving a problem that could be handled with straightforward abstract methods at a fraction of the complexity. The interface pair pattern (domain-side + memento-side with boxing bridge) is architecturally defensible but premature for a library nobody is using yet.

| Severity | Count |
|----------|-------|
| High     | 2     |
| Medium   | 3     |
| Low      | 2     |

---

## 1. Complexity Assessment

**Project scale:** Early-stage framework library, zero consumers, 2 months old, ~1,585 LOC in production code, ~1,847 LOC in tests.

**Complexity indicators:**
- 347-line `MementoHelper` using `System.Linq.Expressions`, `ConcurrentDictionary`, `RuntimeHelpers.GetUninitializedObject`, and compiled expression trees
- 69-line `ReflectionHelper` building compiled property readers/writers via expression trees
- 10 new interfaces organized as domain/memento pairs with non-generic + generic hierarchy
- Boxing/unboxing bridge layer (`BoxedId`, `BoxedCreatedBy`, `BoxedDeletedBy`, `BoxedTenantId`) to cross the type erasure boundary
- Default interface method (DIM) implementations for boxing semantics

**Assessment: HIGH complexity relative to project scale.** This is infrastructure-grade code (compiled delegates, concurrent caches, expression trees) in a framework that has never been consumed. The patterns are appropriate for a mature, performance-critical framework. They are premature for a library in its formative stage where the API surface is still being discovered.

---

## 2. Over-Engineering Findings

### HIGH-1: Reflection + Expression Trees for Cross-Cutting Property Access

**Evidence:** `src/Yaf.Domain/Helpers/MementoHelper.cs` (347 lines), `src/Yaf.Domain/Helpers/ReflectionHelper.cs` (69 lines)

**Problem:** The `MementoBridge` inner class uses reflection to discover interface implementations at runtime, then compiles expression-tree delegates for property read/write. This is a performance optimization pattern borrowed from serialization libraries (System.Text.Json, EF Core). For a framework with zero consumers and no measured performance bottleneck, it adds significant cognitive overhead without demonstrated benefit.

The entire `MementoHelper.MementoBridge.Build()` method (lines 257-315) builds compiled delegates for accountability, timestamps, soft-delete, and tenant ID -- all to avoid requiring the consumer to write a few lines of explicit property assignment in their `SnapshotCore`/`RestoreCore`/`HydrateCore` overrides.

**Impact:** This is the single largest source of accidental complexity. If a consumer hits a runtime error in this code, the stack traces will point into compiled expression trees and reflection, making debugging significantly harder. The `TypedIdFactoryCache` (lines 322-345) adds yet another layer of cached compiled constructors.

**Simplification:** Replace the reflection-based bridge with abstract methods or a simpler interface-based approach. The consumer already implements `SnapshotCore`/`RestoreCore`/`HydrateCore`. Adding explicit handling of cross-cutting fields there is minimal boilerplate compared to the debugging cost of the current approach.

**Before (current -- 347 lines of MementoHelper + 69 lines of ReflectionHelper):**
```csharp
// MementoHelper discovers interfaces via reflection, builds compiled delegates
internal static void WriteAccountability(TMemento memento, TSelf entity)
{
    if (entity is not IAccountable || memento is not IHasAccountability hasAccountability)
        return;
    if (Bridge.AccountabilityReader is null)
        return;
    var (createdBy, modifiedBy) = Bridge.AccountabilityReader(entity);
    hasAccountability.BoxedCreatedBy = ExtractPrimitive(createdBy);
    hasAccountability.BoxedModifiedBy = ExtractPrimitive(modifiedBy);
}
```

**After (consumer writes explicit code in SnapshotCore -- zero reflection):**
```csharp
// In the consumer's entity SnapshotCore:
protected override void SnapshotCore(IOrderMemento memento)
{
    memento.Description = Description;
    memento.CreatedBy = CreatedBy?.Value;
    memento.ModifiedBy = ModifiedBy?.Value;
    memento.CreatedAtUtc = CreatedAtUtc;
    memento.ModifiedAtUtc = ModifiedAtUtc;
    memento.DeletedAtUtc = DeletedAtUtc;
    memento.DeletedBy = DeletedBy?.Value;
    memento.TenantId = TenantId?.Value;
}
```

**Estimated impact:** Removes ~416 LOC of infrastructure code (MementoHelper + ReflectionHelper). Eliminates expression tree compilation, reflection, concurrent caches. Consumer adds ~5-10 lines of explicit mapping per entity -- straightforward, debuggable, and discoverable.

**Counterargument acknowledged:** The auto-handling reduces consumer boilerplate and prevents forgetting to map a field. This is a legitimate framework design concern. However, for a library in the "discover the API" phase, simplicity and debuggability outweigh convenience. The auto-handling can be added later when the API stabilizes and real consumers validate the design.

---

### HIGH-2: Entity and AggregateRoot Generic Variants Are Copy-Pasted

**Evidence:** `src/Yaf.Domain/Entity{TId,TSelf,TMemento}.cs` (133 lines) vs `src/Yaf.Domain/AggregateRoot{TId,TSelf,TMemento}.cs` (137 lines)

**Problem:** These two files are functionally identical. A diff (normalizing "Entity" to "AggregateRoot") shows only doc comment differences and one extra paragraph about domain events in the AggregateRoot remarks. The `Snapshot`, `Restore`, `Hydrate` methods are character-for-character the same logic. This is a DRY violation that will cause divergence as features are added.

**Impact:** Every change to the memento orchestration logic must be applied in two places. This is already visible in the 9 refactoring commits on the branch -- each touched both files.

**Simplification:** Extract the shared memento orchestration into a single method or shared base. Since `AggregateRoot<TId>` already extends `Entity<TId>`, the generic variant could do the same:

**Before:**
```
Entity<TId> <-- Entity<TId,TSelf,TMemento>
AggregateRoot<TId> : Entity<TId> <-- AggregateRoot<TId,TSelf,TMemento> (copy-paste of Entity<TId,TSelf,TMemento>)
```

**After (one option):**
```
Entity<TId> <-- Entity<TId,TSelf,TMemento> (memento logic lives here)
AggregateRoot<TId> : Entity<TId> <-- AggregateRoot<TId,TSelf,TMemento> : Entity<TId,TSelf,TMemento>
```

This may require adjusting generic constraints (`TSelf : AggregateRoot<...>` vs `TSelf : Entity<...>`), but the tradeoff is worth exploring to eliminate ~130 lines of duplication.

**Estimated impact:** Removes ~130 lines of duplication. Single source of truth for memento orchestration.

---

### MEDIUM-1: Interface Pair Proliferation (Domain + Memento)

**Evidence:** 10 new interface files forming 5 conceptual pairs:
- `IAccountable` / `IHasAccountability` (domain / memento)
- `ITimestamped` / `IHasTimestamps`
- `ISoftDeletable` / `IHasSoftDelete`
- `ITenantScoped` / `IHasTenantId`
- `IHasVersionInfo` / `IHasVersionHistory` (both memento-side)

Each pair has a non-generic marker + generic variant (e.g., `IAccountable` + `IAccountable<TActorId>`, then `IHasAccountability` + `IHasAccountability<T>`).

**Problem:** This creates a 4-interface matrix for each cross-cutting concern: domain marker, domain generic, memento non-generic (with `object?` boxing), memento generic (with typed properties + DIM boxing bridge). For a consumer, this means understanding 4 interfaces to opt into one concern. The naming convention (`IAccountable` vs `IHasAccountability`) is not immediately obvious -- which goes on the entity and which on the memento?

**Impact:** Moderate cognitive load for framework consumers. The domain/memento split is architecturally motivated (keeping the domain model free of persistence details), but the naming similarity creates confusion. The non-generic markers exist solely for runtime `is` checks in `MementoHelper` -- if the reflection bridge is simplified (see HIGH-1), some of these markers become unnecessary.

**Recommendation:** If HIGH-1 is addressed (removing reflection bridge), the non-generic marker interfaces (`IAccountable`, `ISoftDeletable`, `ITenantScoped`) can be eliminated. The generic interfaces alone are sufficient. This would cut the interface count from 10 to 7.

---

### MEDIUM-2: BoxingHelper + Boxed Properties Pattern

**Evidence:** `src/Yaf.Domain/Helpers/BoxingHelper.cs`, plus `BoxedId`, `BoxedCreatedBy`, `BoxedModifiedBy`, `BoxedDeletedBy`, `BoxedTenantId` properties across memento interfaces.

**Problem:** The boxing bridge exists because `MementoHelper` operates on `object?` to avoid generic type explosion. Each memento interface has both typed properties (`Guid? CreatedBy`) and boxed properties (`object? BoxedCreatedBy`) with DIM implementations that convert between them. This is clever but adds cognitive overhead. The `BoxedX` properties are internal plumbing that consumers see in their intellisense.

**Impact:** Consumers implementing memento interfaces will see both `CreatedBy` (typed) and `BoxedCreatedBy` (boxed) in their IDE. The DIM handles the bridge automatically, so consumers never call the boxed properties directly -- but they are still visible and potentially confusing.

**Recommendation:** If the reflection bridge is removed (HIGH-1), the `Boxed*` properties and `BoxingHelper` become unnecessary. Without the reflection bridge, the consumer maps properties directly in their `SnapshotCore`/`RestoreCore` methods, and the type system handles everything at compile time.

---

### MEDIUM-3: ConcurrentDictionary for TypedId Factory Cache

**Evidence:** `MementoHelper.TypedIdFactoryCache` (lines 322-345)

**Problem:** A `ConcurrentDictionary<Type, Func<object, object>>` caching compiled TypedId constructors. Thread-safety overhead for a cache that is populated exactly once per TypedId type in the application's lifetime, during the first memento operation.

**Impact:** Low runtime impact, but it signals premature optimization. A simple `Dictionary` with a lock, or even a `Lazy<>` per type, would suffice. More importantly, this entire cache is unnecessary if HIGH-1 is addressed.

---

### LOW-1: Version Interfaces Have No Consumer Path

**Evidence:** `IHasVersionInfo` (19 lines), `IHasVersionHistory` (20 lines)

**Problem:** These interfaces define contracts for optimistic concurrency and version history, but no code in the framework reads or acts on them. They are pure forward-looking contracts. `IHasVersionHistory` is a marker interface with zero members.

**Impact:** Minimal (39 LOC total), but they create API surface area that may change before any consumer uses them. Including them in the initial release creates a compatibility commitment with no validation.

**Recommendation:** Consider deferring these until the infrastructure layer that consumes them exists.

---

### LOW-2: TenantId Provided as Convenience but May Mislead

**Evidence:** `src/Yaf.Domain/TenantId.cs` (8 lines)

**Problem:** Minor. The framework provides `TenantId` as a convenience, but the docs say consumers can define their own. Having a framework-provided default might create an implicit recommendation when the choice should be deliberate.

**Impact:** Negligible. 8 lines of code. Clear doc comment explains it is optional.

---

## 3. Developer Experience Assessment

### What Works Well

- **Opt-in composability:** Consumers pick only the interfaces they need. An entity with just `ITimestamped` gets timestamp auto-handling without pulling in accountability or soft-delete. This is a clean design.
- **Graceful degradation:** When entity has domain interfaces but memento lacks the matching memento interfaces, the framework silently skips rather than throwing. Well-tested in `GracefulDegradationTests`.
- **Comprehensive XML docs:** Every public interface, property, and class has detailed XML documentation with cross-references. This is genuinely helpful.
- **Backward compatibility:** Entities without any cross-cutting interfaces work identically to before the PR. The `PlainEntity` test proves this.

### Friction Points

- **Interface naming confusion:** `IAccountable` (domain) vs `IHasAccountability` (memento) -- the naming pattern is not self-documenting. A consumer new to the framework needs to learn: "I" prefix + adjective = domain, "IHas" prefix + noun = memento. This is a convention that requires documentation to discover.
- **Debugging reflection errors:** If a consumer forgets `{ get; private set; }` and uses `{ get; init; }`, the error surfaces at runtime from `ReflectionHelper.BuildPropertyWriter` with a message about missing setters. This is a runtime pit-of-failure that could be a compile-time constraint.
- **Test boilerplate as API preview:** The test file `MementoBridgeTests.cs` (684 lines) shows what a consumer must write: a memento interface, a memento class implementing it, and an entity class implementing both entity and domain interfaces. For the "full" case (`Order`), the consumer writes 77 lines of code (lines 14-77) before any business logic. This is substantial ceremony.
- **Four interfaces per concern:** To opt into accountability, a consumer must know about `IAccountable<TActorId>` (on entity), `IHasAccountability<T>` (on memento interface), plus understand that `IAccountable` and `IHasAccountability` (non-generic) exist but should not be implemented directly. This is a learning curve.

### DX Verdict

The framework is designed for correctness and extensibility but pays a developer experience tax for it. The auto-handling of cross-cutting concerns during Snapshot/Restore/Hydrate is genuinely convenient once understood, but the path to understanding requires absorbing a significant number of abstractions.

---

## 4. Requirements Alignment

The implementation aligns with the plan (`docs/plans/20260327-0936-feat-cross-cutting-domain-interfaces-plan.md`). The five cross-cutting concerns are implemented as specified: accountability, timestamps, soft-delete, tenant scoping, and versioning. The domain/memento split follows the ADR. No requirement inflation detected in the interface designs themselves.

**The over-engineering concern is in the implementation mechanism** (reflection bridge), not in the feature scope. The features are correct; the plumbing is heavier than necessary.

---

## 5. Context Consistency

No contradictory patterns detected. The codebase is internally consistent:
- All interfaces follow the same domain/memento pair pattern
- All boxing bridges use the same DIM approach
- The 9 refactoring commits show iterative improvement toward the current clean state

**One concern:** The `MementoHelper` and `Entity`/`AggregateRoot` generic variants form a tight coupling. Changes to how any cross-cutting concern works require touching `MementoHelper` internals. This is a single point of change, which is good, but it is also a single point of complexity that must be understood holistically.

---

## 6. Recommended Simplifications (Priority Order)

### Priority 1: Defer the Reflection Bridge (HIGH-1)

**Action:** Remove `MementoHelper.MementoBridge`, `ReflectionHelper`, `BoxingHelper`, and the `TypedIdFactoryCache`. Have consumers handle cross-cutting properties explicitly in `SnapshotCore`/`RestoreCore`/`HydrateCore`. Keep the identity auto-handling (it has a clear cost/benefit).

**Impact:** -416 LOC of infrastructure, eliminates expression tree compilation, reflection, and concurrent caches. Consumer adds ~5-10 explicit lines per entity. Framework becomes trivially debuggable.

**Effort:** Medium (2-4 hours). Requires updating tests to reflect explicit mapping.

**Tradeoff:** Consumers write slightly more boilerplate. This can be reintroduced later as a convenience layer once the API has been validated by real usage.

### Priority 2: Eliminate Entity/AggregateRoot Duplication (HIGH-2)

**Action:** Investigate having `AggregateRoot<TId,TSelf,TMemento>` inherit from `Entity<TId,TSelf,TMemento>` or extract shared logic into a helper that both call (the current `MementoHelper` already partially does this, but the calling code in `Snapshot`/`Restore`/`Hydrate` is duplicated).

**Impact:** -130 LOC of duplication. Single source of truth for memento orchestration logic.

**Effort:** Low-Medium (1-2 hours). May require generic constraint adjustments.

### Priority 3: Simplify Interface Hierarchy (MEDIUM-1)

**Action:** If Priority 1 is adopted, remove the non-generic marker interfaces (`IAccountable`, `ISoftDeletable`, `ITenantScoped`). They exist only for the reflection bridge's runtime `is` checks. Without the bridge, they serve no purpose.

**Impact:** -3 interface files, simpler mental model. Consumer sees only the generic interfaces they implement.

**Effort:** Low (30 minutes).

---

## 7. Summary Statistics

| Metric | Current | After Simplifications |
|--------|---------|----------------------|
| Production LOC | ~1,585 | ~1,040 (-34%) |
| Interface files | 16 | 13 |
| Helper classes | 3 | 0-1 |
| Reflection/expression usage | Heavy | None (or identity-only) |
| Test LOC | ~1,847 | ~1,500 (fewer infrastructure tests needed) |
| Concerns per consumer entity | 2 interfaces + auto-magic | 1 interface + explicit code |

---

## 8. Conclusion

The cross-cutting interfaces PR is architecturally sound but prematurely optimized for developer convenience via a reflection-heavy bridge. For a framework with zero consumers, the priority should be:

1. **Get the API right** -- the interface designs themselves are reasonable
2. **Keep the implementation simple** -- the reflection bridge can be added later as a "batteries included" convenience
3. **Eliminate duplication** -- the Entity/AggregateRoot copy-paste will cause maintenance pain

The fundamental question: is the auto-handling of cross-cutting concerns via reflection worth ~416 lines of hard-to-debug infrastructure code when the alternative is ~5-10 lines of explicit mapping per consumer entity? For a mature framework with 100+ consumers, yes. For a framework in its formative phase with zero consumers, the answer is no.

**Recommended action:** Merge the interface designs (they are well-thought-out), but simplify the bridging mechanism before the first release. The reflection-based auto-handling is a feature, not a foundation -- it can be layered on later without breaking changes.
