# Session Diary: Cross-Cutting Domain and Memento Interfaces

**Date:** 2026-03-27 to 2026-03-28
**Duration:** Extended session (~10 hours of interaction)
**Branch:** `feat/cross-cutting-domain-interfaces`
**Commits:** 23

---

## Goal

Implement opt-in cross-cutting concern interfaces for Yaf.Domain: accountability (who created/modified), timestamps (when), soft-deletion, multi-tenancy, and versioning. These extend the existing memento-based persistence pattern with paired domain-side and memento-side interfaces.

## What Was Achieved

### Planning Phase
- Feature plan at `docs/plans/20260327-0936-feat-cross-cutting-domain-interfaces-plan.md`
- Two spec audits (v1 and v2) at `docs/verification/`
- Cross-cutting infrastructure ADR updated

### Implementation
- 10 new interface files (6 domain-side, 4 memento-side + 2 version markers)
- `TenantId` framework-provided typed identifier
- 4 helper classes: `MementoHelper` (identity + conditional builders), `ReflectionHelper` (generic compiled property access), `BoxingHelper` (shared DIM unboxing), `TypedIdBridge` (typed ID boxing/unboxing)
- Auto-mapping in Entity/AggregateRoot base classes for timestamps, accountability, soft-delete
- `ValueObject.RestoreCore` renamed to `HydrateCore` for consistency
- `Restore` delegates to `Hydrate` — `RestoreCore` eliminated

### Testing
- 90 tests total (58 existing + 32 new)
- `MementoBridgeTests` covering round-trips, backward compatibility, version markers
- `BoxingHelperTests` and `ReflectionHelperTests` for the generic utilities

### Documentation
- Solution document: `docs/solutions/design-patterns/cross-cutting-interfaces-dim-boxing-and-typed-id-bridging.md`
- CHANGELOG updated
- Code review report, pragmatic review v1 and v2

### Reviews
- 5-agent parallel code review (architecture, simplicity, patterns, performance, security)
- 2 pragmatic reviews (before and after KISS simplification)
- 1 code quality review

## What Went Well

### Iterative refinement driven by user feedback
The most effective pattern in this session was short feedback loops. The user caught design issues early — "domain interfaces shouldn't have boxing," "ISoftDeletable shouldn't inherit ITimestamped," "non-generic markers aren't needed" — and each correction took 5-10 minutes to implement rather than requiring a large rework.

### Multi-agent reviews surfaced real issues
The 5-agent code review found the Entity/AggregateRoot duplication, the `_handlers ??=` thread safety gap, the `BindingFlags` inconsistency, and the test type accessibility issue. The pragmatic review identified the most impactful simplification (removing the full auto-bridge for KISS). These were genuinely useful, not just noise.

### KISS principle applied effectively
The session started with a complex reflection-based MementoBridge (~350 lines). The pragmatic review challenged it ("is this worth it for zero consumers?"). The user agreed, we stripped it down, then selectively re-added auto-mapping only for concerns that genuinely save consumer boilerplate. The final design is ~40% less code than the peak.

### Naming discussions produced clearer APIs
Renaming `IVersionable` → `IHasVersionHistory`, `CrossCuttingHandlers` → `MementoBridge`, `RestoreCore` → `HydrateCore` all came from the user pointing out naming inconsistencies. Each rename made the API clearer.

## What Went Wrong

### Over-engineering on first pass
The initial implementation built a full reflection-based auto-bridge (MementoBridge with compiled delegates for all concerns, TypedIdFactoryCache, ConcurrentDictionary caching) before validating that this level of automation was needed. The pragmatic reviewer was right: for a framework with zero consumers, this was premature.

**Root cause:** I defaulted to building the most capable solution rather than the simplest one. The plan described auto-handling, so I implemented it fully rather than starting with manual mapping and adding automation incrementally.

### Global replace errors
Several `replace_all` edits had unintended scope — replacing `handlers.` with `Bridge.` inside the `MementoBridge.Build()` method where `handlers` was a local variable, not the field being renamed. This caused compilation errors that needed manual cleanup.

**Root cause:** Using broad `replace_all` on common variable names without scoping the replacement to specific code sections.

### LSP phantom diagnostics
The C# language server persistently reported errors referencing a deleted file (`CrossCuttingConcernTests.cs`) throughout the session. This created noise — every edit showed 10+ phantom errors that masked real issues. The build always succeeded, confirming these were stale LSP state.

**Root cause:** The LSP cache didn't pick up the file rename (`CrossCuttingConcernTests.cs` → `MementoBridgeTests.cs`). No workaround found during the session.

### C# generics nullability complexity
The `T?` semantics for unconstrained generics (where `T : IEquatable<T>`) were initially wrong — `T?` doesn't mean `Nullable<T>` for value types without a `struct` constraint. This required adding `struct` to all memento generic interfaces and changing memento properties from `Guid` to `Guid?`. The `IsDefaultOrNull` hack (testing against `Guid.Empty`) was a symptom of this misunderstanding.

**Root cause:** The initial plan specified `T : IEquatable<T>` following the existing `IHasIdentity<T>` pattern, but didn't account for the fact that making properties nullable required a `struct` constraint.

## Planning & Review Analysis

### Planning Phase

**Sound from the start:**
- The interface hierarchy (domain-side get-only + memento-side get/set with DIM boxing) — this was correct from the first draft and survived all reviews unchanged
- The paired interface naming convention (`IAccountable` / `IHasAccountability`)
- Composition over inheritance for ISoftDeletable
- The deletion lifecycle (exclusive-paths model)

**Required further discussion (6 rounds of clarification):**
1. Naming: ADR names vs user's names → resolved: follow ADR
2. Deletion fields: on ITimestamped or separate ISoftDeletable → evolved through 3 iterations
3. IAccountable generics: should non-generic base have boxing → resolved: no, domain side has no boxing
4. CreatedBy nullability: non-nullable vs nullable → resolved: nullable (null = not persisted yet)
5. ISoftDeletable inheritance: extends ITimestamped or standalone → resolved: standalone (composition)
6. IHasVersionHistory: extends IHasVersionInfo or independent → resolved: independent

**Effective changes from spec audit:**
- Fixed 3 stale text references to ISoftDeletable : ITimestamped (after inheritance was removed)
- Fixed file org table reference to IHasVersionHistory "extends IHasVersionInfo"
- Added documentation for implicit interface implementation requirement
- Acknowledged the ISoftDeletable non-generic marker asymmetry

### Code Review Phase

**Actionable findings (implemented):**
- P2-1: Entity/AggregateRoot duplication → extracted `WriteCrossCuttingConcerns`/`ReadCrossCuttingConcerns` (later superseded by KISS simplification)
- P2-2: Thread safety `_handlers ??=` → replaced with `Lazy<MementoBridge>` (later removed entirely with KISS)
- P2-4: Redundant `IsAssignableFrom` + `FindGenericInterface` double-checks → simplified
- P3-6: `IHasSoftDelete.ActorIdType` nullable → made non-nullable
- W4 (code review): `BindingFlags` inconsistency → fixed

**Noise (not implemented):**
- P2-3: Test types `internal` vs `private` — `file` scoped types don't work because public test classes reference them; no actual collision exists
- P3-5: IHasVersionInfo/IHasVersionHistory YAGNI — kept as low-cost forward declarations

**Most impactful review finding:**
The pragmatic review's observation that the full MementoBridge was over-engineered. This led to the KISS simplification that removed ~270 lines, then the targeted re-addition of auto-mapping for only the concerns that genuinely save boilerplate.

## Other Notes

- The `file` keyword for file-scoped types in C# cannot be used when public types in the same file reference those types. This limits its use for test fixture types that are shared across test classes in the same file.
- `nameof(IAccountable<ITypedId>.CreatedBy)` fails with CS8920 because `ITypedId` has `static abstract` members. Workaround: use the memento-side generic with a concrete type, e.g., `nameof(IHasAccountability<Guid>.CreatedBy)`.
- The 21-commit refinement journey (from full auto-bridge → KISS removal → targeted re-addition) demonstrates that "build, review, simplify" is more effective than trying to get the design perfect upfront. Each iteration was informed by concrete code, not abstract planning.

## Communication Assessment

### What was clear
- The user's design intent was always communicated directly: "make CreatedBy nullable," "remove non-generic markers," "KISS approach is better." Short, precise instructions that were immediately actionable.
- The user caught naming and design inconsistencies early and redirected quickly — "ISoftDeletable inherits ITimestamped but generic doesn't inherit IAccountable, that's inconsistent."
- Review requests (`/maister:reviews-spec-audit`, `/compound-engineering:\workflows:review`, `/maister:reviews-pragmatic`) were well-timed — after implementation, not before.

### What was ambiguous
- "Simple mapper for accountable, timestamped, deletion" — it wasn't immediately clear whether this meant auto-mapping in the base class (which we'd just removed for KISS) or protected helper methods consumers call. A short clarifying question would have saved a wasted implementation attempt.
- The scope of "KISS" — did the user want to remove all auto-mapping, or just the reflection-based bridge? The answer turned out to be "remove the bridge, keep ReflectionHelper for future use" — which was clarified through discussion.

### What could improve
- **Claude side:** Should have asked "are we re-adding auto-mapping or keeping it manual?" before implementing the TypedIdBridge. The KISS simplification removed auto-mapping, then the next request re-added it — a clarifying question would have avoided the round-trip.
- **Human side:** The session was very long. Breaking it into 2-3 shorter sessions (plan → implement → review+simplify) might have reduced context fatigue and the cascading rename issues.
- **Both sides:** The `replace_all` errors suggest we should prefer targeted edits over global replacements, especially for common identifier names. A "verify the diff" step before each commit would catch these.
