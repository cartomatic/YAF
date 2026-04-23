# Contributing Conventions

Workflow and quality conventions for all contributors (human and AI).

## Git Workflow

### Branching & Merging

- **Squash and merge** all PRs for a clean linear history on `main`
- **Delete branches** (remote and local) after merging
- PR titles follow [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/): `<type>(<scope>): <description>`

### Changelog

- Maintain `CHANGELOG.md` following [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) with entries grouped by date (`## YYYY-MM-DD`)
- Update the changelog when creating a PR and when committing on a branch with an open PR

## Code Quality

### XML Documentation

All public API members must have XML doc comments. This is enforced by `CS1591` (warning-as-error via `.editorconfig`). No exceptions — if it's public, it's documented.

### Property Conventions for Memento-Capable Types

Domain entity properties that participate in memento Snapshot/Hydrate must use `{ get; private set; }`. Positional record parameters and `init` accessors are not compatible with `RuntimeHelpers.GetUninitializedObject` used during restoration.

## Documentation Artifacts

### Filename Format

All documentation artifacts use a timestamped filename: `yyyymmdd-hhmm-short-description.md` (dashes as word separators).

### Directory Structure

Each Claude Code plugin stores its artifacts in a dedicated dotfile directory at the repo root. Project-level docs remain in `docs/`.

| Type | Location | Owner |
|------|----------|-------|
| Plans | `.ce/plans/` | compound-engineering plugin |
| Brainstorms | `.ce/brainstorms/` | compound-engineering plugin |
| Research | `.ce/research/` | compound-engineering plugin |
| Verification & reviews | `.ce/verification/` | compound-engineering plugin |
| Solutions & learnings | `.ce/solutions/<category>/` | compound-engineering plugin |
| Maister artifacts | `.maister/` | maister plugin |
| Session diaries | `docs/diary/` | project |
| ADRs | `docs/adr/<scope>/` with `INDEX.md` | project |
| General docs | `docs/general/` | project |

Plugins must use their assigned directory and must not write artifacts to `docs/` or to each other's directories.

### Verification Artifacts

All files in `.ce/verification/` (spec audits, code reviews, pragmatic reviews) must be prefixed with the originating plan's date-name identifier so they are traceable to the feature they belong to.

Example for plan `.ce/plans/20260327-0936-feat-cross-cutting-domain-interfaces-plan.md`:
- `.ce/verification/20260327-0936-feat-cross-cutting-domain-interfaces-spec-audit-v2.md`
- `.ce/verification/20260327-0936-feat-cross-cutting-domain-interfaces-code-review.md`
- `.ce/verification/20260327-0936-feat-cross-cutting-domain-interfaces-pragmatic-review-v1.md`

### ADR Location

Architecture Decision Records are stored in `docs/adr/` organized by scope:
- `docs/adr/architecture/` — cross-cutting architectural decisions
- `docs/adr/domain/` — domain layer decisions
- `docs/adr/infrastructure/` — infrastructure layer decisions
- `docs/adr/api/` — API layer decisions

Each scope folder has an `INDEX.md` listing all ADRs. The root `docs/adr/INDEX.md` links to all scope indexes.

### Solution Documents

Documented solutions go in `.ce/solutions/<category>/` with YAML frontmatter for searchability. Categories include: `design-patterns/`, `build-errors/`, `test-failures/`, `runtime-errors/`, `logic-errors/`, `performance-issues/`.
