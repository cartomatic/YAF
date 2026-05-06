# Spec Audit: Application Layer CQRS Abstractions

**Spec:** `.maister/tasks/development/2026-04-26-application-cqrs-abstractions/implementation/spec.md`
**Status:** Mostly Compliant — actionable issues identified, no architectural blockers.

## Summary

The spec is well-organized and correctly translates ADRs and user-overridden decisions into a concrete file/namespace layout. Two-level command hierarchy, async-only handlers, and zero-dependency rule are all correctly captured. However, several substantive issues should be resolved before implementation.

## Findings

### Critical
None.

### Important

**I1. `IValidator<T>` async signature is undeclared ADR override.**
- Spec line 130: `Task<Result> ValidateAsync(...)`. ADR-1141 line 117: `Result Validate(T instance)` (sync).
- Spec rationalizes async (line 130) but does NOT call this out as an ADR override (only `ISanitizable` placement is flagged).
- **Recommendation**: Document the async override in `Out of Scope`/`Design Decisions`, update ADR-1141 to match.

**I2. `BusinessLogEntry` fields diverge from ADR without rationale.**
- ADR-1146: `IdentityId (Guid)`, `ActivityId (Guid)`. Spec: `ActorId (Guid)`, `ActivityId (string?)`.
- Both changes are correct (ActorId aligns with provider; ActivityId string matches W3C trace context per `IActivityScoped`), but undocumented.
- **Recommendation**: Add "ADR Drift / Corrections" subsection. Update ADR-1146.

**I3. `Result` using directive strategy is unspecified.**
- `Result`/`Result<T>` live in `Yaf.Domain` namespace. Spec doesn't say whether handler files use `using Yaf.Domain;` or fully-qualify.
- **Recommendation**: Specify "All files use `using Yaf.Domain;` for Result/Error/TenantId/ActorId" OR declare a `GlobalUsings.cs`.

**I4. Marker interface XML doc template is ambiguous.**
- Spec points to `IDomainEvent.cs` (which has members) as template; markers like `ICommand`, `INotification`, `ISanitizable` should follow `IEncryptable.cs` (true marker).
- **Recommendation**: Specify "marker interfaces follow `IEncryptable.cs` style; member-bearing interfaces follow `IDomainEvent.cs` style."

**I5. Non-generic `ICommand` is a user override not noted as ADR addition.**
- ADR-1144 only shows `ICommand<TResult>`. Spec adds non-generic `ICommand` per scope-clarifications.md.
- **Recommendation**: Add inline note that non-generic ICommand is a user override; update ADR-1144.

**I6. `IBusinessEventLog.AppendAsync` API contract is ambiguous.**
- BusinessLogEntry has TenantId/ActorId/CorrelationId/ActivityId/OccurredAtUtc as properties, but spec says implementation auto-populates them.
- Caller API is unclear: construct full record with placeholders? Use init-only? Separate input DTO?
- **Recommendation**: Choose one of (a) AppendAsync(BusinessLogEntry) with init-overwrite, (b) AppendAsync(what, aggregateType, aggregateId, metadata?) — recommend (b) per ADR usage example.

### Minor

**M2. `ISanitizer.Sanitize<T>` mutates in place — incompatible with record commands.**
- "Mutates the instance in place" assumes mutable types. Records (idiomatic CQRS) are immutable.
- **Recommendation**: Change signature to `T Sanitize<T>(T instance) where T : ISanitizable` (return sanitized copy).

**M4. `INotificationHandler<T>` returns `Task` (not `Task<Result>`) — not justified.**
- Returning `Task` is correct for fan-out, but spec doesn't explain why notifications differ from commands/queries.
- **Recommendation**: Add one-sentence justification.

**M5. Variance annotations not specified for all type parameters.**
- `ICommandHandler<TCommand>` (single param), `IQueryHandler<...,TResult>` — some omitted.
- **Recommendation**: Default to "input contravariant (`in`); result types invariant (Result<T> is invariant)."

**M6. Type count discrepancy.**
- Section "All 17 types" but table claims 8 CQRS types vs 6 in file structure.
- **Recommendation**: Fix table to "CQRS interfaces (6 types)".

**M8. `IBusinessEventLog.AppendAsync` returns `Task` not `Task<Result>` — unjustified.**
- **Recommendation**: Add one sentence on choice (fail-fast vs result-handling for audit).

## ADR Alignment

| ADR | Aligned? | Notes |
|-----|----------|-------|
| ADR-1144 (CQRS) | Mostly | Adds non-generic ICommand per user override (I5). |
| ADR-1146 (App patterns) | Mostly | ISanitizable override flagged. BusinessLogEntry diverges (I2, I6). |
| ADR-1141 (Validation) | Diverges | Async signature is undeclared override (I1). |

## User-Override Handling

| Override | Captured? |
|----------|-----------|
| ISanitizable in Application | Yes |
| ICommand<TResult> : ICommand inheritance | Yes (but ADR addition not flagged — I5) |
| Async IValidator<T> | **No — undeclared** (I1) |
| Sub-namespaces per concern | Yes |
| Sync context provider properties | Yes |
| BusinessLogEntry as record | Yes |
| BusinessLogEntry field renames vs ADR | **No — undocumented** (I2) |

## Implementability

The spec is implementable after resolving I1, I2, I3, I6 (most impactful).

All referenced files verified to exist. Project structure confirmed. Zero external dependencies achievable.

## Stakeholder Questions

1. **Validator async vs sync**: Confirm async is correct, update ADR-1141?
2. **BusinessLogEntry caller API**: Full record with overwrites, or `AppendAsync(what, aggregateType, aggregateId, metadata)`?
3. **ISanitizer signature**: Mutate in place or return sanitized copy?
4. **BusinessLogEntry.ActorId type**: Primitive Guid (per spec line 158-163) or domain ActorId typed ID?
