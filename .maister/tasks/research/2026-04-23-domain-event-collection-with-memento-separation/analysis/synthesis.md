# Synthesis: Domain Event Collection with Memento Separation

## Research Question

How can domain events be automatically collected from aggregate roots when the infrastructure layer only interacts with mementos (DTOs), given that: (1) domain objects are separated from storage via memento pattern, (2) EF Core only sees mementos not domain objects, (3) repositories map domain objects to/from mementos, (4) requiring manual event collection in every repository is error-prone, (5) the ADR mandates explicit dispatch after SaveChanges/before CommitAsync?

## Executive Summary

Four parallel research streams -- codebase analysis, DDD literature, .NET ecosystem mechanisms, and industry patterns -- converge on a single architectural insight: the "lost reference" problem created by memento separation requires an **aggregate tracking mechanism** in the infrastructure layer. The standard .NET approach (ChangeTracker scanning) is definitively incompatible with YAF's architecture, but the alternative -- a scoped aggregate tracker combined with a generic repository base class -- is well-established across DDD literature, .NET practice, and broader industry patterns.

All four sources independently identify the **repository as the natural collection point** for domain events, since it is the only component that holds references to both the domain aggregate (which carries events) and the memento (which EF Core tracks). The strongest approach composes three mechanisms: a scoped `IAggregateTracker` service, a generic `MementoRepository<TAggregate, TId, TMemento>` base class that auto-registers aggregates, and a custom `IDomainEventDispatcher` for mediator-agnostic dispatch. This composition requires zero per-repository boilerplate and preserves all YAF constraints.

## Cross-Source Analysis

### Validated Findings (Confirmed by Multiple Sources)

**Finding 1: ChangeTracker scanning is incompatible with memento separation**
- **Sources**: All four (codebase, DDD patterns, .NET ecosystem, external approaches)
- **Confidence**: High (100%)
- **Evidence**: The codebase analysis confirms EF Core tracks mementos, not aggregates. DDD patterns document (Pattern 2) labels it "INCOMPATIBLE". .NET ecosystem document (Mechanism 1) confirms "ChangeTracker only contains memento DTOs". External approaches (Patterns 2, 3, 11) all flag ChangeTracker dependency as the blocker.
- **Significance**: This eliminates the most common .NET pattern (eShopOnContainers, Jason Taylor's template, Milan Jovanovic's tutorials, Wolverine's built-in integration) and confirms YAF needs a custom approach.

**Finding 2: The repository is the natural bridge point for event collection**
- **Sources**: All four
- **Confidence**: High (95%)
- **Evidence**: Codebase analysis identifies the repository as "the natural place to retain aggregate references, since it already mediates between aggregates and mementos." DDD patterns (Patterns 1, 3, 8) all place collection at the repository. .NET ecosystem (Mechanism 4) designs the generic repository base specifically for this. External approaches (Patterns 1, 6, 7, 9) all use the repository as the integration point.
- **Significance**: This is the strongest convergent finding -- every compatible approach places event collection at or near the repository.

**Finding 3: A generic repository base class eliminates per-repository boilerplate**
- **Sources**: DDD patterns, .NET ecosystem, external approaches
- **Confidence**: High (90%)
- **Evidence**: DDD patterns sketch `Repository<TAggregate, TId, TMemento>` with auto-Track calls. .NET ecosystem (Mechanism 4) provides a detailed `MementoRepository<TAggregate, TMemento, TId>` implementation rated at 95% confidence. External approaches (Patterns 1, 7) confirm "zero per-repository code" when a base class handles registration.
- **Significance**: This directly addresses constraint (4) -- minimizing developer effort. Concrete repositories only need custom query methods.

**Finding 4: Domain events are transient and intentionally excluded from memento snapshots**
- **Sources**: Codebase analysis
- **Confidence**: High (100%)
- **Evidence**: `_domainEvents` is a private field on `AggregateRoot<TId>`, never written to memento. The test `Restore_DomainEventsCollectionIsEmpty` explicitly validates this. `Snapshot`/`Restore` never touch `_domainEvents`.
- **Significance**: Events must be collected BEFORE the aggregate reference is lost. The memento cannot carry them across the persistence boundary.

**Finding 5: Scoped lifetime is essential for the tracking service**
- **Sources**: DDD patterns, .NET ecosystem, external approaches
- **Confidence**: High (90%)
- **Evidence**: DDD patterns note "the tracker must be request-scoped so aggregates from multiple repositories are collected." .NET ecosystem designs `IDomainEventCollector` as scoped. External approaches confirm scoped lifetime matches HTTP request / command handler scope.
- **Significance**: The tracker service must live for exactly one unit-of-work scope -- not singleton (leaks across requests), not transient (loses state between repositories).

**Finding 6: Dispatch must be explicit, after SaveChanges, before CommitAsync**
- **Sources**: Codebase analysis (ADR), DDD patterns, .NET ecosystem
- **Confidence**: High (100%)
- **Evidence**: The domain events ADR specifies: "Before CommitAsync(), infrastructure dispatches accumulated events." The data consistency ADR confirms: "After operation returns successfully: -> Domain events dispatched (within transaction)." Both DDD patterns and .NET ecosystem designs place dispatch in UoW.CommitAsync.
- **Significance**: This constrains the dispatch timing and eliminates patterns that dispatch during SaveChanges or after commit.

### Contradictions and Tensions

**Tension 1: Aggregate Tracker vs. Event Collector**
- **Description**: DDD patterns distinguish between tracking aggregates (holding references, deferring event extraction) and collecting events (copying events out immediately). The .NET ecosystem blurs this distinction.
- **Resolution**: These are two implementations of the same concept. Tracking aggregates (deferred extraction) is slightly more elegant because it preserves the single-responsibility principle -- events stay on the aggregate until the UoW needs them. The DDD patterns source rates tracker at 90%, collector at 85%.
- **Recommendation**: Use aggregate tracking (hold references) rather than eager event copying. This also handles the edge case where an aggregate raises additional events during handler execution.

**Tension 2: Where should the tracker interface live?**
- **Description**: The .NET ecosystem suggests `IDomainEventCollector` could live in the application layer. The codebase analysis notes that the existing cross-cutting pattern uses interfaces in the domain layer. External approaches are silent on this.
- **Resolution**: Since `AggregateRoot<TId>` is in the domain layer and the tracker needs to accept `AggregateRoot<TId>` instances, the interface could live in the domain layer (alongside the existing `IDomainEvent` interface). However, tracking is an infrastructure concern. A simple approach: the tracker interface accepts `object` or a marker interface already on `AggregateRoot`, avoiding new domain-layer abstractions.
- **Recommendation**: The tracker interface should live in the infrastructure layer. The UoW (also infrastructure) consumes it directly. No domain-layer changes needed since `AggregateRoot<TId>` already exposes `DomainEvents` and `ClearDomainEvents()` publicly.

**Tension 3: Repository base class vs. Repository decorator**
- **Description**: External approaches (Pattern 6) propose a repository decorator using Scrutor, which would auto-wrap all repositories without inheritance. DDD patterns and .NET ecosystem prefer a base class.
- **Resolution**: Both achieve the same automation. The decorator is more flexible (no inheritance requirement) but adds DI complexity and makes debugging harder (proxy layer). The base class is simpler, explicit, and aligns with YAF's existing pattern of abstract base classes (`AggregateRoot`, `Entity`, `ValueObject`). Since YAF is building repositories from scratch (no legacy repos to wrap), the base class is preferable.
- **Recommendation**: Use the base class approach. Reserve the decorator for future scenarios where third-party repositories need event collection without modification.

### Confidence Assessment

| Finding | Confidence | Basis |
|---------|------------|-------|
| ChangeTracker incompatible | High (100%) | 4/4 sources, direct code evidence |
| Repository is the bridge | High (95%) | 4/4 sources, architectural reasoning |
| Generic base eliminates boilerplate | High (90%) | 3/4 sources, code sketches provided |
| Events are transient, not in memento | High (100%) | Direct code + test evidence |
| Scoped lifetime required | High (90%) | 3/4 sources, DI pattern analysis |
| Explicit dispatch timing | High (100%) | ADR mandate, 3/4 sources |
| Aggregate tracking > event copying | Medium (80%) | 2/4 sources, architectural reasoning |
| Base class > decorator | Medium (75%) | Architectural alignment, no direct comparison |

## Patterns and Themes

### Pattern: Repository-as-Aggregate-Registrar

- **Description**: The repository registers each aggregate it loads or creates with a scoped tracking service. The UoW harvests events from all tracked aggregates at commit time.
- **Evidence**: DDD patterns (Pattern 1), .NET ecosystem (Mechanism 2+4), External approaches (Patterns 1, 7)
- **Prevalence**: Most frequently recommended compatible pattern across all sources
- **Quality**: Well-established; multiple reference architectures use variants (Kamil Grzybek's Modular Monolith, ABP Framework for MongoDB, Marten)

### Pattern: Cross-Cutting Interface Bridge

- **Description**: YAF already bridges domain-to-memento concerns using interface detection (`if (entity is ITimestamped ts && memento is IHasTimestamps hts)`). This established pattern could inform how the tracker discovers event-bearing aggregates.
- **Evidence**: Codebase analysis (MementoHelper, cross-cutting interfaces)
- **Prevalence**: YAF-specific, but consistent with the broader interface-detection approach
- **Quality**: Mature within YAF; proven pattern

### Pattern: Deferred Event Dispatch (Bogard/Khorikov Principle)

- **Description**: Events are accumulated on aggregates during business operations, then dispatched later by infrastructure -- never dispatched synchronously at raise time.
- **Evidence**: DDD patterns (all), .NET ecosystem (Mechanism 1 discussion), External approaches (Pattern 3)
- **Prevalence**: Universal in modern DDD; YAF already implements this (AddDomainEvent is protected, dispatch is infrastructure's job)
- **Quality**: Industry standard; YAF's existing design is aligned

### Pattern: Mediator-Agnostic Dispatch

- **Description**: Define a custom `IDomainEventDispatcher` interface rather than depending on MediatR's `IMediator.Publish`. Resolve handlers from DI using reflection or generic type wrappers.
- **Evidence**: .NET ecosystem (Mechanism 7), codebase analysis (ADR specifies mediator-agnostic CQRS)
- **Prevalence**: Less common than MediatR but aligned with YAF's explicit no-external-dependency stance
- **Quality**: Straightforward implementation; Milan Jovanovic provides a complete reference

### Anti-Pattern: AsyncLocal/Ambient Context for Domain Events

- **Description**: Using `AsyncLocal<List<IDomainEvent>>` as a static ambient collector that aggregates call into directly.
- **Evidence**: DDD patterns (Pattern 4), .NET ecosystem (Mechanism 3), External approaches (Pattern 4)
- **Prevalence**: Documented but not widely adopted for domain events
- **Quality**: All three sources flag concerns -- static coupling, testability issues, hidden dependency, potential violation of zero-dep domain. Rated 60-70% confidence.

## Key Insights

### Insight 1: The "Lost Reference" Problem Has a Single Structural Solution

**Description**: The fundamental challenge is that the aggregate reference (which carries events) is "lost" when the repository converts it to a memento for EF Core. Every compatible solution works by maintaining an additional reference to the aggregate somewhere in the infrastructure layer's scoped lifetime.

**Supporting Evidence**: DDD patterns explicitly names this the "lost reference" problem and states: "Every compatible pattern solves this by maintaining an additional reference to the aggregate." The codebase analysis identifies the same gap: "The aggregate root reference may or may not be retained by the repository implementation."

**Implications**: There is no way to avoid holding aggregate references in infrastructure. The only question is where (tracker service, repository, UoW) and how (explicit registration, base class automation, decorator interception).

**Confidence**: High (95%)

### Insight 2: YAF's Existing API Needs Zero Changes

**Description**: The domain layer's current `AddDomainEvent()` / `DomainEvents` / `ClearDomainEvents()` API on `AggregateRoot<TId>` is exactly what the recommended approach needs. No domain-layer modifications are required.

**Supporting Evidence**: DDD patterns states: "Current AddDomainEvent() / DomainEvents / ClearDomainEvents() API unchanged." The codebase analysis confirms these are already public/protected with the right visibility. The .NET ecosystem's generic repository base directly uses `aggregate.DomainEvents` and `aggregate.ClearDomainEvents()`.

**Implications**: Implementation can focus entirely on the infrastructure layer. The domain layer is complete for this feature.

**Confidence**: High (100%)

### Insight 3: The Composition of Three Mechanisms is the Optimal Design

**Description**: The optimal solution composes: (1) scoped aggregate tracker for collection, (2) generic repository base for automation, (3) custom dispatcher for mediator-agnostic dispatch. Each solves one concern; together they solve the full problem.

**Supporting Evidence**: The .NET ecosystem explicitly recommends this three-layer composition: "Collection Layer (Mechanism 2 + 4) + Dispatch Layer (Mechanism 7) + Delivery Layer (Mechanism 8, optional)." DDD patterns converges on the same combination. External approaches confirms the layering.

**Implications**: The design is modular -- each component can be implemented and tested independently. The outbox pattern can be added later as an orthogonal reliability concern.

**Confidence**: High (90%)

### Insight 4: Cross-Cutting Patterns Are NOT a Precedent for Event Collection

**Description**: YAF's existing cross-cutting concern handling (timestamps, accountability) operates on mementos at the DbContext level. Domain events operate on aggregates. These are fundamentally different mechanisms despite both being "cross-cutting."

**Supporting Evidence**: The codebase analysis states: "Cross-cutting concerns (timestamps, accountability) are handled at the memento/DbContext level, not the aggregate level -- so they are NOT a precedent for event collection." Timestamps are set via `SaveChanges` interception on mementos. Events must be harvested from aggregates before memento conversion.

**Implications**: Do not attempt to reuse the MementoHelper or SaveChanges interception pattern for event collection. A separate tracking mechanism is needed.

**Confidence**: High (100%)

### Insight 5: Edge Cases Are Well-Understood and Handleable

**Description**: The aggregate tracker pattern handles all common edge cases without special logic.

**Supporting Evidence**: DDD patterns documents: aggregate loaded but not modified (events collection empty, harmless), new aggregate not loaded (repository's AddAsync also tracks), same aggregate loaded multiple times (deduplicate by identity or simply store references), multiple aggregates per UoW (tracker maintains a set).

**Implications**: The implementation is straightforward with no known edge-case pitfalls.

**Confidence**: High (85%)

## Relationships and Dependencies

### Component Relationship Map

```
Domain Layer (NO CHANGES):
  AggregateRoot<TId>
    .AddDomainEvent(event)     -- protected, aggregate raises events
    .DomainEvents              -- public read-only, infrastructure reads
    .ClearDomainEvents()       -- public, infrastructure calls after dispatch

Application Layer:
  IDomainEventHandler<TEvent>  -- handler interface (per ADR)
  Context providers            -- populate event envelope at dispatch time

Infrastructure Layer (NEW COMPONENTS):
  IAggregateTracker            -- scoped service, tracks aggregate references
    .Track(aggregate)          -- called by repository base
    .GetTrackedAggregates()    -- called by UoW at commit time

  MementoRepository<TAggregate, TId, TMemento>  -- abstract base class
    .GetByIdAsync()            -- load memento, restore aggregate, Track()
    .AddAsync()                -- snapshot memento, persist, Track()
    .SaveAsync()               -- snapshot memento, update, Track()

  IDomainEventDispatcher       -- dispatches events to handlers
    .DispatchAsync(events)     -- resolves handlers from DI

  UnitOfWork : IUnitOfWork     -- orchestrates the lifecycle
    .CommitAsync():
      1. SaveChangesAsync()    -- flush mementos to DB
      2. tracker.GetTrackedAggregates()
      3. harvest DomainEvents from each
      4. dispatcher.DispatchAsync(events)
      5. ClearDomainEvents() on each aggregate
      6. CommitTransactionAsync()
```

### Data Flow

```
1. Command handler calls repository.GetByIdAsync(id)
2. Repository loads memento from DbContext
3. Repository calls TAggregate.Restore(memento) to create domain object
4. Repository calls tracker.Track(aggregate)  <-- BRIDGE POINT
5. Repository returns aggregate to command handler
6. Command handler calls domain methods (events accumulate on aggregate._domainEvents)
7. Command handler calls repository.SaveAsync(aggregate)
8. Repository calls aggregate.Snapshot(memento) to update memento
9. Repository adds/updates memento in DbContext
10. UoW.CommitAsync() begins
11. UoW calls DbContext.SaveChangesAsync() -- mementos flushed to DB
12. UoW calls tracker.GetTrackedAggregates()
13. UoW harvests DomainEvents from each tracked aggregate
14. UoW calls dispatcher.DispatchAsync(events) -- handlers execute within transaction
15. UoW calls ClearDomainEvents() on each aggregate
16. UoW calls transaction.CommitAsync()
```

### Integration Points

- **Repository <-> Tracker**: Every repository method that touches an aggregate calls `Track()`. This is the key integration point, automated by the base class.
- **UoW <-> Tracker**: The UoW reads tracked aggregates at commit time. Single method call.
- **UoW <-> Dispatcher**: The UoW passes harvested events to the dispatcher. Single method call.
- **Dispatcher <-> DI Container**: The dispatcher resolves `IDomainEventHandler<TEvent>` implementations from the service provider.
- **UoW <-> DbContext**: Standard EF Core SaveChanges + transaction management.

## Gaps and Uncertainties

### Gap 1: Recursive/Cascading Event Handling

**Description**: If a domain event handler modifies another aggregate (which raises more events), should those secondary events also be dispatched? The ADR is silent on this.

**Impact**: Medium -- affects UoW dispatch loop design. Must decide between single-pass dispatch and iterative dispatch (loop until no new events).

**Recommendation**: Document this as an open design question. The tracker pattern supports both approaches -- just re-check tracked aggregates after each dispatch pass.

### Gap 2: Event Ordering Guarantees

**Description**: When multiple aggregates raise events in a single UoW, what is the dispatch order? Per-aggregate insertion order? Global order across aggregates?

**Impact**: Low-Medium -- most handlers should be independent, but ordering may matter for audit logs or projections.

**Recommendation**: Define as per-aggregate insertion order (which `AggregateRoot` already preserves), with aggregate ordering matching registration order. Document this guarantee.

### Gap 3: Failed Handler Recovery

**Description**: The ADR states handler failure causes transaction rollback. But what about partial handler execution -- if handler 2 of 5 fails, do handlers 1's side effects get rolled back?

**Impact**: Medium -- depends on whether handlers have non-transactional side effects (HTTP calls, file writes).

**Recommendation**: This is outside the scope of event collection but should be addressed in the dispatcher/handler design. In-process handlers within a transaction naturally get rolled back for database side effects.

### Gap 4: Integration Event Conversion

**Description**: The ADR mentions integration events (cross-boundary) as separate from domain events. The collection mechanism collects domain events. How and when are integration events produced?

**Impact**: Low for this research -- integration events are a separate concern. The scoped collector pattern can be extended to collect integration events from handlers.

### Gap 5: No Existing Infrastructure Code

**Description**: The infrastructure layer does not yet exist. All patterns are evaluated against ADR specifications and domain-layer code, not against actual repository or UoW implementations.

**Impact**: Low -- the domain layer API is stable and all patterns are designed to work with it. Implementation will require design decisions about EF Core configuration, DbContext structure, and DI registration, but these do not affect the event collection pattern choice.

## Synthesis by Framework

### Technical Research Framework

**Component Analysis**:
- **What exists**: `AggregateRoot<TId>` with full event API (`AddDomainEvent`, `DomainEvents`, `ClearDomainEvents`). Memento infrastructure (`IMemento`, `MementoHelper`, memento base classes). ADRs specifying dispatch lifecycle.
- **What is missing**: `IAggregateTracker` (or equivalent), generic repository base, `IDomainEventDispatcher`, UoW event dispatch logic.
- **How it should be structured**: Three new infrastructure components (tracker, repository base, dispatcher) plus UoW dispatch integration.

**Pattern Analysis**:
- **Dominant pattern**: Repository-as-registrar with scoped aggregate tracking (4/4 sources recommend)
- **Consistency**: High -- all compatible approaches share the same structural insight
- **Maturity**: Established -- pattern is used in production by ABP Framework, Kamil Grzybek's Modular Monolith, and Marten-based systems

**Flow Analysis**:
- **Data flow**: Aggregate -> (tracked by) -> Repository -> (memento to) -> EF Core; events flow: Aggregate -> (harvested by) -> UoW -> (dispatched via) -> Dispatcher -> Handlers
- **Error flow**: Handler failure -> UoW catches -> transaction rollback -> events not cleared

### Literature Research Framework

**Current State Analysis**:
- **Standard approach**: EF Core ChangeTracker scanning (eShopOnContainers pattern)
- **Weakness**: Incompatible with separated domain/persistence models
- **Gap**: No widely-documented reference architecture for memento-separated event collection

**Best Practices Comparison**:
- Spring Data's `@DomainEvents` -- repository as collection point (convention-based)
- ABP Framework's MongoDB handling -- repository triggers event publication directly
- Marten's explicit event appending -- aggregate tracks uncommitted events, repository forwards them
- All confirm: when the ORM does not track domain objects, the repository fills the gap

**Applicability Assessment**:
- Repository-as-registrar: directly applicable, no adaptation needed
- Generic base class: directly applicable, aligns with YAF's CRTP patterns
- Custom dispatcher: directly applicable, aligns with mediator-agnostic ADR
- Outbox pattern: applicable as future enhancement, orthogonal to collection

## Conclusions

### Primary Conclusions

1. **The aggregate tracker pattern is the clear solution** (Confidence: High, 95%). Four independent research streams converge on the same answer: a scoped service that tracks aggregate references, used by a generic repository base class, with the UoW harvesting events at commit time. No alternative comes close in combined compatibility, automation, and simplicity.

2. **No domain-layer changes are required** (Confidence: High, 100%). The existing `AggregateRoot<TId>` API is complete. All new components live in the infrastructure layer.

3. **The standard .NET approach (ChangeTracker scanning) is definitively incompatible** (Confidence: High, 100%). This is not a matter of adaptation -- the fundamental premise (ORM tracks domain objects) does not hold with memento separation.

### Secondary Conclusions

4. **The base class approach is preferable to the decorator approach** for YAF's greenfield infrastructure. Both achieve automation; the base class is simpler and aligns with YAF's existing abstract base class patterns.

5. **The outbox pattern is an orthogonal future enhancement** that does not affect the collection pattern choice. It can be layered on top of any collection mechanism.

6. **Event ordering and cascading dispatch are open design questions** that should be addressed during implementation but do not affect the fundamental architecture.

### Recommendations

1. **Implement the three-component composition**: `IAggregateTracker` (scoped) + `MementoRepository<TAggregate, TId, TMemento>` (abstract base) + `IDomainEventDispatcher` (custom, mediator-agnostic).

2. **Place all new interfaces in the infrastructure layer** -- the domain layer already provides everything needed via `AggregateRoot<TId>.DomainEvents` and `ClearDomainEvents()`.

3. **Document the dispatch lifecycle and ordering guarantees** in an ADR amendment or new ADR, addressing cascading events and multi-aggregate ordering.

4. **Consider the outbox pattern as a separate future work item** for reliable event delivery in distributed scenarios.
