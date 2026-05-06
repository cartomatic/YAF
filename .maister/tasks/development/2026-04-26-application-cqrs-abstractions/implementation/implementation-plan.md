# Implementation Plan: Application Layer CQRS and Cross-Cutting Abstractions

## Overview

- **Total Steps:** 56 step items across 7 task groups
- **Task Groups:** 7 (6 implementation + 1 test review)
- **Expected Tests:** 18-32 total (3-5 per implementation group + up to 10 in test review)
- **Project:** `src/Yaf.Application/` (greenfield — no existing source files)
- **Test Project:** `tests/Yaf.Application.Tests/` (already configured with `InternalsVisibleTo`)
- **Risk:** Low (greenfield interfaces, no existing code modified)

All 17 types are new. Each task group corresponds to one sub-namespace under `Yaf.Application`. The CQRS group is foundational; the remaining implementation groups (Notifications, Validation, Context, Sanitization, Audit) can be built sequentially or in parallel after CQRS lands. The final group reviews the test surface and fills strategic gaps.

## Implementation Steps

### Task Group 1: CQRS Core (`Yaf.Application.Cqrs`)
**Dependencies:** None
**Estimated Steps:** 9
**Files Created:** 6 interface files

- [x] 1.0 Complete CQRS core abstractions
  - [x] 1.1 Write 4-6 focused tests in `tests/Yaf.Application.Tests/Cqrs/CqrsContractsTests.cs`
    - `ICommandGeneric_InheritsFromICommand_AssignableToBaseInterface` — verifies `ICommand<TResult> : ICommand` hierarchy
    - `ICommandHandler_HandleMethod_ReturnsTaskOfResult` — mock handler implementation, assert return type via reflection
    - `ICommandHandlerGeneric_HandleMethod_ReturnsTaskOfResultOfTResult` — mock implementation
    - `IQueryHandler_HandleMethod_ReturnsTaskOfResultOfTResult` — mock implementation
    - `ICommandHandler_TCommand_IsContravariant` — reflection check on generic parameter attributes (`in` keyword)
    - `IQueryHandler_TQuery_IsContravariant` — reflection check
    - Use AwesomeAssertions; method naming `Method_Condition_ExpectedBehavior`
  - [x] 1.2 Create `src/Yaf.Application/Cqrs/ICommand.cs`
    - File-scoped namespace `Yaf.Application.Cqrs;`
    - Semicolon body marker: `public interface ICommand;`
    - XML docs: summary + remarks (`<para>` blocks) following `IEncryptable.cs` style
    - `using Yaf.Domain;` only if cross-references require
  - [x] 1.3 Create `src/Yaf.Application/Cqrs/ICommand{TResult}.cs`
    - `public interface ICommand<out TResult> : ICommand;` — covariant `TResult` (marker has no members; matches `IDomainEvent<out T>` convention)
    - XML docs include `<typeparam name="TResult">` and `<see cref="ICommand"/>` cross-reference
  - [x] 1.4 Create `src/Yaf.Application/Cqrs/ICommandHandler{TCommand}.cs`
    - `using Yaf.Domain;` for `Result`
    - `public interface ICommandHandler<in TCommand> where TCommand : ICommand`
    - Single method: `Task<Result> Handle(TCommand command, CancellationToken cancellationToken);`
    - XML docs: summary, remarks, `<typeparam>`, `<param>` for both arguments, `<returns>`, `<see cref="Result"/>`
  - [x] 1.5 Create `src/Yaf.Application/Cqrs/ICommandHandler{TCommand,TResult}.cs`
    - `using Yaf.Domain;` for `Result<T>`
    - `public interface ICommandHandler<in TCommand, TResult> where TCommand : ICommand<TResult>`
    - Single method: `Task<Result<TResult>> Handle(TCommand command, CancellationToken cancellationToken);`
    - XML docs include both `<typeparam>` entries and `<see cref="ICommand{TResult}"/>`
  - [x] 1.6 Create `src/Yaf.Application/Cqrs/IQuery{TResult}.cs`
    - Semicolon body marker: `public interface IQuery<out TResult>;` — covariant `TResult` (marker has no members)
    - XML docs explain queries return typed results; cross-reference `<see cref="Result{T}"/>`
  - [x] 1.7 Create `src/Yaf.Application/Cqrs/IQueryHandler{TQuery,TResult}.cs`
    - `using Yaf.Domain;`
    - `public interface IQueryHandler<in TQuery, TResult> where TQuery : IQuery<TResult>`
    - Single method: `Task<Result<TResult>> Handle(TQuery query, CancellationToken cancellationToken);`
    - XML docs cover `<typeparam>` for both, `<see cref="IQuery{TResult}"/>`
  - [x] 1.8 Build the solution: `dotnet build src/Yaf.Application/Yaf.Application.csproj`
    - Verify zero warnings (CS1591 must pass)
  - [x] 1.9 Run only the new tests: `dotnet test tests/Yaf.Application.Tests/Yaf.Application.Tests.csproj --filter "FullyQualifiedName~Cqrs"`
    - Confirm 4-6 tests pass

**Acceptance Criteria:**
- All 6 CQRS files compile with zero warnings (CS1591 enforced)
- `ICommand<TResult>` is assignable to `ICommand` (inheritance verified by test)
- All handler interfaces declare contravariant `in TCommand` / `in TQuery`
- All XML docs present following `IDomainEvent.cs` style
- Existing 90 domain tests still pass

---

### Task Group 2: Notifications (`Yaf.Application.Notifications`)
**Dependencies:** Group 1 (CQRS) — none technically required but sequenced for stability
**Estimated Steps:** 5
**Files Created:** 2 interface files

- [x] 2.0 Complete notification abstractions
  - [x] 2.1 Write 2-3 focused tests in `tests/Yaf.Application.Tests/Notifications/NotificationContractsTests.cs`
    - `INotification_IsMarkerInterface_HasNoMembers` — reflection-based member check
    - `INotificationHandler_HandleMethod_ReturnsTask` — mock implementation, verify return type is `Task` (not `Task<Result>`)
    - `INotificationHandler_TNotification_IsContravariant` — reflection check
  - [x] 2.2 Create `src/Yaf.Application/Notifications/INotification.cs`
    - File-scoped namespace `Yaf.Application.Notifications;`
    - Semicolon body marker: `public interface INotification;`
    - XML docs: summary + remarks with `<para>` describing fan-out semantics and relationship to domain events
  - [x] 2.3 Create `src/Yaf.Application/Notifications/INotificationHandler{TNotification}.cs`
    - `public interface INotificationHandler<in TNotification> where TNotification : INotification`
    - Single method: `Task Handle(TNotification notification, CancellationToken cancellationToken);`
    - XML docs explain why return type is `Task` (not `Task<Result>`) — fan-out has no aggregated outcome
  - [x] 2.4 Build the project — confirm CS1591 passes
  - [x] 2.5 Run notification tests only: `dotnet test --filter "FullyQualifiedName~Notifications"`

**Acceptance Criteria:**
- 2 notification files compile with zero warnings
- Handler returns `Task` (verified by test, not `Task<Result>`)
- Marker has no members (verified by test)
- XML docs justify the return-type choice in remarks

---

### Task Group 3: Validation (`Yaf.Application.Validation`)
**Dependencies:** Group 1 (CQRS — for `Result` reference patterns)
**Estimated Steps:** 4
**Files Created:** 1 interface file

- [x] 3.0 Complete validation abstraction
  - [x] 3.1 Write 2-3 focused tests in `tests/Yaf.Application.Tests/Validation/ValidatorContractTests.cs`
    - `IValidator_ValidateAsyncMethod_ReturnsTaskOfResult` — mock implementation, assert return type
    - `IValidator_T_IsContravariant` — reflection check
    - `IValidator_ValidateAsync_AcceptsCancellationToken` — signature verification
  - [x] 3.2 Create `src/Yaf.Application/Validation/IValidator{T}.cs`
    - `using Yaf.Domain;` for `Result`
    - `public interface IValidator<in T>`
    - Single method: `Task<Result> ValidateAsync(T instance, CancellationToken cancellationToken);`
    - XML docs:
      - `<summary>` — pipeline validator for business preconditions
      - `<remarks>` — note ADR-1141 drift (async vs sync), why `Result` (not `Result<T>`)
      - `<see cref="Result"/>`
  - [x] 3.3 Build the project — confirm CS1591 passes
  - [x] 3.4 Run validation tests only: `dotnet test --filter "FullyQualifiedName~Validation"`

**Acceptance Criteria:**
- File compiles with zero warnings
- Method is async (`Task<Result>`)
- Contravariant `in T`
- ADR drift noted in `<remarks>`

---

### Task Group 4: Context Providers (`Yaf.Application.Context`)
**Dependencies:** Group 1 (CQRS — sequenced for stability)
**Estimated Steps:** 7
**Files Created:** 4 interface files

- [x] 4.0 Complete context provider abstractions
  - [x] 4.1 Write 4-5 focused tests in `tests/Yaf.Application.Tests/Context/ContextProviderContractsTests.cs`
    - `ITenantContextProvider_TenantIdProperty_ReturnsNullableTenantId` — reflection check on property type
    - `IIdentityContextProvider_ActorIdProperty_ReturnsActorId` — non-nullable
    - `ICorrelationIdProvider_CorrelationIdProperty_ReturnsGuid` — non-nullable
    - `IActivityIdProvider_ActivityIdProperty_ReturnsNullableString` — `string?`
    - `ContextProviders_AllPropertiesAreReadOnly` — no setters via reflection
  - [x] 4.2 Create `src/Yaf.Application/Context/ITenantContextProvider.cs`
    - `using Yaf.Domain;` for `TenantId`
    - `public interface ITenantContextProvider`
    - Property: `TenantId? TenantId { get; }`
    - XML docs: summary + remarks explaining nullable rationale (system operations, tenant management); cross-reference `<see cref="Yaf.Domain.Interfaces.ITenantScoped"/>`
  - [x] 4.3 Create `src/Yaf.Application/Context/IIdentityContextProvider.cs`
    - `using Yaf.Domain;` for `ActorId`
    - `public interface IIdentityContextProvider`
    - Property: `ActorId ActorId { get; }`
    - XML docs explain non-nullability (system actor for background jobs); cross-reference `IActorScoped`
  - [x] 4.4 Create `src/Yaf.Application/Context/ICorrelationIdProvider.cs`
    - `public interface ICorrelationIdProvider`
    - Property: `Guid CorrelationId { get; }`
    - XML docs explain non-nullability (infrastructure generates if absent); cross-reference `ICorrelated`
  - [x] 4.5 Create `src/Yaf.Application/Context/IActivityIdProvider.cs`
    - `public interface IActivityIdProvider`
    - Property: `string? ActivityId { get; }`
    - XML docs explain nullable rationale (no active `System.Diagnostics.Activity`); cross-reference `IActivityScoped`
  - [x] 4.6 Build the project — confirm CS1591 passes for all 4 files
  - [x] 4.7 Run context tests only: `dotnet test --filter "FullyQualifiedName~Context"`

**Acceptance Criteria:**
- 4 provider files compile with zero warnings
- All properties are read-only (verified by test)
- Property types match spec exactly: `TenantId?`, `ActorId`, `Guid`, `string?`
- Each interface XML-cross-references its Domain marker counterpart

---

### Task Group 5: Sanitization (`Yaf.Application.Sanitization`)
**Dependencies:** Group 1 (CQRS — sequenced for stability)
**Estimated Steps:** 5
**Files Created:** 2 interface files

- [x] 5.0 Complete sanitization abstractions
  - [x] 5.1 Write 2-3 focused tests in `tests/Yaf.Application.Tests/Sanitization/SanitizationContractsTests.cs`
    - `ISanitizable_IsMarkerInterface_HasNoMembers` — reflection check
    - `ISanitizer_SanitizeMethod_HasGenericConstraint` — verify `where T : ISanitizable`
    - `ISanitizer_SanitizeMethod_IsSynchronous` — return type is `T`, not `Task<T>`
  - [x] 5.2 Create `src/Yaf.Application/Sanitization/ISanitizable.cs`
    - File-scoped namespace `Yaf.Application.Sanitization;`
    - Semicolon body marker: `public interface ISanitizable;`
    - XML docs: summary + remarks documenting ADR-1146 drift (placement: Application instead of Domain) with rationale (sanitization is application-pipeline concern)
  - [x] 5.3 Create `src/Yaf.Application/Sanitization/ISanitizer.cs`
    - `public interface ISanitizer`
    - Single method: `T Sanitize<T>(T instance) where T : ISanitizable;`
    - XML docs explain synchronous nature (in-memory transformation), return-a-copy semantics for record/class compatibility, and pipeline ordering (before validation)
  - [x] 5.4 Build the project — confirm CS1591 passes
  - [x] 5.5 Run sanitization tests only: `dotnet test --filter "FullyQualifiedName~Sanitization"`

**Acceptance Criteria:**
- 2 sanitization files compile with zero warnings
- `ISanitizable` is a true marker (no members)
- `ISanitizer.Sanitize<T>` enforces `where T : ISanitizable` constraint (verified by test)
- ADR drift documented in `ISanitizable` remarks

---

### Task Group 6: Business Event Log (`Yaf.Application.Audit`)
**Dependencies:** Group 1 (CQRS — sequenced for stability)
**Estimated Steps:** 6
**Files Created:** 2 files (1 record + 1 interface)

- [x] 6.0 Complete business event log abstractions
  - [x] 6.1 Write 4-6 focused tests in `tests/Yaf.Application.Tests/Audit/AuditContractsTests.cs`
    - `BusinessLogEntry_IsRecord_HasValueSemantics` — reflection check (`EqualityContract` exists)
    - `BusinessLogEntry_HasNineRequiredProperties_MatchingSpec` — property names + types verified by reflection
    - `BusinessLogEntry_TenantIdProperty_IsNullableGuid` — type check
    - `BusinessLogEntry_ActivityIdProperty_IsNullableString` — type check
    - `BusinessLogEntry_MetadataProperty_IsNullableReadOnlyDictionary` — type check
    - `IBusinessEventLog_AppendAsyncMethod_AcceptsExpectedParameters` — signature verification (4 params + cancellation token)
  - [x] 6.2 Create `src/Yaf.Application/Audit/BusinessLogEntry.cs`
    - File-scoped namespace `Yaf.Application.Audit;`
    - `public sealed record BusinessLogEntry` with 9 required init properties:
      - `string What { get; init; }` — required
      - `string AggregateType { get; init; }` — required
      - `string AggregateId { get; init; }` — required
      - `Guid? TenantId { get; init; }` — required (nullable)
      - `Guid ActorId { get; init; }` — required
      - `Guid CorrelationId { get; init; }` — required
      - `string? ActivityId { get; init; }` — required (nullable)
      - `DateTimeOffset OccurredAtUtc { get; init; }` — required
      - `IReadOnlyDictionary<string, object>? Metadata { get; init; }` — optional (nullable)
    - Use `required` modifier where appropriate
    - XML docs: type-level summary + remarks, per-property `<summary>` on each
    - Note primitive-type rationale in remarks (DTO, avoids domain reconstruction on read-back)
  - [x] 6.3 Create `src/Yaf.Application/Audit/IBusinessEventLog.cs`
    - `public interface IBusinessEventLog`
    - Single method:
      ```csharp
      Task AppendAsync(
          string what,
          string aggregateType,
          string aggregateId,
          IReadOnlyDictionary<string, object>? metadata,
          CancellationToken cancellationToken);
      ```
    - XML docs: explain implementation responsibility (constructs `BusinessLogEntry` from context providers + system clock), justify `Task` return (fail-fast via exceptions), document parameter-based design vs ADR-1146 entry-based example (drift)
  - [x] 6.4 Document the rename `IdentityId` → `ActorId` and `ActivityId` Guid→string drift in XML remarks
  - [x] 6.5 Build the project — confirm CS1591 passes for both files
  - [x] 6.6 Run audit tests only: `dotnet test --filter "FullyQualifiedName~Audit"`

**Acceptance Criteria:**
- 2 audit files compile with zero warnings
- `BusinessLogEntry` is a sealed record with exactly 9 properties (verified by test)
- Property types match spec exactly (primitives, not domain typed IDs)
- `IBusinessEventLog.AppendAsync` parameter list matches spec
- ADR drift documented (`ActorId` rename, `ActivityId` type change, parameter-based signature)

---

### Task Group 7: Test Review & Gap Analysis
**Dependencies:** Groups 1, 2, 3, 4, 5, 6 (all implementation groups)
**Estimated Steps:** 5

- [x] 7.0 Review and fill critical gaps
  - [x] 7.1 Inventory existing tests across the 6 groups (expect 18-26 baseline tests)
    - List each test class and the contracts it asserts
  - [x] 7.2 Analyze gaps for THIS feature only
    - Cross-namespace: do CQRS handler signatures actually compose with `Result`/`Result<T>` from Domain in a worked example?
    - Pipeline integration: does `ISanitizer.Sanitize<T>` accept a `T` that is also `ICommand<TResult>`? (write a single demonstration test if not already covered)
    - Variance: are all `in` annotations actually exercised (assigning a derived-type handler to a base-type variable)?
    - Generic constraints: do all `where` clauses reject invalid types at compile time?
  - [x] 7.3 Write up to 10 additional strategic tests in `tests/Yaf.Application.Tests/Integration/AbstractionIntegrationTests.cs`
    - Focus on cross-cutting behavior, not duplicate coverage
    - Examples: composability tests, contravariance assignment tests, end-to-end shape proof
    - Hard cap: 10 tests — discard candidates beyond the limit
  - [x] 7.4 Run feature-specific tests only: `dotnet test tests/Yaf.Application.Tests/Yaf.Application.Tests.csproj`
    - Expect 18-32 total tests passing
    - Do NOT run the entire solution test suite (existing 90 domain tests are already verified by CI gating)
  - [x] 7.5 Final regression check: `dotnet build` of the full solution — confirm zero warnings (CS1591 across all projects)

**Acceptance Criteria:**
- All feature tests pass (~18-32 total)
- No more than 10 additional tests added in this group
- All 17 spec types are exercised by at least one test
- Zero compiler warnings across the solution
- Existing 90 domain tests still pass (no regressions)

---

## Execution Order

1. **Group 1: CQRS Core** (9 steps) — foundational; no dependencies
2. **Group 2: Notifications** (5 steps) — depends on Group 1
3. **Group 3: Validation** (4 steps) — depends on Group 1
4. **Group 4: Context Providers** (7 steps) — depends on Group 1
5. **Group 5: Sanitization** (5 steps) — depends on Group 1
6. **Group 6: Business Event Log** (6 steps) — depends on Group 1
7. **Group 7: Test Review & Gap Analysis** (5 steps) — depends on Groups 1-6

Groups 2-6 may be executed in parallel after Group 1 if desired, but sequencing them in the listed order is recommended for clean commit history and incremental verification.

## Standards Compliance

Follow standards from `.maister/docs/standards/`:

### Global Standards
- **`global/coding-style.md`** — descriptive names, no dead code, DRY, Clean Architecture (Application depends only on Domain)
- **`global/commenting.md`** — XML docs replace narrative commenting; no change-log comments inside code
- **`global/conventions.md`** — file naming with PascalCase + braces for generics
- **`global/error-handling.md`** — Result pattern via Domain types; no exceptions in interface contracts (except `IBusinessEventLog` for fail-fast)
- **`global/minimal-implementation.md`** — only the 17 specified types; no speculative abstractions

### Backend Standards
- **`backend/csharp-conventions.md`** — file-scoped namespaces, semicolon body markers, contravariance (`in TCommand`/`in TQuery`/`in TNotification`/`in T`), records for value semantics (`BusinessLogEntry`)
- **`backend/models.md`** — XML documentation required (CS1591 enforced as error); `<summary>`, `<remarks>`, `<typeparam>`, `<param>`, `<returns>`, `<see cref>` per `IDomainEvent.cs` style
- **`backend/api.md`** — YAF-owned CQRS abstractions, sanitization-before-validation pipeline ordering documented in XML remarks
- **`backend/domain-patterns.md`** — Result pattern from Domain reused; no exceptions for business errors

### Testing Standards
- **`testing/test-writing.md`** — xUnit + AwesomeAssertions; `Method_Condition_ExpectedBehavior` naming; CS1591 exempt in test projects; one test class per logical contract; no mocking framework needed (test types are simple in-test mocks)

## Notes

- **Test-Driven**: Each group starts with 2-8 tests written FIRST (X.1), then files are added (X.2..X.n-2), then build verification (X.n-1), then targeted test run (X.n)
- **Run Incrementally**: Use `--filter "FullyQualifiedName~<Sub-namespace>"` to run only the new tests for the group; do NOT run the entire suite between groups
- **Mark Progress**: Check off steps as completed for resumability
- **Reuse First**: Spec lists 12 reusable Domain components — reference them in XML cross-references (`<see cref>`) and use them directly (`Result`, `Result<T>`, `TenantId`, `ActorId`)
- **ADR Drift**: 6 user overrides documented in spec — record each in the relevant XML `<remarks>` so future ADR updates have a paper trail
- **CS1591 is an Error**: Every public type and member needs XML docs; build fails otherwise — verify after every group
- **No External Dependencies**: Only `Yaf.Domain` reference; no NuGet packages added
