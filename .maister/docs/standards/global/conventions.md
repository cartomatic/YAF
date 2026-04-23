## Development Conventions

### Predictable Structure
Organize files and directories in a logical, navigable layout.

### Up-to-Date Documentation
Keep README files current with setup steps, architecture overview, and contribution guidelines.

### Clean Version Control
Write clear commit messages, use feature branches, and add meaningful descriptions to pull requests.

### Environment Variables
Store configuration in environment variables; never commit secrets or API keys.

### Minimal Dependencies
Keep dependencies lean and up-to-date; document why major ones are included.

### Consistent Reviews
Follow a defined code review process with clear expectations for reviewers and authors.

### Testing Standards
Define required test coverage (unit, integration, etc.) before merging.

### Feature Flags
Use flags for incomplete features instead of long-lived branches.

### Build What's Needed
Avoid speculative code and "just in case" additions (see minimal-implementation.md).

### Conventional Commits for PRs
PR titles must follow `<type>(<scope>): <description>`. Common types: `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`, `revert`. Enforced in CI.

```
feat(domain): add Result<T> monad for error handling
fix(api): correct null reference in user endpoint
docs: update architecture decision records
```

### Squash and Merge
All PRs are squash-merged for clean linear history. Delete remote and local branches after merge.

### Keep a Changelog
Maintain `CHANGELOG.md` in [Keep a Changelog](https://keepachangelog.com/) format, grouped by date (`## YYYY-MM-DD`). Update when creating PRs and when committing on branches with open PRs.

### Timestamped Doc Filenames
All documentation artifacts use the format `yyyymmdd-hhmm-short-description.md` with dashes as word separators.

```
20260423-1430-result-pattern-design.md
20260415-0900-memento-analysis.md
```

### Branch Naming
Branches follow `{type}/{short-description}` matching Conventional Commits types.

```
feat/result-pattern
fix/entity-null-check
docs/adr-memento-pattern
```

### GitVersion Semantic Versioning
Auto-versioning from commit types: `feat` bumps minor, `fix` bumps patch, `BREAKING CHANGE` bumps major. Auto-tag on main merge.

### Code Formatting CI Gate
`dotnet format --verify-no-changes` enforced in CI pipeline. All code must pass formatting checks before merge.

### No Human-Written Code
All code must be AI-generated. Humans provide documentation, plans, decisions, tools, and skills only. This is a core project constraint.
