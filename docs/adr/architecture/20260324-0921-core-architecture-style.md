# Core Architecture Style

- **Timestamp:** 2026-03-24 09:21
- **Status:** under review
- **Scope:** architecture
- **Stakeholders:** Proposed by: Claude Code, Decided by: @cartomatic

---

## Summary

YAF adopts Clean Architecture (Onion Architecture) as its core architectural style, enforcing a strict dependency rule where all dependencies point inward toward the domain. The architecture is organized as concentric layers — Domain at the center, then Application, then Infrastructure and Adapters at the outer ring — with communication between layers mediated through interfaces (ports) defined by inner layers and implemented by outer layers (adapters).

## Drivers

1. **Framework independence** — The domain model must not depend on any framework, ORM, or library. Business logic should be testable in isolation.
2. **Persistence ignorance** — Domain objects must not know how they are stored. Swapping EF Core for Dapper or a document database should not require domain changes.
3. **Testability** — Each layer should be independently testable. Domain logic tests should run without databases, HTTP, or DI containers.
4. **NuGet package delivery** — YAF ships as separate NuGet packages per layer. The architecture must map cleanly to package boundaries with minimal coupling.
5. **Extensibility** — Consumers should be able to swap or extend infrastructure implementations (persistence, messaging, auth) without touching domain or application code.
6. **AI-generated code** — Clear architectural boundaries help AI tools generate code in the right place with the right dependencies.

## Options

### Option A: Clean Architecture (Onion Architecture)

Concentric layers with the dependency rule: all dependencies point inward. Domain is the innermost layer with zero dependencies. Application orchestrates domain operations. Infrastructure and Adapters implement contracts defined by inner layers.

**Pros:**
- Strong separation of concerns with enforceable dependency rule
- Domain layer is pure .NET — no framework dependencies
- Maps naturally to NuGet package boundaries (one package per layer)
- Well-understood in the .NET ecosystem; extensive tooling and guidance
- Ports & adapters pattern enables swappable implementations

**Cons:**
- More boilerplate than simpler architectures (interfaces, mappings)
- Can feel over-engineered for very small projects
- Requires discipline to avoid shortcut dependencies

### Option B: Vertical Slice Architecture

Organize code by feature rather than by layer. Each feature contains its own handler, model, and persistence logic.

**Pros:**
- Less ceremony for simple features
- Changes are localized to a single folder
- Good fit for CQRS-heavy applications

**Cons:**
- Harder to enforce shared domain invariants across features
- Cross-cutting concerns (audit, encryption, tenancy) require conventions rather than structural enforcement
- Doesn't map well to a reusable framework — YAF provides building blocks, not features
- Shared domain types (entities, value objects) still need a common home

### Option C: Modular Monolith (Feature Modules with Shared Kernel)

Independent modules per bounded context, each with their own layers, communicating through a shared kernel or integration events.

**Pros:**
- Good bounded context isolation
- Natural path toward microservices if needed
- Each module is independently deployable in theory

**Cons:**
- YAF is a framework library, not an application — module boundaries are the consumer's responsibility
- Adds structural complexity that belongs in the consuming application, not the framework
- Shared kernel design is application-specific

## Recommendation

**Option A: Clean Architecture** — It directly addresses all drivers. The strict dependency rule maps cleanly to NuGet package boundaries (Yaf.Domain → Yaf.Application → Yaf.Infrastructure → Yaf.Api), each with explicit and minimal dependencies. The ports & adapters pattern enables YAF's core value proposition: opinionated defaults with swappable implementations.

Vertical Slices and Modular Monolith are application-level patterns that consumers can adopt within their own code. YAF provides the building blocks — consumers decide how to organize their features.

## Consequences

**Positive:**
- Each NuGet package has a clear responsibility and minimal dependency surface
- Yaf.Domain has zero third-party dependencies — pure .NET
- Consumers can reference only the layers they need (e.g., Yaf.Domain alone for DDD base types)
- Infrastructure implementations are swappable through interfaces defined in Domain and Application
- Testing is straightforward — domain logic tests need no infrastructure

**Negative:**
- Mapping between layers (domain ↔ memento ↔ DTO) adds boilerplate
- Consumers must understand the layered model to use YAF effectively
- Some cross-cutting concerns (like the memento pattern) span multiple layers, requiring careful interface design

## Conclusion

YAF uses **Clean Architecture with a strict inward dependency rule**, organized as:

1. **Domain** (innermost) — Entities, aggregates, value objects, domain events, domain services, specifications, error definitions. Zero external dependencies.
2. **Application** — CQRS handlers, application services, context provider interfaces, validation pipeline. Depends only on Domain.
3. **Infrastructure** — Persistence (EF Core), encryption, scheduling, context provider implementations. Depends on Domain + Application.
4. **Adapters** (outermost) — API controllers, middleware, auth, health checks. Depends on Application (not Infrastructure directly).

Communication between layers follows the **Ports & Adapters** pattern: inner layers define interfaces (ports), outer layers provide implementations (adapters). Dependency injection wires them together at composition root.

## More Information

- [YAF Library Design Brainstorm](../../brainstorms/20260322-1755-yaf-library-design-brainstorm.md) — package structure and dependency graph
- [YAF DDD Concepts Brainstorm](../../brainstorms/20260322-2036-yaf-ddd-concepts-brainstorm.md) — layer responsibilities
- Robert C. Martin, *Clean Architecture* (2017)
- Alistair Cockburn, *Hexagonal Architecture* (Ports & Adapters)
