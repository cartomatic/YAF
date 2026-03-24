# CI/CD and Deployment

- **Timestamp:** 2026-03-24 10:31
- **Status:** under review
- **Scope:** architecture
- **Stakeholders:** Proposed by: Claude Code, Decided by: @cartomatic

---

## Summary

YAF uses GitHub Actions for CI/CD. The pipeline builds, tests, and versions the library on every PR event and merge to main. Deployment means publishing NuGet packages — stable releases automatically on merge to main, prerelease packages on-demand from PRs. Containerization applies only to example applications, not the library itself.

## Drivers

1. **GitHub-native** — The repository is hosted on GitHub. GitHub Actions is the natural CI/CD choice — no external service to configure or maintain.
2. **Automated quality gates** — Every PR must pass build + test before merge. No manual "did you run the tests?" checks.
3. **Automated publishing** — Merging to main should publish packages without manual steps. Reduce friction between "code merged" and "package available."
4. **Prerelease consultation** — External reviewers need to test against proposed changes. On-demand prerelease packages enable this without polluting the stable feed.
5. **Minimal infrastructure** — YAF is a library. Deployment is NuGet publishing, not server provisioning. Keep CI/CD focused on build → test → pack → publish.

## Options

### CI Platform

| Option | Assessment |
|--------|------------|
| **GitHub Actions** | **Selected.** Native to GitHub, free for public repos, good ecosystem of actions, supports Docker for TestContainers. |
| Azure DevOps | Capable but requires separate service. Adds complexity without benefit for an open-source GitHub project. |
| CircleCI / Travis | External services. No advantage over GitHub Actions for this project. |

### Deployment Model

| Option | Assessment |
|--------|------------|
| **NuGet-only** | **Selected.** YAF is a library — deployment means publishing NuGet packages. No servers, no containers, no environments to manage. |
| NuGet + container images | Unnecessary for the library. Container builds apply to example apps only and are not published. |

### NuGet Signing

| Option | Assessment |
|--------|------------|
| **No signing initially** | **Selected.** Adds complexity without proportional benefit at this stage. Can be added later when the package has consumers who require it. |
| Sign from the start | Requires certificate management, key rotation, and secure storage in CI. Premature for a new project. |

## Recommendation

GitHub Actions with two main workflows: a PR workflow for validation and a release workflow for publishing. Keep it simple — no environments, no staging, no signing.

## Consequences

**Positive:**
- Fully automated pipeline from PR to published package
- GitHub Actions is free for public repos and requires no external accounts
- Docker support in GitHub Actions runners enables TestContainers in CI
- On-demand prerelease publishing keeps the feed clean while enabling consultation

**Negative:**
- GitHub Actions runner minutes are limited for private repos (not an issue while public)
- TestContainers in CI adds time to the test step (mitigated by container caching)
- No package signing means consumers who require signed packages cannot use YAF initially

## Conclusion

### Workflows

#### 1. PR Validation (`pr.yml`)

**Triggers:** PR opened, synchronized (push), reopened.

```
Steps:
1. Checkout
2. Setup .NET 10 SDK
3. Restore dependencies
4. Build (Release configuration)
5. Compute version (from Conventional Commit type + PR metadata)
   → Embed as prerelease: {next-version}-pr.{pr-number}.{run-number}
6. Run unit tests (Yaf.Domain.Tests, Yaf.Application.Tests)
7. Run integration tests (Yaf.Infrastructure.Tests — requires Docker)
8. Run API tests (Yaf.Api.Tests — requires Docker)
9. Run adapter tests (Yaf.Application.Wolverine.Tests)
10. Run example tests (examples/*/tests/ — requires Docker)
11. AI-assisted code review (automated reviewer action)
11. Pack NuGet packages (with embedded prerelease version)
12. Upload packages as build artifacts (not published to any feed)
```

**Gate:** All steps must pass before merge is allowed.

#### 2. Prerelease Publish (`prerelease.yml`)

**Triggers:** Manual workflow dispatch on a PR branch, or a designated label applied to a PR.

```
Steps:
1–11. Same as PR Validation (build, test, version, pack)
12. Publish prerelease packages to configured feed
```

**Version format:** `{next-version}-pr.{pr-number}.{run-number}`

#### 3. Release (`release.yml`)

**Triggers:** Push to main (after squash merge).

```
Steps:
1. Checkout
2. Setup .NET 10 SDK
3. Determine version bump from merged commit message (Conventional Commit type)
4. Check for src/ changes — skip release if only docs/examples/build changed
5. Restore dependencies
6. Build (Release configuration)
7. Run full test suite
8. Pack NuGet packages (with stable version)
9. Publish to nuget.org
10. Create git tag (v{version})
11. Create GitHub Release with changelog excerpt
```

**Version format:** `{MAJOR}.{MINOR}.{PATCH}` (stable)

### Version Computation

The CI pipeline computes the next version based on:
1. **Latest git tag** — determines the current version baseline
2. **Conventional Commit type** in the merged commit (PR title) — determines bump level
3. **Path filter** — only `src/` changes trigger a bump; `docs/`, `examples/`, `build/` do not

A versioning tool (e.g., GitVersion, Nerdbank.GitVersioning, or a custom script) manages this computation.

### Containerization

Docker applies to **example applications only**, not the library packages. **Linux containers only** — Windows containers are not supported or used.

- Example apps use Linux-based Docker multi-stage builds
- Example app containers are **not published** to any registry
- Aspire orchestration may be used for local multi-service development with the example app
- All Dockerfiles target Linux base images (e.g., `mcr.microsoft.com/dotnet/aspnet:10.0` which defaults to Linux)

### Pipeline Configuration

| Setting | Value |
|---------|-------|
| **CI platform** | GitHub Actions |
| **Runner** | `ubuntu-latest` (Linux — Docker support for TestContainers) |
| **.NET SDK** | 10.x (pinned via `global.json`) |
| **Build configuration** | Release |
| **NuGet stable feed** | nuget.org |
| **NuGet prerelease feed** | Configurable (GitHub Packages, Azure Artifacts, or nuget.org with prerelease tag) |
| **Package signing** | None (initially) |
| **Branch protection** | Main branch requires passing PR Validation workflow |

### What CI Does NOT Do

- No deployment to servers or cloud environments — YAF is a library
- No container image publishing — Docker is for example apps and test infrastructure only
- No package signing — deferred to later
- No automatic changelog generation — CHANGELOG.md is maintained as part of the PR process

## More Information

- [ADR: Development Workflow](20260324-1017-development-workflow.md) — branching, versioning, PR process
- [ADR: Testing Strategy](20260324-1000-testing-strategy.md) — test levels that CI must execute
- [ADR: Solution Structure](20260324-0953-solution-structure-and-package-layering.md) — packages to build and publish
