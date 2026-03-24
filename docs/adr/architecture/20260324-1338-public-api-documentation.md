# Public API Documentation

- **Timestamp:** 2026-03-24 13:38
- **Status:** under review
- **Scope:** architecture
- **Stakeholders:** Proposed by: Claude Code, Decided by: @cartomatic

---

## Summary

All public APIs in YAF NuGet packages must have XML documentation comments (`///`). This includes public types, methods, properties, and interfaces across all packages. XML documentation enables IntelliSense for consumers, generates API reference documentation, and helps AI tools produce correct code against YAF's interfaces.

## Drivers

1. **Consumer experience** — Developers using YAF packages rely on IntelliSense. Missing XML docs mean guessing at parameter semantics, return values, and intended usage.
2. **AI-driven development** — YAF is built by AI and consumed in AI-assisted workflows. XML documentation is the primary way AI tools understand the framework's contracts.
3. **API reference generation** — XML docs feed into tools like DocFX or GitHub Pages for browsable API reference documentation.
4. **Contract clarity** — For a framework library, public interfaces ARE the product. Their documentation must be precise about expectations, constraints, and behavior.
5. **Consistency** — A uniform documentation standard across all packages ensures predictable quality.

## Options

### Documentation Standard

| Option | Assessment |
|--------|------------|
| **XML documentation comments (`///`) on all public members** | **Selected.** .NET standard. Powers IntelliSense, API reference generation, and compiler warnings for missing docs. |
| README-only documentation | Doesn't appear in IntelliSense. Disconnected from code. Goes stale. |
| Code comments only (`//`) | Not structured. Not visible in IntelliSense. Cannot generate API docs. |
| No documentation requirement | Unacceptable for a framework library. Consumers have no guidance. |

### Enforcement

| Option | Assessment |
|--------|------------|
| **Compiler warning CS1591 + CI enforcement** | **Selected.** Enable `<GenerateDocumentationFile>` and `<NoWarn>` does NOT suppress CS1591. CI fails on missing public API docs. |
| Manual review only | Inconsistent. Docs get missed over time. |

## Recommendation

Mandatory XML documentation on all public members with compiler-enforced warnings treated as errors in CI.

## Consequences

**Positive:**
- Every public type, method, and property has IntelliSense documentation
- API reference can be auto-generated from XML docs
- Compiler catches missing documentation at build time
- AI tools produce better code when consuming well-documented interfaces

**Negative:**
- Documentation adds effort to every public API change (acceptable for a framework library)
- Boilerplate risk — low-quality docs that just restate the method name (mitigated: code review catches this)

## Conclusion

### Requirements

1. **All public types** — classes, interfaces, records, structs, enums — must have `<summary>` documentation
2. **All public methods** — must have `<summary>`, `<param>` for each parameter, `<returns>` for non-void returns, and `<exception>` for documented exceptions
3. **All public properties** — must have `<summary>` documentation
4. **Generic type parameters** — must have `<typeparam>` documentation
5. **Interfaces** — documentation on the interface; implementations inherit but may add implementation-specific remarks via `<remarks>`

### Quality Standards

- **`<summary>`** — describes what the member is/does, not how. One sentence preferred.
- **`<param>`** — describes the parameter's role and any constraints (e.g., "Must not be empty").
- **`<returns>`** — describes what the return value represents, including failure cases for `Result<T>`.
- **`<remarks>`** — optional, for usage examples, edge cases, or design rationale.
- **`<example>`** — optional but encouraged for non-obvious usage patterns on key interfaces.
- **No filler** — `/// <summary>Gets the name.</summary>` on a `Name` property adds no value. Describe what the name represents in context.

### Build Configuration

```xml
<!-- Directory.Build.props — applies to all src/ projects -->
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
</PropertyGroup>
```

CS1591 (missing XML comment) is treated as an error in all source projects. Test projects and examples are excluded.

### Scope

| Project | XML Docs Required |
|---------|------------------|
| `src/Yaf.Domain` | Yes — all public members |
| `src/Yaf.Application` | Yes — all public members |
| `src/Yaf.Infrastructure` | Yes — all public members |
| `src/Yaf.Api` | Yes — all public members |
| `src/Yaf.ServiceDefaults` | Yes — all public members |
| `src/Yaf.Application.Wolverine` | Yes — all public members |
| `tests/*` | No |
| `examples/*` | No |

## More Information

- [ADR: Development Workflow](20260324-1017-development-workflow.md) — CI enforcement
- [ADR: Solution Structure](20260324-0953-solution-structure-and-package-layering.md) — package layout
- [Microsoft: XML documentation comments](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/)
