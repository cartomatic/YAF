# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), with changes grouped by date.

## 2026-03-25

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
