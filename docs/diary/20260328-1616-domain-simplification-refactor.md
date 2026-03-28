# Session Diary: Domain Simplification Refactor

**Date:** 2026-03-28
**Duration:** ~8 hours (single extended session)

---

## Goal

Introduce domain event interfaces (`ICorrelated`, `IActorScoped`, `IActivityScoped`, enriched `IDomainEvent`, `IDomainEvent<out T>`) and — driven by the design needs of those interfaces — progressively simplify the entire domain layer by removing all generic type parameters, eliminating compiled expression infrastructure, and establishing a clean read-only/write-only interface pattern for memento mapping.

## What Was Achieved

### PR #17: TypedId Guid-only simplification (merged)

- `TypedId<T>` → `TypedId` (always Guid)
- Collapsed two-tier memento interfaces (IHasIdentity, IHasAccountability, IHasTenantId, IHasSoftDelete) from non-generic base + generic DIM variant → single-tier with direct `Guid?` properties
- Deleted `BoxingHelper`
- Simplified `TypedIdBridge` and `MementoHelper`
- 81 tests passing

### PR #18: Domain event interfaces + non-generic simplification (open)

Built in progressive commits, each one simplifying further:

1. **Domain event interfaces** — `ICorrelated`, `IActorScoped`, `IActivityScoped`, enriched `IDomainEvent`, `IDomainEvent<out T>` with covariance
2. **Non-generic domain interfaces** — introduced `ActorId` framework type; `ITenantScoped<TTenantId>` → `ITenantScoped`, `IAccountable<TActorId>` → `IAccountable`, `ISoftDeletable<TActorId>` → `ISoftDeletable`
3. **Deleted TypedIdBridge + TypedIdFactoryCache** — no longer needed with non-generic interfaces
4. **Eliminated compiled property writers** — domain interfaces got setters, then corrected to read-only with internal writer complements (`ITimestampedWriter`, `IAccountableWriter`, `ISoftDeletableWriter`)
5. **Deleted ReflectionHelper entirely** — zero callers after compiled writer removal
6. **Replaced compiled expression factory with `Activator.CreateInstance`** — last piece of `System.Linq.Expressions` gone
7. **Extracted cross-cutting mapping into MementoHelper** — eliminated ~50 lines of duplication between Entity and AggregateRoot

**Net result across both PRs:**
- Started: 90 tests, complex generic infrastructure (TypedIdBridge, TypedIdFactoryCache, BoxingHelper, ReflectionHelper, compiled expressions, DIM boxing, two-tier memento interfaces, generic domain interfaces)
- Ended: 77 tests, zero compiled expressions, zero reflection helpers, direct interface reads/writes, non-generic domain interfaces, framework ActorId/TenantId types

### Documentation produced

- `docs/plans/20260328-0738-refactor-typedid-guid-only-simplification-plan.md` (completed)
- `docs/plans/20260328-0738-feat-domain-event-interfaces-plan.md` (completed)
- `docs/solutions/design-patterns/typedid-guid-only-simplification.md`
- `docs/solutions/design-patterns/non-generic-domain-interfaces-and-direct-memento-mapping.md`
- ADR updates for domain building blocks and cross-cutting infrastructure
- CHANGELOG entries

## What Went Well

- **Progressive simplification** — each commit revealed the next simplification opportunity. The user's guidance was instrumental: "if ITypedId allows different types, is that sensible?" led to Guid-only, which led to non-generic interfaces, which led to TypedIdBridge deletion, which led to ReflectionHelper deletion, which led to compiled expression elimination. Each step was a natural consequence of the previous one.
- **Compiler-driven refactoring** — removing a generic parameter or interface member produced build errors at every call site. The compiler was the checklist. Zero runtime surprises.
- **Test suite as safety net** — 77-91 tests (count varied as tests were added/removed) caught every round-trip regression. The memento Snapshot/Restore/Hydrate tests were particularly valuable.
- **Review agents caught real issues** — dead code (`FindGenericInterface`, `BuildPropertyReader`), stale XML docs (`Activator.CreateInstance` reference), and ADR inconsistencies were all found by review agents and fixed before merge.
- **User steering was decisive** — "domain interfaces should be read-only" corrected a pragmatic-but-wrong shortcut (adding setters to domain interfaces). The internal writer pattern is cleaner and properly separates concerns.

## What Went Wrong

- **Initially added setters to domain interfaces** — when simplifying Hydrate to use direct interface writes, I added `{ get; set; }` to `ITimestamped`, `IAccountable`, `ISoftDeletable`. The user correctly caught that domain interfaces must be read-only. Required an extra commit to introduce internal writer interfaces. Root cause: prioritizing simplicity over encapsulation without considering the domain contract semantics.
- **IUserScoped naming churn** — created `IUserScoped` with `Guid UserId`, then renamed to `IActorScoped` with `ActorId ActorId` when the non-generic simplification happened. Could have asked about naming earlier in the planning phase.
- **Multiple review rounds** — ran the compound-engineering review workflow twice on PR #18 (before and after the non-generic changes). The first review's findings were mostly invalidated by the subsequent refactoring. Could have batched the changes before reviewing.

## Planning & Review Analysis

- **Plan quality** — the initial two plans (TypedId simplification + domain events) were solid and executed cleanly. The non-generic interface simplification and Entity/AggregateRoot duplication extraction were not planned — they emerged during implementation from user guidance. This was appropriate: the plans scoped what was known; the user's insight during execution revealed further opportunities.
- **Review effectiveness** — the architecture, simplicity, performance, and pattern recognition agents consistently identified the Entity/AggregateRoot duplication (~50 lines) as the top concern. This was eventually addressed. Dead code detection (FindGenericInterface, BuildPropertyReader) was the most actionable finding — directly fixable.
- **Code review findings** — maister:reviews-code consistently found 0 critical issues across both runs. Warnings were legitimate but non-blocking (duplication, null ID semantics, stale docs).

## Communication Assessment

**What was clear:**
- The user's architectural direction was always clear and well-reasoned. "Domain interfaces should be read-only" was a precise correction with an obvious rationale.
- "ActorId/UserId type — this will simplify IAccountable too" — concise, pointed directly at the right generalization.
- "Domain object IDs should also be handled the same way as tenant IDs or user IDs" — clear pattern recognition that led to eliminating the compiled factory.

**What was ambiguous:**
- "Let's introduce ActorId/UserId type" — the slash suggested either name. Clarified quickly with a targeted question.
- The scope of "domain event interfaces" expanded significantly (from 3 new interfaces to a full domain layer simplification). This was driven by the user's insight during execution — the right call, but the plan artifacts became partially outdated.

**What could improve:**
- When a simplification chain becomes apparent (generic → non-generic → delete bridge → delete helper → delete expressions), propose the full chain upfront rather than discovering it commit by commit. The user saw it before I did.
- Ask "should this interface be read-only?" proactively for any domain interface, not after adding setters.

## Other Notes

- The `InternalsVisibleTo` on the test project was essential — without it, tests couldn't implement the internal writer interfaces.
- `Activator.CreateInstance` for TId construction is the last remaining reflection in the domain layer. It exists because `TId` is a generic type parameter and C# can't do `new TId(guid)`. A `static abstract` factory on `ITypedId` could eliminate it but would add boilerplate to every concrete ID type.
- The Entity/AggregateRoot duplication in Snapshot/Restore/Hydrate orchestration (~15 lines) still exists. The auto-mapping duplication (~50 lines) was extracted to MementoHelper. The remaining duplication is the public API methods themselves — harder to extract due to C# single inheritance.
