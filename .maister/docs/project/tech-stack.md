# Technology Stack

## Overview
This document describes the technology choices and rationale for YAF (Yet Another Framework).

## Languages

### C# (10+)
- **Usage**: 100% of codebase
- **Rationale**: Modern, type-safe language with strong DDD ecosystem; .NET 10 provides latest language features
- **Key Features Used**: File-scoped namespaces, records (ValueObject), pattern matching, switch expressions, nullable reference types, readonly structs (Result), implicit conversions, CallerMemberName attributes

## Frameworks

### Backend
- **.NET 10.0** (preview) — Target runtime for the library
- **No API framework yet** — ASP.NET Core planned for API layer

### Testing
- **xUnit 2.9.3** — Test framework
- **AwesomeAssertions 9.4.0** — Fluent assertion library
- **coverlet 6.0.4** — Code coverage collection (XPlat format)
- **xunit.runner.visualstudio 3.1.4** — Test runner integration

## Database
Not yet implemented. EF Core planned per architecture decisions.

## Build Tools & Package Management

### NuGet
- **Package management**: Implicit via .csproj `<PackageReference>` elements
- **No central package management** (single library project, not needed yet)

### dotnet CLI
- **Build**: `dotnet build src/YAF.slnx`
- **Test**: `dotnet test src/YAF.slnx`
- **Format**: `dotnet format src/YAF.slnx --verify-no-changes`

### Solution Format
- **YAF.slnx** — Modern XML-based solution format

### Shared Build Configuration
- **Directory.Build.props** — Centralized build settings:
  - `TreatWarningsAsErrors = true`
  - `EnforceCodeStyleInBuild = true`
  - `GenerateDocumentationFile = true`
  - `Nullable = enable`
  - `ImplicitUsings = enable`

## Infrastructure

### CI/CD
- **GitHub Actions** (`.github/workflows/pr.yml`)
  - PR title validation (Conventional Commits)
  - Build & test with coverage
  - Semantic version tagging on main

### Version Management
- **GitVersion 6.x** — Semantic versioning from git history
  - GitHub Flow workflow
  - Convention: `feat:` → minor, `fix:` → patch, `BREAKING CHANGE` → major
  - Current version: 0.1.0

## Development Tools

### Linting & Formatting
- **EditorConfig** — 90+ rules enforced at build time
  - Interface prefix (I) — enforced as error
  - Private field underscore (_) — enforced as error
  - File-scoped namespaces — enforced as warning
  - Allman brace style
- **dotnet format** — Verified in CI/CD (no changes allowed)

### Code Analysis
- **Built-in C# analyzers** — Enabled via EditorConfig
- **CS1591** — Missing XML docs treated as error for public API

### Coverage Reporting
- **ReportGenerator** — HTML, text summary, GitHub markdown, Cobertura formats
- **Dynamic badge** — Coverage percentage tracked via GitHub Gist

## Key Dependencies
| Package | Version | Purpose |
|---------|---------|---------|
| xunit | 2.9.3 | Test framework |
| AwesomeAssertions | 9.4.0 | Fluent assertions |
| coverlet.collector | 6.0.4 | Code coverage |
| Microsoft.NET.Test.Sdk | 17.14.1 | Test SDK |

---
*Last Updated*: 2026-04-23
*Auto-detected*: All entries based on codebase analysis (csproj files, editorconfig, CI workflow, Directory.Build.props)
