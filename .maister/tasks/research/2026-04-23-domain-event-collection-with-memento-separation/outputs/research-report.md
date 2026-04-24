# Research Report: Domain Event Collection with Memento Separation

| Field | Value |
|-------|-------|
| **Research Type** | Mixed (Technical + Literature) |
| **Date** | 2026-04-23 |
| **Methodology** | Codebase analysis + DDD pattern research + .NET ecosystem investigation + industry pattern cataloguing |

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Research Objectives](#research-objectives)
3. [Methodology](#methodology)
4. [Problem Statement: The "Lost Reference" Gap](#problem-statement)
5. [Findings](#findings)
   - [Incompatible Approaches](#incompatible-approaches)
   - [Compatible Approaches](#compatible-approaches)
   - [Dispatch Mechanisms](#dispatch-mechanisms)
6. [Analysis and Insights](#analysis-and-insights)
   - [Compatibility Matrix](#compatibility-matrix)
   - [Convergent Evidence](#convergent-evidence)
   - [Quality Assessment](#quality-assessment)
7. [Recommended Architecture](#recommended-architecture)
8. [Conclusions](#conclusions)
9. [Recommendations](#recommendations)
10. [Appendices](#appendices)

---

## Executive Summary

This research investigated how YAF can automatically collect domain events from aggregate roots when the infrastructure layer only interacts with mementos (DTOs). The core challenge -- the "lost reference" problem -- arises because EF Core's ChangeTracker tracks mementos, not domain aggregates, so the standard .NET pattern of scanning the ChangeTracker for events is incompatible with YAF's architecture.

Four parallel research streams analyzed the YAF codebase, DDD literature, .NET ecosystem mechanisms, and broader industry patterns. All four streams converge on a single architectural answer: **a scoped aggregate tracker service, combined with a generic repository base class that auto-registers aggregates, and a custom domain event dispatcher**. This composition requires zero per-repository boilerplate, preserves YAF's zero-dependency domain layer, maintains the memento pattern, and satisfies the ADR's explicit dispatch requirements.

The domain layer needs no modifications -- `AggregateRoot<TId>` already provides the complete event API (`AddDomainEvent`, `DomainEvents`, `ClearDomainEvents`). All new components reside in the infrastructure layer.

**Key numbers**: 19 patterns and mechanisms catalogued across all sources. 3 are incompatible (ChangeTracker-dependent). 5 are fully compatible. 3 compose into the recommended solution. Overall confidence: High (90-95%).

---

## Research Objectives

### Primary Research Question

How can domain events be automatically collected from aggregate roots when the infrastructure layer only interacts with mementos (DTOs)?

### Sub-Questions

1. What is the exact architectural gap between YAF's domain event API and EF Core's persistence model?
2. Which existing patterns for domain event collection are compatible with memento separation?
3. What .NET-specific mechanisms can be composed to solve the collection problem?
4. How do industry-standard DDD frameworks handle event collection when domain objects are not ORM-tracked?
5. What is the minimal infrastructure needed to achieve automatic, developer-transparent event collection?

### Scope

**Included**: Domain event collection mechanisms, dispatch timing, repository patterns, UoW integration, DI-based approaches, compatibility with YAF's existing constraints.

**Excluded**: Integration events, event serialization/deserialization, message bus integration, outbox implementation details (noted as future work), specific EF Core configuration for mementos.

---

## Methodology

### Research Type and Approach

Mixed methodology combining:
- **Codebase analysis**: Direct inspection of YAF's source code, tests, and ADRs to establish exact constraints and existing API surface
- **DDD literature review**: Cataloguing patterns from established DDD authors and reference architectures
- **Technology investigation**: .NET-specific mechanisms including EF Core interceptors, DI patterns, and framework integrations
- **Industry pattern survey**: Broader patterns from Java (Spring, Axon), event sourcing (Marten, Eventuous), and messaging frameworks (MassTransit, NServiceBus, Wolverine)

### Data Sources

| Source | Files Analyzed | Scope |
|--------|---------------|-------|
| Codebase analysis | 12 source files, 3 test files, 5 ADRs | YAF domain layer + ADR constraints |
| DDD patterns | 8 patterns from 14 external sources | Aggregate event collection literature |
| .NET ecosystem | 8 mechanisms from 16 external sources | .NET-specific technical mechanisms |
| Industry patterns | 11 patterns from 18 external sources | Cross-platform industry approaches |

### Analysis Framework

Technical + Literature mixed framework:
- Component analysis (what exists, what is missing)
- Pattern compatibility assessment (each pattern against 4 YAF constraints)
- Cross-source validation (convergent vs. contradictory findings)
- Trade-off analysis (automation level, developer effort, testability, coupling)

---

## Problem Statement

### The "Lost Reference" Gap

YAF's architecture creates a structural gap between domain events and persistence:

```
Domain Layer:
  AggregateRoot<TId> owns _domainEvents (private List<IDomainEvent>)
    - AddDomainEvent(event)    -- protected, aggregate raises events
    - DomainEvents             -- public read-only collection
    - ClearDomainEvents()      -- public, for infrastructure to call

Memento Boundary:
  Snapshot(memento)            -- copies state TO memento, EXCLUDES events
  Restore(memento)             -- creates aggregate FROM memento, events are empty

Infrastructure Layer:
  EF Core ChangeTracker        -- tracks memento DTOs, NOT aggregate roots
  DbContext.SaveChangesAsync() -- persists mementos, has no access to events
```

The gap: after the repository calls `Snapshot(memento)` to convert an aggregate to a memento for EF Core, the aggregate reference (and its events) may be discarded. At commit time, the only objects visible to EF Core are mementos, which do not carry events. The events are "lost" unless something retains a reference to the aggregate.

### Why Standard Approaches Fail

The standard .NET pattern scans `ChangeTracker.Entries<AggregateRoot>()` for entities with events. In YAF:
- `ChangeTracker.Entries<AggregateRoot>()` returns **zero results** because aggregates are never tracked
- `ChangeTracker.Entries<MementoBase>()` returns mementos, which have **no event collections**
- The bridge between "aggregate that has events" and "memento that EF Core persists" is the repository

### Why Cross-Cutting Patterns Are Not a Precedent

YAF's timestamps and accountability fields are handled at the memento/DbContext level (via SaveChanges interception on memento entities). This works because those fields exist on the memento. Domain events exist on the aggregate, not the memento, so the same mechanism cannot be reused.

---

## Findings

### Incompatible Approaches

These approaches depend on the ORM tracking domain objects directly. They are documented to explain why YAF cannot use them and to justify the need for a custom approach.

#### F1: EF Core ChangeTracker Scanning (eShopOnContainers Pattern)

**Category**: .NET ecosystem standard | **Confidence**: High (100%) | **Verdict**: INCOMPATIBLE

Scans `ChangeTracker.Entries<Entity>()` for tracked entities with pending domain events. Used by eShopOnContainers, Jason Taylor's Clean Architecture, most MediatR-based samples, and Milan Jovanovic's tutorials.

**Why it fails**: ChangeTracker only contains memento DTOs in YAF. The `Entries<AggregateRoot>()` call returns zero results because aggregate root classes are never tracked by EF Core.

**Sources**: Microsoft Learn docs, Milan Jovanovic, eShopOnContainers source

#### F2: Wolverine EF Core Domain Event Harvesting

**Category**: Framework integration | **Confidence**: High (100%) | **Verdict**: INCOMPATIBLE

Wolverine 5.6+ provides `PublishDomainEventsFromEntityFrameworkCore<Entity>(x => x.Events)` which scrapes events from ChangeTracker entries.

**Why it fails**: Same ChangeTracker dependency. Wolverine's lambda `x => x.Events` expects the tracked entity to have an Events property. Mementos do not.

**Sources**: Jeremy D. Miller blog, Wolverine documentation

#### F3: NHibernate/ORM Post-Operation Hooks

**Category**: ORM lifecycle hooks | **Confidence**: High (100%) | **Verdict**: INCOMPATIBLE

NHibernate's `IPostInsertEventListener` / `IPostUpdateEventListener` receive the persisted entity, from which events can be extracted. Vladimir Khorikov's "simple and reliable solution" uses this approach.

**Why it fails**: The ORM passes the entity being persisted to the listener. In YAF's case, the persisted entity IS the memento, not the aggregate.

**Sources**: Vladimir Khorikov, NHibernate documentation

### Compatible Approaches

These approaches work with memento separation because they do not depend on ChangeTracker access to domain objects.

#### F4: Repository-as-Aggregate-Registrar with Scoped Tracker

**Category**: DDD infrastructure pattern | **Confidence**: High (95%) | **Automation**: Full (via base class)

The repository, upon loading or saving an aggregate, registers that aggregate with a scoped `IAggregateTracker` service. The UoW, at commit time, iterates all registered aggregates, harvests their `DomainEvents`, dispatches them, and calls `ClearDomainEvents()`.

**How it works**:
1. `repository.GetByIdAsync(id)` -- loads memento, restores aggregate, calls `tracker.Track(aggregate)`
2. Domain logic runs on aggregate (events accumulate)
3. `repository.SaveAsync(aggregate)` -- snapshots memento, persists, re-registers
4. `UoW.CommitAsync()` -- SaveChanges, harvest events from tracked aggregates, dispatch, clear, commit transaction

**Evidence**: Recommended by DDD patterns source (Pattern 1, 90% confidence), .NET ecosystem source (Mechanism 2+4, 90-95% confidence), External approaches source (Pattern 1, Tier 1). Kamil Grzybek's Modular Monolith, ABP Framework (MongoDB case), and Spring Data's `@DomainEvents` all implement variants of this pattern.

**Trade-offs**:
| Dimension | Assessment |
|-----------|------------|
| Developer effort per repository | Zero (base class automates) |
| Domain layer impact | None (existing API sufficient) |
| Testability | High (tracker is mockable, events inspectable on aggregate) |
| Framework coupling | Low (no ORM/framework dependency) |
| Scoped lifetime management | Required (tracker must be request-scoped) |

**Sources**: Kamil Grzybek, Steven Giesel, ABP Framework docs, CodeOpinion, Spring Data/Baeldung

#### F5: Generic Repository Base Class (MementoRepository)

**Category**: .NET infrastructure pattern | **Confidence**: High (95%) | **Automation**: Full (inheritance)

An abstract `MementoRepository<TAggregate, TId, TMemento>` base class that encapsulates memento-to-domain mapping AND event collector registration. Concrete repositories inherit the behavior with zero additional boilerplate.

```csharp
// Concrete repository -- minimal code
public class OrderRepository : MementoRepository<Order, OrderId, OrderMemento>, IOrderRepository
{
    protected override DbSet<OrderMemento> DbSet => Context.Orders;
    // Only add custom query methods
}
```

The base class handles `GetByIdAsync` (restore + track), `SaveAsync` (snapshot + persist + track), and `AddAsync` (snapshot + add + track) with automatic event collector registration.

**Evidence**: .NET ecosystem source rates this at 95% confidence as "the most promising pattern for YAF." DDD patterns describes essentially the same as Pattern 1's implementation. External approaches (Pattern 7, adapted from Spring Data) confirms "zero per-repository code" with a base class.

**Sources**: CodeOpinion, Kamil Grzybek, Milan Jovanovic, .NET ecosystem analysis

#### F6: Repository Decorator via DI Container

**Category**: Cross-cutting concern pattern | **Confidence**: High (85%) | **Automation**: Full (DI registration)

A generic decorator wraps all `IRepository<T>` implementations, intercepting `GetByIdAsync`/`Save` to register aggregates with a scoped event collector. Using Scrutor: `services.Decorate(typeof(IRepository<>), typeof(EventCollectingRepository<>))`.

**Evidence**: External approaches (Pattern 6) rates this as Tier 1 directly applicable. Milan Jovanovic and Andrew Lock document the decorator pattern for cross-cutting concerns in .NET DI.

**Trade-offs vs. base class**: More flexible (no inheritance requirement), but adds DI complexity, makes debugging harder (proxy layer), and is less explicit. Since YAF is building repositories from scratch, the base class is preferable.

**Sources**: Milan Jovanovic, Andrew Lock, External approaches analysis

#### F7: AsyncLocal/Ambient Event Collector

**Category**: Static ambient context | **Confidence**: Medium (60-70%) | **Automation**: Full (no per-repo code)

A static `DomainEventTracker` using `AsyncLocal<List<IDomainEvent>>` that aggregates call `Raise()` on directly. A middleware creates the scope and reads events at the end.

**Evidence**: Documented by Ken van Grinsven. All three non-codebase sources flag concerns: static coupling, testability issues, hidden dependency, potential violation of zero-dep domain principle.

**Verdict**: Compatible with memento separation but violates YAF's explicit dependency principle and would require changing the existing `AddDomainEvent()` API to call `DomainEventTracker.Raise()` instead. Not recommended.

**Sources**: Ken van Grinsven

#### F8: Functional / Command-Returns-Events

**Category**: Architectural alternative | **Confidence**: Medium (50%) | **Automation**: Full (framework-level)

Domain methods return events rather than accumulating them. The handler collects returned events and dispatches them. Used by Wolverine (cascading messages), Eventuous, and Axon Framework.

**Evidence**: Eliminates the collection problem entirely. However, requires a significant API change (breaking `AddDomainEvent()` pattern) and a different mental model (functional vs. OOP DDD). YAF's existing API is well-established with tests.

**Verdict**: Viable architecture but incompatible with YAF's current design without a breaking API change. Not recommended for initial implementation.

**Sources**: Wolverine docs, Eventuous docs, Axon Framework docs

### Dispatch Mechanisms

These are orthogonal to collection -- they handle the "publish to handlers" step.

#### F9: Custom IDomainEventDispatcher (Mediator-Agnostic)

**Category**: Dispatch mechanism | **Confidence**: High (95%)

A custom dispatcher resolves `IDomainEventHandler<TEvent>` implementations from DI using reflection and generic type wrappers. Avoids MediatR dependency while providing the same publish/subscribe semantics. Aligns with YAF's ADR specifying mediator-agnostic CQRS abstractions.

**Sources**: Milan Jovanovic (complete implementation reference)

#### F10: MediatR-Based Dispatch

**Category**: Dispatch mechanism | **Confidence**: High (100%)

Domain events implement `INotification`; handlers implement `INotificationHandler<T>`. Most widely used dispatch mechanism in .NET DDD.

**Verdict**: Compatible but introduces framework dependency. YAF's ADR prefers mediator-agnostic abstractions, so a custom dispatcher (F9) is preferable with MediatR as an optional adapter.

**Sources**: Microsoft Learn, Wrapt, eShopOnContainers

#### F11: Outbox Pattern

**Category**: Delivery reliability | **Confidence**: High (90%)

Domain events serialized to an `OutboxMessages` table within the same transaction, then processed by a background worker. Orthogonal to collection -- can be layered on top of any collection mechanism.

**Verdict**: Future enhancement. Does not affect the collection pattern choice. Supported by MassTransit, NServiceBus, and Wolverine.

**Sources**: Enterprise Craftsmanship, MassTransit docs, NServiceBus docs

### Summary Table

| ID | Finding | Category | Confidence | Compatible? |
|----|---------|----------|------------|-------------|
| F1 | ChangeTracker Scanning | .NET standard | 100% | NO |
| F2 | Wolverine EF Core | Framework | 100% | NO |
| F3 | ORM Post-Op Hooks | ORM lifecycle | 100% | NO |
| F4 | Aggregate Tracker + Scoped Service | DDD infrastructure | 95% | YES |
| F5 | Generic Repository Base | .NET infrastructure | 95% | YES |
| F6 | Repository Decorator | DI pattern | 85% | YES |
| F7 | AsyncLocal Ambient Collector | Static context | 65% | YES (concerns) |
| F8 | Functional Returns-Events | Architecture | 50% | YES (breaking) |
| F9 | Custom Dispatcher | Dispatch | 95% | YES |
| F10 | MediatR Dispatch | Dispatch | 100% | YES |
| F11 | Outbox Pattern | Delivery | 90% | YES (orthogonal) |

---

## Analysis and Insights

### Compatibility Matrix

Each approach evaluated against YAF's four key constraints:

| Approach | Zero-Dep Domain | Memento Pattern | Explicit Dispatch | Minimal Effort |
|----------|:-:|:-:|:-:|:-:|
| F1: ChangeTracker | Yes | **NO** | Yes | Zero |
| F2: Wolverine EF | No | **NO** | Yes | Zero |
| F3: ORM Hooks | Partial | **NO** | Yes | Zero |
| F4: Aggregate Tracker | **Yes** | **Yes** | **Yes** | **Zero (base class)** |
| F5: Generic Repo Base | **Yes** | **Yes** | **Yes** | **Zero (inherited)** |
| F6: Repo Decorator | **Yes** | **Yes** | **Yes** | **Zero (DI)** |
| F7: AsyncLocal | Concerns | **Yes** | Partial | Zero |
| F8: Returns-Events | **Yes** | **Yes** | **Yes** | Per-method |
| F9: Custom Dispatcher | **Yes** | N/A | **Yes** | One-time |
| F10: MediatR | No (app dep) | N/A | **Yes** | One-time |
| F11: Outbox | **Yes** | N/A | **Yes** | One-time |

**Only F4 + F5 + F9 satisfy all four constraints simultaneously.** F6 is a viable alternative to F5 but adds DI complexity.

### Convergent Evidence

The strongest finding of this research is the convergence across all four independent sources:

1. **All four sources** identify ChangeTracker scanning as incompatible
2. **All four sources** identify the repository as the natural bridge point
3. **Three of four sources** independently design a generic repository base class with auto-tracking
4. **Three of four sources** recommend a scoped tracking/collector service
5. **Two of four sources** recommend a custom (non-MediatR) dispatcher

This level of convergence across independently conducted research streams provides strong confidence in the recommendation.

### Quality Assessment

**Strengths**:
- YAF's domain layer is well-designed for this pattern -- `DomainEvents` and `ClearDomainEvents()` are already public with correct visibility
- The recommended pattern is established in production systems (ABP Framework, Kamil Grzybek's Modular Monolith)
- Zero domain-layer changes required
- The composition is modular -- each component can be implemented and tested independently

**Weaknesses**:
- No existing YAF infrastructure code to validate against -- the repository and UoW are not yet built
- The pattern requires discipline in the base class design to handle edge cases (concurrent modifications, nested UoW)
- Recursive event handling (handlers raising new events) is an open design question

**Opportunities**:
- The generic repository base class can also automate other cross-cutting concerns (validation, memento mapping)
- The outbox pattern can be added as a future reliability layer
- The tracker pattern naturally extends to support integration event collection

**Threats**:
- If a concrete repository bypasses the base class (e.g., raw SQL queries), events from those operations will not be collected
- Performance with very large numbers of tracked aggregates per UoW (unlikely in practice)

---

## Recommended Architecture

### Overview

The recommended solution composes three infrastructure components:

```
Collection Layer:
  IAggregateTracker (scoped service)
    + MementoRepository<TAggregate, TId, TMemento> (abstract base class)

Dispatch Layer:
  IDomainEventDispatcher (custom, mediator-agnostic)
    + IDomainEventHandler<TEvent> (handler interface)

Orchestration:
  UnitOfWork.CommitAsync() coordinates collection -> dispatch -> clear
```

### Component Design

**IAggregateTracker** (infrastructure, scoped):
- `Track(AggregateRoot aggregate)` -- register an aggregate for event harvesting
- `GetTrackedAggregates()` -- return all tracked aggregates (for UoW)
- Implementation: simple `List<AggregateRoot>` with optional identity-based deduplication

**MementoRepository<TAggregate, TId, TMemento>** (infrastructure, abstract):
- Inherits from no domain type; implements `IRepository<TAggregate, TId>`
- Constructor receives `DbContext` and `IAggregateTracker`
- `GetByIdAsync`: load memento -> `TAggregate.Restore(memento)` -> `tracker.Track(aggregate)` -> return
- `AddAsync`: `aggregate.Snapshot(memento)` -> `dbSet.Add(memento)` -> `tracker.Track(aggregate)`
- `SaveAsync`: `aggregate.Snapshot(memento)` -> `dbSet.Update(memento)` -> `tracker.Track(aggregate)`

**IDomainEventDispatcher** (application layer interface, infrastructure implementation):
- `DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct)`
- Implementation resolves `IDomainEventHandler<TEvent>` from DI container
- Uses `ConcurrentDictionary<Type, Type>` for handler type caching

**UoW Event Dispatch Integration**:
```
CommitAsync():
  1. await dbContext.SaveChangesAsync()        // flush mementos to DB
  2. var aggregates = tracker.GetTrackedAggregates()
  3. var events = aggregates.SelectMany(a => a.DomainEvents).ToList()
  4. await dispatcher.DispatchAsync(events)    // within transaction
  5. foreach (var a in aggregates) a.ClearDomainEvents()
  6. await transaction.CommitAsync()
```

### Lifecycle Flow

```
Command Handler                 Repository (base class)         Tracker         UoW
     |                               |                            |              |
     |-- GetByIdAsync(id) ---------->|                            |              |
     |                               |-- Find memento (EF Core)   |              |
     |                               |-- Restore(memento)         |              |
     |                               |-- Track(aggregate) ------->|              |
     |<-- aggregate ------------------|                            |              |
     |                                                             |              |
     |-- aggregate.DoSomething()                                   |              |
     |   (events accumulate)                                       |              |
     |                                                             |              |
     |-- SaveAsync(aggregate) ------>|                             |              |
     |                               |-- Snapshot(memento)         |              |
     |                               |-- Update memento (EF Core)  |              |
     |                               |-- Track(aggregate) -------->|              |
     |                                                             |              |
     |                                                             |              |
     |                                                    CommitAsync() -------->|
     |                                                             |  SaveChanges |
     |                                                             |<- GetTracked |
     |                                                             |  Harvest     |
     |                                                             |  Dispatch    |
     |                                                             |  Clear       |
     |                                                             |  Commit txn  |
```

### Why This Design

1. **Zero domain changes**: `AggregateRoot<TId>` already has `DomainEvents` and `ClearDomainEvents()`
2. **Zero per-repository boilerplate**: Base class handles Track() in every CRUD method
3. **Memento pattern preserved**: Tracker holds aggregate references; EF Core holds mementos
4. **Explicit dispatch**: UoW.CommitAsync() is the single, clear dispatch point
5. **ADR-compliant timing**: Dispatch after SaveChanges, before CommitAsync, within transaction
6. **Testable**: Every component is injectable and mockable
7. **Extensible**: Outbox, integration events, recursive dispatch can be added later

---

## Conclusions

### Primary Conclusions

**1. The aggregate tracker pattern is the clear solution.** (Confidence: High, 95%)

Four independent research streams -- codebase analysis, DDD literature, .NET ecosystem, and industry patterns -- all converge on the same answer. A scoped aggregate tracking service, combined with a generic repository base class, provides automatic domain event collection that is compatible with memento separation, preserves the zero-dependency domain layer, and requires zero per-repository developer effort.

**2. No domain-layer changes are required.** (Confidence: High, 100%)

The existing `AggregateRoot<TId>` API provides everything needed: `AddDomainEvent()` (protected, for aggregates), `DomainEvents` (public read-only, for infrastructure), and `ClearDomainEvents()` (public, for post-dispatch cleanup). All new components reside entirely in the infrastructure layer.

**3. The standard .NET approach is definitively incompatible.** (Confidence: High, 100%)

EF Core ChangeTracker scanning -- used by eShopOnContainers, Wolverine, and most .NET DDD tutorials -- cannot work with memento separation. This is not a matter of adaptation; the fundamental premise (ORM tracks domain objects) does not hold. This confirms YAF needs a custom approach.

### Secondary Conclusions

**4. Base class > decorator for YAF's greenfield infrastructure.** Both achieve automation, but the base class is simpler, more explicit, and aligns with YAF's existing pattern of abstract base classes.

**5. Custom dispatcher > MediatR** for YAF's mediator-agnostic architecture. The ADR specifies YAF-owned CQRS abstractions. A custom `IDomainEventDispatcher` with DI-based handler resolution achieves the same result without framework coupling.

**6. The outbox pattern is orthogonal future work** that can be layered on top of the recommended collection mechanism without architectural changes.

### Direct Answer to the Research Question

Domain events can be automatically collected from aggregate roots in YAF's memento-separated architecture by:

1. Introducing a **scoped `IAggregateTracker` service** that holds references to aggregate root instances
2. Having the **generic repository base class** (`MementoRepository<TAggregate, TId, TMemento>`) automatically register every aggregate it loads, creates, or saves with the tracker
3. Having the **Unit of Work**, at commit time, harvest `DomainEvents` from all tracked aggregates and dispatch them via a custom `IDomainEventDispatcher` -- after `SaveChangesAsync()` but before `CommitAsync()`, within the transaction, per the ADR mandate

This approach requires zero per-repository boilerplate, zero domain-layer changes, and satisfies all four key constraints (zero-dep domain, memento pattern, explicit dispatch, minimal developer effort).

---

## Recommendations

| # | Recommendation | Priority | Effort | Rationale |
|---|---------------|----------|--------|-----------|
| 1 | Implement `IAggregateTracker` as a scoped infrastructure service | High | Low | Core collection mechanism; single interface + implementation |
| 2 | Implement `MementoRepository<TAggregate, TId, TMemento>` abstract base class | High | Medium | Automates tracking in all repository CRUD operations |
| 3 | Implement `IDomainEventDispatcher` with DI-based handler resolution | High | Medium | Mediator-agnostic dispatch aligned with ADR |
| 4 | Integrate event dispatch into `UnitOfWork.CommitAsync()` | High | Low | Orchestration; wires the three components together |
| 5 | Document dispatch lifecycle and ordering in ADR | Medium | Low | Clarify cascading events, multi-aggregate ordering |
| 6 | Add outbox pattern for reliable delivery | Low | Medium | Future enhancement for distributed scenarios |
| 7 | Consider integration event collection mechanism | Low | Low | Extend tracker to support post-commit integration events |

### Implementation Order

The natural implementation order follows the dependency chain:

1. `IAggregateTracker` (no dependencies, can be unit tested standalone)
2. `IDomainEventDispatcher` (depends on DI container, can be integration tested)
3. `MementoRepository<TAggregate, TId, TMemento>` (depends on tracker, DbContext)
4. UoW integration (depends on all three above)
5. End-to-end tests with a concrete aggregate, repository, and handlers

---

## Appendices

### A. Complete Source List

**Codebase Sources**:
- `src/Yaf.Domain/AggregateRoot.cs`
- `src/Yaf.Domain/AggregateRoot{TId,TSelf,TMemento}.cs`
- `src/Yaf.Domain/Interfaces/IDomainEvent.cs`
- `src/Yaf.Domain/Interfaces/IMemento.cs`
- `src/Yaf.Domain/Interfaces/IHydratable.cs`
- `src/Yaf.Domain/Interfaces/IMementoBase.cs`
- `src/Yaf.Domain/Helpers/MementoHelper.cs`
- `src/Yaf.Domain/MementoBase.cs`
- `tests/Yaf.Domain.Tests/AggregateRootTests.cs`
- `tests/Yaf.Domain.Tests/DomainEventTests.cs`
- `docs/adr/domain/20260324-1113-domain-events-and-integration-events.md`
- `docs/adr/infrastructure/20260324-1256-data-consistency.md`
- `docs/adr/infrastructure/20260324-1229-persistence-strategy.md`
- `docs/adr/domain/20260324-1032-domain-building-blocks.md`
- `docs/adr/domain/20260324-1104-state-management-memento-pattern.md`
- `docs/adr/architecture/20260324-1146-application-layer-patterns.md`

**External Sources**:
- Microsoft Learn - Domain events: Design and implementation
- Milan Jovanovic - How To Use Domain Events / Building a Custom Domain Events Dispatcher / Decorator Pattern
- Jimmy Bogard - A Better Domain Events Pattern
- Kamil Grzybek - How to Publish and Handle Domain Events / Modular Monolith with DDD (GitHub)
- Vladimir Khorikov - Domain Events: Simple and Reliable Solution / Domain Model Separate from Persistence Model
- Steven Giesel - Domain Events and the Unit of Work Pattern
- Ken van Grinsven - Minimal Impact Domain Events
- Jeremy D. Miller - Classic .NET Domain Events with Wolverine and EF Core
- CodeOpinion (Derek Comartin) - Aggregate Root Design: Behavior & Data
- Richard Banks - DDD Entity Framework and the Memento Pattern
- Ledjon Behluli - Change Tracking While Doing DDD (Revisited)
- TheCodeWrapper - EF Core: Effectively Decouple the Data and Domain Model
- ABP.IO - Event Bus Documentation
- Andrew Lock - Adding Decorated Classes Using Scrutor
- Marten - Aggregates, Events, Repositories
- Axon Framework - Aggregates Documentation
- Wolverine - Publishing Domain Events from EF Core
- Baeldung - DDD Aggregates and @DomainEvents
- paucls - Aggregate Roots and Domain Events Publication
- SapiensWorks - DDD Persisting Aggregate Roots in a Unit of Work
- Wrapt - .NET Domain Events Using MediatR
- Ardalis - Immediate Domain Event Salvation with MediatR
- MassTransit - Transactional Outbox
- NServiceBus - Outbox
- Martin Fowler - Domain Event
- EF Core Interceptors Documentation

### B. Gaps and Uncertainties

| Gap | Impact | Status |
|-----|--------|--------|
| Recursive/cascading event handling | Medium | Open design question |
| Event ordering across aggregates | Low-Medium | Needs ADR clarification |
| Failed handler partial recovery | Medium | Outside collection scope |
| Integration event conversion timing | Low | Separate future concern |
| No existing infrastructure code to validate | Low | Domain API is stable |

### C. Pattern Catalogue Summary

**Incompatible with memento separation (3)**:
- EF Core ChangeTracker scanning
- Wolverine EF Core integration
- NHibernate/ORM post-operation hooks

**Compatible with memento separation (5)**:
- Aggregate tracker in UoW (recommended)
- Generic repository base class (recommended, composes with tracker)
- Repository decorator via DI
- AsyncLocal ambient collector (concerns about static coupling)
- Functional command-returns-events (requires API change)

**Dispatch mechanisms (2)**:
- Custom IDomainEventDispatcher (recommended)
- MediatR INotification/INotificationHandler

**Delivery enhancement (1)**:
- Outbox pattern (future work)
