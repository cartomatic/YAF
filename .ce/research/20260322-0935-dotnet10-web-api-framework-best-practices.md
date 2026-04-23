# .NET 10 Web API Framework Best Practices Research

**Date:** 2026-03-22
**Purpose:** Research current best practices (2025-2026) for building .NET 10 web API applications that can be generalized into a reusable library/framework (YAF).

---

## Table of Contents

1. [Clean Architecture / DDD in .NET 10](#1-clean-architecture--ddd-in-net-10)
2. [Ports and Adapters (Hexagonal Architecture)](#2-ports-and-adapters-hexagonal-architecture)
3. [Web API Patterns](#3-web-api-patterns)
4. [Scalability Patterns](#4-scalability-patterns)
5. [Async Communication](#5-async-communication)
6. [Containerization](#6-containerization)
7. [Cross-Cutting Concerns](#7-cross-cutting-concerns)
8. [What Makes a Good .NET Library/Framework](#8-what-makes-a-good-net-libraryframework)
9. [Recommendations for YAF](#9-recommendations-for-yaf)

---

## 1. Clean Architecture / DDD in .NET 10

### Current Consensus on Layers

The widely-adopted four-layer structure remains the standard:

| Layer | Responsibility | Dependencies |
|-------|---------------|--------------|
| **Domain** | Entities, Value Objects, Aggregate Roots, Domain Events, Domain Services | None (innermost) |
| **Application** | Use cases, Commands/Queries, DTOs, interfaces (ports) | Domain only |
| **Infrastructure** | Database, external APIs, message brokers, file storage | Domain + Application |
| **Presentation / API** | Controllers/Endpoints, request/response mapping | Application (+ Infrastructure for DI wiring) |

**Key principle:** Inner layers never depend on outer layers. Outer layers depend inward via interfaces.

### DDD: What's Widely Adopted

**Strategic DDD (high value, universally recommended):**
- **Bounded Contexts** -- clear boundaries between modules/services
- **Context Mapping** -- explicit relationships between bounded contexts
- **Ubiquitous Language** -- shared vocabulary between code and business domain

**Tactical DDD (adopt selectively):**
- **Aggregate Roots** -- entry points to clusters of entities treated as a single transactional unit
- **Value Objects** -- immutable objects defined by their attributes (e.g., Money, EmailAddress)
- **Domain Events** -- capture side effects of domain operations
- **Repository pattern** -- abstract persistence behind domain-oriented interfaces

### What's Considered Over-Engineered

- Applying full DDD tactical patterns to simple CRUD domains
- Separate read/write databases (CQRS with event sourcing) when not needed
- Domain services for logic that naturally belongs in entities
- Excessive abstraction layers between simple operations

### Generalizable for a Library

- **Project structure templates** -- provide the folder/project layout
- **Base classes** -- `Entity<TId>`, `ValueObject`, `AggregateRoot<TId>`, `DomainEvent`
- **Repository interfaces** -- `IRepository<T>`, `IReadRepository<T>`
- **Domain event dispatching** -- infrastructure for publishing domain events
- **NOT generalizable:** Actual domain models, business rules, bounded context definitions (these are application-specific)

### Key References

- [Ardalis Clean Architecture Template for ASP.NET Core 10](https://github.com/ardalis/CleanArchitecture)
- [Clean Architecture and DDD in Practice 2025](https://wojciechowski.app/en/articles/clean-architecture-domain-driven-design-2025)
- [DDD in Clean Architecture .NET 10 with Full Code](https://medium.com/devopsturkiye/how-to-use-domain-driven-design-ddd-in-clean-architecture-net-10-with-full-code-8f7d532e1a39)

---

## 2. Ports and Adapters (Hexagonal Architecture)

### How It Maps to .NET 10

Hexagonal Architecture (Alistair Cockburn, 1994/2005) maps naturally to .NET's dependency injection system:

**Ports** = Interfaces defined in the Domain or Application layer
- **Incoming (Driving) Ports:** Interfaces the application implements (e.g., `IOrderService`, command/query handlers)
- **Outgoing (Driven) Ports:** Interfaces the application depends on (e.g., `IOrderRepository`, `IEmailSender`, `IPaymentGateway`)

**Adapters** = Implementations in the Infrastructure or Presentation layer
- **Incoming Adapters:** API controllers, message consumers, CLI commands
- **Outgoing Adapters:** EF Core repositories, SMTP email senders, HTTP clients

### What Works Well in .NET

```
// Port (in Application layer)
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(OrderId id, CancellationToken ct);
    Task AddAsync(Order order, CancellationToken ct);
}

// Adapter (in Infrastructure layer)
public class EfOrderRepository : IOrderRepository { ... }

// Wiring (in Composition Root / Program.cs)
services.AddScoped<IOrderRepository, EfOrderRepository>();
```

### Practical Guidelines

- Adapters must NOT contain business logic -- they translate between external systems and the core
- Ports (interfaces) are owned by the Domain/Application layer, never by Infrastructure
- Use `IServiceCollection` extension methods per adapter category for clean registration
- Test the core by mocking outgoing ports; test adapters with integration tests

### Generalizable for a Library

- **Port base interfaces** -- `IRepository<T>`, `IUnitOfWork`, `IDomainEventPublisher`
- **Adapter registration patterns** -- extension methods like `AddInfrastructure()`, `AddPersistence()`
- **NOT generalizable:** Specific adapter implementations (these depend on chosen infrastructure)

### Key References

- [Hexagonal Architecture with .NET: Designing for Testability](https://engineering87.github.io/2025/07/19/exagonal-architecture.html)
- [Ports and Adapters in Practice with .NET](https://medium.com/@rafaeljcamara/ports-and-adapters-architecture-hexagonal-architecture-in-practice-with-net-7b80bb9f68c0)

---

## 3. Web API Patterns

### Minimal APIs vs Controllers in .NET 10

**Minimal APIs are the recommended approach for new projects.** Microsoft's official guidance positions them as the default for new HTTP APIs.

| Aspect | Minimal APIs | Controllers | FastEndpoints |
|--------|-------------|-------------|---------------|
| Performance | Best | Slightly slower | On par with Minimal APIs |
| Boilerplate | Low | High | Medium (structured) |
| Built-in validation (.NET 10) | Yes (new) | Yes | Yes |
| OpenAPI support | Native (3.1, YAML) | Via Swashbuckle | Built-in |
| Scalability of codebase | Needs discipline | Natural via classes | Natural via REPR pattern |
| Filters/middleware | Endpoint filters | Action filters | Built-in pipeline |

**New in .NET 10:**
- Built-in `AddValidation()` for Minimal APIs (removes need for custom validation code)
- OpenAPI 3.1 with YAML endpoint support
- `ServerSentEvents` result type for streaming
- Improved diagnostics in request pipeline

### FastEndpoints: The REPR Pattern

FastEndpoints implements Request-Endpoint-Response (REPR), giving Minimal API performance with controller-like organization:

```
Features/
  Orders/
    CreateOrder/
      CreateOrderEndpoint.cs    // Endpoint + Request + Response
      CreateOrderValidator.cs   // FluentValidation
    GetOrder/
      GetOrderEndpoint.cs
```

**Verdict for a framework:** FastEndpoints adds a dependency but provides excellent structure. For YAF, providing support for both Minimal APIs and FastEndpoints (or a similar REPR pattern) via adapters would be ideal.

### API Versioning

The `Asp.Versioning` libraries (dotnet/aspnet-api-versioning) are the standard:
- URL path versioning: `/api/v1/orders`
- Header versioning: `api-version: 1.0`
- Query string versioning: `?api-version=1.0`
- Supports both Minimal APIs and Controllers

### Result Pattern

**Ardalis.Result** (v10.1.0) is the leading result pattern library:
- Maps result statuses to HTTP status codes automatically
- Supports both Controllers and Minimal APIs (`ToMinimalApiResult()`)
- Railway-oriented programming with `Map` and `Bind`
- Integrates with ProblemDetails (RFC 7807/9457)

**ErrorOr** is a lighter alternative focused on discriminated union-style error handling.

### CQRS

**MediatR licensing change:** MediatR adopted a commercial license for newer versions, prompting reassessment.

**Alternatives:**
- **Wolverine** -- free, convention-based handlers (no interfaces), built-in mediator + messaging + outbox in one package
- **Hand-rolled CQRS** -- simple `ICommandHandler<TCommand, TResult>` / `IQueryHandler<TQuery, TResult>` interfaces with DI registration
- **Cortex.Mediator** -- free, MIT-licensed MediatR alternative

**Recommendation for a library:** Provide CQRS abstractions (interfaces) without mandating a specific mediator. Allow users to plug in Wolverine, MediatR, or hand-rolled implementations.

### Generalizable for a Library

- **Result pattern base types** or integration with Ardalis.Result
- **CQRS interfaces** -- `ICommand<TResult>`, `IQuery<TResult>`, `ICommandHandler<T, TResult>`, `IQueryHandler<T, TResult>`
- **API versioning wiring** via extension methods
- **ProblemDetails configuration** as middleware
- **Endpoint organization conventions** (folder structure, naming)
- **NOT generalizable:** Specific endpoints, request/response DTOs, business validation rules

### Key References

- [What's New with APIs in .NET 10](https://www.telerik.com/blogs/whats-new-apis-net-10-real-improvements)
- [FastEndpoints, Controllers, and Minimal APIs Compared](https://blog.nimblepros.com/blogs/fastendpoints-controllers-and-minimal-apis-compared/)
- [Implementing CQRS Without MediatR in .NET 10](https://dotnetcopilot.com/implementing-cqrs-without-mediatr-in-net-10-using-clean-architecture/)
- [Ardalis.Result Overview](https://result.ardalis.com/)
- [Asp.Versioning on GitHub](https://github.com/dotnet/aspnet-api-versioning)

---

## 4. Scalability Patterns

### Stateless Design

The foundation of horizontal scaling. All session state externalized to:
- **Redis** for distributed cache and session state
- **Database** for persistent state
- **Message broker** for async workflows

### Caching Strategies

| Strategy | Use Case | Implementation |
|----------|----------|----------------|
| **In-memory cache** | Single-instance, frequently read data | `IMemoryCache` (built-in) |
| **Distributed cache** | Multi-instance, shared cache | `IDistributedCache` with Redis |
| **Response caching** | Cacheable HTTP responses | Response caching middleware |
| **Output caching** | Server-side response caching (.NET 7+) | `OutputCache` middleware |
| **HybridCache** (.NET 9+) | L1 in-memory + L2 distributed | `HybridCache` (stampede protection built-in) |

**Redis best practices:**
- Use as centralized store for rate-limiting data across nodes
- Lua scripts for atomic operations (sliding windows, token buckets)
- Sorted sets for time-based tracking

### Rate Limiting

ASP.NET Core has built-in rate limiting middleware (since .NET 7):
- Fixed window, sliding window, token bucket, concurrency limiter
- For distributed scenarios: Redis-backed rate limiting (e.g., `AspNetCoreRateLimit` or custom with Redis)

### Health Checks

.NET Aspire establishes the standard pattern:
- `/health` -- full readiness check (all dependencies)
- `/alive` -- liveness check (app is running, not crashed)
- Component-specific health checks registered by infrastructure packages

### Distributed Tracing / Observability

OpenTelemetry is the standard:
- **Traces:** ASP.NET Core, HttpClient, and runtime instrumentation
- **Metrics:** built-in meters + custom counters
- **Logs:** Serilog 4.x automatically captures TraceId/SpanId from `Activity.Current`
- **Export:** to Jaeger, Zipkin, Azure Monitor, or any OTLP-compatible backend

### Generalizable for a Library

- **Cache abstractions** and configuration helpers (Redis, HybridCache)
- **Rate limiting configuration** via extension methods
- **Health check registration patterns** -- `AddDefaultHealthChecks()` with sensible defaults
- **OpenTelemetry setup** -- preconfigured tracing, metrics, logging
- **NOT generalizable:** Specific cache keys, rate limit policies per endpoint, custom health checks for specific infrastructure

### Key References

- [Rate Limiting in .NET with Redis](https://redis.io/tutorials/rate-limiting-in-dotnet-with-redis/)
- [Advanced Rate Limiting in .NET](https://www.milanjovanovic.tech/blog/advanced-rate-limiting-use-cases-in-dotnet)
- [.NET Aspire Service Defaults](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/service-defaults)

---

## 5. Async Communication

### Framework Landscape (2025-2026)

| Framework | License | Mediator | Messaging | Outbox | Maturity |
|-----------|---------|----------|-----------|--------|----------|
| **Wolverine** | MIT (free) | Yes | Yes | Built-in | Growing rapidly |
| **MassTransit** | Commercial (v9+) | No | Yes | Built-in | Battle-tested, years of production use |
| **NServiceBus** | Commercial | No | Yes | Built-in | Enterprise standard |
| **MediatR** | Commercial (v13+) | Yes | No | No | Widely adopted, licensing shift |

### Wolverine: The Emerging Standard

Wolverine is the most compelling choice for new .NET projects because it unifies mediator + messaging + outbox:

**Key features:**
- Convention-based handlers (no interfaces -- handler class ending in `Handler`, method ending in `Handle`)
- Built-in transactional outbox with EF Core, Marten, or raw SQL
- Supports RabbitMQ, Azure Service Bus, Amazon SQS, Kafka
- Durable message persistence using your application's database
- Saga/workflow support
- Source-generated code for performance (uses Roslyn at runtime)
- Cascading messages (handlers can return messages to publish)
- Built-in retry policies, dead-letter queues

**Migration from MediatR:** Wolverine provides a direct migration path with its `IMessageBus` acting as both mediator and message publisher.

### Outbox Pattern

The transactional outbox pattern solves the dual-write problem:
1. Business data and outgoing events are written in the same database transaction
2. A background process publishes events from the outbox table
3. Both Wolverine and MassTransit have built-in outbox implementations

### Event-Driven Architecture Patterns

- **Domain Events** -- intra-bounded-context, synchronous or via local queue
- **Integration Events** -- cross-bounded-context, via message broker
- **Event Sourcing** -- optional, store state as sequence of events (use only when the event history itself is valuable)

### Generalizable for a Library

- **Event abstractions** -- `IDomainEvent`, `IIntegrationEvent` interfaces
- **Event dispatching infrastructure** -- collect domain events from aggregates, dispatch after save
- **Messaging configuration helpers** -- `AddMessaging(options => ...)` that wires up Wolverine/MassTransit
- **Outbox pattern support** -- via chosen framework
- **NOT generalizable:** Specific event types, handler implementations, saga definitions

### Key References

- [Wolverine Documentation](https://wolverinefx.net/)
- [Wolverine for MediatR Users](https://wolverinefx.net/introduction/from-mediatr)
- [Wolverine Durable Messaging](https://wolverinefx.net/guide/durability/)
- [MassTransit Outbox Pattern](https://medium.com/@fcakiroglu16/implementing-the-outbox-and-inbox-pattern-with-masstransit-a-reliable-messaging-approach-in-net-52e943f6826d)
- [Messaging Frameworks Comparison (Visual Studio Magazine)](https://visualstudiomagazine.com/articles/2025/08/11/messaging-made-simple-choosing-the-right-framework-for-net.aspx)

---

## 6. Containerization

### Docker Best Practices for .NET 10

**Multi-stage builds** remain the standard pattern:

```dockerfile
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY *.sln .
COPY src/MyApp/*.csproj src/MyApp/
RUN dotnet restore
COPY . .
RUN dotnet publish src/MyApp -c Release -o /app

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "MyApp.dll"]
```

**Key practices:**
- Copy `.csproj` files first for layer caching of `dotnet restore`
- Use `aspnet` runtime image (not SDK) for production
- Run as non-root user
- Use `.dockerignore` to exclude `bin/`, `obj/`, `.git/`
- Use environment variables for runtime configuration (avoid rebuilds)

### .NET Built-in Container Support

.NET 8+ (improved in .NET 10) can produce container images without Dockerfiles:

```xml
<PropertyGroup>
  <EnableSdkContainerSupport>true</EnableSdkContainerSupport>
</PropertyGroup>
```

Then: `dotnet publish --os linux --arch x64 /t:PublishContainer`

### .NET Aspire for Orchestration

Aspire is the recommended local dev orchestration tool (requires .NET 10 SDK for CLI):

- **AppHost project** -- defines the distributed application topology
- **Service Defaults project** -- shared configuration for OpenTelemetry, health checks, service discovery
- **Dashboard** -- built-in observability UI for local development
- Generates Docker Compose files for deployment
- Supports Docker Desktop, Podman, and other OCI runtimes
- `AddDockerfile()` / `WithDockerfile()` for custom containers

### Kubernetes Readiness

- Health endpoints (`/health`, `/alive`) map directly to Kubernetes readiness/liveness probes
- Aspire can generate Kubernetes manifests
- Stateless design (Section 4) is prerequisite for K8s horizontal pod autoscaling

### Generalizable for a Library

- **Dockerfile templates** for the framework's expected project structure
- **Aspire service defaults** as a shared project pattern
- **Health endpoint configuration** that K8s probes can use out of the box
- **NOT generalizable:** Specific Aspire AppHost topology, K8s deployment manifests (infrastructure-specific)

### Key References

- [.NET Aspire Service Defaults](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/service-defaults)
- [.NET Aspire Docker Integration](https://learn.microsoft.com/en-us/dotnet/aspire/deployment/docker-integration)
- [.NET Aspire Tutorial with .NET 10 and PostgreSQL](https://codewithmukesh.com/blog/aspire-for-dotnet-developers-deep-dive/)
- [.NET Aspire Health Checks](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/health-checks)

---

## 7. Cross-Cutting Concerns

### Logging: Serilog + OpenTelemetry

**Serilog** remains the structured logging standard. **OpenTelemetry** handles collection, correlation, and export.

**Best practice integration:**
- Serilog 4.x automatically captures `TraceId` and `SpanId` from `Activity.Current`
- Use Serilog for developer-friendly structured logging
- Use OpenTelemetry for distributed trace correlation and export
- They complement each other -- Serilog writes, OTEL correlates and ships

**Library should provide:**
- Pre-configured Serilog setup with sensible defaults (console + structured JSON)
- OpenTelemetry integration wired up automatically
- Correlation ID middleware
- Sensitive data filtering

### Exception Handling

**ProblemDetails (RFC 9457)** is the standard for API error responses:
- Built into ASP.NET Core via `app.UseExceptionHandler()` + `app.UseStatusCodePages()`
- .NET 8+ has `IExceptionHandler` for typed exception handling
- Map domain exceptions to appropriate HTTP status codes

**Library should provide:**
- Global exception handler middleware
- Domain exception to ProblemDetails mapping
- Consistent error response format

### Validation

**FluentValidation** remains the de facto standard, but .NET 10 adds built-in `AddValidation()` for Minimal APIs.

**Approaches:**
- FluentValidation for complex domain validation rules
- Built-in validation for simple request validation
- MediatR/Wolverine pipeline behaviors for automatic validation before handlers

**Library should provide:**
- Validation pipeline integration (works with CQRS handlers)
- Validation error to ProblemDetails mapping
- Extension point for custom validators

### Authentication / Authorization

**Standard stack:**
- JWT Bearer authentication (`Microsoft.AspNetCore.Authentication.JwtBearer`)
- OpenID Connect for full auth flows
- Keycloak as a popular self-hosted identity provider
- `Keycloak.AuthServices` for .NET-specific integration

**Library should provide:**
- `AddAuthentication()` / `AddAuthorization()` wrappers with sensible defaults
- JWT configuration helpers
- Policy-based authorization scaffolding
- NOT the identity provider itself (that's infrastructure)

### Configuration

**Options pattern** is the standard for library configuration:

```csharp
// Library provides:
public class YafOptions
{
    public bool EnableDetailedErrors { get; set; }
    public string DefaultCacheProvider { get; set; } = "memory";
}

// Consumer configures:
services.AddYaf(options =>
{
    options.EnableDetailedErrors = true;
    options.DefaultCacheProvider = "redis";
});
```

### Dependency Injection Organization

**ServiceCollection Extension Pattern:**
- One extension method per concern: `AddYafCore()`, `AddYafPersistence()`, `AddYafMessaging()`
- Consumers compose only what they need
- Each extension method accepts an `Action<TOptions>` for configuration

### Generalizable for a Library

- **ALL of the above** are prime candidates for library inclusion
- Pre-configured middleware pipeline
- Extension methods for each concern
- Sensible defaults with override capability
- Consistent error handling and response formats

### Key References

- [Structured Logging with Serilog in ASP.NET Core .NET 10](https://codewithmukesh.com/blog/structured-logging-with-serilog-in-aspnet-core/)
- [OpenTelemetry with Serilog in .NET](https://dev.to/ankit01oss/implementing-opentelemetry-with-serilog-in-a-net-application-practical-guide-22go)
- [Mastering the ASP.NET Core Request Pipeline](https://developersvoice.com/blog/csharp/mastering-asp-net-core-request-pipeline-patterns/)
- [JWT Bearer Authentication in ASP.NET Core (.NET 10)](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0)

---

## 8. What Makes a Good .NET Library/Framework

### NuGet Packaging Best Practices

- **Clear metadata:** short description, relevant tags (NuGet.org search algorithm uses tags)
- **Semantic versioning:** follow SemVer strictly
- **Symbol packages:** include PDB for debugging
- **README in NuGet:** supported since NuGet 5.x
- **Minimal dependencies:** avoid dragging in heavy transitive dependencies
- **Target multiple TFMs** if needed, but for YAF targeting `net10.0` only is fine

### Extensibility Points

1. **IServiceCollection extensions** -- the primary integration point
2. **Options pattern** -- `Action<TOptions>` delegates for configuration
3. **Interface-based abstractions** -- consumers can replace any component
4. **Middleware pipeline** -- consumers can insert their own middleware
5. **Source generators** -- for compile-time code generation (advanced, reduces runtime reflection)

### Convention Over Configuration

Inspired by Rails/ASP.NET Core itself:
- Auto-discover handlers, validators, repositories by convention (assembly scanning)
- Default folder structure that "just works"
- Override any convention explicitly when needed
- Provide `dotnet new` templates for project scaffolding

### Source Generators

Modern .NET libraries increasingly use source generators:
- Eliminate runtime reflection
- Compile-time validation of configurations
- Auto-generate DI registrations, mapping code, serialization
- Ship as NuGet packages alongside analyzers
- **Caveat:** provide runtime reflection fallbacks for scenarios where source generators can't be used

**Technical requirements:**
- Target .NET Standard 2.0 for the generator itself
- Use `Microsoft.CodeAnalysis.CSharp` and `Microsoft.CodeAnalysis.Analyzers`
- Mark 3rd-party dependencies with `PrivateAssets="all"` and `GeneratePathProperty="true"`

### Library Structure Pattern

```
YAF/
  src/
    YAF.Core/                    # Domain primitives, base classes, interfaces
    YAF.Application/             # CQRS abstractions, pipeline behaviors
    YAF.Infrastructure/          # Default implementations (EF Core, etc.)
    YAF.Infrastructure.Redis/    # Optional Redis adapter
    YAF.Infrastructure.RabbitMQ/ # Optional RabbitMQ adapter
    YAF.Api/                     # API helpers, middleware, filters
    YAF.Generators/              # Source generators (optional)
  samples/
    YAF.Sample.WebApi/           # Example application
  tests/
    YAF.Core.Tests/
    YAF.Application.Tests/
    YAF.Infrastructure.Tests/
    YAF.Api.Tests/
```

### Key References

- [NuGet Package Authoring Best Practices](https://learn.microsoft.com/en-us/nuget/create-packages/package-authoring-best-practices)
- [Get Started Creating High-Quality .NET Libraries](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/get-started)
- [Options Pattern Guidance for .NET Library Authors](https://learn.microsoft.com/en-us/dotnet/core/extensions/options-library-authors)
- [ServiceCollection Extension Pattern](https://dotnetcoretutorials.com/servicecollection-extension-pattern/)
- [Source Generators in .NET (Infinum Handbook)](https://infinum.com/handbook/dotnet/best-practices/source-generators)
- [Framework Design Guidelines (Book)](https://www.pearson.com/en-us/subject-catalog/p/framework-design-guidelines-conventions-idioms-and-patterns-for-reusable-net-libraries/P200000000204/9780135896327)

---

## 9. Recommendations for YAF

### What YAF Should Provide (Generalizable)

**Tier 1 -- Core (must have):**
- Domain primitive base classes: `Entity<TId>`, `AggregateRoot<TId>`, `ValueObject`, `DomainEvent`
- CQRS interfaces: `ICommand<TResult>`, `IQuery<TResult>`, handlers
- Result pattern (integrate with Ardalis.Result or provide own)
- Repository interfaces: `IRepository<T>`, `IReadRepository<T>`, `IUnitOfWork`
- Domain event collection and dispatching

**Tier 2 -- Infrastructure Defaults (recommended):**
- EF Core integration with repository implementations
- Serilog + OpenTelemetry pre-configuration
- Global exception handling with ProblemDetails
- Health check defaults (`/health`, `/alive`)
- Validation pipeline (FluentValidation or built-in)
- Authentication/authorization helpers (JWT Bearer)
- Rate limiting configuration

**Tier 3 -- Optional Adapters (choose what you need):**
- Redis distributed cache adapter
- Wolverine messaging integration
- RabbitMQ / Azure Service Bus transport
- Outbox pattern support
- API versioning helpers

**Tier 4 -- Developer Experience:**
- `dotnet new` project templates
- `AddYaf()` / `AddYafCore()` / `AddYafInfrastructure()` extension methods
- Aspire service defaults project template
- Documentation and sample application
- Source generators for boilerplate reduction (future)

### What YAF Should NOT Provide (Application-Specific)

- Actual domain models, entities, business rules
- Specific API endpoints or DTOs
- Database schemas or migrations
- UI components
- Identity provider implementation
- Specific deployment manifests

### Technology Choices Summary

| Concern | Recommended | Alternative |
|---------|------------|-------------|
| API style | Minimal APIs | FastEndpoints |
| Mediator/CQRS | Wolverine (or hand-rolled) | MediatR (if licensing acceptable) |
| Messaging | Wolverine | MassTransit |
| ORM | EF Core | Dapper (for read-side) |
| Validation | FluentValidation + built-in | Built-in only |
| Logging | Serilog + OpenTelemetry | Microsoft.Extensions.Logging + OTEL |
| Result pattern | Ardalis.Result | ErrorOr |
| Caching | HybridCache + Redis | IDistributedCache |
| Orchestration | .NET Aspire | Docker Compose |
| API versioning | Asp.Versioning | Custom headers |
| Auth | JWT Bearer + OIDC | Cookie auth (for web apps) |

### Design Principles for YAF

1. **Composition over inheritance** -- prefer interfaces and extension methods over deep class hierarchies
2. **Pay for what you use** -- separate NuGet packages per concern, no monolithic dependency
3. **Sensible defaults, full overridability** -- everything works out of the box, everything can be replaced
4. **No vendor lock-in** -- abstractions over specific implementations
5. **Minimal ceremony** -- reduce boilerplate without hiding important decisions
6. **Testability first** -- every component mockable, every integration testable in isolation
