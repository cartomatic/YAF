# Development Workflow

- **Timestamp:** 2026-03-24 10:17
- **Status:** under review
- **Scope:** architecture
- **Stakeholders:** Proposed by: Claude Code, Decided by: @cartomatic

---

## Summary

YAF follows a trunk-based development workflow with short-lived feature branches, AI-assisted code reviews, Conventional Commit-driven semantic versioning, and automated version embedding on every build. Stable releases publish automatically on merge to main; prerelease packages for PRs require explicit intent.

## Drivers

1. **AI-generated code** — All code is AI-generated. The workflow must include safeguards (reviews, CI checks) to catch incorrect or unsafe generated code.
2. **Small team** — Primarily one human decision-maker + AI tools. The workflow should be lightweight, not ceremonial.
3. **Fast feedback** — Changes should reach main quickly. Long-lived branches create merge pain and delay feedback.
4. **Consumer confidence** — Published NuGet packages must have clear, predictable version numbers. Consumers need to trust that a version bump reflects real changes.
5. **Consultation workflow** — Sometimes a change needs external review before merging. Prerelease packages allow others to test against a proposed change.
6. **Reproducibility** — Every build artifact should carry an embedded version that traces back to the exact commit and PR.

## Options

### Branching Strategy

| Option | Assessment |
|--------|------------|
| **Trunk-based development** | **Selected.** Short-lived feature branches off main, merged via squash. No long-lived develop/release branches. Simple, fast, fits small team. |
| GitFlow | Too ceremonial for a small team. Develop/release/hotfix branches add overhead without proportional benefit. |
| GitHub Flow | Similar to trunk-based but traditionally allows longer-lived branches. Trunk-based is stricter about keeping branches short. |

### PR Review Process

| Option | Assessment |
|--------|------------|
| **AI-assisted review + human approval** | **Selected.** AI reviews catch mechanical issues (style, bugs, security); human reviews focus on architectural intent and business correctness. |
| Human-only review | Misses the advantage of AI catching rote issues. Higher burden on the human reviewer. |
| AI-only review | Insufficient — architectural decisions and business intent require human judgment. |

### Version Strategy

| Option | Assessment |
|--------|------------|
| **Conventional Commits → Semantic Versioning** | **Selected.** PR titles follow Conventional Commits (already a project convention). CI derives the version bump from the commit type. Predictable, automated, traceable. |
| Manual version bumps | Error-prone, easy to forget, doesn't scale. |
| CalVer (calendar versioning) | Doesn't communicate breaking vs non-breaking changes. Poor fit for a NuGet library. |

### Prerelease Package Publishing

| Option | Assessment |
|--------|------------|
| **Explicit intent (label, manual trigger)** | **Selected.** Version is always embedded in the build, but publishing a prerelease NuGet from a PR requires deliberate action. Avoids polluting the feed with every push. |
| Automatic on every PR push | Too noisy. Creates packages nobody asked for. |
| No prerelease packages | Limits the consultation workflow. External reviewers can't test against a proposed change. |

## Recommendation

Trunk-based development with AI-assisted reviews, Conventional Commit-driven versioning, and explicit prerelease publishing.

## Consequences

**Positive:**
- Simple branching model — no long-lived branches to maintain or merge
- AI-assisted reviews provide a consistent quality baseline for every PR
- Semantic versioning derived from commit types is predictable and automatic
- Prerelease packages enable external consultation without polluting the main feed
- Every build artifact is traceable to a commit and version

**Negative:**
- Trunk-based requires discipline to keep branches short-lived (acceptable with small team)
- AI review tools need configuration and maintenance
- Conventional Commit enforcement requires CI validation (can be automated with commitlint or similar)

## Conclusion

### Branching

- **Main branch** is the single source of truth. Always deployable.
- **Feature branches** are short-lived, branched from main, and merged back via **squash merge**.
- **Branch naming** follows: `<type>/<short-description>` (e.g., `feat/domain-building-blocks`, `fix/memento-mapping`).
- **No long-lived branches** — no develop, release, or hotfix branches.

### PR Process

1. **Create PR** with a Conventional Commit title (e.g., `feat(domain): add Entity and AggregateRoot base types`).
2. **CI runs automatically:** build, test, version embedding, lint.
3. **AI-assisted review** runs automatically on PR creation and updates — catches style issues, potential bugs, security concerns.
4. **Human review** focuses on architectural intent, business correctness, and whether the change aligns with ADRs.
5. **Squash merge** to main. PR title becomes the commit message.
6. **Delete branch** after merge (remote and local).

### Versioning

Semantic versioning (`MAJOR.MINOR.PATCH`) derived from Conventional Commit types in PR titles:

| Commit Type | Version Bump | Example |
|-------------|-------------|---------|
| `fix:` | Patch | `1.2.3` → `1.2.4` |
| `feat:` | Minor | `1.2.3` → `1.3.0` |
| `feat!:` or `BREAKING CHANGE` | Major | `1.2.3` → `2.0.0` |
| `docs:`, `chore:`, `ci:`, `style:` | No bump | No release (non-source changes) |
| `refactor:`, `perf:`, `test:` | Patch | `1.2.3` → `1.2.4` |

**Version scope:** Only changes to `src/` trigger a version bump. Changes to `docs/`, `examples/`, `build/`, or repository configuration do not.

**Version embedding:** Every CI build (main and PR) computes and embeds the version into build artifacts, regardless of whether a package is published.

### Publishing

| Trigger | Version Format | Feed | Automatic? |
|---------|---------------|------|-----------|
| **Merge to main** (with src/ changes) | `1.2.3` (stable) | nuget.org | Yes — automatic |
| **PR** (explicit intent) | `1.2.3-pr.{number}.{build}` | Prerelease feed (configurable) | No — requires explicit trigger (label, manual workflow, or comment) |

Prerelease packages carry the PR number and build counter in the version suffix, making them traceable and sortable.

### Commit & PR Conventions

- PR titles **must** follow Conventional Commits format: `<type>(<optional scope>): <description>`
- CI validates the PR title format (commitlint or equivalent)
- Squash merge ensures one clean commit per PR on main
- CHANGELOG.md is updated as part of the PR (automated or manual)

## More Information

- [Conventional Commits specification](https://www.conventionalcommits.org/en/v1.0.0/)
- [Semantic Versioning specification](https://semver.org/)
- [ADR: Solution Structure](20260324-0953-solution-structure-and-package-layering.md) — package layout
- [CLAUDE.md](../../../CLAUDE.md) — PR title conventions
