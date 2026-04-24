# DDD Patterns: Domain Event Collection with Aggregate/Persistence Separation

## Research Focus

How DDD frameworks and reference architectures handle domain event collection when domain objects are NOT directly ORM-tracked -- specifically applicable to YAF's memento-based separation where EF Core only sees mementos, not aggregate roots.

---

## Pattern 1: Repository-as-Event-Registrar (Aggregate Tracker via UoW)

### How It Works

The repository, upon loading or saving an aggregate, registers that aggregate with a scoped "aggregate tracker" or "event source registry" maintained by the Unit of Work. Before commit, the UoW iterates all registered aggregates, harvests their `DomainEvents` collections, and dispatches them.

**Flow:**
1. Application service calls `repository.GetById(id)` -- repository loads memento, hydrates aggregate, **registers aggregate with UoW**
2. Application service calls domain methods on the aggregate (events accumulate)
3. Application service calls `repository.Save(aggregate)` -- repository extracts memento, persists via EF Core, **re-registers or confirms aggregate**
4. UoW `CommitAsync()` -- before/after `SaveChanges`, UoW iterates registered aggregates, harvests `DomainEvents`, dispatches them, then calls `ClearDomainEvents()`

**Sketch:**
```csharp
// Infrastructure: scoped service
public interface IAggregateTracker
{
    void Track(IAggregateRoot aggregate);
    IReadOnlyCollection<IDomainEvent> HarvestEvents();
}

// Repository base class
public abstract class Repository<TAggregate, TMemento> 
    where TAggregate : AggregateRoot<..., TMemento>
{
    private readonly IAggregateTracker _tracker;
    
    public async Task<TAggregate> GetByIdAsync(Guid id)
    {
        var memento = await _dbContext.Set<TMemento>().FindAsync(id);
        var aggregate = TAggregate.Restore(memento);
        _tracker.Track(aggregate);  // <-- registration
        return aggregate;
    }
    
    public void Save(TAggregate aggregate)
    {
        var memento = aggregate.Snapshot();
        _dbContext.Set<TMemento>().Update(memento);
        _tracker.Track(aggregate);  // <-- ensure tracked
    }
}

// UoW dispatches before commit
public class UnitOfWork : IUnitOfWork
{
    private readonly IAggregateTracker _tracker;
    private readonly IDomainEventDispatcher _dispatcher;
    
    public async Task CommitAsync()
    {
        var events = _tracker.HarvestEvents(); // gets + clears
        await _dispatcher.DispatchAsync(events);
        await _dbContext.SaveChangesAsync();
    }
}
```

### Source References

- **Kamil Grzybek's Modular Monolith**: Uses `UnitOfWorkCommandHandlerDecorator` that dispatches all domain events as part of the UoW commit. The UoW accesses aggregate event collections during the save/commit phase.
  - Source: [Kamil Grzybek - How to publish and handle Domain Events](https://www.kamilgrzybek.com/blog/posts/how-to-publish-handle-domain-events)
  - Source: [GitHub - modular-monolith-with-ddd](https://github.com/kgrzybek/modular-monolith-with-ddd)

- **Two-Step Event Publication Pattern**: Aggregate root creates and registers domain events; when the application service persists via repository, infrastructure publishes events only if save succeeds.
  - Source: [DDD Aggregate Roots and Domain Events Publication](https://paucls.wordpress.com/2018/05/31/ddd-aggregate-roots-and-domain-events-publication/)

- **SapiensWorks UoW Pattern**: Repositories share the UoW and register aggregates with it. The UoW coordinates persistence and event dispatch across all participating repositories.
  - Source: [SapiensWorks - DDD Persisting Aggregate Roots in a Unit of Work](https://blog.sapiensworks.com/post/2013/05/01/DDD-Persisting-Aggregate-Roots-In-A-Unit-Of-Work.aspx)

### Pros
- **Works perfectly with memento separation** -- the repository holds the aggregate reference and registers it; the UoW never needs to look at the ChangeTracker
- **Zero manual effort per repository** -- can be automated in a generic `Repository<T, TMemento>` base class
- **Testable** -- `IAggregateTracker` is a simple scoped service, easily mockable
- **Explicit** -- clear registration points, easy to trace
- **Zero domain layer impact** -- domain layer already has `DomainEvents` / `ClearDomainEvents`; no changes needed

### Cons
- **Requires scoped lifetime management** -- the tracker must be request-scoped so aggregates from multiple repositories are collected
- **Aggregate lifecycle management** -- must handle edge cases: aggregate loaded but not modified, aggregate created (not loaded), multiple aggregates of same type
- **Additional infrastructure type** -- one new interface + implementation (`IAggregateTracker`)

### Compatibility with YAF
- **Memento separation**: FULLY COMPATIBLE -- does not depend on ORM tracking domain objects
- **Zero-dep domain**: COMPATIBLE -- no new dependencies in domain layer
- **Explicit dispatch**: COMPATIBLE -- dispatch point is explicit in UoW.CommitAsync
- **Confidence**: High (90%) -- this is a well-established pattern adapted for memento separation

---

## Pattern 2: EF Core ChangeTracker Scanning (Standard eShop Pattern)

### How It Works

This is the most common .NET pattern, used by Microsoft's eShop reference architecture. The `DbContext.SaveEntitiesAsync()` method scans EF Core's `ChangeTracker` for all tracked entities that implement a domain event interface, harvests events, and dispatches them via MediatR before calling `base.SaveChangesAsync()`.

**Flow:**
1. Entity base class has `AddDomainEvent()` / `DomainEvents` / `ClearDomainEvents()`
2. EF Core directly tracks domain entities (entities ARE the persistence model)
3. In `SaveEntitiesAsync()`, scan `ChangeTracker.Entries()` for entities with events
4. Dispatch all events via mediator
5. Call `base.SaveChangesAsync()`

**Code from eShop:**
```csharp
public class OrderingContext : DbContext, IUnitOfWork
{
    public async Task<bool> SaveEntitiesAsync(CancellationToken ct)
    {
        // Dispatch Domain Events collection
        await _mediator.DispatchDomainEventsAsync(this);
        var result = await base.SaveChangesAsync(ct);
        return true;
    }
}

// Extension method
static class MediatorExtension
{
    public static async Task DispatchDomainEventsAsync(
        this IMediator mediator, DbContext ctx)
    {
        var domainEntities = ctx.ChangeTracker
            .Entries<Entity>()
            .Where(x => x.Entity.DomainEvents?.Any() == true)
            .ToList();

        var domainEvents = domainEntities
            .SelectMany(x => x.Entity.DomainEvents)
            .ToList();

        domainEntities.ForEach(e => e.Entity.ClearDomainEvents());

        foreach (var domainEvent in domainEvents)
            await mediator.Publish(domainEvent);
    }
}
```

### Source References
- Source: [Microsoft Learn - Domain Events Design and Implementation](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation)
- Source: [Cesar de la Torre - Using Domain Events within a .NET Core Microservice](https://devblogs.microsoft.com/cesardelatorre/using-domain-events-within-a-net-core-microservice/)

### Pros
- **Zero-configuration** -- no explicit registration needed; ChangeTracker auto-discovers
- **Well-documented** -- official Microsoft reference architecture pattern
- **Simple** -- minimal infrastructure code

### Cons
- **INCOMPATIBLE with memento separation** -- ChangeTracker only tracks EF entities (mementos in YAF's case); mementos do NOT carry domain events
- **Couples domain to EF** -- requires domain entities to be EF-tracked
- **MediatR dependency** -- typically relies on INotification / IMediator

### Compatibility with YAF
- **Memento separation**: INCOMPATIBLE -- this is precisely the pattern that breaks with memento separation
- **This pattern exists here as a negative example** -- to document WHY YAF cannot use the standard approach and needs an alternative
- **Confidence**: High (100%) -- well-understood, clearly incompatible

---

## Pattern 3: Scoped Domain Event Collector Service (Injected Collector)

### How It Works

A scoped `IDomainEventCollector` service is registered in DI. Repositories (or application services) explicitly push events from aggregates into the collector. The UoW dispatches collected events at commit time.

**Flow:**
1. `IDomainEventCollector` is registered as scoped in DI
2. After aggregate operations, the repository (or app service) calls `collector.CollectFrom(aggregate)`
3. At commit time, the UoW (or a pipeline behavior/middleware) dispatches all collected events

**Sketch:**
```csharp
public interface IDomainEventCollector
{
    void CollectFrom(IAggregateRoot aggregate);
    void Add(IDomainEvent domainEvent);
    IReadOnlyCollection<IDomainEvent> Flush();
}

// Usage in repository
public class OrderRepository : IOrderRepository
{
    private readonly IDomainEventCollector _collector;
    
    public async Task SaveAsync(Order order)
    {
        var memento = order.Snapshot();
        _dbContext.Set<OrderMemento>().Update(memento);
        _collector.CollectFrom(order); // harvest events from aggregate
    }
}
```

### Source References

- **Kamil Grzybek's pattern**: The publisher accesses aggregate `Events` collections during save/commit. The publisher resolves notification handlers via IoC using reflection.
  - Source: [Kamil Grzybek - How to publish and handle Domain Events](https://www.kamilgrzybek.com/blog/posts/how-to-publish-handle-domain-events)

- **General DDD pattern**: Application service orchestrates: load aggregate, invoke domain methods, persist via repository, publish events.
  - Source: [DDD Aggregate Roots and Domain Events Publication](https://paucls.wordpress.com/2018/05/31/ddd-aggregate-roots-and-domain-events-publication/)

### Pros
- **Works with memento separation** -- events collected from aggregate objects, not from ChangeTracker
- **Flexible** -- collector can receive events from any source (aggregates, services, manual adds)
- **Testable** -- simple scoped service, easy to mock
- **Explicit collection points** -- clear where events are harvested

### Cons
- **Requires manual call per repository** -- each repository must call `collector.CollectFrom(aggregate)` in its Save method (though this can be automated in a base class)
- **Risk of forgetting** -- if a repository author forgets to collect, events are silently lost
- **Slightly different from tracker pattern** -- collector receives events (copies them out), tracker holds references to aggregates

### Compatibility with YAF
- **Memento separation**: FULLY COMPATIBLE
- **Zero-dep domain**: COMPATIBLE -- collector lives in infrastructure
- **Explicit dispatch**: COMPATIBLE
- **Confidence**: High (85%) -- well-established, minor risk of developer error mitigated by base class

---

## Pattern 4: Static AsyncLocal Domain Event Tracker

### How It Works

A static class uses `AsyncLocal<List<IDomainEvent>>` to store domain events scoped to the current async execution context. Aggregates call `DomainEventTracker.Raise(event)` directly. A middleware/behavior initializes the scope and dispatches events after the operation completes.

**Flow:**
1. Middleware/behavior creates a scope: `DomainEventTracker.CreateScope()`
2. Aggregate domain methods call `DomainEventTracker.Raise(new OrderPlaced(...))`
3. After persistence, middleware calls `DomainEventTracker.GetDomainEvents()` and dispatches them
4. Scope is disposed, events are cleaned up

**Code (from Ken van Grinsven):**
```csharp
internal static class DomainEventTracker
{
    private static readonly AsyncLocal<List<IDomainEvent>> _domainEvents = new();
    
    public static void Raise(IDomainEvent domainEvent)
    {
        _domainEvents.Value ??= [];
        _domainEvents.Value.Add(domainEvent);
    }
    
    public static IReadOnlyCollection<IDomainEvent> GetDomainEvents()
    {
        return _domainEvents.Value ?? [];
    }
}
```

### Source References
- Source: [Ken van Grinsven - Minimal Impact Domain Events](https://medium.com/@kenvgrinsven/minimal-impact-domain-events-313deb1af20e)

### Pros
- **Zero infrastructure coupling in aggregates** -- no base class, no interface, no injected service
- **Works with any persistence strategy** -- completely decoupled from ORM
- **Aggregate purity** -- aggregate never exposes an event collection publicly
- **Request-scoped automatically** via AsyncLocal

### Cons
- **Static/ambient context anti-pattern** -- harder to test, hidden dependency, violates explicit dependency principle
- **Domain layer references infrastructure type** -- aggregate must call `DomainEventTracker.Raise()`, which means the domain layer depends on this static class (unless it's IN the domain layer, which adds infrastructure concerns there)
- **Thread safety concerns** -- AsyncLocal flows DOWN the call stack but not UP; nested parallelism could cause issues
- **Debugging difficulty** -- events appear "magically" without explicit collection points

### Compatibility with YAF
- **Memento separation**: COMPATIBLE -- does not depend on ORM tracking
- **Zero-dep domain**: POTENTIALLY INCOMPATIBLE -- if `DomainEventTracker` is in infrastructure, domain must reference it; if in domain, domain gets infrastructure responsibility
- **Explicit dispatch**: PARTIALLY COMPATIBLE -- dispatch is explicit in middleware, but event raising is implicit/ambient
- **Confidence**: Medium (60%) -- works but violates YAF's explicit dependency and zero-dep domain principles

---

## Pattern 5: Return Events from Domain Methods (Functional/Explicit Pattern)

### How It Works

Instead of accumulating events in a collection on the aggregate, domain methods return the events they produce. The application service or command handler collects these return values and dispatches them.

**Flow:**
1. Domain method returns events: `var events = order.Confirm()`
2. Application service collects returned events
3. Repository persists the aggregate (memento)
4. Application service dispatches collected events

**Sketch (inspired by Wolverine cascading messages):**
```csharp
// Domain method returns events
public class Order : AggregateRoot<OrderId, Order, OrderMemento>
{
    public IReadOnlyCollection<IDomainEvent> Confirm()
    {
        // business logic...
        Status = OrderStatus.Confirmed;
        return [new OrderConfirmed(Id, ConfirmedAt)];
    }
}

// Application service
public async Task Handle(ConfirmOrderCommand cmd)
{
    var order = await _repo.GetByIdAsync(cmd.OrderId);
    var events = order.Confirm();
    await _repo.SaveAsync(order);
    await _eventDispatcher.DispatchAsync(events);
}
```

### Source References
- **Wolverine Cascading Messages**: Domain methods return message objects directly; Wolverine handlers return these as cascading messages.
  - Source: [Jeremy D. Miller - Classic .NET Domain Events with Wolverine and EF Core](https://jeremydmiller.com/2025/12/04/classic-net-domain-events-with-wolverine-and-ef-core/)

- **Eventuous Framework**: Separates state from behavior; aggregate operations produce events that are then applied to state.
  - Source: [Eventuous Documentation](https://eventuous.dev/docs/domain/aggregate/)

### Pros
- **Maximally explicit** -- every event is visible in the call chain
- **No hidden state** -- no internal event collection, no ambient context
- **Perfectly testable** -- assert on return values
- **Works with any persistence** -- completely ORM-independent

### Cons
- **Manual collection burden** -- application service must collect events from every domain method call
- **Awkward with multiple operations** -- if an aggregate method triggers multiple internal operations that each produce events, the return type becomes complex
- **Breaks current YAF pattern** -- YAF already uses `AddDomainEvent()` + `DomainEvents` collection on `AggregateRoot`; switching to return-based would be a significant API change
- **Not composable** -- hard to accumulate events across multiple method calls on the same aggregate

### Compatibility with YAF
- **Memento separation**: COMPATIBLE
- **Zero-dep domain**: COMPATIBLE
- **Explicit dispatch**: FULLY COMPATIBLE
- **Current API compatibility**: INCOMPATIBLE -- requires changing AggregateRoot's event API
- **Confidence**: Medium (50%) -- viable pattern but requires breaking API change

---

## Pattern 6: Wolverine/Framework-Integrated ChangeTracker Scraping

### How It Works

Wolverine 5.6+ provides a `PublishDomainEventsFromEntityFrameworkCore<TEntity>()` configuration that automatically scrapes events from EF Core's ChangeTracker. The framework knows how to find entities implementing a base type and extract their events.

**Code:**
```csharp
opts.PublishDomainEventsFromEntityFrameworkCore<Entity>(x => x.Events);
```

### Source References
- Source: [Wolverine - Publishing Domain Events](https://wolverinefx.io/guide/durability/efcore/domain-events)
- Source: [Jeremy D. Miller - Classic .NET Domain Events with Wolverine](https://jeremydmiller.com/2025/12/04/classic-net-domain-events-with-wolverine-and-ef-core/)

### Compatibility with YAF
- **Memento separation**: INCOMPATIBLE -- same problem as Pattern 2; relies on ChangeTracker which only sees mementos
- **Included for completeness** -- demonstrates that even modern frameworks default to ChangeTracker scanning, confirming YAF needs a custom approach

---

## Pattern 7: NHibernate Event Listener (ORM Post-Operation Hooks)

### How It Works

Vladimir Khorikov's approach uses NHibernate's event listeners (`IPostInsertEventListener`, `IPostDeleteEventListener`, `IPostUpdateEventListener`). These fire after successful persistence and receive the persisted entity, from which events are extracted.

**Key mechanism:** The ORM passes the entity reference to the event listener callback, so the listener can cast to `AggregateRoot` and harvest events.

### Source References
- Source: [Vladimir Khorikov - Domain Events: Simple and Reliable Solution](https://enterprisecraftsmanship.com/posts/domain-events-simple-reliable-solution/)

### Compatibility with YAF
- **Memento separation**: INCOMPATIBLE -- NHibernate listeners receive the entity being persisted (which in YAF's case is the memento, not the aggregate)
- **Included as reference** -- demonstrates the ORM-callback approach and why it doesn't work with memento separation

---

## Pattern 8: Dual-Layer Repository with Event Bridge (TheCodeWrapper Pattern)

### How It Works

A two-tier repository structure where:
- Inner `DataEntityRepository<T>` manages EF-tracked data entities
- Outer `EFRepository<T, M>` wraps the inner repo, mapping between domain aggregates (T) and data entities (M)

The domain aggregate implements `GetUncommittedEvents()` / `ClearUncommittedEvents()`. The outer repository harvests events from the aggregate before/after delegating persistence to the inner repository.

**Flow:**
1. Outer repository loads data entity via inner repo, maps to domain aggregate
2. Application service operates on domain aggregate (events accumulate)
3. Outer repository maps aggregate back to data entity, persists via inner repo
4. Events are harvested from aggregate and dispatched
5. `ClearUncommittedEvents()` called on aggregate

### Source References
- Source: [TheCodeWrapper - EF Core: Effectively Decouple the Data and Domain Model](https://dev.to/thecodewrapper/ef-core-effectively-decouple-the-data-and-domain-model-4h8j)

### Pros
- **Works with memento separation** -- repository holds aggregate reference throughout
- **Clean layering** -- data entities never leave infrastructure layer
- **Event collection at repository level** -- natural point for harvesting

### Cons
- **Each repository must implement event harvesting** -- though automatable in base class
- **AutoMapper dependency** -- original pattern uses AutoMapper for bidirectional mapping (YAF uses memento pattern instead, which is equivalent)

### Compatibility with YAF
- **Memento separation**: FULLY COMPATIBLE -- this pattern IS a memento separation pattern
- **Zero-dep domain**: COMPATIBLE
- **Confidence**: High (85%) -- directly applicable to YAF's architecture

---

## Comparative Analysis

| Pattern | Memento Compatible | Zero-Dep Domain | Automation Level | Developer Effort | Testability |
|---------|-------------------|-----------------|------------------|-----------------|-------------|
| 1. Repository-as-Registrar (Aggregate Tracker) | YES | YES | High (base class) | One-time setup | High |
| 2. ChangeTracker Scanning (eShop) | **NO** | Partial | Automatic | Zero | High |
| 3. Scoped Event Collector | YES | YES | Medium (base class) | Per-repo call (automatable) | High |
| 4. AsyncLocal Static Tracker | YES | **NO** (static dep) | High | Zero per-repo | Low |
| 5. Return Events from Methods | YES | YES | None | Per-method | Very High |
| 6. Wolverine ChangeTracker | **NO** | Partial | Automatic | Zero | Medium |
| 7. NHibernate Listeners | **NO** | Partial | Automatic | Zero | Medium |
| 8. Dual-Layer Repository | YES | YES | Medium (base class) | Per-repo (automatable) | High |

### Patterns Compatible with Memento Separation

Only patterns **1, 3, 4, 5, and 8** are compatible with YAF's memento-based architecture. Of these:

- **Pattern 1 (Aggregate Tracker via UoW)** and **Pattern 3 (Scoped Collector)** are very similar -- the difference is whether the UoW holds aggregate references (tracker) or event copies (collector). Pattern 1 is slightly more elegant because it defers event harvesting to the last moment.

- **Pattern 8 (Dual-Layer Repository)** is essentially the same as Pattern 1/3 but viewed from the repository's perspective -- the repository is the natural location for bridging aggregates and mementos.

- **Pattern 4 (AsyncLocal)** works but violates explicit dependency principles.

- **Pattern 5 (Return Events)** is clean but incompatible with YAF's current `AddDomainEvent()` API.

---

## Recommended Approach for YAF

**Pattern 1 (Repository-as-Event-Registrar / Aggregate Tracker)** is the strongest fit, potentially combined with elements of Pattern 8.

### Rationale

1. **The repository already holds both the aggregate and memento** -- it is the natural bridge point. When the repository hydrates an aggregate from a memento, it holds the aggregate reference. When it persists, it snapshots the memento from the aggregate. At both points, it can register the aggregate with a tracker.

2. **Automatable in a generic base class** -- a `Repository<TAggregate, TId, TMemento>` base class can implement `Track(aggregate)` calls in `GetByIdAsync()` and `SaveAsync()`, requiring zero manual effort per concrete repository.

3. **Scoped `IAggregateTracker` is simple** -- one interface, one implementation, registered as scoped. The UoW accesses it at commit time to harvest events.

4. **Preserves all YAF constraints:**
   - Zero-dependency domain layer (tracker is in infrastructure)
   - Memento pattern intact (aggregate is tracked, not memento)
   - Explicit dispatch (happens in UoW.CommitAsync before/after SaveChanges)
   - Current `AddDomainEvent()` / `DomainEvents` / `ClearDomainEvents()` API unchanged

5. **Edge cases handled:**
   - Multiple aggregates per UoW: tracker maintains a set of all tracked aggregates
   - Aggregate loaded but not modified: events collection will be empty, harmless
   - New aggregate (not loaded): repository's `AddAsync()` method also tracks
   - Same aggregate loaded multiple times: tracker uses identity (ID) to deduplicate or simply stores references

### Implementation Sketch

```
Domain Layer (unchanged):
  AggregateRoot.AddDomainEvent(event)
  AggregateRoot.DomainEvents
  AggregateRoot.ClearDomainEvents()

Infrastructure Layer (new):
  IAggregateTracker (scoped)
    .Track(IAggregateRoot aggregate)
    .GetTrackedAggregates() -> IReadOnlyCollection<IAggregateRoot>
  
  AggregateTracker : IAggregateTracker (implementation)
  
  Repository<TAggregate, TId, TMemento> (base class)
    GetByIdAsync() -> hydrate + Track()
    SaveAsync() -> Snapshot() + persist + Track()
    AddAsync() -> persist + Track()
  
  UnitOfWork : IUnitOfWork
    CommitAsync():
      1. Get tracked aggregates from IAggregateTracker
      2. Harvest DomainEvents from each
      3. Dispatch events (before or after SaveChanges per ADR)
      4. ClearDomainEvents on each aggregate
      5. SaveChangesAsync / CommitTransactionAsync
```

---

## Key Insight: The "Lost Reference" Problem

The fundamental challenge with memento separation is what can be called the **"lost reference" problem**:

1. The application service works with **aggregate objects** (which carry events)
2. The repository converts aggregates to **mementos** for persistence
3. EF Core only tracks **mementos** (which don't carry events)
4. At commit time, the only objects visible to the infrastructure are **mementos**
5. The aggregate references (and their events) are "lost" -- they exist only as local variables in the application service

**Every compatible pattern solves this by maintaining an additional reference to the aggregate** somewhere in the infrastructure layer's scoped lifetime -- whether in a tracker service (Pattern 1), a collector service (Pattern 3), the repository itself (Pattern 8), or an ambient context (Pattern 4).

---

## Sources

- [Microsoft Learn - Domain Events Design and Implementation](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation)
- [Jimmy Bogard - A Better Domain Events Pattern](https://lostechies.com/jimmybogard/2014/05/13/a-better-domain-events-pattern/)
- [Kamil Grzybek - How to Publish and Handle Domain Events](https://www.kamilgrzybek.com/blog/posts/how-to-publish-handle-domain-events)
- [GitHub - Kamil Grzybek Modular Monolith with DDD](https://github.com/kgrzybek/modular-monolith-with-ddd)
- [Vladimir Khorikov - Domain Events: Simple and Reliable Solution](https://enterprisecraftsmanship.com/posts/domain-events-simple-reliable-solution/)
- [Vladimir Khorikov - Having the Domain Model Separate from the Persistence Model](https://enterprisecraftsmanship.com/posts/having-the-domain-model-separate-from-the-persistence-model/)
- [Richard Banks - DDD Entity Framework and the Memento Pattern](https://www.richard-banks.org/2018/08/ddd-entity-framework-and-memento-pattern.html)
- [Ledjon Behluli - Change Tracking while doing DDD (Revisited)](https://www.ledjonbehluli.com/posts/change_tracking_ddd_revisited/)
- [TheCodeWrapper - EF Core: Effectively Decouple the Data and Domain Model](https://dev.to/thecodewrapper/ef-core-effectively-decouple-the-data-and-domain-model-4h8j)
- [Ken van Grinsven - Minimal Impact Domain Events](https://medium.com/@kenvgrinsven/minimal-impact-domain-events-313deb1af20e)
- [DDD Aggregate Roots and Domain Events Publication](https://paucls.wordpress.com/2018/05/31/ddd-aggregate-roots-and-domain-events-publication/)
- [Jeremy D. Miller - Classic .NET Domain Events with Wolverine and EF Core](https://jeremydmiller.com/2025/12/04/classic-net-domain-events-with-wolverine-and-ef-core/)
- [Wolverine - Publishing Domain Events from EF Core](https://wolverinefx.io/guide/durability/efcore/domain-events)
- [SapiensWorks - DDD Persisting Aggregate Roots in a Unit of Work](https://blog.sapiensworks.com/post/2013/05/01/DDD-Persisting-Aggregate-Roots-In-A-Unit-Of-Work.aspx)
- [Milan Jovanovic - How To Use Domain Events](https://www.milanjovanovic.tech/blog/how-to-use-domain-events-to-build-loosely-coupled-systems)
