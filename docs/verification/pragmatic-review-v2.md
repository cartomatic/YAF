# Pragmatic Code Review v2: Yaf.Domain After Simplification

**Date:** 2026-03-28
**Scope:** `src/Yaf.Domain/` -- main branch, post-simplification
**Status:** Appropriate -- Complexity Matches Purpose (with one significant remaining issue)

---

## Executive Summary

Since the v1 pragmatic review, the codebase has undergone substantial simplification. The monolithic 347-line `MementoHelper` has been decomposed into three focused helpers totaling 318 lines (`MementoHelper` 118 + `ReflectionHelper` 69 + `TypedIdBridge` 111). Non-generic marker interfaces were removed. `RestoreCore` was eliminated from entities in favor of delegating to `Hydrate`. The auto-mapping of cross-cutting concerns (timestamps, accountability, soft-delete) was retained -- a deliberate design choice that trades implementation complexity for consumer convenience.

The result is a tighter, more focused codebase. Total production LOC dropped from ~1,585 to ~1,462 (excluding obj/). The 90 passing tests provide good coverage. The remaining issues are narrower and more specific than in v1.

| Severity | Count |
|----------|-------|
| High     | 1     |
| Medium   | 2     |
| Low      | 2     |

**v1 comparison:** Down from 2 High, 3 Medium, 2 Low. The overall posture shifted from "over-engineered" to "appropriate with one structural issue."

---

## 1. Complexity Assessment

**Project scale:** Early-stage framework library, zero consumers, ~2 months old, 1,462 production LOC, 1,534 test LOC.

**Complexity indicators:**
- 4 helper files (318 LOC total) using `System.Linq.Expressions`, `ConcurrentDictionary`, `RuntimeHelpers.GetUninitializedObject`
- 17 interface files, organized as domain/memento pairs
- Default interface method (DIM) implementations for boxing semantics
- Compiled expression-tree delegates for property access

**What improved since v1:**
- `MementoHelper` dropped from 347 to 118 lines (66% reduction). The `MementoBridge` inner class and its complex `Build()` method are gone. The conditional delegate builders are now simple one-liner methods that gate on interface checks and delegate to `ReflectionHelper`.
- `TypedIdBridge` extracted as a standalone 111-line class with clear responsibility: boxing/unboxing typed IDs for memento transfer. Previously buried inside MementoHelper.
- `ReflectionHelper` unchanged at 69 lines -- this was already well-factored.
- Non-generic marker interfaces removed (v1 MEDIUM-1 addressed).
- `RestoreCore` eliminated from Entity/AggregateRoot; `Restore` now delegates to `Hydrate`.

**Assessment: APPROPRIATE complexity for a DDD framework library.** The expression-tree compilation and reflection remain, but they now serve a clear purpose (auto-mapping cross-cutting concerns) and live in focused, testable helpers. The helpers are individually understandable. This is a reasonable trade-off for a framework that aims to reduce consumer boilerplate.

---

## 2. Over-Engineering Findings

### HIGH-1: Entity and AggregateRoot Generic Variants Remain 100% Duplicated

**Evidence:** `src/Yaf.Domain/Entity{TId,TSelf,TMemento}.cs` (145 lines) and `src/Yaf.Domain/AggregateRoot{TId,TSelf,TMemento}.cs` (145 lines)

**Problem:** This was HIGH-2 in v1 and remains completely unaddressed. A normalized diff (substituting "AggregateRoot" for "Entity") shows exactly two differences: a grammar artifact ("entitys" vs "entities") and a comment annotation. The entire memento orchestration -- static field declarations, `Snapshot`, `Restore`, `Hydrate`, `SnapshotTimestamps`, `HydrateTimestamps`, `SnapshotAccountability`, `HydrateAccountability`, `SnapshotSoftDelete`, `HydrateSoftDelete` -- is character-for-character identical.

This is 145 lines of pure copy-paste. Every future change to the memento auto-mapping (adding a new cross-cutting concern, fixing a bug in hydration logic) must be applied in both files and kept in sync.

**Impact:** Maintenance burden and divergence risk. The v1 review noted that "each of the 9 refactoring commits touched both files." The same pattern will continue.

**Recommendation:** Have `AggregateRoot<TId, TSelf, TMemento>` inherit from a shared base that provides the memento orchestration, or extract the orchestration into a composed helper that both call. The C# generic constraint system makes direct inheritance tricky (`TSelf : AggregateRoot<...>` vs `TSelf : Entity<...>`), but a composition approach is straightforward:

**Option A -- Extract to a helper method pattern:**
```csharp
// In MementoHelper, add orchestration methods that Entity and AggregateRoot both call:
internal static void DoSnapshot(TSelf self, TMemento memento, TId id,
    Action<TSelf, TMemento> snapshotCore) { ... }
internal static void DoHydrate(TSelf self, TMemento memento,
    Action<TSelf, TMemento> hydrateCore) { ... }
```

**Option B -- Accept the duplication as a conscious trade-off:**
If the generic constraint issue makes refactoring genuinely awkward, document the duplication explicitly with a code comment and consider a Roslyn analyzer or test that verifies the two files stay in sync.

**Estimated impact:** Option A removes ~100 lines of duplication. Option B costs zero lines but adds a sync-verification mechanism.

---

### MEDIUM-1: ValueObject Still Uses RestoreCore While Entity Uses HydrateCore

**Evidence:** `src/Yaf.Domain/ValueObject{TSelf,TMemento}.cs` lines 34, 49

**Problem:** The `ValueObject<TSelf, TMemento>.Restore` method calls `RestoreCore`, while `Entity<TId, TSelf, TMemento>.Restore` calls `Hydrate` which calls `HydrateCore`. The RestoreCore/HydrateCore naming inconsistency means consumers must remember two different method names depending on whether they are implementing an Entity or a ValueObject.

Additionally, `ValueObject.Restore` does not call a `Hydrate` method -- it calls `RestoreCore` directly. This means ValueObjects lack an in-place hydration path (which is correct, since value objects are immutable), but the method naming divergence is a minor API inconsistency.

**Impact:** Low-moderate. Consumers implementing both entities and value objects will notice the naming difference. The behavior difference (entities support `Hydrate`, value objects do not) is intentional and correct. The naming just does not signal this clearly.

**Recommendation:** Consider renaming `RestoreCore` to `HydrateCore` on ValueObject for API consistency, with a doc comment explaining that value objects only support restoration (not in-place hydration). Alternatively, keep the naming difference but document why it exists.

---

### MEDIUM-2: TypedIdBridge ReadPair/WritePair API Assumes 1-or-2 Properties

**Evidence:** `src/Yaf.Domain/Helpers/TypedIdBridge.cs` -- `ReadPair` and `WritePair` methods, plus the `_reader2`/`_writer2` nullable pattern.

**Problem:** The `TypedIdBridge` is built around a "pair" abstraction (two optional properties), because accountability has `CreatedBy`/`ModifiedBy` and soft-delete has `DeletedBy` (single property, second slot unused). This works for the current two use cases but is a brittle abstraction -- it encodes the assumption that typed ID properties always come in pairs of at most two. The `ReadPair` returns `(object? first, object? second)` with the second being null for soft-delete, and `WritePair` receives a null second argument that gets silently ignored.

In `Entity{TId,TSelf,TMemento}.cs` line 131, the call `_softDeleteBridge.ReadPair((TSelf)this)` returns `(deletedBy, _)` -- the underscore discard signals that the pair abstraction is not a natural fit for single-property use.

**Impact:** Low-moderate. The code works correctly. The issue is that the abstraction leaks its "optimized for accountability" origin into the soft-delete use case. If a future cross-cutting concern has 3 typed ID properties, the pair abstraction would need to be extended.

**Recommendation:** Consider splitting into `ReadSingle`/`WriteSingle` and `ReadPair`/`WritePair` methods. Or, since there are only two callers, accept the current approach and document the pair assumption.

---

### LOW-1: IHasVersionInfo and IHasVersionHistory Have No Framework Consumer

**Evidence:** `src/Yaf.Domain/Interfaces/IHasVersionInfo.cs`, `src/Yaf.Domain/Interfaces/IHasVersionHistory.cs`

**Problem:** Carried forward from v1 LOW-1. These interfaces define contracts for optimistic concurrency and version history, but no code in the framework reads or acts on them. Tests verify only that the interfaces can be implemented and composed independently.

**Impact:** Minimal (39 LOC total). These are well-documented forward-looking contracts. The risk is that the API may change before any consumer validates the design.

**Recommendation:** Acceptable for now. They are cheap to carry and removing them would create churn when the infrastructure layer is built. Flag for review when the persistence layer is implemented.

---

### LOW-2: ConcurrentDictionary in TypedIdFactoryCache

**Evidence:** `src/Yaf.Domain/Helpers/TypedIdBridge.cs` lines 89-111

**Problem:** Carried forward from v1 MEDIUM-3 (downgraded). The `ConcurrentDictionary<Type, Func<object, object>>` provides thread-safe caching for compiled TypedId constructors. The cache is populated once per TypedId type in the application lifetime.

**Impact:** Negligible. `ConcurrentDictionary` is the idiomatic .NET choice for this pattern. The v1 review flagged this as part of the broader "premature optimization" concern, but in isolation it is perfectly reasonable.

**Recommendation:** No action needed. This is standard .NET caching practice.

---

## 3. Developer Experience Assessment

### What Works Well

- **Consumer boilerplate is reasonable.** The test `Order` class (MementoBridgeTests.cs lines 37-82) shows a realistic consumer entity: 45 lines including properties, constructor, factory method, and memento mapping. Only `SnapshotCore` and `HydrateCore` need to map entity-specific properties -- identity, timestamps, accountability, and soft-delete are handled automatically. This is a good trade-off.
- **Opt-in composability.** The `PlainEntity` test (lines 97-108) proves that entities without cross-cutting interfaces work cleanly with zero overhead.
- **Comprehensive XML docs.** Every public API member has documentation. Cross-references between related types are thorough.
- **Clear error messages.** `ReflectionHelper` throws with messages like "OrderEntity is missing the 'CreatedBy' property. Use implicit interface implementation (e.g., public ... CreatedBy { get; private set; })." This is developer-friendly.
- **Validation at hydration time.** `ThrowIfInvalid()` is called at the end of `Hydrate`, catching bad mementos early.

### Friction Points

- **Interface naming convention requires learning.** `IAccountable<TActorId>` (entity) vs `IHasAccountability<T>` (memento) -- the pattern is consistent but not self-documenting. A consumer must learn: adjective = entity interface, "IHas" + noun = memento interface.
- **Debugging compiled delegates.** If something goes wrong in the auto-mapping, stack traces include compiled expression tree frames. The decomposed helpers make this better than v1 (smaller surface area per helper), but it is still harder to debug than explicit property assignment.
- **`{ get; private set; }` requirement is a runtime discovery.** If a consumer uses `{ get; init; }` (a common modern C# pattern), they discover the incompatibility at runtime via a `ReflectionHelper` exception. This cannot easily be made a compile-time error, but it could be documented more prominently.

### DX Verdict

Developer experience is good. The framework provides meaningful convenience (auto-mapping of 4 cross-cutting concerns) at a manageable complexity cost. The consumer-facing API (implement interfaces, override two methods) is clean. The main DX risk is debugging the reflection layer, which is mitigated by clear error messages.

---

## 4. Requirements Alignment

The implementation aligns with the framework's stated goals: DDD building blocks with memento-based persistence and opt-in cross-cutting concerns. No requirement inflation detected. The auto-mapping of cross-cutting concerns is a deliberate framework feature, not accidental complexity.

The v1 review questioned whether auto-mapping was premature. Since the team has chosen to keep it, the implementation is now well-decomposed and reasonable for the purpose it serves. The key question -- "is the reflection-based auto-handling worth the complexity?" -- has been answered affirmatively by the team, and the current implementation supports that choice competently.

---

## 5. Context Consistency

No contradictory patterns detected. The codebase is internally consistent:

- All auto-mapped concerns follow the same pattern: static field initialization via `MementoHelper<TId, TSelf, TMemento>.Build*`, private Snapshot/Hydrate methods with interface checks.
- The Entity and AggregateRoot generic variants are consistent with each other (too consistent -- see HIGH-1).
- Boxing/unboxing is handled uniformly via DIM implementations on memento interfaces.
- `ValueObject` follows a different pattern (no auto-mapping, no `Hydrate`) which is appropriate for its immutable semantics.

**Unused code check:**
- `ITenantScoped<TTenantId>`: Declared in its own file, used in tests (`Order` implements it), not referenced by framework auto-mapping. Tenant mapping is manual in `SnapshotCore`/`HydrateCore`. This is intentional -- the interface is for domain marking, not auto-mapping.
- `IDomainEvent`: Marker interface used by `AggregateRoot.AddDomainEvent`. No framework dispatch mechanism yet. Appropriate to define now.
- `IHasVersionInfo` / `IHasVersionHistory`: Forward-looking contracts (see LOW-1).

No dead code, unused private methods, or abandoned patterns found.

---

## 6. Recommended Simplifications (Priority Order)

### Priority 1: Eliminate Entity/AggregateRoot Duplication (HIGH-1)

**Action:** Extract the shared memento orchestration (static fields, Snapshot, Restore, Hydrate, and all private auto-mapping methods) into a composable mechanism that both `Entity<TId,TSelf,TMemento>` and `AggregateRoot<TId,TSelf,TMemento>` share.

**Impact:** Removes ~100 lines of duplication. Single source of truth for memento orchestration. Eliminates the "must change two files" maintenance pattern.

**Effort:** Medium (2-4 hours). The main challenge is the generic constraint difference (`TSelf : Entity<...>` vs `TSelf : AggregateRoot<...>`).

### Priority 2: Align ValueObject Method Naming (MEDIUM-1)

**Action:** Rename `ValueObject<TSelf,TMemento>.RestoreCore` to `HydrateCore` for API consistency across entity and value object base classes.

**Impact:** Consistent consumer API. Both entities and value objects override `SnapshotCore` and `HydrateCore`.

**Effort:** Low (30 minutes). Rename + update tests.

### Priority 3: Document the `{ get; private set; }` Requirement Prominently (DX improvement)

**Action:** Add a prominent remark to the `ITimestamped`, `IAccountable<T>`, and `ISoftDeletable<T>` XML docs explaining that entity properties must use `{ get; private set; }` (not `{ get; init; }`) for memento hydration to work.

**Impact:** Prevents a common consumer mistake from being a runtime surprise.

**Effort:** Low (15 minutes).

---

## 7. Summary Statistics

| Metric | v1 (pre-simplification) | v2 (current) | After v2 Recommendations |
|--------|------------------------|--------------|--------------------------|
| Production LOC | ~1,585 | ~1,462 | ~1,360 |
| Helper LOC | ~416 (monolithic) | ~318 (3 focused files) | ~318 (unchanged) |
| Interface files | 16 | 17 | 17 |
| Helper classes | 1 (+ inner class) | 4 (focused) | 4 |
| Copy-pasted code | ~130 lines | ~145 lines | ~0 lines |
| Test count | 90 | 90 | 90 |
| High findings | 2 | 1 | 0 |
| Medium findings | 3 | 2 | 0 |

---

## 8. Conclusion

The Yaf.Domain codebase has improved meaningfully since the v1 review. The major simplifications -- decomposing MementoHelper, removing non-generic markers, eliminating RestoreCore from entities -- addressed the most impactful v1 findings. The remaining reflection/expression-tree infrastructure is now a deliberate, well-contained design choice rather than accidental complexity.

**The one significant remaining issue is the Entity/AggregateRoot copy-paste (HIGH-1).** This is 145 lines of identical code that creates ongoing maintenance risk. Addressing it would make the codebase structurally clean.

Beyond that, the framework is appropriately complex for what it does. The auto-mapping of cross-cutting concerns via compiled delegates is a legitimate framework feature that saves consumers meaningful boilerplate. The helpers are focused, testable, and individually understandable. The interface design is clean and composable.

**Verdict: The codebase is in good shape.** Fix the duplication, align the naming, and this is a solid foundation for a DDD framework library.
