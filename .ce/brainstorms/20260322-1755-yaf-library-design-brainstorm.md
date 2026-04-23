# YAF Library Design Brainstorm

**Date:** 2026-03-22
**Status:** Draft
**Participants:** Human (architect/decision-maker), Claude Code (research & facilitation)

## What We're Building

A .NET 10 backbone library for rapid web API development. YAF provides opinionated defaults for the most common cross-cutting concerns while allowing consumers to swap implementations through clean abstractions.

**Target consumer:** Developer starting a new business API who wants Clean Architecture scaffolding, DDD base types, CQRS, and production-ready middleware without wiring it all up from scratch.

## Why This Approach

**Layered: opinionated core + optional modules.** A thin opinionated core enforces project structure and DI conventions. Optional modules bring their own conventions for specific concerns (persistence, CQRS adapters, messaging). This balances fast setup with flexibility — consumers aren't locked into choices they disagree with.

## Package Structure

Mirrors Clean Architecture layers directly:

| Package | Layer | Contents |
|---------|-------|----------|
| **Yaf.Domain** | Domain | Entity\<TId\>, AggregateRoot\<TId\>, ValueObject, IDomainEvent, IRepository\<T\>, IUnitOfWork |
| **Yaf.Application** | Application | ICommand/IQuery/ICommandHandler/IQueryHandler (with IResult), INotification/INotificationHandler, validation abstractions, application service interfaces |
| **Yaf.Infrastructure** | Infrastructure | Base DbContext (audit fields, EF Core conventions), repository implementations, Serilog + OpenTelemetry config, configuration helpers |
| **Yaf.Api** | Presentation | Base ApiController (ProblemDetails, error handling), health/liveness probes, JWT Bearer auth scaffolding, API versioning, FluentValidation integration, Aspire service defaults |

### Package Dependency Graph

```
Yaf.Domain              (no dependencies — pure .NET)
  ↑
Yaf.Application         (depends on Yaf.Domain)
  ↑
Yaf.Infrastructure      (depends on Yaf.Domain + Yaf.Application; brings EF Core)
  ↑
Yaf.Api                 (depends on Yaf.Application; brings ASP.NET Core, Serilog, FluentValidation, Asp.Versioning)

Yaf.Application.Wolverine (depends on Yaf.Application; brings Wolverine)
Yaf.ServiceDefaults      (standalone; brings Aspire service defaults, OpenTelemetry)
```

A consumer referencing only `Yaf.Domain` gets zero third-party dependencies. Each layer adds only its own dependencies.

### Adapter Packages (Separate)

| Package | Purpose |
|---------|---------|
| **Yaf.Application.Wolverine** | Wires Yaf.Application CQRS abstractions to Wolverine. First adapter shipped. |
| *(Future)* Yaf.Application.MediatR | MediatR adapter for CQRS (if consumers need it) |
| *(Future)* Yaf.Messaging | Integration events, outbox pattern, broker adapters |
| *(Future)* Yaf.Api.MinimalApis | Minimal API endpoint conventions, filters, result extensions |

## Key Decisions

### 1. CQRS: Own the abstractions, adapt to implementations

YAF defines its own `ICommand<TResult>`, `IQuery<TResult>`, `ICommandHandler<TCommand, TResult>`, `IQueryHandler<TQuery, TResult>`, `INotification`, and `INotificationHandler<TNotification>`. These are mediator-agnostic.

Concrete adapters (starting with Wolverine) bridge YAF's interfaces to the mediator library. This lets consumers swap Wolverine for MediatR (or anything else) without touching application code.

**Rationale:** Wolverine is MIT-licensed, convention-based, and combines mediator + messaging + outbox in one package. MediatR going commercial makes it a less attractive default. Owning the abstractions future-proofs the framework regardless.

### 2. Controllers first, Minimal APIs later

The initial release targets Controllers with a base `YafApiController` providing:
- Automatic ProblemDetails responses
- Consistent error handling
- Model state integration

Minimal API support (endpoint filters, result extensions) will come as a separate `Yaf.Api.MinimalApis` module.

**Rationale:** Controllers are still widely used in enterprise .NET. Minimal APIs are growing but the ecosystem (especially tooling and testing patterns) is still maturing.

### 3. Persistence: Repository + EF Core, no automatic domain event dispatch

`Yaf.Domain` defines `IRepository<T> where T : AggregateRoot<TId>` and `IUnitOfWork`. `Yaf.Infrastructure` provides EF Core implementations with a base `YafDbContext` that handles audit fields and EF conventions.

Domain event dispatching on `SaveChanges` is explicitly deferred — events will be handled through an explicit mechanism when the design is clearer.

**Rationale:** Implicit event dispatch on save is convenient but creates hidden side effects that are hard to reason about and test. Better to add it deliberately later.

### 4. Core cross-cutting concerns included in Yaf.Api

Out of the box, `AddYaf()` gives you:
- **Structured logging** — Serilog with OpenTelemetry correlation
- **Error handling** — ProblemDetails middleware, global exception handler
- **Health checks** — `/health` and `/alive` endpoints
- **Validation** — FluentValidation pipeline integration
- **API versioning** — Asp.Versioning preconfigured
- **Auth scaffolding** — JWT Bearer configuration helpers, authorization policy patterns

### 5. Aspire service defaults as a separate package

`Yaf.ServiceDefaults` is a standalone package following the Aspire convention of a dedicated ServiceDefaults project. It preconfigures OpenTelemetry, health checks, resilience policies, and service discovery. Consumer opts in explicitly — `Yaf.Api` does not depend on it.

**Rationale:** Keeps Yaf.Api free of Aspire coupling. Consumers who don't use Aspire don't pay the dependency cost.

### 6. Testing: xUnit + TestContainers

The framework itself is tested with xUnit for unit tests and TestContainers for integration tests against real databases. This matches the installed dotnet-skills patterns and provides high confidence in the infrastructure adapters.

### 7. Messaging deferred

Async messaging (integration events, outbox pattern, broker adapters) is not in the initial scope. Domain events stay in-process. A `Yaf.Messaging` module will be added later.

### 8. Example application domain deferred

The reference application that demonstrates YAF will be built, but the specific domain (catalog, orders, etc.) will be chosen when implementation begins.

## What the Consumer Experience Looks Like

```csharp
// Program.cs — minimal setup
var builder = WebApplication.CreateBuilder(args);

builder.AddYaf(options =>
{
    options.UseEfCore<AppDbContext>();          // Yaf.Infrastructure
    options.UseWolverine();                      // Yaf.Application.Wolverine
    options.UseJwtBearer(builder.Configuration); // Yaf.Api
});

var app = builder.Build();
app.UseYaf(); // middleware pipeline
app.Run();
```

## Technology Stack Summary

| Concern | Technology | Package |
|---------|-----------|---------|
| DDD base types | Custom (no dependency) | Yaf.Domain |
| CQRS abstractions | Custom interfaces | Yaf.Application |
| CQRS implementation | Wolverine (first adapter) | Yaf.Application.Wolverine |
| Persistence | EF Core 10 | Yaf.Infrastructure |
| Logging | Serilog + OpenTelemetry | Yaf.Infrastructure / Yaf.Api |
| Validation | FluentValidation | Yaf.Api |
| API versioning | Asp.Versioning | Yaf.Api |
| Error handling | ProblemDetails | Yaf.Api |
| Auth | JWT Bearer (ASP.NET Core) | Yaf.Api |
| Health checks | ASP.NET Core Health Checks | Yaf.Api |
| Orchestration | .NET Aspire service defaults | Yaf.ServiceDefaults |
| Testing | xUnit + TestContainers | Test projects |
| Containerization | Docker multi-stage + Aspire | DevOps |

## Open Questions

*None — all key questions resolved during brainstorm.*

## Research References

- [Best practices research](../research/20260322-0935-dotnet10-web-api-framework-best-practices.md) — detailed findings on .NET 10 patterns
- [Project manifesto](../general/what-and-why-or-the-other-way-round.md) — project vision and constraints
- [Tooling research](../research/20260321-2051-claude-code-tooling-for-dotnet.md) — installed skills and plugins
