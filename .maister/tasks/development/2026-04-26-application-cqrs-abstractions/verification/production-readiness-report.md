# Production Readiness Report

**Date**: 2026-04-27
**Path**: `F:\dev\Cartomatic\YAF\.maister\tasks\development\2026-04-26-application-cqrs-abstractions`
**Target**: production (NuGet library, pre-1.0)
**Branch**: `feat/application-cqrs-abstractions`
**Status**: Ready with Concerns

## Executive Summary

- **Recommendation**: GO WITH MITIGATIONS
- **Overall Readiness**: ~85%
- **Deployment Risk**: Low (contract-only library; no runtime, no I/O, no state)
- **Blockers**: 0  Concerns: 4  Recommendations: 5

The `Yaf.Application` package contains 17 contract-only types (16 interfaces + 1 record), zero external dependencies, and a clean build under warnings-as-errors with `Nullable` and `GenerateDocumentationFile` enabled. Public API surface is well-designed: variance annotations are deliberate and consistent (`out` on covariant markers, `in` on handler inputs, invariant on result-bearing handler positions), constraints are sensible (`notnull` on `Result<T>` payloads), `BusinessLogEntry` is a sealed record with `required` init properties, and XML documentation is consumer-grade across all 17 types.

The library is shippable as a pre-release NuGet package today. The blocking-once-shipped concerns are not in the code itself — they are in the **packaging metadata** (no `PackageId`, version, authors, license, readme, or `RepositoryUrl` on either project) and in **ADR drift** that is documented in the code's XML but has not been backported to the ADRs themselves. Both must be addressed before publishing the first package, but neither prevents merging this task.

## Category Breakdown

This is a contract-only library: monitoring, runtime resilience, performance, and deployment-script categories are not applicable and are reported as N/A. Categories are scoped to library/NuGet-package readiness as requested.

| Category | Score | Status |
|----------|-------|--------|
| Public API stability | 95% | Strong |
| XML documentation completeness | 100% | Strong |
| Versioning compatibility | 70% | Concerns |
| Build configuration | 100% | Strong |
| ADR drift documentation | 60% | Concerns |
| NuGet packageability | 40% | Concerns |
| Monitoring | N/A | Skipped (contract-only) |
| Runtime error handling | N/A | Skipped (contract-only) |
| Performance | N/A | Skipped (contract-only) |
| Security | N/A | Skipped (no I/O, no inputs at this layer) |
| Deployment | N/A | Skipped (no runnable artifact) |

## Findings

### Blockers (must fix before merge)

None. The build is clean (`0 Warning(s), 0 Error(s)`) under `TreatWarningsAsErrors=true`, `EnforceCodeStyleInBuild=true`, `GenerateDocumentationFile=true`, `Nullable=enable` from the root `Directory.Build.props`. CS1591 (missing XML doc) is enforced as an error and passes for all 17 types. Public API decisions (variance, constraints, async vs. sync, primitives in DTOs) are consistent with the spec, the audit, and the broader codebase conventions.

### Concerns (should fix before publishing the first NuGet package)

**C1 — NuGet packaging metadata absent on both `Yaf.Application.csproj` and `Yaf.Domain.csproj`.**
- Location: `F:\dev\Cartomatic\YAF\src\Yaf.Application\Yaf.Application.csproj`, `F:\dev\Cartomatic\YAF\src\Yaf.Domain\Yaf.Domain.csproj`
- Issue: Neither csproj defines `PackageId`, `Authors`, `Description`, `Version` (or `VersionPrefix`/GitVersion integration), `RepositoryUrl`, `PackageLicenseExpression`/`PackageLicenseFile`, `PackageReadmeFile`, or `PackageProjectUrl`. The repository root `Directory.Build.props` only sets language settings — no shared packaging properties either. Without these, `dotnet pack` will produce a package, but it will be unattributed, unversioned (`1.0.0` default), unlinked to the repo, and unlicensed in metadata even though `LICENSE` exists at the repo root.
- Risk: Low for merging this PR (the task's scope was contracts, not packaging). High the moment someone runs `dotnet pack` for distribution — consumers and `nuget.org` validation will reject or accept a malformed package.
- Suggestion: Add a shared `<PropertyGroup>` to the root `Directory.Build.props` (or a new `Directory.Pack.props`) covering `Authors`, `Company`, `Description` (per-project override), `PackageLicenseFile=LICENSE`, `PackageReadmeFile=README.md`, `PackageProjectUrl`, `RepositoryUrl`, `RepositoryType=git`, `IncludeSymbols=true`, `SymbolPackageFormat=snupkg`, `EmbedUntrackedSources=true`, and SourceLink (`Microsoft.SourceLink.GitHub`). Wire GitVersion (already in the tech stack per `.maister/docs/project/tech-stack.md`) for `Version`. Set `IsPackable=true` on `Yaf.Application` and `Yaf.Domain`, and confirm `IsPackable=false` on the test project (already correct).

**C2 — Four ADR drifts documented in code XML but not backported to the ADRs.**
- Location: `docs/adr/domain/20260324-1141-validation-strategy.md`, `docs/adr/architecture/20260324-1144-cqrs-and-mediator-abstraction.md`, `docs/adr/architecture/20260324-1146-application-layer-patterns.md`
- Issue: The code's XML docs honestly call out four deliberate drifts from the ADRs (async `IValidator<T>`, non-generic `ICommand` marker, `ActorId`/`string ActivityId` rename and retype on `BusinessLogEntry`, `ISanitizable` placed in Application instead of Domain, parameter-based `AppendAsync` instead of entry-based). Grepping the three ADRs confirms none of these drifts have been backported: ADR-1141 still describes a synchronous `Result Validate(T)`, ADR-1144 omits the non-generic marker, ADR-1146 still uses `IdentityId`, `ActivityId (Guid)`, places `ISanitizable` in Domain, and shows the entry-based AppendAsync example.
- Risk: Future maintainers reading only the ADRs will write specs and review code against an out-of-date contract. The XML drift sections are correct but redundant if/when the ADRs are corrected; right now they are the only authoritative record.
- Suggestion: Update each ADR with an "Update YYYY-MM-DD" or "Superseded by" subsection capturing the four drifts, citing this task's spec and its `verification/spec-audit.md`. Keep the XML drift paragraphs in place — they remain useful inline rationale for consumers who never read ADRs.

**C3 — `BusinessLogEntry.Metadata` exposes `IReadOnlyDictionary<string, object>` with no guidance on what `object` may be.**
- Location: `F:\dev\Cartomatic\YAF\src\Yaf.Application\Audit\BusinessLogEntry.cs:118`
- Issue: The `Metadata` value type is `object`, which makes it impossible to give serialization, equality, or schema guarantees to consumers. For a record meant to be serialized to an audit store, `object` is a foot-gun: callers can put non-serializable references (delegates, `Stream`, EF entities) and only discover the failure at write time. The XML doc says "key/value bag carrying event-specific data" but does not constrain the value type.
- Risk: Medium. Once consumers depend on this shape, narrowing the value type later (e.g., to `string`, `JsonElement`, or a closed set of primitives) is a binary-breaking change. Pre-1.0 is the only window to constrain it.
- Suggestion: Decide explicitly: keep `object` and add a `<remarks>` paragraph stating that values must be serializable by the audit-store implementation (callers' responsibility); OR narrow to a concrete union (e.g., `string`, `long`, `bool`, `DateTimeOffset`, `Guid`) — preferably modeled as a small discriminated record. The decision should be captured in ADR-1146 alongside the other audit decisions.

**C4 — `ICommand<out TResult>` + `ICommandHandler<in TCommand, TResult>` interaction has a subtle covariance trap that is undocumented.**
- Location: `F:\dev\Cartomatic\YAF\src\Yaf.Application\Cqrs\ICommand{TResult}.cs:27`, `ICommandHandler{TCommand,TResult}.cs:30`
- Issue: `ICommand<TResult>` is covariant in `TResult` (correct, since the marker has no members). However, a single command type `record CreateOrder() : ICommand<OrderId>` is *also* implicitly an `ICommand<object>`. A consumer registering generic handler resolution by `typeof(ICommand<>)` may resolve unexpected handlers. This is a contract-design surface that adapter packages will hit. The XML doc on `ICommand{TResult}` correctly explains *why* covariance is safe, but says nothing about how dispatchers should resolve handlers when a command has multiple `ICommand<>` ancestors.
- Risk: Low for this task (no dispatcher exists yet). Medium when adapter packages (Wolverine, MediatR shims) are built — without guidance they may diverge.
- Suggestion: Add a one-paragraph remark to `ICommand{TResult}` documenting that adapter packages should resolve handlers by the *declared* (not the assignable) `ICommand<TResult>` of the runtime command type. Optionally add an analyzer or a unit test in the test project asserting that a command implements exactly one closed `ICommand<>` interface.

### Recommendations (nice to have)

**R1 — `BusinessLogEntry.Metadata` value type is `object`; consider also exposing a strongly-typed alternative.** See C3 — if you decide to keep `object`, exposing a separate `BusinessLogEntry.WithMetadata<TPayload>` factory or a sibling generic record is a non-breaking enrichment that consumers can opt into.

**R2 — Consider sealing or marking interfaces as `[Experimental]` for the first pre-release.** Pre-1.0 NuGet packages benefit from `System.Diagnostics.CodeAnalysis.ExperimentalAttribute` (.NET 8+) on the assembly or per-type, which gives consumers an explicit opt-in warning and keeps SemVer flexibility for the first few releases.

**R3 — Add an `AssemblyInfo` or `csproj`-level `InternalsVisibleTo` for a future analyzer/source-generator project.** Today only `Yaf.Application.Tests` is included. If the project later adds analyzers (e.g., to enforce C4's "one closed `ICommand<>` per command type"), giving them internals access early avoids a churning csproj.

**R4 — `Yaf.Application.csproj` has no `RootNamespace` or `AssemblyName` overrides.** Defaults are correct (`Yaf.Application`), but explicit settings make future renames safer and document intent.

**R5 — Add public-API surface tests using `Verify` or `PublicApiGenerator`.** With 17 types and only 32 contract tests, an approval-style API-surface test would catch accidental breaking changes (added members, changed variance, added/removed type parameters) on every PR. Particularly valuable for a library shipped via NuGet where binary compatibility matters.

## Per-Category Notes

### Public API stability (95%)

- Variance annotations consistent and deliberate across `ICommand<out TResult>`, `IQuery<out TResult>`, all four handler interfaces (`in TCommand` / `in TQuery` / `in TNotification` / `in T`), with `TResult` correctly invariant where it appears inside `Result<T>` (which is itself invariant). XML docs explain *why* on every variance choice.
- Constraints sensible: `where TResult : notnull` mirrors `Result<T>`'s constraint exactly; `where TCommand : ICommand<TResult>` and `where TQuery : IQuery<TResult>` enforce the matching marker.
- `BusinessLogEntry` is a `sealed record` with `required` init properties on every non-optional field; `Metadata` is correctly the only optional. `IReadOnlyDictionary` (not `Dictionary`) prevents downstream mutation.
- One concern: `BusinessLogEntry.Metadata` value type is `object` (C3); one concern: covariance/handler-resolution interaction is undocumented (C4).

### XML documentation completeness (100%)

- Every public type and member has `<summary>`. CS1591 is enforced as a build error and passes.
- Marker interfaces (e.g., `ICommand`, `INotification`, `ISanitizable`) follow the `IEncryptable.cs` style (semicolon body, summary + remarks); member-bearing interfaces follow the `IDomainEvent.cs` style (full `<remarks>` with `<para>`, `<typeparam>`, `<see cref>` cross-references).
- Drift rationale is embedded inline in the XML docs of `ISanitizable`, `IValidator<T>`, `BusinessLogEntry`, `IBusinessEventLog`, and `ICommand` — readable from IntelliSense without leaving the IDE. This is unusually thorough for a contracts package.

### Versioning compatibility (70%)

- Variance choices and constraints are deliberate and would be expensive to change post-1.0 — they are the right ones (covariance on result markers, contravariance on handler inputs, `notnull` on payloads).
- Concerns: C3 (Metadata `object`), C4 (handler-resolution semantics) are pre-1.0 windows that close after publish.
- No `Version` / `VersionPrefix` is set; `GitVersion` is in the tech stack but not yet wired into the csproj — see C1.

### Build configuration (100%)

- Root `Directory.Build.props`: `TreatWarningsAsErrors=true`, `EnforceCodeStyleInBuild=true`, `GenerateDocumentationFile=true`, `Nullable=enable`, `ImplicitUsings=enable`. All enforced; `dotnet build` produces 0 warnings and 0 errors.
- The project uses file-scoped namespaces, semicolon marker bodies, `required` init properties, and PascalCase-with-braces filenames consistent with `.maister/docs/standards/backend/csharp-conventions.md`.
- `InternalsVisibleTo Yaf.Application.Tests` only; appropriately restrictive.

### ADR drift documentation (60%)

- Drift is well-documented inside the implementation (XML docs) and in the spec/audit artifacts under `.maister/tasks/development/2026-04-26-application-cqrs-abstractions/`.
- Drift is **not** documented in the ADRs themselves — verified by grepping ADR-1141, ADR-1144, ADR-1146 (see C2). Future maintainers reading only `docs/adr/` will be misled.

### NuGet packageability (40%)

- The csproj will produce a `.nupkg` via `dotnet pack`, but without `PackageId`, `Authors`, `Description`, `Version`, `RepositoryUrl`, `PackageLicenseFile`, `PackageReadmeFile`, or SourceLink. See C1.
- `LICENSE` and `README.md` exist at the repo root but are not referenced by any csproj.
- No CI workflow for publishing exists in `.github/workflows/` (only `pr.yml`); this is fine for now but should be planned alongside the packaging metadata fix.

## Next Steps (prioritized)

1. **Before merge**: none — this PR can land as-is. The build is clean, the API surface is well-considered, and the drift is honestly documented in code.
2. **Before first NuGet publish (highest priority)**:
   1. Add NuGet packaging metadata to a shared `Directory.Pack.props` (C1).
   2. Wire GitVersion to drive `Version` in CI.
   3. Add `Microsoft.SourceLink.GitHub`, `IncludeSymbols`, `EmbedUntrackedSources`.
   4. Decide on `BusinessLogEntry.Metadata` policy (C3) — narrow the type or document the contract.
   5. Add the handler-resolution remark to `ICommand<TResult>` (C4).
3. **Before 1.0 stabilization**:
   1. Backport drifts into ADR-1141, ADR-1144, ADR-1146 (C2).
   2. Add public-API surface approval test (R5).
   3. Consider `[Experimental]` for the first pre-release window (R2).

## Verdict

**GO WITH MITIGATIONS** for merging the task. **NO-GO** for publishing a NuGet package until C1 is resolved. The library code itself is production-ready as a contract surface; the gap is in distribution metadata and in ADR-drift bookkeeping, neither of which blocks the implementation merge.
