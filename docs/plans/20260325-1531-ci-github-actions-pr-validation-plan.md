---
title: "ci: GitHub Actions PR validation workflow"
type: ci
status: completed
date: 2026-03-25
---

# ci: GitHub Actions PR Validation Workflow

## Overview

Set up GitHub Actions CI/CD for YAF — starting minimal with a PR validation workflow that builds, tests, and enforces code quality on every pull request. This is the first CI/CD deliverable; NuGet publishing workflows will follow separately.

The [CI/CD ADR](../adr/architecture/20260324-1031-ci-cd-and-deployment.md) defines the full three-workflow blueprint. This plan implements only the PR validation portion plus the prerequisite build infrastructure files that the ADR calls for but do not yet exist.

## Problem Statement / Motivation

There are currently **no automated quality gates** in the repository. PRs can be merged without building, testing, or checking code quality. The ADRs define comprehensive CI/CD requirements, but none of the infrastructure exists:

- No `.github/` directory at all
- No `global.json` to pin the .NET SDK
- No `Directory.Build.props` for shared build properties (despite the [Warnings-as-Errors ADR](../adr/architecture/20260324-1343-warnings-as-errors.md) requiring it)
- No PR title validation (despite the [Development Workflow ADR](../adr/architecture/20260324-1017-development-workflow.md) requiring Conventional Commits)

Without CI, the "all code is AI-generated" constraint has no automated verification — warnings, test failures, and style violations can silently reach `main`.

## Proposed Solution

Deliver four artifacts:

1. **`global.json`** — Pin the .NET 10 SDK for reproducible builds
2. **`Directory.Build.props`** (root) — Centralize `TreatWarningsAsErrors`, `Nullable`, `ImplicitUsings` across all projects
3. **`src/Directory.Build.props`** — Add `GenerateDocumentationFile` for all source projects (inherits root props)
4. **`.github/workflows/pr.yml`** — PR validation workflow (build, test, format check, coverage)
5. **PR title validation** — Enforce Conventional Commits format on PR titles

### Workflow Design

```
PR opened / synchronized / reopened / edited
  │
  ├─ [Job: validate-title]  ← runs on ALL trigger events
  │    └─ Check PR title matches Conventional Commits
  │
  └─ [Job: build-and-test]  ← skipped on 'edited' event (title-only change)
       ├─ Checkout
       ├─ Setup .NET 10 (from global.json)
       ├─ Restore
       ├─ Build (Release, TreatWarningsAsErrors)
       ├─ Test (xUnit + coverlet coverage collection)
       ├─ Generate coverage report (ReportGenerator or coverlet summary)
       ├─ Upload coverage report as build artifact
       └─ Format check (dotnet format --verify-no-changes)
```

### Why Two Jobs

PR title validation must re-run when the title is edited (`edited` event), but rebuilding and retesting on a title edit wastes runner time. Splitting into two jobs lets each respond to the right triggers.

## Technical Considerations

### `global.json`

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestPatch"
  }
}
```

- **Placement:** Repository root (standard convention, `actions/setup-dotnet` looks here)
- **`rollForward: latestPatch`** — Allows patch updates (10.0.101, 10.0.102) for security fixes without breaking reproducibility
- .NET 10 GA'd November 2025; `10.0.100` is the RTM SDK version

### `Directory.Build.props` (root)

```xml
<Project>
  <PropertyGroup>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
```

- **Placement:** Repository root — applies to all projects (`src/`, `tests/`, `examples/`)
- Per the [Warnings-as-Errors ADR](../adr/architecture/20260324-1343-warnings-as-errors.md), all project types get `TreatWarningsAsErrors`

### `src/Directory.Build.props`

```xml
<Project>
  <PropertyGroup>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>
</Project>
```

- **Placement:** `src/` directory — applies only to source projects, not tests or examples
- Inherits all properties from the root `Directory.Build.props` automatically (MSBuild walks up the directory tree)
- Enables XML doc generation and CS1591 enforcement for all source projects
- **Cleanup:** Remove `ImplicitUsings`, `Nullable`, and `GenerateDocumentationFile` from `Yaf.Domain.csproj`; remove `ImplicitUsings` and `Nullable` from `Yaf.Domain.Tests.csproj`

### PR Title Validation

- **Action:** [`amannn/action-semantic-pull-request`](https://github.com/amannn/action-semantic-pull-request) — most popular, lightweight, supports type enumeration
- **Allowed types:** `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`, `revert`
- **Scopes:** Not enforced initially — optional and freeform
- **Breaking changes:** `!` suffix is allowed (e.g., `feat!: redesign API`)

### Workflow Configuration

| Setting | Value | Rationale |
|---------|-------|-----------|
| **Runner** | `ubuntu-latest` | Docker support, free for public repos |
| **Build config** | `Release` | Match production; catches Release-only issues |
| **Solution path** | `src/YAF.slnx` | Solution is not in root — must be explicit in all `dotnet` commands |
| **Concurrency** | `group: pr-${{ github.event.pull_request.number }}`, `cancel-in-progress: true` | Cancel stale runs on rapid pushes |
| **Permissions** | `contents: read`, `pull-requests: read` | Least privilege; title validation needs PR read access |
| **NuGet caching** | `actions/cache` on `~/.nuget/packages` | Trivial to add, saves restore time as dependencies grow |
| **Format check** | `dotnet format src/YAF.slnx --verify-no-changes` | Full format check (whitespace + style); overlaps with build errors is acceptable |
| **Coverage** | `--collect:"XPlat Code Coverage"` with report generation | Collect via coverlet, generate summary report, upload as artifact |

### `.slnx` Compatibility Note

The `.slnx` solution format is new in .NET 10. Both `dotnet build` and `dotnet test` support it. `dotnet format` support should be verified during implementation — if it does not work with `.slnx`, fall back to running format against individual `.csproj` files.

## Acceptance Criteria

### Prerequisites

- [x] `global.json` exists at repository root, pins .NET 10 SDK with `rollForward: latestPatch`
- [x] `Directory.Build.props` exists at repository root with `TreatWarningsAsErrors`, `Nullable`, `ImplicitUsings`
- [x] `src/Directory.Build.props` exists with `GenerateDocumentationFile`
- [x] `Yaf.Domain.csproj` no longer duplicates `ImplicitUsings`, `Nullable`, or `GenerateDocumentationFile` (all inherited)
- [x] `Yaf.Domain.Tests.csproj` no longer duplicates `ImplicitUsings` and `Nullable`
- [x] Solution still builds and tests pass locally after these changes

### PR Workflow

- [x] `.github/workflows/pr.yml` exists and triggers on `pull_request: [opened, synchronize, reopened, edited]`
- [x] `build-and-test` job: restores, builds (Release), runs tests, checks format — skipped on `edited` event
- [x] `validate-title` job: validates PR title against Conventional Commits — runs on all events
- [x] Workflow uses concurrency group to cancel stale runs
- [x] Workflow declares minimal permissions (`contents: read`, `pull-requests: read`)
- [x] NuGet packages are cached between runs
- [x] Code coverage is collected via coverlet and a summary report is generated
- [x] Coverage report is uploaded as a build artifact for PR review

### Verification

- [ ] A PR with a compiler warning fails the build
- [ ] A PR with a failing test fails the workflow
- [ ] A PR with formatting violations fails the format check
- [ ] A PR with a non-conventional title fails the title check
- [ ] Editing a PR title to fix it re-runs only the title check (not the full build)
- [ ] A valid PR passes all checks

## Dependencies & Risks

### Dependencies

- **.NET 10 SDK availability** in `actions/setup-dotnet` — .NET 10 GA'd November 2025, should be available. Verify the exact version string works.
- **`amannn/action-semantic-pull-request`** — third-party action. Pin to a specific version hash for security.

### Risks

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| `.slnx` not supported by `dotnet format` | Low | Fall back to per-project format check |
| .NET 10 SDK version string mismatch in CI | Low | Test locally with `dotnet --version`, verify against setup-dotnet docs |
| `edited` event trigger causes unexpected behavior | Low | Test with title-only edits; `build-and-test` job has `if` guard |

### Out of Scope

- NuGet publishing workflows (`prerelease.yml`, `release.yml`) — separate plan
- Version computation and embedding — needed for publishing, not for PR validation
- Branch protection rules — manual GitHub settings step, documented as follow-up
- Test result reporting (TRX/JUnit upload) — can be added later
- Coverage thresholds — coverage is reported but no minimum threshold enforced yet
- Docker/TestContainers in CI — only unit tests exist currently; add when integration tests arrive

### Follow-up Tasks

- [ ] Configure branch protection on `main` to require the PR workflow (manual GitHub step)
- [ ] Plan and implement `release.yml` workflow for NuGet publishing
- [ ] Add structured test result reporting when test count grows
- [ ] Set up coverage thresholds once baseline coverage is established
- [ ] Consider adding coverage PR comments (e.g., via Codecov or a GitHub Action)

## References & Research

### ADRs (all under review)

- [CI/CD and Deployment](../adr/architecture/20260324-1031-ci-cd-and-deployment.md) — full CI/CD blueprint
- [Testing Strategy](../adr/architecture/20260324-1000-testing-strategy.md) — test levels CI must execute
- [Development Workflow](../adr/architecture/20260324-1017-development-workflow.md) — branching, PR process, versioning
- [Warnings as Errors](../adr/architecture/20260324-1343-warnings-as-errors.md) — `Directory.Build.props` requirements
- [Coding Style](../adr/architecture/20260324-1635-coding-style.md) — `.editorconfig` enforcement

### Key Files

- `src/YAF.slnx` — solution file (.slnx format)
- `src/Yaf.Domain/Yaf.Domain.csproj` — source project (needs cleanup)
- `tests/Yaf.Domain.Tests/Yaf.Domain.Tests.csproj` — test project (needs cleanup)
- `.editorconfig` — code style rules (CS1591 as warning, test exemptions)
