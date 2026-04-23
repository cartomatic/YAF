# Specification Audit (v2): Cross-Cutting Domain and Memento Interfaces

- **Spec**: `docs/plans/20260327-0936-feat-cross-cutting-domain-interfaces-plan.md`
- **Audit date**: 2026-03-27
- **Audit type**: Pre-implementation, second pass (post-revision)
- **Auditor**: Claude Opus 4.6 (spec-auditor agent)

---

## Compliance Status: :warning: Mostly Compliant

The revised specification is substantially improved and internally consistent on the major design decisions. However, three locations in the spec still contain stale text that contradicts the revised design. These are localized copy errors, not design issues, but they will mislead an implementer if not corrected before work begins.

---

## Critical Issues

None.

---

## High Severity Findings

### H-1: ISoftDeletable described as "extends ITimestamped" in three locations (stale text)

**Spec Reference**: Lines 131, 374, 456

**Evidence**:

- Line 131 (Deletion Lifecycle section): "ISoftDeletable (extends ITimestamped)" -- this parenthetical directly contradicts the design decision on line 87 and the Technical Considerations on lines 223 and 230.
- Line 374 (File Organization table): `ISoftDeletable.cs` described as "Non-generic + generic; extends ITimestamped (DeletedAtUtc?, DeletedBy?)" -- contradicts line 87 ("ISoftDeletable is standalone -- No inheritance from ITimestamped or IAccountable") and line 223 ("Non-generic -- standalone, no inheritance").
- Line 456 (ADR Updates acceptance criteria): "Add `ISoftDeletable<TActorId> : ITimestamped` as a new cross-cutting concern" -- contradicts the design decision. The ADR update instruction itself is wrong; it would tell the implementer to document ISoftDeletable as inheriting from ITimestamped in the ADR.

**Category**: Incorrect (stale text from pre-revision)

**Severity**: High -- An implementer following the File Organization table or the ADR update instructions would implement the wrong inheritance. The Deletion Lifecycle table would confuse readers about the actual design.

**Recommendation**: Update all three locations to reflect the standalone design:
- Line 131: "ISoftDeletable (standalone -- DeletedAtUtc set, row stays)"
- Line 374: "Non-generic + generic standalone (DeletedAtUtc?, DeletedBy?)"
- Line 456: "Add `ISoftDeletable<TActorId>` as a new standalone cross-cutting concern"

---

### H-2: IHasVersionHistory described as "extends IHasVersionInfo" in File Organization table (stale text)

**Spec Reference**: Line 382

**Evidence**:

- Line 382 (File Organization table): `IHasVersionHistory.cs` described as "Memento-only marker (extends IHasVersionInfo)" -- contradicts line 71 ("IHasVersionHistory -- independent marker -- activates snapshots + graveyard; memento-only"), line 311 ("Independent marker -- does NOT extend IHasVersionInfo"), and acceptance criterion on line 407 ("IHasVersionHistory independent marker (memento-only, does not extend IHasVersionInfo)").

**Category**: Incorrect (stale text from pre-revision)

**Severity**: High -- Same risk as H-1. An implementer reading the File Organization table would add `: IHasVersionInfo` inheritance.

**Recommendation**: Update line 382 to: "Memento-only independent marker (does not extend IHasVersionInfo)"

---

## Medium Severity Findings

### M-1: MementoHelper architecture needs clarification for new Write/Read methods

**Spec Reference**: Lines 330-339 (MementoHelper Evolution), Line 384 (File Organization)

**Evidence**:

The existing `MementoHelper` is a static generic class with signature:
```
internal static class MementoHelper<TId, TSelf, TMemento>
    where TId : ITypedId
    where TSelf : Entity<TId>
    where TMemento : class
```
(File: `src/Yaf.Domain/Helpers/MementoHelper.cs`)

The spec says "the same approach extends to the new interfaces" (line 330) and lists methods like `WriteAccountability(memento, this)` and `ReadAccountability(memento)` (lines 347-360). These new methods need to:

1. Check if the **entity** (`this`) implements `IAccountable`, `ITimestamped`, `ISoftDeletable`, or `ITenantScoped`.
2. Check if the **memento** implements `IHasAccountability`, `IHasTimestamps`, `IHasSoftDelete`, or `IHasTenantId`.
3. For typed-ID interfaces, discover the actor/tenant ID type at runtime, build compiled factories, and cache them.

The spec says "MementoHelper discovers `IAccountable` marker at runtime" (line 168) and describes a `ConcurrentDictionary<Type, Delegate>` cache "shared across all MementoHelper instantiations" (line 335). However, `MementoHelper<TId, TSelf, TMemento>` is a static generic class -- each unique combination of `(TId, TSelf, TMemento)` gets its own static class with its own static fields. A "shared across all instantiations" `ConcurrentDictionary` would need to be on a non-generic companion class or a non-generic base.

This is feasible but the spec does not specify the approach. The implementer needs to decide between:
- (a) A separate non-generic static class (e.g., `MementoHelperCache`) holding the shared `ConcurrentDictionary`.
- (b) Using the per-`(TId, TSelf, TMemento)` static fields (lazy-initialized per combination), which duplicates factory delegates across TId combinations that share the same actor type.
- (c) Some hybrid approach.

**Category**: Ambiguous

**Severity**: Medium -- The spec describes the caching intent clearly enough that a competent implementer can resolve this, but the "shared across all MementoHelper instantiations" wording could cause confusion given the static generic class design.

**Recommendation**: Add a brief note clarifying that the `ConcurrentDictionary<Type, Delegate>` for typed ID factories should live on a separate non-generic static class (or use a static field on the existing non-generic `MementoHelper` if one is introduced), since per-`(TId, TSelf, TMemento)` static fields do not share state across different generic instantiations.

---

### M-2: Property setter compilation strategy underspecified for Restore vs Hydrate

**Spec Reference**: Lines 334, 337 (MementoHelper Evolution)

**Evidence**:

The spec says "the helper compiles expression-tree-based property setters targeting the entity's `private set` accessor" (line 337) for Restore/Hydrate direction. The existing codebase sets `Id` directly via the `protected set` accessor on `Entity<TId>.Id` (line 19 of `Entity.cs`: `public TId Id { get; protected set; } = default!;`), not via compiled expression trees.

For the new cross-cutting properties (e.g., `CreatedAtUtc`, `CreatedBy`), the spec says entities implement them with `{ get; private set; }` (lines 111, 207, 235). The base class `Snapshot`/`Restore`/`Hydrate` methods would need compiled setters to write to these `private set` properties on derived types.

The spec correctly identifies this need but does not specify:
1. Whether the compiled setters target the interface property or the concrete class property (matters for private set resolution via reflection).
2. How the property is discovered: "by matching the interface property name on the concrete type" (line 338) -- but what if the entity implements the interface explicitly? Explicit interface implementations have different reflection characteristics.

**Category**: Ambiguous

**Severity**: Medium -- Standard reflection pattern, but the explicit-vs-implicit implementation question could cause a subtle bug if not addressed.

**Recommendation**: Add a note that entities must use **implicit** interface implementation for cross-cutting properties (which is already implied by `{ get; private set; }` on the entity class), and that explicit interface implementation is not supported for auto-handled properties. The property discovery should search the concrete type for a property matching the interface member name.

---

### M-3: ISoftDeletable non-generic has a property but IAccountable and ITenantScoped non-generics are pure markers

**Spec Reference**: Lines 44, 156, 223-228, 392-398

**Evidence**:

The interface hierarchy shows three different patterns for non-generic bases:

| Interface | Non-generic pattern |
|-----------|-------------------|
| `IAccountable` | Pure marker -- no properties (line 156, 392) |
| `ITenantScoped` | Pure marker -- no properties (line 262, 397) |
| `ISoftDeletable` | Has `DeletedAtUtc?` property (line 224, 395) |

This asymmetry is intentional and well-justified: `ISoftDeletable` without a generic actor type is still useful (you get soft-delete behavior with a timestamp but without tracking who deleted it), whereas `IAccountable` without a generic actor type has no useful properties to expose.

However, this means `MementoHelper` cannot discover `DeletedAtUtc` from the non-generic `ISoftDeletable` marker alone for the purpose of runtime bridging in the same way it discovers `IAccountable` as a pure marker. The helper needs to:
- Check `entity is ISoftDeletable` to read `DeletedAtUtc` (non-generic, has property).
- Check `entity is ISoftDeletable<TActorId>` via reflection to read `DeletedBy` (generic, needs factory).

The spec describes this flow for `IAccountable` (line 168: "discovers IAccountable marker at runtime, then uses compiled delegates to read typed CreatedBy/ModifiedBy properties") but does not explicitly describe the `ISoftDeletable` variant where the non-generic interface already carries `DeletedAtUtc`.

**Category**: Incomplete

**Severity**: Medium -- The pattern is inferable from context, but the asymmetry deserves an explicit note in the MementoHelper Evolution section.

**Recommendation**: Add a brief note in the MementoHelper Evolution section clarifying that `ISoftDeletable` bridging handles two properties from two different levels: `DeletedAtUtc` from the non-generic `ISoftDeletable` (direct property read, no factory needed) and `DeletedBy` from the generic `ISoftDeletable<TActorId>` (via compiled factory, same pattern as `IAccountable`).

---

## Low Severity Findings

### L-1: IHasSoftDelete non-generic base has `DeletedAtUtc` + boxed `DeletedBy`, but non-generic ISoftDeletable only has `DeletedAtUtc`

**Spec Reference**: Lines 240-253

**Evidence**:

The memento-side `IHasSoftDelete` (non-generic) has three members:
- `DateTimeOffset? DeletedAtUtc { get; set; }` (line 242)
- `Type? ActorIdType { get; }` (line 243)
- `object? BoxedDeletedBy { get; set; }` (line 244)

When the domain entity implements only non-generic `ISoftDeletable` (without `<TActorId>`), the memento would implement `IHasSoftDelete` (non-generic). But `ActorIdType` and `BoxedDeletedBy` have no meaningful values in this case. The nullable `Type?` and `object?` handle this gracefully (both would be null).

However, the spec does not specify what a memento class would look like when it only needs the non-generic `IHasSoftDelete`. The DIM pattern only exists on `IHasSoftDelete<T>`, not on the non-generic base. A memento implementing only `IHasSoftDelete` (not the generic variant) would need to provide explicit implementations for `ActorIdType` (returning null) and `BoxedDeletedBy` (get/set returning null).

**Category**: Incomplete

**Severity**: Low -- Edge case. Most consumers will use `ISoftDeletable<TActorId>` with the generic memento variant. The non-generic-only path is unusual but should still work.

**Recommendation**: Add a note that when a memento implements only the non-generic `IHasSoftDelete` (because the entity uses non-generic `ISoftDeletable`), `ActorIdType` should return `null` and `BoxedDeletedBy` should be a no-op. Alternatively, consider whether the non-generic `IHasSoftDelete` should only carry `DeletedAtUtc` and the `ActorIdType`/`BoxedDeletedBy` members should live exclusively on the generic variant.

---

### L-2: State Management ADR still references Activator.CreateInstance

**Spec Reference**: Line 148 (ADR Updates Required, item 7), Line 461 (acceptance criteria)

**Evidence**:

The spec correctly identifies that the State Management ADR needs updating (line 461: "Update Activator.CreateInstance reference to compiled expression approach"). The existing `MementoHelper.cs` already uses compiled expression trees (lines 58-71 of the source file), not `Activator.CreateInstance`. However, the State Management ADR (line 162) still says "Activator.CreateInstance for Id reconstruction."

This is not a spec defect per se -- the spec already calls out the needed ADR update. Noting it here for completeness.

**Category**: Incomplete (in the ADR, correctly flagged by the spec)

**Severity**: Low -- The spec already tracks this as a required ADR update.

**Recommendation**: No action needed on the spec itself. The implementer should update the ADR as part of the acceptance criteria.

---

### L-3: Existing cross-cutting ADR uses "IVersionable" where spec uses "IHasVersionHistory"

**Spec Reference**: Lines 140-148 (ADR Updates Required)

**Evidence**:

The cross-cutting infrastructure ADR (lines 52, 148, 152, 189, 203) uses the name `IVersionable` throughout. The spec renames this to `IHasVersionHistory` (line 23, 71, 125, 306-316). The spec correctly identifies ADR updates are needed (lines 140-148) but does not explicitly call out the rename from `IVersionable` to `IHasVersionHistory` as one of the update items.

**Category**: Incomplete

**Severity**: Low -- The rename is implicit in the ADR update instructions, but making it explicit prevents the implementer from accidentally keeping the old name.

**Recommendation**: Add an explicit ADR update item: "Rename `IVersionable` to `IHasVersionHistory` throughout the ADR."

---

### L-4: TypedId XML doc references Activator.CreateInstance

**Spec Reference**: Not directly in scope, but relevant to spec's compiled expression approach.

**Evidence**:

`src/Yaf.Domain/TypedId.cs` line 19 has an XML doc comment referencing `Activator.CreateInstance`:
```csharp
/// This constructor is required for memento identity reconstruction via
/// <see cref="System.Activator.CreateInstance(Type, object[])"/>.
```

The actual codebase already uses compiled expression trees in `MementoHelper.cs`. This is an existing documentation inconsistency, not introduced by this spec. Noting it because the spec should not propagate this error.

**Category**: Extra (existing codebase issue, not spec defect)

**Severity**: Low -- Documentation-only; no functional impact.

**Recommendation**: Update the XML doc comment on `TypedId<T>` as part of this work, since the spec is already touching related code.

---

## Clarification Needed

### C-1: What should happen when ISoftDeletable entity has IAccountable but the actor types differ?

**Spec Reference**: Lines 85-86, 236

**Evidence**:

The spec says "IAccountable tracks creation/modification, ISoftDeletable tracks deletion" (line 86) and shows typical composition: `class Order : ITimestamped, IAccountable<UserId>, ISoftDeletable<UserId>` (line 236).

Question: Is it valid for an entity to use different actor types for accountability and soft-delete? For example:
```csharp
class Order : IAccountable<UserId>, ISoftDeletable<ServiceAccountId>
```

This is syntactically valid in C# and the interfaces have independent type parameters. The MementoHelper would need separate factory caches for each. The spec does not explicitly permit or prohibit this pattern.

**Impact**: Low -- this is an edge case, and the architecture supports it naturally since the type parameters are independent. But it is worth a conscious decision on whether to document it as supported.

---

### C-2: Should IHasTimestamps carry `DeletedAtUtc` for non-ISoftDeletable entities?

**Spec Reference**: Lines 59, 196-218

**Evidence**:

`IHasTimestamps` has only `CreatedAtUtc?` and `ModifiedAtUtc?` (lines 59, 212-214). The original cross-cutting ADR has `ITimestamped` carrying `DeletedAtUtc` (ADR line 105). The spec removes deletion fields from `ITimestamped`/`IHasTimestamps` and moves them to `ISoftDeletable`/`IHasSoftDelete`.

This is a clear and well-justified design decision. However, the ADR update instructions (line 142) say "Remove `DeletedBy`/`DeletedAtUtc` from `IAccountable` and `ITimestamped` sections" but the ADR does not actually have `DeletedBy` on `IAccountable` -- it has `DeletedBy` on `IAccountable<TActorId>` (ADR line 94) and `DeletedAtUtc` on `ITimestamped` (ADR line 105).

**Impact**: Very low -- the ADR update instruction is directionally correct but slightly imprecise about where `DeletedBy` currently lives. The implementer updating the ADR should just remove deletion fields wherever they appear.

---

## Extra Features (Not in Referenced ADRs)

### E-1: ISoftDeletable is a new concern not present in any existing ADR

**Spec Reference**: Lines 21, 44-46, 142-146, 222-236

**Evidence**:

The cross-cutting infrastructure ADR covers accountability, timestamping, encryption, versioning, and graveyard -- but does not mention soft-deletion as a separate concern. The multi-tenancy ADR does not mention it either. `ISoftDeletable` is a new concept introduced by this spec.

The spec correctly identifies this gap and includes ADR update instructions (lines 142-146). This is noted for completeness -- the spec is handling the gap properly.

**Category**: Extra (relative to existing ADRs, correctly handled by spec)

**Severity**: N/A -- this is expected for a feature plan that extends the architecture.

---

## Summary of Findings

| ID | Category | Severity | Description |
|----|----------|----------|-------------|
| H-1 | Incorrect | High | ISoftDeletable described as "extends ITimestamped" in 3 locations (stale text) |
| H-2 | Incorrect | High | IHasVersionHistory described as "extends IHasVersionInfo" in File Organization table (stale text) |
| M-1 | Ambiguous | Medium | MementoHelper shared ConcurrentDictionary cache architecture underspecified for static generic class |
| M-2 | Ambiguous | Medium | Compiled property setter discovery not specified for implicit vs explicit interface implementation |
| M-3 | Incomplete | Medium | ISoftDeletable non-generic property handling in MementoHelper not explicitly described |
| L-1 | Incomplete | Low | Non-generic-only IHasSoftDelete memento usage not fully specified |
| L-2 | Incomplete | Low | State Management ADR Activator.CreateInstance reference (correctly flagged by spec) |
| L-3 | Incomplete | Low | IVersionable-to-IHasVersionHistory rename not explicit in ADR update instructions |
| L-4 | Extra | Low | Existing TypedId XML doc references Activator.CreateInstance (codebase issue) |
| C-1 | Clarification | -- | Different actor types for IAccountable vs ISoftDeletable on same entity |
| C-2 | Clarification | -- | ADR update instruction slightly imprecise about where DeletedBy currently lives |

---

## Recommendations

1. **Fix H-1 and H-2 immediately** -- these are copy-paste remnants from the pre-revision spec that directly contradict the design decisions. Three occurrences for H-1 (lines 131, 374, 456) and one for H-2 (line 382).

2. **Address M-1 through M-3 before implementation** -- add brief clarifying notes to the MementoHelper Evolution section. These do not require design changes, just more explicit guidance for the implementer.

3. **L-1 through L-4 can be addressed during implementation** -- they are documentation refinements and edge case clarifications that the implementer can resolve.

4. **C-1 and C-2 are informational** -- they highlight edge cases worth a conscious decision but do not block implementation.

---

## Positive Observations

The revised spec demonstrates several strengths:

- The standalone ISoftDeletable design is cleaner than inheriting from ITimestamped. The composition-over-inheritance approach gives consumers maximum flexibility.
- The independent IHasVersionHistory (not inheriting IHasVersionInfo) correctly separates concurrency control from version history tracking.
- The nullable CreatedBy (`TActorId?`) consistently matches the nullable timestamp pattern -- null means not yet persisted.
- The domain-side/memento-side split is clearly articulated with the DIM pattern well-specified for each interface pair.
- The deletion lifecycle table (line 119-134) provides a clear decision matrix for implementers, despite the stale parenthetical on line 131.
- Acceptance criteria are comprehensive with 30+ specific test cases covering round-trips, graceful degradation, composition, and edge cases.
