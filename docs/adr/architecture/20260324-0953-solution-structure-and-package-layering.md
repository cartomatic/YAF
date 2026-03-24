# Solution Structure and Package Layering

- **Timestamp:** 2026-03-24 09:53
- **Status:** under review
- **Scope:** architecture
- **Stakeholders:** Proposed by: Claude Code, Decided by: @cartomatic

---

## Summary

YAF is delivered as a set of NuGet packages that mirror the Clean Architecture layers. Each package corresponds to exactly one architectural layer, has explicit dependencies, and brings only the third-party libraries required for its responsibility. Adapter packages (e.g., Wolverine, future MediatR) are separate, opt-in packages that bridge YAF abstractions to specific implementations.

## Drivers

1. **Clean Architecture enforcement** — Package boundaries physically enforce the dependency rule. A project that references only Yaf.Domain cannot accidentally depend on EF Core.
2. **Consumer flexibility** — Consumers should be able to reference only the layers they need. A shared library project may need only Yaf.Domain for base types.
3. **Minimal transitive dependencies** — Each package should bring only what it owns. Consumers don't inherit libraries they didn't ask for.
4. **Adapter swappability** — CQRS implementation, messaging, and future concerns should be pluggable without changing core packages.
5. **Monorepo development** — All packages live in a single repository for coordinated development, versioning, and testing.

## Options

### Option A: One Package per Layer + Separate Adapters

```
Yaf.Domain              → zero dependencies (pure .NET)
Yaf.Application         → depends on Yaf.Domain
Yaf.Infrastructure      → depends on Yaf.Domain + Yaf.Application
Yaf.Api                 → depends on Yaf.Application
Yaf.ServiceDefaults     → standalone (Aspire service defaults)

Yaf.Application.Wolverine → depends on Yaf.Application (adapter)
```

Each layer is a single package. Adapters are separate packages under a naming convention (`Yaf.{Layer}.{Adapter}`).

**Pros:**
- Simple, clear mapping from architecture to packages
- Easy to understand and navigate
- Enforces dependency rule at the package level

**Cons:**
- Infrastructure package may grow large over time as it covers persistence, encryption, scheduling, etc.
- No way to reference just the persistence bits without getting everything

### Option B: Fine-Grained Packages per Concern

Split each layer further: `Yaf.Infrastructure.EfCore`, `Yaf.Infrastructure.Encryption`, `Yaf.Infrastructure.Scheduling`, etc.

**Pros:**
- Maximum consumer flexibility — pick only what you need
- Smaller individual packages

**Cons:**
- Package proliferation — many small packages to version, test, and maintain
- Dependency graph becomes complex
- Premature granularity before we know which concerns consumers want to mix and match
- Harder for AI tools to navigate

### Option C: Single Monolithic Package

Ship everything as one `Yaf` package.

**Pros:**
- Simplest possible consumer experience — one package reference
- No versioning coordination issues

**Cons:**
- Violates the dependency rule — consumers get everything, including EF Core, even if they only want domain types
- Cannot use Yaf.Domain in a shared library without pulling in the entire framework
- No flexibility in adapter choice

## Recommendation

**Option A: One Package per Layer + Separate Adapters.** This strikes the right balance between architectural clarity and practical simplicity. If Infrastructure grows too large later, it can be split (Option B) — but starting granular creates unnecessary complexity before we understand real usage patterns.

## Consequences

**Positive:**
- Package references physically enforce the dependency rule — impossible to accidentally reference infrastructure from domain
- Consumers who need only DDD base types reference Yaf.Domain (zero dependencies)
- Adapter naming convention (`Yaf.{Layer}.{Adapter}`) is discoverable and extensible
- Monorepo enables coordinated versioning and cross-package testing
- Yaf.Api does not depend on Yaf.Infrastructure — adapter and API layer are peers, wired at the composition root

**Negative:**
- Consumers who want "everything" must add multiple package references (mitigated: `AddYaf()` extension method guides setup)
- Infrastructure is a single package even though it covers multiple concerns (acceptable initially; split later if needed)
- Monorepo requires coordinated versioning — all packages share a version number

## Conclusion

### Package Structure

| Package | Layer | Dependencies | Brings |
|---------|-------|-------------|--------|
| **Yaf.Domain** | Domain | None (pure .NET) | Entity\<TId\>, AggregateRoot\<TId\>, ValueObject, TypedId\<T\>, Enumeration\<TEnum\>, IDomainEvent, IRepository\<T\>, IUnitOfWork, Result\<T\>, IError/IDomainError, ISpecification\<T\>, IMemento\<T\>, construction patterns |
| **Yaf.Application** | Application | Yaf.Domain | ICommand/IQuery/ICommandHandler/IQueryHandler, INotification/INotificationHandler, IApplicationError, context provider interfaces, validation pipeline abstractions, sanitization pipeline, business event log interfaces |
| **Yaf.Infrastructure** | Infrastructure | Yaf.Domain, Yaf.Application | Cross-cutting infrastructure: accountability/timestamping auto-population, encryption provider, versioning snapshots, deleted object graveyard, context provider implementations, Serilog configuration |
| **Yaf.Infrastructure.Persistence** | Infrastructure | Yaf.Domain, Yaf.Application | Persistence contracts and abstractions: repository base implementations, memento mapping contracts, specification evaluator interface, UoW infrastructure |
| **Yaf.Infrastructure.Persistence.EfCore** | Infrastructure | Yaf.Infrastructure.Persistence | EF Core implementation: base YafDbContext, EF Core repository implementations, specification evaluator, memento EF Core mapping, typed ID and enumeration value converters |
| **Yaf.Infrastructure.Persistence.EfCore.PgSql** | Infrastructure | Yaf.Infrastructure.Persistence.EfCore | PostgreSQL-specific: Npgsql provider configuration, PostgreSQL-specific conventions, migration support for PostgreSQL |
| **Yaf.Api** | Presentation/Adapter | Yaf.Application | Base YafApiController, ProblemDetails middleware, global exception handler, FluentValidation pipeline integration, API versioning setup, JWT Bearer helpers, health check endpoints |
| **Yaf.ServiceDefaults** | Orchestration | None (standalone) | Aspire service defaults, OpenTelemetry, resilience policies, service discovery |

### Adapter Packages

| Package | Bridges | Dependencies |
|---------|---------|-------------|
| **Yaf.Application.Wolverine** | Yaf.Application CQRS → Wolverine dispatch | Yaf.Application, Wolverine |
| *(Future)* Yaf.Application.MediatR | Yaf.Application CQRS → MediatR dispatch | Yaf.Application, MediatR |
| *(Future)* Yaf.Infrastructure.Persistence.EfCore.SqlServer | SQL Server provider | Yaf.Infrastructure.Persistence.EfCore |
| *(Future)* Yaf.Messaging | Integration events, outbox, broker adapters | Yaf.Application |
| *(Future)* Yaf.Api.MinimalApis | Minimal API endpoint conventions | Yaf.Application |

### Database Migrator

| Package | Purpose | Dependencies |
|---------|---------|-------------|
| **Yaf.Infrastructure.Persistence.EfCore.DbMigrator** | Standalone migration runner | Yaf.Infrastructure.Persistence.EfCore |

The DbMigrator is a **separate executable project** that runs EF Core migrations as a short-lived container. It is deployed as a startup/init container that runs before the API containers start. This solves the problem of multiple API instances attempting to run migrations concurrently during scale-out:

- **Deployed as an init container** (Kubernetes init container, Docker Compose depends_on, Aspire lifecycle) — runs once, completes, then API containers start
- **No migration logic in the API** — API projects never run migrations at startup. They assume the database schema is already up to date.
- **Consumer extends** — the consumer's DbMigrator project references their DbContext and migrations. YAF provides the base runner infrastructure.
- **Idempotent** — the migrator checks the current migration state and only applies pending migrations. Safe to re-run.

### Dependency Graph

```
Yaf.Domain                                    (no dependencies — pure .NET)
  ↑
Yaf.Application                               (depends on Yaf.Domain)
  ↑                         ↑
Yaf.Infrastructure          Yaf.Api           (peers — both depend on Yaf.Application)
  ↑                                            (wired together at composition root)
Yaf.Infrastructure.Persistence                (persistence contracts)
  ↑
Yaf.Infrastructure.Persistence.EfCore         (EF Core implementation)
  ↑                         ↑
  │   Yaf.Infrastructure.Persistence.EfCore.PgSql      (PostgreSQL provider)
  │
Yaf.Infrastructure.Persistence.EfCore.DbMigrator       (migration runner)

Yaf.Application.Wolverine                     (depends on Yaf.Application — adapter)
Yaf.ServiceDefaults                           (standalone — no Yaf dependencies)
```

### Solution Layout (Monorepo)

```
YAF.sln
├── src/                                    # Core library
│   ├── Yaf.Domain/
│   ├── Yaf.Application/
│   ├── Yaf.Infrastructure/
│   ├── Yaf.Infrastructure.Persistence/
│   ├── Yaf.Infrastructure.Persistence.EfCore/
│   ├── Yaf.Infrastructure.Persistence.EfCore.PgSql/
│   ├── Yaf.Infrastructure.Persistence.EfCore.DbMigrator/
│   ├── Yaf.Api/
│   ├── Yaf.ServiceDefaults/
│   └── Yaf.Application.Wolverine/
├── tests/                                  # Core library tests
│   ├── Yaf.Domain.Tests/
│   ├── Yaf.Application.Tests/
│   ├── Yaf.Infrastructure.Tests/
│   ├── Yaf.Infrastructure.Persistence.EfCore.Tests/
│   ├── Yaf.Api.Tests/
│   └── Yaf.Application.Wolverine.Tests/
├── examples/                               # Example applications
│   └── ServiceX/                           # Each example is a self-contained service
│       ├── src/                            # Service source code
│       │   ├── ServiceX.Domain/
│       │   ├── ServiceX.Application/
│       │   ├── ServiceX.Infrastructure/
│       │   └── ServiceX.Api/
│       ├── tests/                          # Service tests
│       │   ├── ServiceX.Domain.Tests/
│       │   ├── ServiceX.Application.Tests/
│       │   ├── ServiceX.Infrastructure.Tests/
│       │   └── ServiceX.Api.Tests/
│       └── docs/                           # Service-specific documentation
├── docs/                                   # YAF library documentation
│   ├── adr/
│   ├── brainstorms/
│   ├── research/
│   └── diary/
└── build/                                  (CI/CD, Docker, scripts)
```

### Example Applications

Example applications live in-repo under `examples/` to enable coordinated development and serve as the basis for documentation. Each example is a **self-contained service** with its own `src/`, `tests/`, and `docs/` subfolders — mimicking the structure a real consumer would use when building an application with YAF. This makes examples realistic and self-explanatory.

**Structure per example:**
- `examples/ServiceX/src/` — service source code, layered the same way a consumer would (Domain, Application, Infrastructure, Api)
- `examples/ServiceX/tests/` — full test suite for the service (unit, integration, API tests)
- `examples/ServiceX/docs/` — service-specific documentation (setup, usage, domain explanation)

**Referencing strategy:** Initially, examples use **project references** to the core `src/` packages for fast iteration. Once the library is mature enough to provide the required building blocks, examples will switch to **NuGet package references** to demonstrate the real consumer experience.

**Multiple examples:** The `examples/` folder can contain multiple services demonstrating different aspects of YAF (e.g., a simple CRUD service, a multi-tenant service, a service with event-driven workflows). Each is independent.

### Key Structural Rules

1. **Yaf.Api does NOT depend on Yaf.Infrastructure** — they are peers. The consuming application's composition root (Program.cs) wires infrastructure implementations to application interfaces.
2. **Yaf.Domain has zero NuGet dependencies** — only `net10.0` target framework.
3. **Adapter packages follow the naming convention** `Yaf.{Layer}.{Implementation}`.
4. **All packages share a single version number**, managed from the repository root.

## More Information

- [ADR: Core Architecture Style](20260324-0921-core-architecture-style.md) — layering rationale
- [ADR: Technology Stack](20260324-0948-technology-stack.md) — library choices per layer
- [YAF Library Design Brainstorm](../../brainstorms/20260322-1755-yaf-library-design-brainstorm.md) — original package design
