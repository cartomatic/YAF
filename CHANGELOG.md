# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), with changes grouped by date.

## 2026-03-28

### Added

- `ActorId` — framework-provided actor identifier (like `TenantId`), covers users, service accounts, and bots
- `ICorrelated` — cross-cutting interface carrying a `Guid CorrelationId` for operation tracing
- `IActorScoped` — cross-cutting interface carrying an `ActorId ActorId` identifying who triggered the operation
- `IActivityScoped` — cross-cutting interface carrying a `string? ActivityId` from `System.Diagnostics.Activity`
- `IDomainEvent` enriched from marker to full contract: `EventId` (Guid), `OccurredAtUtc` (DateTimeOffset), inherits `ICorrelated`, `ITenantScoped`, `IActorScoped`, `IActivityScoped`
- `IDomainEvent<out T>` — covariant generic variant for domain events carrying a typed data payload
- 10 new tests for domain event interfaces, cross-cutting context, and covariance

### Changed

- `ITenantScoped<TTenantId>` simplified to non-generic `ITenantScoped` using framework `TenantId` type
- `IAccountable<TActorId>` simplified to non-generic `IAccountable` using framework `ActorId` type
- `ISoftDeletable<TActorId>` simplified to non-generic `ISoftDeletable` using framework `ActorId` type
- Entity/AggregateRoot auto-mapping simplified: direct interface reads for Snapshot, compiled writers with `ActorId` for Hydrate — no more `TypedIdBridge`

### Removed

- `TypedIdBridge<TSelf>` and `TypedIdFactoryCache` — no longer needed with non-generic domain interfaces
- `IUserScoped` — replaced by `IActorScoped` with typed `ActorId`
- Generic type parameters on `ITenantScoped`, `IAccountable`, `ISoftDeletable`
- Open-generic builder overloads on `MementoHelper` (replaced by closed-generic `BuildWriter<TDomain, TMem>`)

### Changed

- `TypedId<T>` simplified to non-generic `TypedId` — all typed IDs are now Guid-backed; external systems with non-Guid identifiers remap at the anti-corruption layer boundary
- `ITypedId` simplified to single interface with `Guid Value` — removed `ITypedId<out T>`, `IdentityType`, and `BoxedValue`
- Memento interfaces collapsed from two-tier (non-generic base + generic DIM variant) to single-tier with direct `Guid?` properties: `IHasIdentity`, `IHasAccountability`, `IHasTenantId`, `IHasSoftDelete`
- `TypedIdBridge` simplified — factory always takes `Guid`, reads `.Value` directly
- `MementoHelper` simplified — no more runtime `IdentityType` compatibility checks
- `TenantId` updated: `TypedId<Guid>` → `TypedId`
- Consumer ID declaration: `TypedId<Guid>` → `TypedId` (e.g., `record OrderId(Guid Value) : TypedId(Value)`)

### Removed

- `ITypedId<out T>` generic interface — replaced by simplified `ITypedId`
- `BoxingHelper` — no longer needed when backing type is always `Guid`
- Generic variants of memento interfaces (`IHasIdentity<T>`, `IHasAccountability<T>`, `IHasTenantId<T>`, `IHasSoftDelete<T>`) — replaced by single-tier interfaces
- DIM (Default Interface Method) implementations on memento interfaces — no boxing layer needed
- Non-Guid TypedId tests (int, long, string backing types)

### Added

- `IAccountable<TActorId>` — domain-side accountability interface (CreatedBy?, ModifiedBy?); generic with typed actor ID
- `ITimestamped` — domain-side timestamp interface (CreatedAtUtc?, ModifiedAtUtc?); both nullable, null = not yet persisted
- `ISoftDeletable<TActorId>` — domain-side soft-deletion interface (DeletedAtUtc?, DeletedBy?); standalone, composition over inheritance
- `ITenantScoped<TTenantId>` — domain-side tenant context interface; generic with typed tenant ID
- `IHasAccountability` / `IHasAccountability<T>` — memento-side accountability with DIM pattern via `BoxingHelper.Unbox<T>`
- `IHasTimestamps` — memento-side timestamps (get/set)
- `IHasSoftDelete` / `IHasSoftDelete<T>` — memento-side soft-deletion with DIM pattern
- `IHasTenantId` / `IHasTenantId<T>` — memento-side tenant identity with DIM pattern
- `IHasVersionInfo` — memento-only optimistic concurrency token (Guid Version)
- `IHasVersionHistory` — memento-only independent marker for version snapshots + graveyard
- `TenantId` — framework-provided typed identifier (`TypedId<Guid>`)
- `TypedIdBridge<TSelf>` — compiled per-property bridge for typed ID boxing (entity → primitive) and unboxing (primitive → entity via cached factory)
- `BoxingHelper` — shared `Unbox<T>` used by all memento DIM implementations
- `ReflectionHelper` — generic compiled property readers/writers (no domain knowledge)
- Auto-mapping in base classes for timestamps (`ITimestamped` ↔ `IHasTimestamps`), accountability (`IAccountable<TActorId>` ↔ `IHasAccountability<T>`), and soft-delete (`ISoftDeletable<TActorId>` ↔ `IHasSoftDelete<T>`)
- `InternalsVisibleTo` for `Yaf.Domain.Tests` in `Yaf.Domain.csproj`
- Contributing conventions document (`docs/general/contributing-conventions.md`)
- 32 new tests: memento bridge round-trips, BoxingHelper, ReflectionHelper, backward compatibility
- Solution document: cross-cutting interfaces DIM boxing and typed ID bridging patterns

### Changed

- `Entity<TId, TSelf, TMemento>` — `Restore` now delegates to `Hydrate`; `RestoreCore` eliminated. Auto-maps identity, timestamps, accountability, and soft-delete. Consumers implement only `SnapshotCore` and `HydrateCore` for entity-specific properties and tenant context.
- `AggregateRoot<TId, TSelf, TMemento>` — same changes as Entity
- `ValueObject<TSelf, TMemento>` — `RestoreCore` renamed to `HydrateCore` for API consistency across all base classes
- `IHasIdentity<T>` — added `struct` constraint (`where T : struct, IEquatable<T>`), `Id` property now `T?` (nullable); `BoxedId` now `object?`
- `MementoHelper` — added conditional delegate builders (`BuildWriter`, `BuildReader`, `BuildBridge`) that check entity/memento interface compatibility
- ADR: Cross-Cutting Infrastructure — added `ISoftDeletable`, renamed `IVersionable` to `IHasVersionHistory` (independent marker), updated deletion lifecycle to exclusive-paths model, clarified `IHasVersionInfo` as memento-only
- CLAUDE.md — updated current state, added contributing conventions reference, added planning/review analysis to diary requirements

## 2026-03-26

### Added

- `ITypedId` / `ITypedId<T>` — strongly-typed identifier interfaces with `static abstract Type IdentityType`, `object BoxedValue`, and `IEquatable<T>` constraint on backing type
- `TypedId<T>` — abstract record class (explicit constructor, not positional) for consumer-defined typed IDs (e.g., `record OrderId(Guid Value) : TypedId<Guid>(Value)`)
- `IDomainEvent` — marker interface for in-process domain events (context envelope attached by infrastructure at dispatch time)
- `IHasIdentity` / `IHasIdentity<T>` — non-generic base with `IdentityType`/`BoxedId` for runtime identity bridging; generic variant with default interface methods (DIM) so consumers only implement `T Id`; type guard on `BoxedId` setter
- `IValidatable` — domain objects that validate their own state via `GetValidationErrors()`
- `ValidatableExtensions` — `IsValid()` boolean check + `ThrowIfInvalid()` guard clause (single-collect, no double call)
- `Entity<TId>` — base class with identity-based equality, runtime type check, `IEquatable` support, `==`/`!=` operators, null guard on constructor
- `Entity<TId, TSelf, TMemento>` — memento-capable entity (3 type params) with `Snapshot`/`Restore`/`Hydrate`; auto-handles Id when `TMemento : IHasIdentity<T>` matches `TId : ITypedId<T>`; validates on both restore and hydrate via `IValidatable`
- `AggregateRoot<TId>` — entity with domain event collection (`AddDomainEvent` protected, `ClearDomainEvents` public, lazy `??=` initialization, `Array.Empty` for empty reads)
- `AggregateRoot<TId, TSelf, TMemento>` — memento-capable aggregate root with same pattern as Entity
- `MementoHelper<TId, TSelf, TMemento>` — shared memento orchestration (identity bridging, compiled expression delegate factory for TypedId construction, uninitialized instance creation); fail-fast `InvalidOperationException` when TypedId lacks required constructor
- Solution documentation: identity bridging, compiled factories, validation patterns (`docs/solutions/design-patterns/`)

### Changed

- `.editorconfig` — aligned with coding style ADR: naming rule severities to error, added modern C# rules (file-scoped namespaces, collection expressions, expression-bodied members, target-typed new, pattern matching, null checking, IDE0005 unused usings)
- `Directory.Build.props` — added `EnforceCodeStyleInBuild=true` and moved `GenerateDocumentationFile=true` from src-only to root (required by IDE0005)
- `ValueObject<TSelf, TMemento>` — implements `IValidatable`, uses `ThrowIfInvalid()` extension, `Validate()` renamed to `GetValidationErrors()`
- ADR: Domain Building Blocks — TypedId changed from record struct to abstract record class with `ITypedId`/`ITypedId<T>` interface hierarchy; TId constraint changed to `ITypedId`; added `IHasIdentity` for mementos; application-generated IDs mandated
- ADR: State Management — Memento Pattern — corrected incorrect C# limitation claim about static abstract on abstract classes; added `IHasIdentity` to ownership table; added Hydrate validation requirement

## 2026-03-25

### Added

- Dynamic test coverage badge in README (shields.io endpoint via GitHub Gist)
- Automatic git tagging on main merge — `tag-version` job creates `v{semver}` tags using GitVersion-calculated version

### Changed

- ADR: Domain Building Blocks — removed concurrency token from `AggregateRoot<TId>`, concurrency is now opt-in via `IHasVersionInfo` on mementos
- ADR: Cross-Cutting Infrastructure — introduced two-tier versioning: `IHasVersionInfo` for optimistic concurrency, `IVersionable : IHasVersionInfo` for time-travel snapshots + graveyard; graveyard now only applies to `IVersionable` aggregates
- ADR: Data Consistency — updated optimistic concurrency to reference `IHasVersionInfo` on mementos instead of `AggregateRoot`

### Added

- GitHub Actions PR validation workflow (`.github/workflows/pr.yml`) with build, test, coverage, format check, and Conventional Commits PR title validation
- `global.json` pinning .NET 10 SDK (`10.0.200`, `rollForward: latestPatch`)
- Root `Directory.Build.props` centralizing `TreatWarningsAsErrors`, `Nullable`, `ImplicitUsings` across all projects
- `src/Directory.Build.props` enabling `GenerateDocumentationFile` for all source projects
- `.gitattributes` enforcing LF line endings to match `.editorconfig`
- GitVersion integration for Conventional Commits-driven semantic versioning (`GitVersion.yml`, `.config/dotnet-tools.json`)
- Version computation and embedding in PR workflow via `gittools/actions` with `/p:Version=` pass-through
- Version displayed in GitHub Actions step summary on every PR build
- Visual coverage report (GitHub-flavored markdown table) posted as PR comment
- CI workflow also runs on push to main (enables status badges)
- CI and version badges in README
- Code coverage collection via coverlet with ReportGenerator HTML/text summary reports uploaded as build artifacts and posted as PR comment

### Changed

- `Yaf.Domain.csproj` — removed `ImplicitUsings`, `Nullable`, `GenerateDocumentationFile` (now inherited from `Directory.Build.props`)
- `Yaf.Domain.Tests.csproj` — removed `ImplicitUsings`, `Nullable` (now inherited from `Directory.Build.props`)

### Added

- Solution scaffolding: `YAF.slnx` (slnx format, in `src/`), `Yaf.Domain` class library (net10.0), `Yaf.Domain.Tests` xUnit project
- `ValueObject` — marker abstract record for simple value semantics (structural equality via C# records)
- `ValueObject<TSelf, TMemento>` — memento-capable variant with `SnapshotCore`/`RestoreCore`/`Validate` template methods, implements `IMemento<TSelf, TMemento>` directly
- `IMemento<TSelf, TMemento>` — memento contract with `Snapshot` and `static abstract Restore` (CRTP pattern)
- `IHydratable<TMemento>` — separate hydration interface for mutable entities
- `IError` — domain error interface (code + message)
- `ValidationException` — thrown when memento restoration produces invalid state
- `.editorconfig` — Microsoft C# conventions baseline, CS1591 enforcement for XML documentation on public API
- `.gitignore` — standard .NET/VS + NCrunch + Claude Code local settings
- Solution documentation: CRTP + static abstract + memento pattern investigation trail
- Session diary documenting the iterative design process

### Changed

- ADR: State Management — Memento Pattern updated to reflect `IMemento<TSelf, TMemento>` + `IHydratable<TMemento>` split

## 2026-03-24

### Added

- 24 Architecture Decision Records formalized from brainstorm sessions, organized by scope:
  - Architecture (11): core architecture style, technology stack, solution structure, testing strategy, development workflow, CI/CD, CQRS/mediator, application layer patterns, public API documentation, warnings as errors, coding style
  - Domain (5): building blocks, memento pattern, domain/integration events, result/error pattern, validation strategy
  - Infrastructure (5): persistence strategy, cross-cutting concerns, observability, data consistency, multi-tenancy
  - API (3): controller adapter, OpenAPI documentation, authentication and authorization
- ADR INDEX.md at `docs/adr/` for navigating all ADRs by scope
- Session diary for ADR formalization session
- ADR template and organization brainstorm — 14-section template, subfolder-by-scope structure, INDEX.md convention
- Session diary for ADR template brainstorm session

## 2026-03-22

### Added

- Session diary for library design & DDD concepts brainstorm session
- YAF DDD concepts brainstorm — 22 domain concepts categorized across Domain/Application/Infrastructure layers
- YAF library design brainstorm — package structure, CQRS abstractions, cross-cutting concerns, technology choices
- .NET 10 web API best practices research — Clean Architecture, DDD, Wolverine, Aspire, containerization
- Maister plugin installed and configured (`.claude/settings.json`, `docs/general/dev-setup.md`)
- Research: Maister vs Compound Engineering comparison with combined workflow cheatsheet
- Session diary for the Maister vs CE research session

### Removed

- `docs/general/tools.md` — redundant with `docs/general/dev-setup.md`

### Changed

- README: removed dead link to deleted `tools.md`

## 2026-03-21

### Added

- Cartomatic WTFPL license (based on WTFPL v2 with credit appreciation and pint/charity clauses)
- CLAUDE.md with project overview, architecture goals, and key constraints
- Documentation conventions (timestamped filenames, directory structure for plans/brainstorms/research)
- PR title conventions based on Conventional Commits
- Changelog based on Keep a Changelog
- Claude Code tooling setup: csharp-lsp plugin, NuGet MCP server, project-level permissions (`.claude/settings.json`, `.mcp.json`)
- Contributor dev setup guide (`docs/general/dev-setup.md`)
- Session diary convention with first entry (`docs/diary/`)
- Communication guidelines for human-AI collaboration ("when in doubt, ask")
- Research doc on Claude Code plugins, MCP servers, and skills for .NET development

### Changed

- CLAUDE.md with project overview, architecture goals, and key constraints
- Documentation conventions (timestamped filenames, directory structure for plans/brainstorms/research)
- PR title conventions based on Conventional Commits
- Changelog based on Keep a Changelog
- Claude Code tooling setup: csharp-lsp plugin, NuGet MCP server, project-level permissions (`.claude/settings.json`, `.mcp.json`)
- Contributor dev setup guide (`docs/general/dev-setup.md`)
- Session diary convention with first entry (`docs/diary/`)
- Communication guidelines for human-AI collaboration ("when in doubt, ask")
- Research doc on Claude Code plugins, MCP servers, and skills for .NET development

### Changed

- README rewritten to explain documentation structure and project philosophy
