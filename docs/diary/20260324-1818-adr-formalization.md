# Session Diary: ADR Formalization

**Date:** 2026-03-24
**Duration:** ~4 hours
**Participants:** @cartomatic (decision-maker), Claude Code (drafter)

---

## Goal

Formalize the architectural decisions from prior brainstorm sessions into ADRs using the template and folder structure established earlier. The initial scope was 13 topics; the session expanded to 24 ADRs as new topics and refinements emerged during discussion.

## What Was Achieved

- **24 ADRs drafted** across four scopes:
  - Architecture (11): core architecture style, technology stack, solution structure, testing strategy, development workflow, CI/CD, CQRS/mediator, application layer patterns, public API documentation, warnings as errors, coding style
  - Domain (5): building blocks, memento pattern, domain/integration events, result/error pattern, validation strategy
  - Infrastructure (5): persistence strategy, cross-cutting concerns, observability, data consistency, multi-tenancy
  - API (3): controller adapter, OpenAPI documentation, authentication and authorization
- **INDEX.md** created at `docs/adr/` root grouping all ADRs by scope
- **Cross-review** performed by an Explore agent — found 3 broken cross-references (fixed), plus 5 substantive questions raised and resolved with the user
- All ADRs set to **under review** status pending implementation

## Key Discussions and Design Decisions Made During the Session

The most valuable part of this session was not the initial drafting but the iterative refinement through user feedback. Several significant design adjustments emerged:

### 1. Value Objects as Records (ADR #7)
Initial draft proposed an abstract `ValueObject` base with manual `GetEqualityComponents()`. User correctly pointed out that C# records provide immutability and structural equality for free — no point reimplementing what the framework handles. Changed to a minimal marker base record.

### 2. Domain Object Read Access (ADR #8)
Initial memento ADR implied "no public getters" for full encapsulation. User identified the flaw: the application layer needs to read domain state for DTO projection. Without public getters, projection logic would have to live in the domain, polluting it with ever-growing data perspective concerns. Adjusted to: public getters with private setters. Memento handles the persistence write boundary; public getters handle the read boundary.

### 3. Unit of Work Redesign (ADR #17)
This was the most significant mid-session redesign. The initial UoW was a simple SaveChanges wrapper. Through two rounds of feedback:
- **First round:** User explained that UoW needs to wrap multiple SaveChanges calls (for ID generation, etc.) and that domain events should dispatch within the transaction for rollback on handler failure.
- **Second round:** User proposed `ExecuteAsync(operation)` + `RequestTransactionRollback()` — the UoW owns the transaction lifecycle entirely, the consumer never touches it, and rollback integrates with the Result pattern without throwing exceptions. This is cleaner than manual Begin/Commit/Rollback and aligns perfectly with CQRS pipeline integration.

### 4. Tenant-Scoped Privileges (ADR #22)
Initial auth ADR had a flat privilege model. User expanded it significantly: users belong to multiple tenants with different roles/privileges per tenant. Privileges are granted within a tenant scope. This shaped the `IPrivilegeStore` API and the relationship between identity resolution and tenant resolution.

### 5. Context Propagation Across Services (ADR #13)
User identified that context providers need to survive three types of cross-service hops: integration events, service-to-service HTTP calls, and API calls. Initial draft only covered HTTP requests. Added automatic outbound propagation via HttpClient delegating handlers and message header enrichers.

### 6. Memento Schema Evolution Responsibility (ADR #8)
User caught a contradiction: `IMemento<TMemento>` is strongly typed, so `Hydrate(TMemento)` always receives the current shape. The "tolerant reader" concern was incorrectly placed on the domain method. Corrected: schema drift for historical data (versioning/graveyard JSON) is handled by Infrastructure's JSON deserialization, not by the domain's `Hydrate` method.

### 7. Persistence Package Granularity (ADR #3, #14)
User requested concrete infrastructure splitting examples: `Yaf.Infrastructure.Persistence` (contracts) → `Yaf.Infrastructure.Persistence.EfCore` (EF Core) → `Yaf.Infrastructure.Persistence.EfCore.PgSql` (PostgreSQL). Also introduced the DbMigrator concept — a short-lived init container for migrations, solving the scale-out concurrent migration problem.

### 8. Migration Conflict Prevention (ADR #14)
User provided a practical solution for concurrent EF Core migration conflicts: treat `DbContextModelSnapshot.cs` as binary in `.gitattributes`. This forces git to signal conflicts instead of silently merging divergent snapshots.

### 9. Pipeline Order: Sanitization Before Validation (ADR #12)
User requested sanitization before validation in the CQRS pipeline. Initial drafts had mixed ordering. Corrected across four ADRs to be consistent: Sanitization → Validation → Handler.

### 10. Example Application Structure (ADR #3, #4)
User wanted examples to be self-contained services with their own `src/`, `tests/`, `docs/` — mimicking real consumer projects. Also emphasized that example tests serve as executable documentation and must be fully tested.

## What Went Well

- **One-at-a-time workflow was effective.** Going through ADRs sequentially let the user review each before moving on. Quick "ok" / "go on" kept momentum; detailed feedback arrived precisely when the user had something to adjust.
- **User's domain expertise surfaced real design issues.** The UoW redesign, tenant-scoped privileges, and memento schema evolution corrections all came from the user spotting practical problems that abstract drafting missed.
- **Cross-review caught real issues.** The automated review found broken cross-references and raised substantive questions that led to 5 meaningful improvements.
- **Incremental refinement pattern.** Several ADRs were updated multiple times as later discussions revealed implications (e.g., the domain events ADR was updated when the UoW design changed, then again when pipeline order was corrected).

## What Went Wrong

- **Initial pipeline order inconsistency.** Sanitization vs validation ordering was inconsistent across ADRs (some said validation first, others sanitization first). This wasn't caught until the user explicitly requested the correction. Should have established the pipeline order once and propagated it consistently from the start.
- **Memento encapsulation was too aggressive initially.** The "no public getters" stance was technically pure DDD but practically unworkable. This was caught early (ADR #8) but indicates a pattern of defaulting to theoretical purity over practical needs.
- **Pending cross-references.** Three ADRs were written with `(pending)` links to ADRs that hadn't been created yet. These were only caught during the cross-review, not at write time. Should have used relative paths with a TODO marker that's easier to grep for.
- **Session length.** 4 hours for 24 ADRs is efficient but long. The quality of later ADRs benefited from earlier discussions, but fatigue risk is real for both human and AI.

## Other Notes

- The user's feedback style was consistently precise — short corrections with clear reasoning. This made updates fast and unambiguous.
- Several decisions were explicitly deferred (messaging/outbox, sagas, read models, MinimalApis) — this was a deliberate scoping choice, not oversight.
- The ADR template from the earlier brainstorm session worked well in practice. The Drivers → Options → Recommendation → Consequences flow forced structured thinking on each decision.
- Total output: 25 files, ~4200 lines of architectural documentation.

## Communication Assessment

### What Was Clear

- **User's direction signals were unambiguous.** "ok", "go on", "yup" meant proceed as-is. Detailed feedback meant stop and adjust. No ambiguity about whether a change was requested.
- **User's corrections were specific and actionable.** "Value object — let's use record type for this" is a clear, implementable instruction. No guesswork needed.
- **Questions from Claude were well-received.** Asking about branching strategy, CI platform, and NuGet signing before drafting those ADRs avoided wasted work. The user answered concisely and added context beyond what was asked (e.g., the prerelease package intent).
- **The one-at-a-time review cadence worked.** Presenting each ADR with a brief summary of key points let the user decide quickly whether to proceed or adjust.

### What Was Ambiguous

- **"lets jump back to core architecture style" actually meant the Solution Structure ADR.** The user's intent was about package granularity (persistence splitting), which lives in the Solution Structure ADR, not the Core Architecture Style ADR. Claude correctly understood the intent from context, but the reference was technically to the wrong ADR.
- **Scope of "containerization" was initially unclear.** The brainstorm mentioned Docker + Aspire but didn't clarify whether containers were for the library or just examples. This was resolved by asking, but could have been clearer in the brainstorm.

### What Could Improve

- **Claude side:** Establish cross-cutting decisions (like pipeline order) once and propagate immediately, rather than discovering inconsistencies later. When writing a series of related ADRs, maintain an explicit "decisions so far" running list.
- **Human side:** When referencing an existing ADR for modification, including the ADR number or a keyword from the title would reduce ambiguity (though in practice this wasn't a significant problem — context made intent clear).
- **Both sides:** For very long sessions, a mid-session checkpoint ("here's where we are, here's what's left") would help maintain alignment and catch drift early.
