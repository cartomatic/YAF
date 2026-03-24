# Warnings as Errors

- **Timestamp:** 2026-03-24 13:43
- **Status:** under review
- **Scope:** architecture
- **Stakeholders:** Proposed by: Claude Code, Decided by: @cartomatic

---

## Summary

All C# compiler warnings are treated as errors across the entire solution. Code with unresolved warnings does not compile. This is enforced via `Directory.Build.props` and applies to all source projects, test projects, and example projects. No warnings are suppressed globally — each suppression must be justified and applied at the narrowest possible scope.

## Drivers

1. **AI-generated code quality** — All code is AI-generated. Warnings are the compiler's feedback that something may be wrong. Ignoring them lets subtle issues accumulate.
2. **Zero tolerance for drift** — Warnings that are "just warnings" get ignored. They pile up until the warning list is so long that new, meaningful warnings are lost in the noise.
3. **Clean signal** — A clean build with zero warnings means every new warning is immediately visible and actionable.
4. **Consistent enforcement** — If warnings are optional, different contributors (human or AI) handle them inconsistently. Treating them as errors removes ambiguity.

## Options

| Option | Assessment |
|--------|------------|
| **`TreatWarningsAsErrors` globally** | **Selected.** All warnings are errors. Code does not compile with warnings. Applied via `Directory.Build.props` to the entire solution. |
| Warnings as errors in CI only | Developers see warnings locally but can ignore them. Only CI catches them. Delays feedback and allows warning debt to build between pushes. |
| Selective warnings as errors | Requires maintaining a list of promoted warnings. New warning codes are not covered until manually added. Gaps are inevitable. |
| No enforcement | Warnings accumulate until they're meaningless. |

## Recommendation

`TreatWarningsAsErrors` globally via `Directory.Build.props`. No exceptions by default.

## Consequences

**Positive:**
- Zero warning noise — every build is clean or doesn't compile
- New warnings from SDK/analyzer updates are immediately visible
- Nullable reference type warnings are enforced (important for correctness)
- AI tools produce cleaner code because they must resolve all warnings
- CI and local builds behave identically

**Negative:**
- Occasionally requires `#pragma warning disable` for justified cases (mitigated: must include a comment explaining why)
- SDK or analyzer upgrades that introduce new warnings may break the build (mitigated: this is the desired behavior — forces resolution)
- Some third-party package warnings may surface (mitigated: `NoWarn` in specific project files for package-originated warnings, not globally)

## Conclusion

### Build Configuration

```xml
<!-- Directory.Build.props — applies to ALL projects in the solution -->
<PropertyGroup>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
</PropertyGroup>
```

### Scope

| Project Type | Warnings as Errors |
|-------------|-------------------|
| `src/*` | Yes |
| `tests/*` | Yes |
| `examples/*` | Yes |

### Suppression Rules

When a warning genuinely cannot or should not be resolved, suppress it at the **narrowest possible scope**:

1. **Prefer inline `#pragma`** with a comment over project-level `<NoWarn>`
2. **Never suppress globally** in `Directory.Build.props` — each project owns its suppressions
3. **Every suppression must include a comment** explaining why the warning is not applicable
4. **Review suppressions periodically** — a suppressed warning may become resolvable with code changes

```csharp
#pragma warning disable CS8618 // Non-nullable field not initialized — set by EF Core via memento
private string _name;
#pragma warning restore CS8618
```

### Nullable Reference Types

`<Nullable>enable</Nullable>` is set globally. Nullable warnings (CS8600–CS8655) are errors. This enforces null safety across the codebase — particularly important for:
- Domain objects with required properties
- Result pattern (accessing `.Value` on a failure result)
- Context providers that may return null for unauthenticated requests

## More Information

- [ADR: Public API Documentation](20260324-1338-public-api-documentation.md) — CS1591 (missing XML docs) is also an error
- [ADR: Development Workflow](20260324-1017-development-workflow.md) — CI enforcement
- [ADR: Solution Structure](20260324-0953-solution-structure-and-package-layering.md) — Directory.Build.props location
