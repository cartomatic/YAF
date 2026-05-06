# Session Diary: Application Layer CQRS and Cross-Cutting Abstractions

**Date:** 2026-05-06
**Duration:** ~2 sessions across multiple days
**PR:** [#28](https://github.com/cartomatic/YAF/pull/28)
**Predecessor PR:** [#27](https://github.com/cartomatic/YAF/pull/27) (Yaf.Application project scaffold)

## Goal

Build the full application layer of YAF on top of the empty `Yaf.Application` project, implementing every contract called out by ADR-1144 (CQRS), ADR-1146 (Application Layer Patterns), and ADR-1141 (Validation Strategy). The output should be mediator-agnostic interfaces and a small DTO/audit record set that future adapter packages (Wolverine, MediatR) and consumer code can build against without coupling to any specific dispatch library.

## What Was Achieved

### Pre-flight (PR #27)
- Created `Yaf.Application` class library with reference to `Yaf.Domain` and `InternalsVisibleTo` for tests
- Created `Yaf.Application.Tests` xUnit project matching the domain test project setup
- Verified solution structure builds cleanly

### Core implementation (PR #28)
- 18 mediator-agnostic types across 6 sub-namespaces:
  - **Cqrs** — `ICommand`, `ICommand<out TResult>`, `ICommandHandler<in TCommand>`, `ICommandHandler<in TCommand, TResult>`, `IQuery<out TResult>`, `IQueryHandler<in TQuery, TResult>`
  - **Notifications** — `INotification`, `INotificationHandler<in TNotification>`
  - **Validation** — `IValidator<in T>` (async)
  - **Context** — `ITenantContextProvider`, `IIdentityContextProvider`, `ICorrelationIdProvider`, `IActivityIdProvider`
  - **Sanitization** — `[Sanitize]` attribute, `ISanitizer`
  - **Audit** — `BusinessLogEntry` (sealed record, 9 properties), `IBusinessEventLog`
- Two-level command hierarchy: non-generic `ICommand` (void) and generic `ICommand<out TResult>` (typed) with inheritance
- Covariant markers (`out TResult` on `ICommand<>` / `IQuery<>`), contravariant handler inputs (`in TCommand`/`in TQuery`/`in T`/`in TNotification`)
- 35 application-layer tests (210 total solution tests passing), 0 build warnings
- Zero external NuGet dependencies — only `Yaf.Domain` reference

### Specification and audit
- Spec went through audit (`spec-auditor`) which flagged 6 important findings; all resolved in the spec before implementation
- Reality assessment, code review, pragmatic review, and production readiness review all passed (auto-fixed 4 code-review warnings before merging)
- Documented 6 ADR drifts inline in source XML and consolidated in spec

### ADR backport (same PR)
- ADR-1141 status under review → accepted; `IValidator<T>` updated to async
- ADR-1144 status under review → accepted; added non-generic `ICommand`, covariance, two handler overloads, variance/CS1961 explanation
- ADR-1146 status under review → accepted; `[Sanitize]` attribute replaces `ISanitizable`, BusinessLogEntry field renames/type changes, parameter-based `AppendAsync`

## What Went Well

- **Maister `/work` orchestrator flow was effective**. Auto-classification routed straight to development, the phased structure (codebase analysis → gap analysis → spec → audit → plan → implementation → verification → finalize) caught issues at the right time.
- **Subagent delegation in implementation**. Each task group ran in a fresh `task-group-implementer` subagent. Group 1 surfaced the `where T : notnull` constraint propagation by hitting CS8714 immediately — that knowledge then carried through to all subsequent groups via the spec.
- **Spec audit caught real drifts before code was written**. All six findings (validator async-vs-sync, BusinessLogEntry field divergence, using-directive strategy, marker XML doc template, non-generic ICommand attribution, AppendAsync API ambiguity) were genuine and resolved by editing the spec, not patching code.
- **Reality assessment confirmed the design composes**. The integration test that runs Sanitize → Validate → Handle end-to-end with real Domain types (`ActorId`, `TenantId`) was the single most valuable piece of evidence that the abstractions actually work together.
- **User feedback after implementation drove meaningful improvements**. Five concrete items — AggregateId as Guid, covariant TResult attempt, "commands never throw" affirmation, query parity with commands, and `[Sanitize]` attribute over `ISanitizable` marker — all landed in the same PR with one round of revision.
- **Two-PR sequence kept changes reviewable**. Scaffolding (#27) and implementation (#28) as separate PRs gave a clean baseline for reviewing the actual abstractions.

## What Went Wrong

- **Stale LSP cache repeatedly fired false-positive diagnostics** (CS0234, CS0246, CS1574). After every group's file creation, the IDE/csharp-ls server fired diagnostics about unresolvable types in just-created files. Each time, an actual `dotnet build` succeeded. Root cause: csharp-ls doesn't refresh aggressively after directory-level file additions. Workflow cost was minimal — verified each diagnostic by running `dotnet build` and continued — but the false alarms were noisy. Future mitigation: explicitly note in subagent prompts that LSP staleness is expected and only `dotnet build` results matter.
- **Tried covariant `out TResult` on handlers despite knowing it wouldn't work**. The user pushed back twice on the variance choice. The first time I correctly explained CS1961 (Task<T> and Result<T> both invariant). The second time I retested anyway to produce evidence, which was the right call — but I should have been firmer the first time about the structural impossibility rather than leaving room for a second exchange. Saved compiler error message in ADR-1144 so this question doesn't get re-litigated.
- **Spec-creator subagent quietly drifted from ADR-1141** by switching `IValidator<T>` to async. The spec audit caught it (finding I1), and the user's eventual decision was to keep async — so the drift turned out to be correct — but the subagent should have either flagged the override explicitly or asked. Future mitigation: spec-creator prompts should require any deviation from cited ADRs to be called out in a dedicated section, not buried in rationale.
- **NU1900 NuGet vulnerability-fetch error from a stale Azure DevOps feed** kept tripping `--no-restore` chains. Worked around with `-p:NuGetAudit=false` but the feed should be removed from NuGet config. Filed as a recommended follow-up but not addressed in this PR.
- **Subagent reports occasionally claimed work that didn't quite match the file state**. For example, one report said "I removed the `<see cref>` references and replaced with prose" while the file still had crefs that the next build failed on. Trust-but-verify saved us — running build after each group caught these. Future mitigation: rely on actual diagnostics, not subagent self-reports, when deciding "is this group done?"

## Planning & Review Analysis

### Specification phase
- **Sound from the start**: Scope (full ADR coverage), file structure (one type per file with brace-style generics), XML doc requirements, no-external-dependencies constraint
- **Required clarification**: Two-level vs single-level command hierarchy, ISanitizable layer (Domain per ADR vs Application per user override), namespace organization (flat vs sub-namespaces), context provider sync vs async
- **Required after-the-fact override**: Async `IValidator<T>` (against ADR-1141), `[Sanitize]` attribute model (against ISanitizable design), `AggregateId` as Guid not string

### Spec audit
- **Actionable findings**: All 6 important items (I1–I6) were real. The most consequential was I6 (`AppendAsync` API shape) — choosing parameter-based over entry-based prevented an awkward init-overwrite pattern that would have been visible to every consumer.
- **Low noise**: Minor findings were mostly XML-doc tightening; reasonable to defer.
- **Good ROI**: Audit took ~3 minutes of user time and prevented six rounds of post-implementation rework.

### Code review (post-implementation)
- **Actionable findings**: 4 warnings, all auto-fixable in a single round (BusinessLogEntry.ActivityId required-but-nullable inconsistency; null-contract docs on three interfaces; null/empty/whitespace docs on AppendAsync; Metadata value-type guidance).
- **Pragmatic review** flagged ADR-essay XML docstring duplication (Medium) — defensible since IntelliSense surfaces docs at the point of use; left as-is.

### Reality check
- **Strongly affirmed the design**: 17 (later 18) types, all exercised by tests, integration test composing the full pipeline. No false completions detected.

## Other Notes

- **Pattern reuse from Domain layer was high-value**. `IDomainEvent<out T>` was the direct template for the covariant marker pattern; `IEncryptable` for the marker-with-XML-docs style; `Result<T>` and `Error` consumed directly. The Domain layer's groundwork made Application straightforward.
- **Documenting drift in source XML proved valuable**. Future readers of `BusinessLogEntry.cs`, `IValidator{T}.cs`, etc., now see the rationale for every deviation from the ADRs without having to dig through spec/audit history.
- **The `where TResult : notnull` constraint propagation was discovered, not designed**. Group 1's first build failed with CS8714 because `Result<T>` requires `notnull`. Adding the constraint to all four generic types that flow through `Result<T>` was the only way forward. The discovery was added to the spec/ADRs after implementation.
- **Workflow cost vs value**: ~46 implementation steps + ~6 verification phases. The structured workflow added overhead, but every phase produced an artifact that paid off later (codebase analysis informed gap analysis, spec audit pre-fixed the implementation, verification reports drove the auto-fixes).

## Communication Assessment

**What was clear:**
- The original task ask was unambiguous (full ADR scope, mediator-agnostic, in `Yaf.Application`)
- User's decisions on scope expansion (full ADR vs minimal CQRS), namespace organization, and design-time questions were decisive
- Post-implementation feedback was specific and actionable (5 concrete items)
- The reality of CS1961 was demonstrable with compiler output, which moved the conversation forward

**What was ambiguous:**
- "ICommandHandler can have covariant TResult" — I had already shown the user CS1961, but the second exchange was needed to retry with fresh evidence. Could have been preempted by leading with the compiler output the first time
- "the command comments also apply to queries; pls make them same in terms of return types" — multiple valid interpretations (full structural mirror with non-generic IQuery, vs comment-style alignment only). Asked a clarifying question; user chose comment alignment without adding non-generic IQuery
- The "Type free-text comments" affordance in `AskUserQuestion` was used and then comments were sent in a subsequent message — natural enough, but a slight friction point

**What could improve on both sides:**
- Future variance questions should pair the suggestion with "what would the compiler accept here?" so the answer is grounded in evidence
- Spec-creator subagent should be required to flag any divergence from cited ADRs in a dedicated section, not bury it in rationale text
- `dotnet test` and `dotnet build` should be the source of truth, not subagent self-reports — the orchestrator should always re-verify after each group

## Files Touched

**Source:**
- `src/Yaf.Application/Cqrs/` — 6 files (ICommand, ICommand{TResult}, ICommandHandler{TCommand}, ICommandHandler{TCommand,TResult}, IQuery{TResult}, IQueryHandler{TQuery,TResult})
- `src/Yaf.Application/Notifications/` — 2 files (INotification, INotificationHandler{TNotification})
- `src/Yaf.Application/Validation/` — 1 file (IValidator{T})
- `src/Yaf.Application/Context/` — 4 files (ITenantContextProvider, IIdentityContextProvider, ICorrelationIdProvider, IActivityIdProvider)
- `src/Yaf.Application/Sanitization/` — 2 files (SanitizeAttribute, ISanitizer)
- `src/Yaf.Application/Audit/` — 2 files (BusinessLogEntry, IBusinessEventLog)

**Tests:**
- `tests/Yaf.Application.Tests/{Cqrs,Notifications,Validation,Context,Sanitization,Audit,Integration}/` — 7 test files, 35 tests

**Docs:**
- `docs/adr/architecture/20260324-1144-cqrs-and-mediator-abstraction.md` — updated
- `docs/adr/architecture/20260324-1146-application-layer-patterns.md` — updated
- `docs/adr/domain/20260324-1141-validation-strategy.md` — updated
- `CHANGELOG.md` — updated
- `.maister/tasks/development/2026-04-26-application-cqrs-abstractions/` — full workflow artifacts (spec, plan, work-log, audit, verification, etc.)
