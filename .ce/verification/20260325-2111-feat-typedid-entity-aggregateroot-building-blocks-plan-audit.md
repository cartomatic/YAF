# Specification Audit Report

**Specification:** `docs/plans/20260325-2111-feat-typedid-entity-aggregateroot-building-blocks-plan.md`
**Audit Type:** Pre-implementation (no code exists yet for these types)
**Audit Date:** 2026-03-26
**Compliance Status:** N/A (pre-implementation)
**Specification Readiness:** Mostly Ready -- actionable findings below

---

## Summary

The specification is well-structured, thorough, and largely consistent with the referenced ADRs and existing codebase patterns. It correctly identifies the single-inheritance constraint, correctly references the CRTP + static abstract pattern from the solutions doc, and provides clear acceptance criteria with a sensible implementation order.

However, this audit found **1 Critical**, **3 High**, **4 Medium**, and **3 Low** findings that should be addressed before implementation begins.

---

## Critical Issues

### C-1: ADR says TypedId is a record struct; spec says record class -- ADR not yet updated

**Spec Reference:** Section "Key Design Decisions" (line 47), Section "ADR Updates Needed" (lines 246-249)

**Evidence:**
- ADR `docs/adr/domain/20260324-1032-domain-building-blocks.md` line 36: *"**`TypedId<T>` as a record struct** -- **Selected.**"*
- ADR line 92: *"`TypedId<T>` -- Strongly-typed ID wrapper. Record struct."*
- ADR line 120-121: *"`record struct TypedId<T>(T Value)` where T is the backing type"* and *"Consumer creates their own: `public record struct OrderId(Guid Value) : TypedId<Guid>(Value)`"*
- Spec line 47: *"Abstract record **class** (not struct) -- Record structs cannot be inherited."*
- Brainstorm `docs/brainstorms/20260322-2036-yaf-ddd-concepts-brainstorm.md` line 46: *"`TypedId<T>` -- Record-based strongly-typed ID."* (does not specify struct vs class)

**Gap Description:** The spec correctly identifies that record structs cannot be inherited and therefore TypedId must be a record class. However, the ADR explicitly selects record struct as the design decision and has NOT been updated. The spec acknowledges this at lines 246-249 ("ADR Updates Needed") but treating ADR updates as a post-implementation task is risky -- anyone reading the ADR before or during implementation will see contradictory guidance.

**Category:** Incomplete

**Severity:** Critical -- The ADR is the authoritative design document. If a different implementer picks up this work, they may follow the ADR (record struct) rather than the plan (record class), producing a fundamentally incompatible implementation. The ADR update must happen before or atomically with implementation, not after.

**Recommendation:** Update the ADR as the first step of this work item, or add an explicit prerequisite step in the Implementation Order section. The plan's "ADR Updates Needed" section should be promoted to a required pre-implementation step, not a footnote.

---

## High-Severity Issues

### H-1: ADR constraint on TId contradicts spec -- not flagged as a change

**Spec Reference:** Lines 48, 97 (Key Design Decisions table), lines 246-249 (ADR Updates Needed)

**Evidence:**
- ADR `docs/adr/domain/20260324-1032-domain-building-blocks.md` line 97: *"Generic `TId` constrained to `TypedId<T>` (or `IEquatable<TId>` for flexibility)"*
- Spec line 48: *"`where TId : ITypedId` -- Clean interface constraint."*
- Spec line 249: acknowledges ADR needs updating for this

**Gap Description:** The ADR proposes constraining TId to `TypedId<T>` directly or `IEquatable<TId>`. The spec changes this to `ITypedId` (a new non-generic interface). This is a sound design choice -- it enables constraining without coupling to a concrete base type -- but the spec does not explain *why* the ADR's `TypedId<T>` constraint was rejected. Someone reviewing should understand the reasoning.

**Category:** Incomplete

**Severity:** High -- The rationale for deviating from the ADR should be explicitly stated to prevent future confusion or reversal.

**Recommendation:** Add a brief rationale in the Key Design Decisions table or Technical Considerations explaining why `ITypedId` was chosen over constraining directly to `TypedId<T>` (e.g., enables consumers to implement ITypedId without inheriting TypedId<T> if needed, cleaner for generic constraints).

### H-2: Memento ADR contains incorrect C# limitation claim -- contradicted by existing code

**Spec Reference:** Lines 59-61 (Technical Considerations, "C# Pattern: Static Abstract on Abstract Classes")

**Evidence:**
- Memento ADR `docs/adr/domain/20260324-1104-state-management-memento-pattern.md` line 123: *"**C# limitation:** Abstract base types (e.g., `ValueObject<TSelf, TMemento>`) cannot declare `: IMemento<TSelf, TMemento>` because C# does not allow abstract classes to defer `static abstract` interface members to derived types."*
- Existing code `src/Yaf.Domain/ValueObject{TSelf,TMemento}.cs` line 17: `public abstract record ValueObject<TSelf, TMemento> : ValueObject, IMemento<TSelf, TMemento>` -- this compiles and works.
- Solutions doc `docs/solutions/logic-errors/csharp-static-abstract-crtp-memento-pattern.md` lines 20-21: *"A concrete static method on an abstract class DOES satisfy a `static abstract` interface member."*
- Spec line 61: correctly states *"exactly like `ValueObject<TSelf, TMemento>` does"*

**Gap Description:** The Memento ADR contains a factually incorrect statement that was already disproven by the implemented ValueObject code and documented in the solutions doc. The spec for this work item correctly relies on the working pattern but does not flag the ADR as needing correction. If the ADR is read in isolation, it gives wrong guidance.

**Category:** Incorrect (in the ADR, not the spec)

**Severity:** High -- The incorrect ADR claim could mislead future implementers into unnecessary workarounds. While the spec itself is correct, the referenced ADR needs a correction that should be tracked.

**Recommendation:** Add to the "ADR Updates Needed" section: the Memento ADR line 123 needs correction to reflect that abstract classes CAN implement `IMemento<TSelf, TMemento>` with a concrete static method.

### H-3: Hydrate method on Entity memento variant -- validation not specified

**Spec Reference:** Lines 104, 107, 157 (Hydrate description and acceptance criteria)

**Evidence:**
- Spec line 104: `Hydrate(TMemento) -- calls HydrateCore (in-place state update)`
- Spec line 156: `Restore()` calls Validate, throws ValidationException on errors
- Spec line 157: `Hydrate()` calls HydrateCore -- no mention of validation
- Existing ValueObject pattern (`src/Yaf.Domain/ValueObject{TSelf,TMemento}.cs`): `Restore` calls `Validate`, but there is no `Hydrate` on ValueObject (immutable)

**Gap Description:** The spec defines that `Restore` validates and throws `ValidationException`, but is silent on whether `Hydrate` should also validate after `HydrateCore`. This is a meaningful design decision: if Hydrate is used to reload state from a persisted memento (database), should invalid data from a corrupted or migrated memento throw? Or should Hydrate trust the data because it came from the persistence layer?

**Category:** Ambiguous

**Severity:** High -- If Hydrate does not validate, corrupted persistence data silently produces invalid domain objects. If it does validate, it could break loading of entities that were valid under a previous schema version. Either choice is defensible but must be explicit.

**Recommendation:** Add an explicit decision to the spec: either (a) Hydrate calls Validate after HydrateCore and throws ValidationException, or (b) Hydrate trusts the memento data without validation, with documented rationale. Add a corresponding acceptance criterion.

---

## Medium-Severity Issues

### M-1: TypedId<T> -- no constraint on T specified

**Spec Reference:** Lines 69-74 (TypedId Design), line 138 (acceptance criterion)

**Evidence:**
- Spec line 71: `TypedId<T>` -- no mention of any constraint on `T`
- Spec line 138: *"Works with `int`, `long`, `string` backing types (not just Guid)"*
- ADR line 120: `record struct TypedId<T>(T Value)` -- also no constraint on T

**Gap Description:** `TypedId<T>` has no constraint on `T`. Should `T` be constrained to `IEquatable<T>` (to enable equality comparison on the Value)? Since TypedId is a record class, record equality will use `EqualityComparer<T>.Default`, which works for most types. But without `IEquatable<T>`, the behavior is implicit and undocumented. Also, should `T` be constrained to `notnull` to prevent `TypedId<string?>` or `TypedId<int?>`?

**Category:** Ambiguous

**Severity:** Medium -- The lack of constraints will not cause compilation errors for common cases (Guid, int, long, string) but leaves the door open to misuse with nullable or non-equatable types.

**Recommendation:** Consider adding `where T : notnull` at minimum. Consider `where T : notnull, IEquatable<T>` for stronger compile-time safety. Document the decision either way.

### M-2: Entity<TId> is a class, not a record -- equality operator overloads require care

**Spec Reference:** Lines 79-94 (Entity Equality Implementation)

**Evidence:**
- Spec line 93: *"Overloads `==` and `!=` operators"*
- Spec line 34: `Entity<TId>` is an *abstract class* (not a record)
- C# behavior: for classes, `==` defaults to reference equality unless overloaded

**Gap Description:** The spec says Entity overloads `==` and `!=`, which is correct for identity-based equality. However, the spec does not mention that the operators should accept `Entity<TId>?` (nullable) parameters to handle null comparisons correctly. The pseudocode at line 82 shows `if other is null -> false` for Equals, but the operator overloads are not specified with the same detail. Standard .NET pattern is:

```csharp
public static bool operator ==(Entity<TId>? left, Entity<TId>? right)
    => Equals(left, right);
```

**Category:** Incomplete

**Severity:** Medium -- Incorrect operator overloads are a common source of null-reference bugs or infinite recursion (`==` calling `Equals` calling `==`). The spec should specify the pattern to prevent this.

**Recommendation:** Add pseudocode or a note for the `==`/`!=` operator overloads specifying: accept nullable parameters, delegate to `Equals(object?, object?)` static method (which handles nulls), avoid infinite recursion.

### M-3: Snapshot method on Entity/AggregateRoot memento variants -- Id inclusion unclear

**Spec Reference:** Line 102 (`Snapshot(TMemento) -- calls SnapshotCore (includes Id)`)

**Evidence:**
- Spec line 102: *"Snapshot(TMemento) -- calls SnapshotCore (includes Id)"*
- ValueObject pattern (`src/Yaf.Domain/ValueObject{TSelf,TMemento}.cs` line 22-26): `Snapshot` calls `SnapshotCore` -- the consumer's `SnapshotCore` decides what to include

**Gap Description:** The parenthetical "(includes Id)" is ambiguous. Does the base `Snapshot` method automatically write the Id to the memento (how -- via what property name?), or does it mean that the consumer's `SnapshotCore` implementation is responsible for including the Id? If the base class does it, the memento needs a known property (e.g., `Id` of type `Guid`), which couples the base class to memento structure. If the consumer does it, the parenthetical is just a reminder, not a contract.

**Category:** Ambiguous

**Severity:** Medium -- If the implementer interprets this as "base class auto-writes Id", they would need a convention for how to access the Id property on TMemento, which is a significant design choice not specified anywhere.

**Recommendation:** Clarify whether Id inclusion in the memento is the consumer's responsibility (in `SnapshotCore`) or automatic. If consumer responsibility, change the comment to something like "consumer must include Id in SnapshotCore". If automatic, specify the mechanism.

### M-4: DomainEvents property -- behavior when no events have been raised

**Spec Reference:** Lines 117-118 (Domain Events Collection)

**Evidence:**
- Spec line 117: *"private List<IDomainEvent> _domainEvents (lazy-initialized with ??=)"*
- Spec line 118: *"public IReadOnlyCollection<IDomainEvent> DomainEvents -> _domainEvents.AsReadOnly()"*

**Gap Description:** If `_domainEvents` is null (lazy initialized), what does `DomainEvents` return? `_domainEvents?.AsReadOnly()` returns null, which breaks the `IReadOnlyCollection<IDomainEvent>` contract. The getter must either (a) use `??=` itself to initialize the list, or (b) return `Array.Empty<IDomainEvent>()` when the backing list is null. Option (a) means reading `DomainEvents` allocates a List even when no events exist. Option (b) returns a different collection type (array vs ReadOnlyCollection).

**Category:** Ambiguous

**Severity:** Medium -- Infrastructure code calling `DomainEvents` (e.g., to dispatch events) would get a NullReferenceException if this is not handled correctly.

**Recommendation:** Specify the exact behavior of the `DomainEvents` getter when no events have been raised. A common pattern: `public IReadOnlyCollection<IDomainEvent> DomainEvents => (_domainEvents ?? []).AsReadOnly()` or use the `??=` pattern in the getter.

---

## Low-Severity Issues

### L-1: Brainstorm says AggregateRoot carries concurrency token; ADR and spec say it does not

**Spec Reference:** Line 238 (References section, referencing Cross-Cutting Infrastructure ADR)

**Evidence:**
- Brainstorm `docs/brainstorms/20260322-2036-yaf-ddd-concepts-brainstorm.md` line 43: *"`AggregateRoot<TId>` -- Entity that serves as a consistency boundary. Owns domain event collection. **Carries optimistic concurrency token.**"*
- Brainstorm line 208: *"`AggregateRoot<TId>` carries a concurrency token."*
- ADR `docs/adr/domain/20260324-1032-domain-building-blocks.md` line 108: *"**Does not carry a concurrency token itself** -- optimistic concurrency is opt-in via `IHasVersionInfo`"*
- Spec line 238: references Cross-Cutting Infrastructure ADR, states concurrency is NOT on AggregateRoot

**Gap Description:** The brainstorm (earlier document) says AggregateRoot carries a concurrency token. The ADR and spec (later documents) explicitly say it does not. The brainstorm was superseded but not corrected. This is not a spec problem per se, but it is a documentation inconsistency that could confuse someone reading the brainstorm.

**Category:** Incorrect (in brainstorm, not spec)

**Severity:** Low -- The ADR and spec are correct and consistent with each other. The brainstorm is a draft and is expected to be superseded.

**Recommendation:** No action needed for the spec. Optionally, add a note to the brainstorm that section 16 was superseded by the Cross-Cutting Infrastructure ADR.

### L-2: ADR says TypedId uses implicit/explicit conversion from T; spec says no conversions

**Spec Reference:** Line 74 (TypedId Design)

**Evidence:**
- ADR `docs/adr/domain/20260324-1032-domain-building-blocks.md` line 92 (Type Catalog): *"Construction: Implicit/explicit conversion from `T`"*
- Spec line 74: *"No implicit/explicit conversion operators -- consumers use `id.Value` or `new OrderId(guid)`"*
- Spec line 248-249 (ADR Updates Needed): does not mention this change

**Gap Description:** The ADR says TypedId has implicit/explicit conversions; the spec explicitly removes them. This is a reasonable simplification but is not listed in the "ADR Updates Needed" section.

**Category:** Incomplete

**Severity:** Low -- Minor omission in the ADR updates tracking. The spec is clear about the decision.

**Recommendation:** Add to "ADR Updates Needed": no implicit/explicit conversion operators on TypedId.

### L-3: Test file naming -- spec uses singular filenames, existing tests use plural

**Spec Reference:** Lines 216-219 (New Test Files)

**Evidence:**
- Spec line 216: `TypedIdTests.cs`, `EntityTests.cs`, `AggregateRootTests.cs`
- Existing test file: `tests/Yaf.Domain.Tests/ValueObjectTests.cs`

**Gap Description:** The naming is actually consistent (both use `*Tests.cs` suffix). No issue here upon closer inspection.

**Category:** N/A -- False alarm, naming is consistent.

**Severity:** N/A

---

## Clarification Needed

### Q-1: Should Hydrate validate? (See H-3)

Should `Entity<TId, TSelf, TMemento>.Hydrate(TMemento)` call `Validate()` after `HydrateCore()` and throw `ValidationException` on failure, or should it trust the memento data?

### Q-2: Should TypedId<T> constrain T? (See M-1)

Should `T` in `TypedId<T>` be constrained to `notnull`, `IEquatable<T>`, both, or neither?

### Q-3: Who writes the Id to the memento? (See M-3)

Does the base Entity `Snapshot` method automatically include the Id in the memento, or is the consumer's `SnapshotCore` responsible?

### Q-4: What does DomainEvents return when empty? (See M-4)

What is the exact behavior of `AggregateRoot<TId>.DomainEvents` when no events have been added? Does the getter initialize the lazy list, return an empty array, or something else?

---

## Extra Features (Not in ADR)

### E-1: ITypedId non-generic marker interface

**Evidence:** Spec lines 28, 69 introduce `ITypedId` (non-generic) as a new interface not mentioned in the ADR.

**Assessment:** This is a sensible addition that enables `where TId : ITypedId` constraints without requiring the generic parameter. The ADR should be updated to include it.

### E-2: HydrateCore template method on Entity/AggregateRoot

**Evidence:** Spec lines 104, 107, 111. The Memento ADR defines `Hydrate(TMemento)` on `IHydratable` but does not define a `HydrateCore` template method pattern for base classes.

**Assessment:** This is a natural extension of the template method pattern used for Snapshot/Restore. Consistent with the existing design.

---

## Positive Observations

1. **Pattern consistency**: The spec closely follows the established `ValueObject<TSelf, TMemento>` pattern for the memento variants, which will make implementation straightforward.

2. **Single-inheritance handling**: The spec correctly identifies and addresses the C# single-inheritance limitation for `AggregateRoot<TId, TSelf, TMemento>` (line 65).

3. **Lazy initialization for RuntimeHelpers**: The spec correctly references the `??=` pattern for domain events collection (line 123), acknowledging that `GetUninitializedObject` bypasses field initializers.

4. **Clear acceptance criteria**: The acceptance criteria (lines 128-179) are specific and testable.

5. **File plan**: The proposed file structure (lines 200-219) is consistent with existing project conventions.

6. **Implementation order**: The dependency-ordered implementation sequence (lines 222-229) is correct.

---

## Findings Summary

| ID | Category | Severity | Title |
|----|----------|----------|-------|
| C-1 | Incomplete | Critical | ADR says record struct, spec says record class -- ADR not yet updated |
| H-1 | Incomplete | High | ADR TId constraint contradicts spec -- rationale missing |
| H-2 | Incorrect | High | Memento ADR contains disproven C# limitation claim |
| H-3 | Ambiguous | High | Hydrate validation behavior not specified |
| M-1 | Ambiguous | Medium | No constraint on TypedId<T> generic parameter |
| M-2 | Incomplete | Medium | Equality operator overload pattern not fully specified |
| M-3 | Ambiguous | Medium | Id inclusion in Snapshot unclear |
| M-4 | Ambiguous | Medium | DomainEvents getter behavior when no events raised |
| L-1 | Incorrect | Low | Brainstorm contradicts ADR on concurrency token (brainstorm is stale) |
| L-2 | Incomplete | Low | Conversion operator change not tracked in ADR Updates Needed |

---

## Recommendations

### Before Implementation

1. **Update the Domain Building Blocks ADR** (C-1, H-1, L-2): Change TypedId from record struct to record class, change TId constraint to `ITypedId`, add `ITypedId`/`ITypedId<T>` to the type catalog, remove implicit/explicit conversion from TypedId construction column. This should be a prerequisite step, not a follow-up.

2. **Correct the Memento ADR** (H-2): Remove or correct the incorrect claim at line 123 about abstract classes not being able to implement `static abstract` interfaces. Reference the solutions doc as evidence.

3. **Resolve ambiguities** (H-3, M-1, M-3, M-4): Answer the clarification questions Q-1 through Q-4 and update the spec with explicit decisions.

### During Implementation

4. **Specify operator overload pattern** (M-2): When implementing `==`/`!=`, use the standard nullable-safe pattern to avoid null-reference bugs and infinite recursion.

### Optional

5. **Update brainstorm** (L-1): Add a note that section 16 (concurrency on AggregateRoot) was superseded. Low priority since brainstorms are draft documents.
