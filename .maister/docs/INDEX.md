# Documentation Index

**IMPORTANT**: Read this file at the beginning of any development task to understand available documentation and standards.

## Quick Reference

### Project Documentation
Project-level documentation covering vision, goals, architecture, and technology choices.

### Technical Standards
Coding standards, conventions, and best practices organized by domain.

---

## Project Documentation

Located in `.maister/docs/project/`

### Vision (`project/vision.md`)
YAF project purpose, current state, 6-12 month goals, evolution history, and differentiators. Covers the AI-driven development experiment, DDD building blocks scope, and quality enforcement approach.

### Roadmap (`project/roadmap.md`)
Development priorities organized by urgency: infrastructure layer, EF Core adapter, ASP.NET Core API layer (high priority); NuGet packaging, observability, domain event publishing (medium priority); ADR finalization, namespace organization, test naming (technical debt). Includes effort estimates.

### Tech Stack (`project/tech-stack.md`)
Technology choices and rationale: C# 10+ on .NET 10, xUnit/AwesomeAssertions for testing, coverlet for coverage, dotnet CLI build commands, Directory.Build.props shared settings, EditorConfig enforcement (90+ rules), GitVersion for semantic versioning, GitHub Actions CI/CD.

### Architecture (`project/architecture.md`)
DDD + Clean Architecture pattern with zero-dependency domain layer. Documents Yaf.Domain components (TypedId, Entity, AggregateRoot, ValueObject, Error, Result), cross-cutting interfaces (IAccountable, IHasTimestamps, IHasSoftDelete, IHasVersionInfo, ITenant), memento pattern, and planned infrastructure/API layers.

---

## Technical Standards

### Global Standards

Located in `.maister/docs/standards/global/`

#### Coding Style (`standards/global/coding-style.md`)
Naming consistency, automatic formatting, descriptive names, focused functions, uniform indentation, no dead code, DRY principle, and Clean Architecture dependency rule (domain zero deps, strict inward dependencies).

#### Commenting (`standards/global/commenting.md`)
Self-documenting code philosophy, sparse commenting only when logic is not self-evident, and avoiding change-log comments in code.

#### Development Conventions (`standards/global/conventions.md`)
Conventional Commits for PR titles, squash-and-merge workflow, Keep a Changelog format, timestamped doc filenames, branch naming (`{type}/{description}`), GitVersion semantic versioning, `dotnet format` CI gate, no human-written code constraint, predictable project structure, environment variable configuration, and minimal dependencies.

#### Error Handling (`standards/global/error-handling.md`)
Clear user messages, fail-fast validation, typed exceptions, centralized handling, graceful degradation, warnings-as-errors in Directory.Build.props, narrowest-scope warning suppression with justification, and nullable reference types (`is null`/`is not null`, no unqualified null-forgiving operator).

#### Minimal Implementation (`standards/global/minimal-implementation.md`)
Build only what is needed, clear purpose for every method, removing unused exploration artifacts, no future stubs, and no speculative abstractions.

#### Validation (`standards/global/validation.md`)
Server-side validation as primary, client-side for immediate feedback, early input validation, specific field-level error messages, and allowlists over blocklists.

### Backend Standards

Located in `.maister/docs/standards/backend/`

#### API Design (`standards/backend/api.md`)
RESTful principles, consistent naming, versioning, plural nouns, limited nesting, proper status codes, Clean Architecture layers (`Yaf.{Layer}.{Implementation}` adapter packages), CQRS abstractions (YAF-owned ICommand/IQuery, mediator-agnostic), and sanitization-before-validation pipeline ordering.

#### C# Conventions (`standards/backend/csharp-conventions.md`)
Microsoft naming conventions (_camelCase private fields, PascalCase public), Allman brace style, file-scoped namespaces, PascalCase file naming (generics with braces), pattern matching over casts, guard clauses (ThrowIfNull/ThrowIfNullOrWhiteSpace), records for value semantics, expression-bodied members, C# 12 collection expressions, string.Empty defaults, nullable reference types (default!, ? annotations), read-write interface separation, and semicolon-body marker interfaces.

#### Domain Patterns (`standards/backend/domain-patterns.md`)
Entity encapsulation (no public constructors/setters), memento property accessors (`{ get; private set; }`), Result pattern for business errors, strongly-typed IDs (TypedId with Guid), IMemento snapshot/restore, three-level validation (domain/application/API), static factory methods returning Result<T>, application-generated Guid IDs, and CRTP for memento restore methods.

#### Database Migrations (`standards/backend/migrations.md`)
Reversible migrations, small focused changes, zero-downtime awareness, separate schema and data migrations, and careful indexing.

#### Models (`standards/backend/models.md`)
Clear naming conventions, timestamps for auditing, database-level constraints, appropriate data types, indexed foreign keys, multi-layer validation, XML documentation required (CS1591 as error, tests exempt, `<inheritdoc />` for implementations), and comprehensive XML docs (`<summary>` for what not how, `<remarks>` for rationale, `<see cref>` for cross-references).

#### Database Queries (`standards/backend/queries.md`)
Parameterized queries, N+1 avoidance, selecting only needed columns, strategic indexing, and transaction usage.

### Testing Standards

Located in `.maister/docs/standards/testing/`

#### Test Writing (`standards/testing/test-writing.md`)
xUnit + AwesomeAssertions + TestContainers stack, `Method_Condition_ExpectedBehavior` naming, one test project per source project, real databases (no in-memory EF/SQLite), minimal mocking (external boundaries only), memento round-trip tests, implicit AAA pattern, lambda-based exception assertions, test helper types as nested classes, CS1591 exempt in test projects, and shared TestContainers instances.

### Frontend Standards

*Not initialized for this project. If you need frontend standards, you can:*
- *Add them manually using the docs-manager skill*
- *Run `/maister:standards-discover --scope=frontend` to auto-discover*

---

## How to Use This Documentation

1. **Start Here**: Always read this INDEX.md first to understand what documentation exists
2. **Project Context**: Read relevant project documentation before starting work
3. **Standards**: Reference appropriate standards when writing code
4. **Keep Updated**: Update documentation when making significant changes
5. **Customize**: Adapt all documentation to your project's specific needs

## Updating Documentation

- Project documentation should be updated when goals, tech stack, or architecture changes
- Technical standards should be updated when team conventions evolve
- Always update INDEX.md when adding, removing, or significantly changing documentation
