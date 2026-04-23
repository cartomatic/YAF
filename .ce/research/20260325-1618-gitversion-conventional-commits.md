# GitVersion Conventional Commits Mode -- Research

**Date:** 2025-03-25
**Sources:** gitversion.net docs, GitTools/GitVersion GitHub repo, GitTools/actions GitHub repo, Context7

---

## 1. Summary

GitVersion is a .NET tool that calculates Semantic Versions from Git history. It supports Conventional Commits through regex-based commit message matching that maps commit types to version bumps. It is **not** a native first-class mode -- rather, you override the default `+semver:` commit message patterns with Conventional Commits regexes.

Current stable version: **6.x** (6.7.x as of this writing).

---

## 2. GitVersion.yml Configuration for Conventional Commits

GitVersion does not have a `conventional-commits: true` toggle. Instead, you replace the default `major-version-bump-message`, `minor-version-bump-message`, and `patch-version-bump-message` regexes with patterns that match Conventional Commits syntax.

### Recommended configuration

```yaml
workflow: GitHubFlow/v1

# Conventional Commits version bump patterns
major-version-bump-message: "^(build|chore|ci|docs|feat|fix|perf|refactor|revert|style|test)(\\([\\w\\s-,/\\\\]*\\))?(!:|:.*\\n\\n((.+\\n)+\\n)?BREAKING[ -]CHANGE:\\s.+)"
minor-version-bump-message: "^(feat)(\\([\\w\\s-,/\\\\]*\\))?:"
patch-version-bump-message: "^(fix|perf)(\\([\\w\\s-,/\\\\]*\\))?:"

assembly-versioning-scheme: MajorMinorPatch
assembly-file-versioning-scheme: MajorMinorPatch
tag-prefix: '[vV]?'

branches:
  main:
    label: ''
    increment: Patch
    is-main-branch: true
  pull-request:
    mode: ContinuousDelivery
    label: 'pr.{Number}'
    increment: Inherit
```

### Key settings explained

| Setting | Purpose |
|---------|---------|
| `workflow: GitHubFlow/v1` | Base workflow template (alternatives: `GitFlow/v1`, `TrunkBased/preview1`) |
| `major-version-bump-message` | Regex matching `feat!:`, `fix!:`, or `BREAKING CHANGE:` footer |
| `minor-version-bump-message` | Regex matching `feat:` or `feat(scope):` |
| `patch-version-bump-message` | Regex matching `fix:` or `perf:` |
| `tag-prefix` | Accepts both `v1.0.0` and `1.0.0` tags |
| `commit-message-incrementing` | `Enabled` (default) -- must stay enabled for commit-based bumps |

### What the regexes match

- **Major**: `feat!: something`, `fix(scope)!: something`, or any commit with a `BREAKING CHANGE:` footer
- **Minor**: `feat: something`, `feat(scope): something`
- **Patch**: `fix: something`, `perf(scope): something`
- **No bump**: Commits like `docs:`, `ci:`, `chore:` that match none of the above -- these get the branch's default `increment` (typically `Patch` on main)

**Important**: Commits that match *none* of the three patterns still cause a version increment using the branch's configured `increment` value. If main is set to `increment: Patch`, then `docs:` and `chore:` commits will produce a patch bump. To prevent this, you can use `no-bump-message` with a pattern matching those types, or accept that non-functional commits produce patch bumps.

---

## 3. Installation

### Dotnet local tool (recommended for projects)

```bash
dotnet new tool-manifest   # creates .config/dotnet-tools.json
dotnet tool install GitVersion.Tool
```

Run with: `dotnet gitversion`

This pins the version in `.config/dotnet-tools.json` which gets committed to the repo, ensuring all contributors and CI use the same version. Restore with `dotnet tool restore`.

### Dotnet global tool

```bash
dotnet tool install --global GitVersion.Tool
```

Run with: `dotnet-gitversion`

### GitHub Action (for CI)

```yaml
- uses: gittools/actions/gitversion/setup@v4.4.2
  with:
    versionSpec: '6.7.x'
```

### Recommendation for CI

Use the GitHub Action for CI (it handles caching and cross-platform setup) and a dotnet local tool for local development. Pin versions in both places.

---

## 4. Conventional Commits Mapping

| Commit message | Version bump | Example |
|----------------|-------------|---------|
| `fix: correct null check` | Patch | 1.2.3 -> 1.2.4 |
| `fix(auth): handle token expiry` | Patch | 1.2.3 -> 1.2.4 |
| `perf: optimize query` | Patch | 1.2.3 -> 1.2.4 |
| `feat: add user search` | Minor | 1.2.3 -> 1.3.0 |
| `feat(api): new endpoint` | Minor | 1.2.3 -> 1.3.0 |
| `feat!: redesign auth flow` | Major | 1.2.3 -> 2.0.0 |
| `fix!: change return type` | Major | 1.2.3 -> 2.0.0 |
| `feat: add X`<br><br>`BREAKING CHANGE: removes Y` | Major | 1.2.3 -> 2.0.0 |
| `docs: update readme` | Branch default* | depends on config |
| `chore: update deps` | Branch default* | depends on config |

*Branch default is typically Patch for main. Commits whose messages do not match any bump regex fall through to the branch's `increment` setting.

The highest bump wins when multiple commits exist since the last tag. If there is one `feat:` and ten `fix:` commits, the result is a minor bump (not patch).

---

## 5. Prerelease Version Format for PRs

Yes, GitVersion can produce PR-specific prerelease versions. Configure the `pull-request` branch:

```yaml
branches:
  pull-request:
    mode: ContinuousDelivery
    label: 'pr.{Number}'
    increment: Inherit
    regex: ^(pull-requests|pull|pr)[/-](?<Number>\d*)
    source-branches:
    - main
    - release
    - feature
```

This produces versions like: `1.3.0-pr.42.3` where:
- `1.3.0` is the computed next version
- `pr.42` is the PR number
- `.3` is the commit count / pre-release number

The default label is `PullRequest{Number}` (e.g., `1.3.0-PullRequest0042.1`). Changing to `pr.{Number}` produces the cleaner `pr.42` format.

**Note**: The exact format of the trailing number depends on the mode. In `ContinuousDelivery` mode, it is the height (number of commits since the base version). In `ContinuousDeployment` mode, every commit gets a unique pre-release number.

---

## 6. MSBuild Integration

Install the **GitVersion.MsBuild** NuGet package:

```xml
<PackageReference Include="GitVersion.MsBuild" Version="6.7.*">
  <PrivateAssets>all</PrivateAssets>
</PackageReference>
```

The `<PrivateAssets>all</PrivateAssets>` prevents it from becoming a transitive dependency of your package.

### What it sets automatically

When `UpdateVersionProperties` is `true` (default), these MSBuild properties are populated at build time:

| MSBuild Property | Value Source |
|-----------------|-------------|
| `Version` | `GitVersion_FullSemVer` |
| `VersionPrefix` | `GitVersion_MajorMinorPatch` |
| `VersionSuffix` | Pre-release label |
| `PackageVersion` | `GitVersion_FullSemVer` or `GitVersion_NuGetVersionV2` |
| `AssemblyVersion` | `GitVersion_AssemblySemVer` |
| `FileVersion` | `GitVersion_AssemblySemFileVer` |
| `InformationalVersion` | `GitVersion_InformationalVersion` |

It also generates a `GitVersionInformation` class with all version variables as static properties.

### Configuration via csproj

```xml
<PropertyGroup>
  <UpdateAssemblyInfo>true</UpdateAssemblyInfo>        <!-- default: true -->
  <UpdateVersionProperties>true</UpdateVersionProperties>  <!-- default: true -->
  <UseFullSemVerForNuGet>true</UseFullSemVerForNuGet>    <!-- SemVer 2.0 NuGet versions -->
</PropertyGroup>
```

### Important prerequisites

- Remove any existing `[assembly: AssemblyVersion("...")]` attributes from your code -- GitVersion generates them
- Since GitVersion 6.0, only `dotnet msbuild` is supported (not `msbuild.exe` from Visual Studio directly)
- Do not set `<Version>`, `<AssemblyVersion>`, or `<FileVersion>` in your csproj -- GitVersion will set them

### MSBuild vs CLI approach

You can choose between:
1. **GitVersion.MsBuild NuGet** -- version is computed at build time, embedded in assemblies automatically
2. **CLI tool + `/p:Version=`** -- version is computed in CI, passed explicitly to `dotnet build`

Option 2 gives more control in CI and avoids the MSBuild package dependency. Both are valid.

---

## 7. Path Filtering

GitVersion supports `ignore.paths` with regex patterns:

```yaml
ignore:
  paths:
    - "^docs/"              # ignore commits that only touch docs/
    - "^\.github/"          # ignore CI config changes
```

**Critical behavior**: A commit is ignored only if **all** changed files in that commit match the ignore patterns. If a commit touches both `docs/readme.md` and `src/Foo.cs`, it will NOT be ignored.

### Monorepo pattern -- include only specific paths

Use a negative lookahead to include only `src/` changes:

```yaml
ignore:
  paths:
    - "^(?!src/).*"   # ignore commits where ALL changed files are outside src/
```

Again, the commit is only ignored if every file in the commit matches the pattern. A commit touching both `docs/` and `src/` will still count.

### Alternative: handle path filtering externally

For more precise control, you can use GitHub Actions path filters on the workflow trigger and skip the version bump entirely when only non-source files change. This is often simpler and more predictable than GitVersion's path ignoring.

---

## 8. GitHub Actions Integration

### Official action: GitTools/actions

```yaml
name: Build
on: [push, pull_request]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4
        with:
          fetch-depth: 0   # REQUIRED -- full history

      - name: Install GitVersion
        uses: gittools/actions/gitversion/setup@v4.4.2
        with:
          versionSpec: '6.7.x'

      - name: Determine Version
        id: gitversion
        uses: gittools/actions/gitversion/execute@v4.4.2

      - name: Build
        run: dotnet build --configuration Release /p:Version=${{ steps.gitversion.outputs.fullSemVer }}

      - name: Pack
        run: dotnet pack --configuration Release /p:PackageVersion=${{ steps.gitversion.outputs.fullSemVer }} --no-build
```

### Available outputs

Reference via `${{ steps.gitversion.outputs.<name> }}`:

| Output | Example | Description |
|--------|---------|-------------|
| `major` | `1` | Major version |
| `minor` | `3` | Minor version |
| `patch` | `0` | Patch version |
| `majorMinorPatch` | `1.3.0` | Without prerelease |
| `semVer` | `1.3.0-pr.42.3` | SemVer string |
| `fullSemVer` | `1.3.0-pr.42.3` | Full SemVer |
| `branchName` | `main` | Current branch |
| `sha` | `abc1234` | Commit SHA |
| `nuGetVersionV2` | `1.3.0-pr0042.3` | NuGet-compatible version |

Outputs are also available as environment variables: `${{ env.fullSemVer }}` or `${{ env.GitVersion_FullSemVer }}`.

### Two-step pattern is mandatory

You **must** run `setup` before `execute`. If you skip setup, execute fails with: `Unable to locate executable file: 'dotnet-gitversion'`.

---

## 9. fetch-depth: 0

**Yes, GitVersion requires full git history.** The documentation explicitly states:

> "You must disable shallow fetch by setting `fetch-depth: 0` in your `checkout` step; without it, GitHub Actions might perform a shallow clone, which will cause GitVersion to display an error message."

GitVersion walks the commit graph from the current HEAD back to the last tag to compute the version. A shallow clone truncates this history.

### Workarounds

- **fetch-depth: 0** is the straightforward approach and works for most repos
- For very large repos, you can fetch just enough history: `git fetch --deepen=N` until GitVersion finds a tag. But this is fragile and not recommended.
- Tagging frequently (every release) reduces the amount of history GitVersion needs to walk, improving performance even with full clones

### Performance impact

For most repos, `fetch-depth: 0` adds only a few seconds to checkout. It becomes a concern only for repos with extremely large histories (100k+ commits) or large binary objects. In those cases, use `--filter=blob:none` for a treeless clone that still has full commit history.

---

## 10. Tag Format

Default `tag-prefix` regex: `[vV]?`

This means GitVersion accepts both:
- `v1.0.0` (common convention)
- `1.0.0` (bare version)

Both `v` and `V` prefixes are stripped before parsing the version number.

To enforce a specific format:
```yaml
tag-prefix: 'v'         # only accept v-prefixed tags
tag-prefix: ''           # only accept bare version tags
tag-prefix: '[vV]?'      # accept both (default)
```

When GitVersion **creates** tags (it does not create tags by default -- that is your CI's job), the tag format is your responsibility. Convention: use `v1.0.0` for consistency with the broader GitHub ecosystem.

---

## 11. Known Gotchas

### 1. Conventional Commits is regex-based, not a native mode

GitVersion does not parse Conventional Commits natively. It matches commit message first lines against regexes. This means:
- Multi-line `BREAKING CHANGE:` footers require the regex to match across newlines
- The default Conventional Commits regex in the docs did not support `BREAKING-CHANGE` (with hyphen) until issue [#4258](https://github.com/GitTools/GitVersion/issues/4258) -- use `BREAKING[ -]CHANGE` in your regex
- Scope parsing (e.g., `feat(api):`) depends on the regex allowing the right characters

### 2. Non-matching commits still bump the version

If a commit like `docs: update readme` does not match any bump regex, it does NOT get zero-bumped. Instead, it falls through to the branch's `increment` setting (typically `Patch`). This means documentation-only commits still produce patch bumps unless you configure `no-bump-message`.

### 3. Mode confusion: MainLine vs workflow strategies

In GitVersion 6.x, the old `mode: MainLine` is replaced by `strategies` configuration. The docs and blog posts from the 5.x era recommend `mode: MainLine` but this works differently in 6.x. Use `workflow: GitHubFlow/v1` and configure strategies explicitly for 6.x.

### 4. GitHubFlow not bumping versions ([#4165](https://github.com/GitTools/GitVersion/issues/4165))

Users report that commit message bumps do not work on feature branches with `GitHubFlow/v1` workflow. The increment only takes effect when merged to main. This is by design -- feature branches inherit the increment but the final version is calculated on main after merge.

### 5. Wrong number bumped ([#3678](https://github.com/GitTools/GitVersion/issues/3678))

A reported bug where `feat:` commits produced patch bumps instead of minor bumps. Root cause was typically misconfigured regex (escaping issues in YAML) or stale tags confusing the version walk.

### 6. YAML escaping of regexes

The Conventional Commits regexes contain backslashes and special characters. YAML quoting is tricky:
- Use double quotes with double-escaped backslashes: `"\\("` to match literal `(`
- Or use single quotes with single backslashes: `'\('`
- Test your config with `dotnet gitversion /showconfig` to verify the effective regexes

### 7. `fetch-depth: 0` is non-negotiable

Shallow clones silently produce wrong versions or fail outright. Every CI job that computes a version needs full history.

### 8. MSBuild package conflicts with CLI-computed versions

If you use both `GitVersion.MsBuild` NuGet package AND pass `/p:Version=` on the command line, you get conflicts. Pick one approach:
- MSBuild package (auto-computes at build time)
- CLI tool + `/p:Version=` (explicit, better for CI)

### 9. Pre-release weight and version ordering

NuGet and SemVer sort pre-release versions lexicographically. `pr.9` sorts after `pr.42` because `9` > `4`. GitVersion uses `pre-release-weight` to handle numeric ordering, but NuGet feeds may still show unexpected ordering for pre-release packages.

### 10. Assembly versioning for strong-named assemblies

If you use strong naming, be cautious with `assembly-versioning-scheme: MajorMinorPatch` -- changing the `AssemblyVersion` on every patch breaks binding redirects. Consider `MajorMinor` or `Major` for the assembly version while keeping `FileVersion` at full `MajorMinorPatch`.

---

## 12. Recommended Configuration for YAF

Based on the project's needs (NuGet library, GitHub-hosted, Conventional Commits, PR previews):

```yaml
# GitVersion.yml
workflow: GitHubFlow/v1

# Conventional Commits
major-version-bump-message: "^(build|chore|ci|docs|feat|fix|perf|refactor|revert|style|test)(\\([\\w\\s-,/\\\\]*\\))?(!:|:.*\\n\\n((.+\\n)+\\n)?BREAKING[ -]CHANGE:\\s.+)"
minor-version-bump-message: "^(feat)(\\([\\w\\s-,/\\\\]*\\))?:"
patch-version-bump-message: "^(fix|perf)(\\([\\w\\s-,/\\\\]*\\))?:"
no-bump-message: "^(docs|style|ci|build|chore|test|refactor)(\\([\\w\\s-,/\\\\]*\\))?:"

assembly-versioning-scheme: MajorMinor
assembly-file-versioning-scheme: MajorMinorPatch
tag-prefix: '[vV]?'

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

Key design choices:
- `no-bump-message` prevents docs/chore/ci/test commits from producing unnecessary patch bumps
- `assembly-versioning-scheme: MajorMinor` avoids binding redirect churn for consumers
- PR label `pr.{Number}` produces clean `1.2.0-pr.42.1` versions
- `tag-prefix: '[vV]?'` accepts both `v1.0.0` and `1.0.0` tags

---

## References

- [GitVersion configuration reference](https://gitversion.net/docs/reference/configuration)
- [Version increments and Conventional Commits](https://gitversion.net/docs/reference/version-increments)
- [GitHub Actions integration](https://gitversion.net/docs/reference/build-servers/github-actions)
- [GitTools/actions repository](https://github.com/GitTools/actions)
- [MSBuild integration](https://gitversion.net/docs/usage/msbuild)
- [CLI installation](https://gitversion.net/docs/usage/cli/installation)
- [Issue #4258 -- BREAKING-CHANGE hyphen support](https://github.com/GitTools/GitVersion/issues/4258)
- [Issue #4165 -- GitHubFlow not bumping](https://github.com/GitTools/GitVersion/issues/4165)
- [Issue #3678 -- Wrong number bumped](https://github.com/GitTools/GitVersion/issues/3678)
