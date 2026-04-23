---
title: "ci: add GitVersion for Conventional Commits semantic versioning"
type: ci
status: active
date: 2026-03-25
---

# ci: Add GitVersion for Conventional Commits Semantic Versioning

## Overview

Integrate GitVersion into the YAF project to automatically compute semantic versions from Conventional Commits. GitVersion analyzes git history and commit messages to determine the next version, which is then embedded into assemblies during CI builds. This delivers the "automated version embedding on every build" requirement from the [Development Workflow ADR](../adr/architecture/20260324-1017-development-workflow.md) without yet implementing NuGet publishing.

## Problem Statement / Motivation

The ADR specifies that every CI build should compute and embed a version derived from Conventional Commits. Currently:

- No versioning tool is configured
- Assemblies have default `1.0.0.0` version
- No mechanism to derive version bumps from commit types (`fix:` → patch, `feat:` → minor, etc.)
- No prerelease version format for PR builds
- The PR title validation (Conventional Commits) is in place but not yet connected to versioning

## Proposed Solution

Use **GitVersion** (v6.x) in **Conventional Commits mode** via regex-based commit message matching, configured with the `GitHubFlow/v1` workflow.

### Architecture

```
Developer pushes → CI checkout (full history) → GitVersion computes version
    → dotnet build /p:Version={fullSemVer} → version embedded in assemblies
```

### Approach: CLI in CI, not MSBuild package

Two valid integration paths exist. This plan uses the **CI action + `/p:Version=`** approach:

| Approach | Pros | Cons |
|----------|------|------|
| **GitVersion.MsBuild NuGet** | Auto-embeds on every build (local + CI) | Build-time dependency, harder to debug, conflicts if CLI also used |
| **CI action + `/p:Version=`** (chosen) | Explicit, debuggable, no build dependency, version visible in workflow logs | Local builds get default `1.0.0` (not a problem — CI is the source of truth) |

### Version bump mapping (per ADR)

| Commit type | Bump | GitVersion config |
|-------------|------|-------------------|
| `feat!:`, `fix!:`, `BREAKING CHANGE` footer | **Major** | `major-version-bump-message` |
| `feat:` | **Minor** | `minor-version-bump-message` |
| `fix:`, `perf:`, `refactor:`, `test:`, `revert:` | **Patch** | `patch-version-bump-message` |
| `docs:`, `style:`, `ci:`, `chore:`, `build:` | **No bump** | `no-bump-message` |

### Prerelease format

- **Main**: `1.2.3` (stable, no label)
- **PRs**: `1.2.3-pr.42.3` (PR number + commit height)

## Technical Considerations

### `GitVersion.yml`

```yaml
workflow: GitHubFlow/v1

# Conventional Commits version bump patterns
major-version-bump-message: "^(build|chore|ci|docs|feat|fix|perf|refactor|revert|style|test)(\\([\\w\\s-,/\\\\]*\\))?(!:|:.*\\n\\n((.+\\n)+\\n)?BREAKING[ -]CHANGE:\\s.+)"
minor-version-bump-message: "^(feat)(\\([\\w\\s-,/\\\\]*\\))?:"
patch-version-bump-message: "^(fix|perf|refactor|test|revert)(\\([\\w\\s-,/\\\\]*\\))?:"
no-bump-message: "^(docs|style|ci|chore|build)(\\([\\w\\s-,/\\\\]*\\))?:"

assembly-versioning-scheme: MajorMinor
assembly-file-versioning-scheme: MajorMinorPatch
tag-prefix: '[vV]?'

next-version: 0.1.0

branches:
  main:
    label: ''
    increment: Patch
    is-main-branch: true
  pull-request:
    mode: ContinuousDelivery
    label: 'pr.{Number}'
    increment: Inherit
  feature:
    label: '{BranchName}'
    increment: Inherit
```

Key decisions:
- **`next-version: 0.1.0`** — sets the starting version without needing a retroactive tag. Remove this after the first real tag is created.
- **`assembly-versioning-scheme: MajorMinor`** — prevents binding redirect churn for consumers (AssemblyVersion only changes on major/minor bumps)
- **`assembly-file-versioning-scheme: MajorMinorPatch`** — FileVersion still has full precision for diagnostics
- **`no-bump-message`** — explicitly suppresses version bumps for non-functional commits (without this, they'd fall through to the branch's `increment: Patch`)
- **`tag-prefix: '[vV]?'`** — accepts both `v1.0.0` and `1.0.0` tags

### Local dotnet tool

```json
// .config/dotnet-tools.json (created by dotnet new tool-manifest)
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "gitversion.tool": {
      "version": "6.1.0",
      "commands": ["dotnet-gitversion"]
    }
  }
}
```

Developers can run `dotnet gitversion` locally to check the computed version. The tool manifest is committed to the repo, ensuring consistent versions across all contributors.

### PR workflow changes

The existing `pr.yml` needs these modifications:

1. **`fetch-depth: 0`** on checkout — GitVersion needs full git history to walk commits back to the last tag
2. **GitVersion setup + execute** steps — compute the version
3. **Pass version to build** — `dotnet build /p:Version=${{ steps.gitversion.outputs.fullSemVer }}`
4. **Display version** — show computed version in workflow summary

```yaml
# New/modified steps in build-and-test job:

- name: Checkout
  uses: actions/checkout@v4
  with:
    fetch-depth: 0  # GitVersion needs full history

- name: Install GitVersion
  uses: gittools/actions/gitversion/setup@v3
  with:
    versionSpec: '6.x'

- name: Determine version
  id: gitversion
  uses: gittools/actions/gitversion/execute@v3

- name: Display version
  run: |
    echo "## Version: ${{ steps.gitversion.outputs.fullSemVer }}" >> $GITHUB_STEP_SUMMARY

- name: Build
  run: >-
    dotnet build src/YAF.slnx -c Release --no-restore
    /p:Version=${{ steps.gitversion.outputs.fullSemVer }}
    /p:AssemblyVersion=${{ steps.gitversion.outputs.assemblySemVer }}
    /p:FileVersion=${{ steps.gitversion.outputs.assemblySemFileVer }}
    /p:InformationalVersion=${{ steps.gitversion.outputs.informationalVersion }}

- name: Test with coverage
  run: >-
    dotnet test src/YAF.slnx -c Release --no-build
    --collect:"XPlat Code Coverage"
    --results-directory ./coverage
```

### `fetch-depth: 0` impact

GitVersion requires full git history — shallow clones produce wrong versions or fail. For YAF's current size this adds negligible time. For very large repos, `--filter=blob:none` (treeless clone) is an option but unnecessary here.

### Path filtering

GitVersion supports `ignore.paths` to skip commits that only touch non-source files, but the behavior is tricky (commit is only ignored if **all** files match the pattern). Since we're not publishing yet, path filtering can be deferred to the release workflow. The PR workflow always computes a version regardless.

## Acceptance Criteria

### Configuration

- [x] `GitVersion.yml` exists at repository root with Conventional Commits regexes
- [x] `.config/dotnet-tools.json` exists with `gitversion.tool` pinned
- [x] `dotnet tool restore` succeeds and `dotnet gitversion` runs locally

### CI Integration

- [x] `pr.yml` checkout uses `fetch-depth: 0`
- [x] GitVersion setup and execute steps run before build
- [x] Computed version is passed to `dotnet build` via `/p:Version=`
- [x] Version is displayed in the GitHub Actions step summary
- [x] Build and test steps use the computed version

### Version Correctness

- [ ] A `fix:` commit produces a patch bump
- [ ] A `feat:` commit produces a minor bump
- [ ] A `feat!:` commit produces a major bump
- [ ] A `docs:` or `ci:` commit produces no version bump
- [x] PR builds produce prerelease versions in format `X.Y.Z-pr.{number}.{height}`
- [x] `dotnet gitversion /showconfig` locally shows the expected effective configuration

### Non-regression

- [x] Existing build, test, coverage, format check, and title validation still pass
- [ ] Coverage comment still posts correctly

## Dependencies & Risks

### Dependencies

- **`gittools/actions` GitHub Action** — pin to a specific version for security
- **GitVersion.Tool** — pin version in `.config/dotnet-tools.json`
- **Full git history** — `fetch-depth: 0` is non-negotiable for GitVersion

### Risks

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| Regex misconfiguration silently produces wrong bumps | Medium | Test with `dotnet gitversion /showconfig` and verify each commit type |
| `fetch-depth: 0` slows CI checkout | Low | Negligible for current repo size; treeless clone available if needed |
| Non-matching commits produce unexpected patch bumps | Medium | `no-bump-message` regex covers all non-bumping types |
| YAML regex escaping issues | Medium | Use `dotnet gitversion` locally to validate before pushing |

### Out of Scope

- NuGet publishing workflows — separate plan
- Git tag creation on merge to main — part of release workflow
- Path filtering (`src/` only triggers bump) — deferred to release workflow
- Version display in README or package metadata — cosmetic, can be added later

## References & Research

### ADRs

- [Development Workflow](../adr/architecture/20260324-1017-development-workflow.md) — versioning rules, bump mapping
- [CI/CD and Deployment](../adr/architecture/20260324-1031-ci-cd-and-deployment.md) — version embedding, pipeline structure

### Research

- [GitVersion Conventional Commits Research](../research/20260325-1618-gitversion-conventional-commits.md) — detailed findings on configuration, gotchas, and integration patterns

### External

- [GitVersion configuration reference](https://gitversion.net/docs/reference/configuration)
- [GitVersion GitHub Actions integration](https://gitversion.net/docs/reference/build-servers/github-actions)
- [GitTools/actions repository](https://github.com/GitTools/actions)

### Key Files

- `.github/workflows/pr.yml` — existing PR workflow to modify
- `Directory.Build.props` — root build properties (no changes needed)
- `global.json` — SDK pinning (no changes needed)
