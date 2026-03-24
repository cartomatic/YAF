# Technology Stack

- **Timestamp:** 2026-03-24 09:48
- **Status:** under review
- **Scope:** architecture
- **Stakeholders:** Proposed by: Claude Code, Decided by: @cartomatic

---

## Summary

YAF targets .NET 10 as its minimum platform and selects specific libraries for each cross-cutting concern. The guiding principle is: pick one well-supported, actively maintained library per concern, prefer MIT/Apache-licensed options, and avoid libraries with uncertain licensing futures.

## Drivers

1. **Long-term viability** — Libraries must be actively maintained with clear licensing. Avoid dependencies that may go commercial or dormant.
2. **Minimal dependency surface** — Each package should bring only what it needs. Consumers should not inherit a large transitive dependency graph.
3. **Convention over configuration** — Prefer libraries that work well with opinionated defaults while allowing customization.
4. **.NET ecosystem alignment** — Prefer libraries that are idiomatic in .NET and have strong community adoption.
5. **AI-driven development** — Libraries with good documentation and broad usage are easier for AI tools to work with correctly.
6. **NuGet package isolation** — Technology choices must respect the layered package structure. A library used in Infrastructure must not leak into Domain.

## Options

### Runtime & Framework

| Option | Description | Assessment |
|--------|-------------|------------|
| **.NET 10** | Latest LTS-track release. C# 13, performance improvements, Native AOT progress. | **Selected.** Current platform, long-term support expected. |
| .NET 9 | Current release, but will be out of support sooner. | Rejected. No reason to target an older runtime when starting fresh. |

### ORM / Data Access

| Option | Description | Assessment |
|--------|-------------|------------|
| **EF Core 10** | Microsoft's primary ORM. Rich mapping, migrations, LINQ provider. | **Selected.** Industry standard for .NET, strong tooling, good AI familiarity. |
| Dapper | Micro-ORM, raw SQL. | Not as default — lacks change tracking, migrations, conventions needed for the memento pattern and audit auto-population. Could be supported as a future adapter. |

### CQRS / Mediator

| Option | Description | Assessment |
|--------|-------------|------------|
| **Wolverine** (adapter) | MIT-licensed. Mediator + messaging + outbox in one package. Convention-based. | **Selected as first adapter.** Combines CQRS dispatch with messaging capabilities for future use. |
| MediatR | Widely used but moved to commercial licensing. | Not selected as default. Can be supported as an alternative adapter via YAF's own CQRS abstractions. |
| Custom dispatch | Build a mediator from scratch. | Unnecessary complexity. YAF owns the abstractions; dispatch is delegated to proven libraries. |

### Logging

| Option | Description | Assessment |
|--------|-------------|------------|
| **Serilog** | Structured logging with rich sink ecosystem. | **Selected.** De facto standard for structured logging in .NET. OpenTelemetry integration available. |
| Microsoft.Extensions.Logging alone | Built-in, minimal. | Insufficient — no structured sink ecosystem, limited enrichment. Serilog builds on top of it. |
| NLog | Alternative structured logging. | Smaller ecosystem than Serilog in modern .NET. No compelling advantage. |

### Observability

| Option | Description | Assessment |
|--------|-------------|------------|
| **OpenTelemetry (.NET)** | Vendor-neutral telemetry: traces, metrics, logs. | **Selected.** Industry standard. Integrates with Serilog and Aspire service defaults. |

### Validation

| Option | Description | Assessment |
|--------|-------------|------------|
| **FluentValidation** | Fluent rule builder, pipeline integration. | **Selected.** Mature, well-documented, good AI familiarity. Integrates at the API/Application boundary. |
| Data Annotations | Built-in but limited expressiveness. | Insufficient for complex business validation. Mixes concerns (attributes on models). |

### API Versioning

| Option | Description | Assessment |
|--------|-------------|------------|
| **Asp.Versioning** | Microsoft-supported API versioning. URL, header, query string strategies. | **Selected.** Official Microsoft package, well-maintained. |

### Authentication

| Option | Description | Assessment |
|--------|-------------|------------|
| **ASP.NET Core JWT Bearer** | Built-in JWT authentication middleware. | **Selected.** Framework-native, no extra dependencies. YAF provides configuration helpers, not a custom auth system. |

### Health Checks

| Option | Description | Assessment |
|--------|-------------|------------|
| **ASP.NET Core Health Checks** | Built-in health check framework. | **Selected.** Framework-native. Rich ecosystem of check packages (EF Core, SQL, Redis, etc.). |

### Orchestration / Service Defaults

| Option | Description | Assessment |
|--------|-------------|------------|
| **.NET Aspire Service Defaults** | Preconfigured OpenTelemetry, health checks, resilience, service discovery. | **Selected as optional package.** Yaf.ServiceDefaults is standalone — consumers opt in. |

### Testing

| Option | Description | Assessment |
|--------|-------------|------------|
| **xUnit** | Most widely used .NET test framework. | **Selected.** Strong tooling, good AI familiarity. |
| **TestContainers** | Docker-based integration test infrastructure. | **Selected.** Real databases in tests, no mocking infrastructure. |

### Containerization

| Option | Description | Assessment |
|--------|-------------|------------|
| **Docker multi-stage builds (Linux containers only)** | Standard containerization. | **Selected.** Industry standard, Aspire integration. Linux containers only — no Windows containers. |

## Recommendation

The technology stack as described above. Each choice is driven by: active maintenance, permissive licensing, .NET ecosystem alignment, and minimal dependency surface per package.

## Consequences

**Positive:**
- All selected libraries are MIT or Apache-2.0 licensed — no commercial licensing risk
- Strong AI tooling familiarity across the stack — well-documented, widely used
- Yaf.Domain remains dependency-free; each outer layer adds only its own specific dependencies
- The Wolverine choice gives a path from in-process CQRS to distributed messaging without changing the programming model
- Aspire service defaults as a separate package means no forced cloud coupling

**Negative:**
- Wolverine is less widely known than MediatR — smaller community, fewer StackOverflow answers
- Serilog is a third-party dependency that consumers may have opinions about (mitigated: it's the de facto standard)
- EF Core as the default ORM means consumers who prefer Dapper or raw SQL need to work around the conventions (mitigated: future adapter packages)
- .NET 10 minimum means no support for older runtimes (acceptable for a greenfield framework)

## Conclusion

YAF's technology stack:

| Layer | Technology |
|-------|-----------|
| **Platform** | .NET 10, latest C# language version (currently C# 13). New code uses latest features; existing code is not retroactively refactored for new syntax. |
| **Domain** | No dependencies (pure .NET) |
| **Application** | Yaf's own CQRS abstractions |
| **CQRS Adapter** | Wolverine (first adapter, MIT licensed) |
| **Persistence** | EF Core 10 |
| **Logging** | Serilog + OpenTelemetry |
| **Validation** | FluentValidation |
| **API Versioning** | Asp.Versioning |
| **Auth** | ASP.NET Core JWT Bearer |
| **Health Checks** | ASP.NET Core Health Checks |
| **Service Defaults** | .NET Aspire (optional, separate package) |
| **Testing** | xUnit + TestContainers |
| **Containers** | Docker multi-stage builds (Linux containers only) |

## More Information

- [YAF Library Design Brainstorm](../../brainstorms/20260322-1755-yaf-library-design-brainstorm.md) — technology decisions and rationale
- [.NET 10 Best Practices Research](../../research/20260322-0935-dotnet10-web-api-framework-best-practices.md) — detailed findings
