# Testing Strategy

- **Timestamp:** 2026-03-24 10:00
- **Status:** under review
- **Scope:** architecture
- **Stakeholders:** Proposed by: Claude Code, Decided by: @cartomatic

---

## Summary

YAF uses a multi-level testing strategy: unit tests for domain and application logic, integration tests with real infrastructure (via TestContainers) for persistence and adapters, and API tests for the HTTP layer. Example applications are fully tested as well — they serve as self-explanatory, executable documentation for how to use the library. The framework prioritizes testing against real dependencies over mocking infrastructure, reflecting the principle that mocked tests can mask real failures.

## Drivers

1. **AI-generated code quality** — All code is AI-generated. Tests are the primary safety net to catch incorrect implementations.
2. **Infrastructure confidence** — Memento mapping, EF Core conventions, encryption, and query filters must be tested against real databases, not mocks.
3. **Fast feedback** — Unit tests must run in milliseconds. Integration tests should start quickly and be parallelizable.
4. **Consumer trust** — A framework library must have high test confidence. Consumers need to trust that YAF's building blocks work correctly.
5. **Maintainability** — Tests should be easy for AI tools to read, write, and update. Prefer simple, explicit test patterns over clever abstractions.

## Options

### Test Framework

| Option | Assessment |
|--------|------------|
| **xUnit** | **Selected.** Most widely used in .NET, good AI familiarity, built-in parallelization. |
| NUnit | Viable but less common in modern .NET projects. No compelling advantage. |
| MSTest | Improving, but smaller community and fewer patterns available. |

### Integration Test Infrastructure

| Option | Assessment |
|--------|------------|
| **TestContainers** | **Selected.** Spins up real databases (PostgreSQL, SQL Server) in Docker for integration tests. No mock/prod divergence risk. |
| In-memory EF Core provider | Rejected. Behaves differently from real databases (no constraints, no SQL translation). Masks real failures. |
| SQLite in-memory | Better than EF in-memory but still lacks features of real databases (JSON columns, specific SQL functions). |
| Shared test database | Requires external setup, not portable, parallel test conflicts. |

### Mocking Strategy

| Option | Assessment |
|--------|------------|
| **Minimal mocking — real dependencies preferred** | **Selected.** Mock only external services and boundaries that are impractical to run (third-party APIs, email). Use real databases, real DI containers, real middleware. |
| Heavy mocking | Rejected. Mocked infrastructure tests give false confidence. A mock-passing test suite with a broken migration is worse than no tests. |

## Recommendation

Multi-level testing with real infrastructure:

- **Unit tests** — Domain and Application layers. No DI container, no database. Test business logic, invariants, and handler orchestration in isolation.
- **Integration tests** — Infrastructure layer. Real database via TestContainers. Test EF Core mappings, repository implementations, memento round-trips, query filters, encryption, audit auto-population.
- **API tests** — API layer. In-process `WebApplicationFactory` with real middleware pipeline. Test HTTP responses, ProblemDetails mapping, validation, auth, versioning.
- **Mocking** — Only for external boundaries that cannot be run locally (third-party HTTP APIs, email services, cloud-specific services).

## Consequences

**Positive:**
- Integration tests catch real issues: broken migrations, incorrect EF mappings, query filter bugs
- Domain unit tests are fast (milliseconds) and require no infrastructure
- `WebApplicationFactory` tests exercise the real middleware pipeline including validation, error handling, and auth
- TestContainers ensures portable, repeatable test environments — no shared database setup
- Clean Architecture makes unit testing natural — Domain and Application layers have no infrastructure dependencies

**Negative:**
- Integration tests require Docker, adding a development environment dependency
- TestContainers tests are slower than in-memory tests (seconds per test class for container startup, mitigated by container reuse)
- CI/CD must support Docker (most modern CI platforms do)

## Conclusion

### Test Levels

| Level | Scope | Infrastructure | Speed | Project |
|-------|-------|---------------|-------|---------|
| **Unit** | Domain logic, application handlers, value objects, specifications | None — pure .NET | Fast (ms) | Yaf.Domain.Tests, Yaf.Application.Tests |
| **Integration** | EF Core mappings, repositories, memento round-trips, query filters, encryption, audit fields | TestContainers (real database) | Medium (seconds) | Yaf.Infrastructure.Tests |
| **API** | HTTP endpoints, middleware, ProblemDetails, validation, auth | WebApplicationFactory + TestContainers | Medium (seconds) | Yaf.Api.Tests |
| **Adapter** | Wolverine dispatch, message handling | Wolverine test harness | Medium | Yaf.Application.Wolverine.Tests |
| **Example** | Full usage scenarios, end-to-end flows demonstrating library features | WebApplicationFactory + TestContainers | Medium | examples/ServiceX/tests/* |

### Test Project Structure

```
tests/
├── Yaf.Domain.Tests/                  # Unit tests — zero infrastructure
├── Yaf.Application.Tests/             # Unit tests — handlers, pipelines (mocked repositories)
├── Yaf.Infrastructure.Tests/          # Integration — real DB via TestContainers
├── Yaf.Api.Tests/                     # API tests — WebApplicationFactory
└── Yaf.Application.Wolverine.Tests/   # Adapter tests

examples/
└── ServiceX/
    └── tests/                             # Example app tests — executable documentation
        ├── ServiceX.Domain.Tests/
        ├── ServiceX.Application.Tests/
        ├── ServiceX.Infrastructure.Tests/
        └── ServiceX.Api.Tests/
```

### Example Application Tests

Example applications are **fully tested**. Each example has its own `tests/` folder mirroring the example's `src/` structure. Their tests serve a dual purpose:

1. **Correctness** — verify that the example app works correctly against YAF's building blocks
2. **Documentation** — tests are readable, self-explanatory demonstrations of how to use the library. They show consumers how to wire up commands, queries, repositories, validation, auth, and other YAF features in a real application.

Example tests follow the same conventions as library tests (real databases, minimal mocking) and are included in CI. If example tests fail, it signals that either the library or the documentation is broken — both are worth catching.

### Conventions

1. **One test project per source project** — mirrors the `src/` structure under `tests/`. Example apps also get test projects.
2. **Real databases for integration tests** — no in-memory EF Core provider, no SQLite substitution.
3. **Mock only external boundaries** — third-party APIs, email, cloud services. Everything else uses real implementations.
4. **Container reuse** — TestContainers instances are shared across test classes within a project to minimize startup overhead.
5. **Test naming** — `MethodName_Scenario_ExpectedResult` or descriptive names that read as specifications.

## More Information

- [ADR: Technology Stack](20260324-0948-technology-stack.md) — xUnit and TestContainers selection
- [YAF Library Design Brainstorm](../../brainstorms/20260322-1755-yaf-library-design-brainstorm.md) — testing decisions
