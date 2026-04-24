# External Approaches: Domain Event Collection with Aggregate/Persistence Separation

## Research Question

What industry-wide patterns exist for automated domain event collection when domain objects are separated from persistence models (i.e., when an ORM's change tracker cannot directly access domain objects)?

---

## Pattern 1: Aggregate Tracker / Identity Map in Unit of Work

### Description

The Unit of Work (UoW) maintains an internal registry of all aggregates that were loaded or created during the current operation. When `SaveChangesAsync` is called, the UoW iterates through tracked aggregates, harvests their domain events, and dispatches them at the appropriate lifecycle point.

This is conceptually Fowler's Identity Map applied to event collection: the UoW knows about every aggregate in scope and can pull events from all of them.

### How It Achieves Automation

The repository's `GetByIdAsync` and `AddAsync` methods register the aggregate with the UoW automatically. The developer never manually registers event sources -- the repository base class handles it:

```csharp
// Conceptual pattern
public abstract class RepositoryBase<TAggregate> where TAggregate : AggregateRoot
{
    private readonly IUnitOfWork _uow;

    public async Task<TAggregate> GetByIdAsync(Guid id)
    {
        var aggregate = /* load via memento, hydrate */;
        _uow.Track(aggregate);  // automatic registration
        return aggregate;
    }

    public void Add(TAggregate aggregate)
    {
        _uow.Track(aggregate);  // automatic registration
        // persist memento
    }
}

public class UnitOfWork : IUnitOfWork
{
    private readonly List<AggregateRoot> _trackedAggregates = new();

    public void Track(AggregateRoot aggregate) => _trackedAggregates.Add(aggregate);

    public async Task CommitAsync()
    {
        // 1. SaveChanges (persist mementos)
        await _dbContext.SaveChangesAsync();
        
        // 2. Harvest events from all tracked aggregates
        var events = _trackedAggregates
            .SelectMany(a => a.DomainEvents)
            .ToList();
        
        // 3. Dispatch events
        foreach (var e in events) await _dispatcher.DispatchAsync(e);
        
        // 4. Clear events
        _trackedAggregates.ForEach(a => a.ClearDomainEvents());
    }
}
```

### Trade-offs

| Dimension | Assessment |
|-----------|------------|
| **Automation level** | High -- once the repository base class calls `Track()`, all concrete repositories get it for free |
| **Developer cognitive load** | Low -- developers work with aggregates normally; tracking is invisible |
| **Testability** | High -- UoW and tracking can be mocked; aggregate events can be inspected directly |
| **Framework coupling** | Low -- no dependency on ORM change tracker; works with any persistence mechanism |
| **Performance** | Minimal overhead -- just maintaining a list of references |

### Real-World Examples

- **eShopOnContainers** (Microsoft): Uses EF Core's ChangeTracker as the identity map, querying tracked entities for domain events during `SaveChangesAsync`. However, this couples to EF Core's entity tracking.
- **ABP Framework**: UoW pattern with automatic event collection from tracked aggregates.
- **Kamil Grzybek's Modular Monolith**: UoW collects events from domain entities before commit.

### Relevance to Memento Separation

This is the most directly applicable pattern for memento-separated architectures. The key insight: **the UoW tracks the domain object reference, not the persistence model**. Even though the repository converts aggregate -> memento for EF Core, the UoW retains a reference to the original aggregate. Events are harvested from the domain object, not the EF entity.

**Source**: [Steven Giesel - Domain events and the Unit of Work pattern](https://steven-giesel.com/blogPost/ae55581a-9722-4735-8d0e-bfcfe4f6ad5a), [Microsoft Learn - Domain events design and implementation](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation)

---

## Pattern 2: Event Harvesting via ORM Change Tracker

### Description

The most common pattern in .NET DDD projects: hook into EF Core's `SaveChanges` pipeline to scan tracked entities for domain events. The `ChangeTracker.Entries<AggregateRoot>()` query finds all entities with pending events.

### How It Achieves Automation

```csharp
// From eShopOnContainers / Milan Jovanovic pattern
public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
{
    var domainEvents = ChangeTracker
        .Entries<Entity>()
        .Where(e => e.Entity.DomainEvents.Any())
        .SelectMany(e => e.Entity.DomainEvents)
        .ToList();

    // Clear events from entities
    ChangeTracker.Entries<Entity>()
        .Where(e => e.Entity.DomainEvents.Any())
        .ToList()
        .ForEach(e => e.Entity.ClearDomainEvents());

    // Dispatch before or after base.SaveChanges
    foreach (var domainEvent in domainEvents)
        await _mediator.Publish(domainEvent, ct);

    return await base.SaveChangesAsync(ct);
}
```

### Trade-offs

| Dimension | Assessment |
|-----------|------------|
| **Automation level** | Fully automatic -- zero per-repository code |
| **Developer cognitive load** | Very low -- completely transparent |
| **Testability** | Medium -- requires EF Core DbContext in tests |
| **Framework coupling** | High -- tightly coupled to EF Core's ChangeTracker |
| **Performance** | Low overhead -- ChangeTracker already tracks entities |

### Relevance to Memento Separation

**This pattern does NOT work with memento separation.** When the repository maps `AggregateRoot -> Memento -> EF Entity`, the ChangeTracker only sees the memento/EF entity, not the domain object. The domain object (which holds events) is not tracked by EF Core.

This is precisely the gap that YAF needs to solve. This pattern is documented here as a contrast -- it represents the "standard" approach that breaks when domain and persistence models are separated.

**Source**: [Microsoft Learn - Domain events design and implementation](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation), [Milan Jovanovic - Building a custom domain events dispatcher](https://www.milanjovanovic.tech/blog/building-a-custom-domain-events-dispatcher-in-dotnet)

---

## Pattern 3: Jimmy Bogard's "Better Domain Events" -- Deferred Collection

### Description

Instead of dispatching events immediately when raised (the static `DomainEvents.Raise()` anti-pattern), events are stored on the entity and dispatched later by infrastructure. The "better" pattern separates raising from dispatching.

### How It Achieves Automation

The ORM (NHibernate in the original, EF Core in modern versions) hooks into post-save events to find entities with pending domain events:

```csharp
// NHibernate version (original pattern)
internal class EventListener : IPostInsertEventListener, IPostUpdateEventListener
{
    public void OnPostUpdate(PostUpdateEvent ev)
    {
        DispatchEvents(ev.Entity as AggregateRoot);
    }

    private void DispatchEvents(AggregateRoot aggregateRoot)
    {
        if (aggregateRoot == null) return;
        foreach (IDomainEvent domainEvent in aggregateRoot.DomainEvents)
            DomainEvents.Dispatch(domainEvent);
        aggregateRoot.ClearEvents();
    }
}
```

### Key Insight

The pattern's core value is the separation: "Separating the two concerns of raising versus dispatching keeps our domain model fully encapsulated without introducing land mines in our model." Events can be dispatched synchronously (same transaction) or asynchronously (persisted as JSON for offline processing).

### Trade-offs

| Dimension | Assessment |
|-----------|------------|
| **Automation level** | Fully automatic via ORM hooks |
| **Developer cognitive load** | Very low |
| **Testability** | High -- events are inspectable on the aggregate before dispatch |
| **Framework coupling** | Medium-High -- requires ORM that tracks domain objects directly |
| **Performance** | Low overhead |

### Relevance to Memento Separation

Like Pattern 2, the original version depends on the ORM tracking the domain object. However, the *principle* (deferred dispatch, events-on-entity) is fully compatible -- it just needs a different collection mechanism than ORM hooks.

**Source**: [Jimmy Bogard - A better domain events pattern](https://lostechies.com/jimmybogard/2014/05/13/a-better-domain-events-pattern/)

---

## Pattern 4: AsyncLocal / Ambient Event Collector (Scoped Service)

### Description

A thread-safe, scoped event collection mechanism using `AsyncLocal<T>`. Events are raised through a static tracker that accumulates them within the current execution context, rather than storing them on the aggregate itself.

### How It Achieves Automation

```csharp
internal static class DomainEventTracker
{
    private static readonly AsyncLocal<List<IDomainEvent>> _domainEvents = new();

    public static void Raise(IDomainEvent domainEvent)
    {
        _domainEvents.Value ??= new List<IDomainEvent>();
        _domainEvents.Value.Add(domainEvent);
    }

    public static IReadOnlyCollection<IDomainEvent> GetDomainEvents()
        => _domainEvents.Value ?? Array.Empty<IDomainEvent>();

    public static void CreateScope()
    {
        _domainEvents.Value = new List<IDomainEvent>();
    }
}
```

Usage flow:
1. Middleware/pipeline behavior calls `CreateScope()` at the start of the request
2. Domain entities call `DomainEventTracker.Raise()` during business operations
3. After save, middleware reads `GetDomainEvents()` and dispatches them
4. Context cleanup happens naturally when execution context exits

### Trade-offs

| Dimension | Assessment |
|-----------|------------|
| **Automation level** | Medium -- aggregates must call `Raise()` on the static tracker instead of `AddDomainEvent()` on self |
| **Developer cognitive load** | Medium -- developers must know about the tracker; slightly non-obvious |
| **Testability** | Low-Medium -- static/ambient state complicates unit testing; requires scope setup in tests |
| **Framework coupling** | Low -- no ORM dependency; works with any persistence |
| **Performance** | Minimal overhead; AsyncLocal is well-optimized in .NET |

### Relevance to Memento Separation

Fully compatible with memento separation because it does not depend on ORM tracking at all. Events flow through the ambient context regardless of how persistence works. However, it introduces a static dependency from the domain layer (or requires the domain to accept an `IEventCollector` dependency), which may violate the zero-dependency domain constraint.

**Source**: [Ken van Grinsven - Minimal impact domain events](https://medium.com/@kenvgrinsven/minimal-impact-domain-events-313deb1af20e)

---

## Pattern 5: Functional / Command-Returns-Events Approach

### Description

Instead of storing events on the aggregate, command handlers return events as their result. The aggregate is a pure function: `(state, command) -> events`. The infrastructure receives the returned events and handles persistence and dispatch.

### How It Achieves Automation

```csharp
// Wolverine-style aggregate handler
[AggregateHandler]
public static IEnumerable<object> Handle(MarkItemReady command, Order order)
{
    if (order.Items.TryGetValue(command.ItemName, out var item))
    {
        item.Ready = true;
        yield return new ItemReady(command.ItemName);
    }

    if (order.IsReadyToShip())
    {
        yield return new OrderReady();
    }
}
```

The framework automatically:
1. Loads the aggregate (via Marten's `FetchForWriting`)
2. Passes it to the handler with the command
3. Collects returned events
4. Appends events to the event stream
5. Commits via `SaveChangesAsync()`

### The Decide/Apply Pattern

This embodies functional event sourcing:
- **Decide**: Pure business logic determines what events should occur (the handler)
- **Apply**: Framework applies events back to aggregate state (projection)

Events never live on the aggregate -- they flow through the handler return value.

### Trade-offs

| Dimension | Assessment |
|-----------|------------|
| **Automation level** | Fully automatic -- framework handles everything after the handler returns |
| **Developer cognitive load** | Medium -- requires different mental model (functional vs. OOP) |
| **Testability** | Very high -- handlers are pure functions; test by asserting returned events |
| **Framework coupling** | High -- requires framework like Wolverine/Axon that supports this pattern |
| **Performance** | Good -- no state tracking overhead |

### Relevance to Memento Separation

Highly compatible. Events never need to be "collected" from the aggregate because they are returned as the command result. The persistence layer receives events directly and can do whatever it needs (persist memento, store events, dispatch). The aggregate's internal state is irrelevant to event collection.

However, this requires a significant architectural shift -- it is essentially event sourcing or CQRS-style handling, which may not align with YAF's current entity-based DDD approach.

**Source**: [Dev.to - A more functional approach to event sourcing and DDD](https://dev.to/n1ckdm/a-more-function-approach-to-event-sourcing-and-ddd-491d), [Wolverine - Aggregate handlers and event sourcing](https://wolverinefx.net/guide/durability/marten/event-sourcing.html)

---

## Pattern 6: Repository Decorator / Proxy Pattern

### Description

Wrap concrete repositories with a decorator that automatically registers aggregates with an event collector. Using DI container decoration (e.g., Scrutor in .NET), all repositories gain event collection behavior without modification.

### How It Achieves Automation

```csharp
// Generic repository decorator
public class EventCollectingRepository<T> : IRepository<T> where T : AggregateRoot
{
    private readonly IRepository<T> _inner;
    private readonly IDomainEventCollector _eventCollector;

    public EventCollectingRepository(IRepository<T> inner, IDomainEventCollector collector)
    {
        _inner = inner;
        _eventCollector = collector;
    }

    public async Task<T> GetByIdAsync(Guid id)
    {
        var aggregate = await _inner.GetByIdAsync(id);
        _eventCollector.RegisterSource(aggregate);  // automatic
        return aggregate;
    }

    public async Task SaveAsync(T aggregate)
    {
        _eventCollector.RegisterSource(aggregate);  // ensure registered
        await _inner.SaveAsync(aggregate);
    }
}

// Registration with Scrutor
services.Decorate(typeof(IRepository<>), typeof(EventCollectingRepository<>));
```

The scoped `IDomainEventCollector` accumulates all registered aggregates. At commit time, it harvests events from all registered sources.

### Trade-offs

| Dimension | Assessment |
|-----------|------------|
| **Automation level** | High -- one-time decorator registration covers all repositories |
| **Developer cognitive load** | Low -- completely transparent to repository implementers |
| **Testability** | High -- decorator and inner repository can be tested independently |
| **Framework coupling** | Medium -- requires DI container that supports decoration |
| **Performance** | Minimal overhead -- one extra method call per repository operation |

### Relevance to Memento Separation

Excellent fit. The decorator intercepts the domain object before/after the inner repository does its memento conversion. The decorator sees the `AggregateRoot` (with events), while the inner repository only needs to handle memento persistence. The event collection concern is completely separated from the persistence concern.

**Source**: [Milan Jovanovic - Decorator pattern in ASP.NET Core](https://www.milanjovanovic.tech/blog/decorator-pattern-in-asp-net-core), [Andrew Lock - Adding decorated classes using Scrutor](https://andrewlock.net/adding-decorated-classes-to-the-asp.net-core-di-container-using-scrutor/)

---

## Pattern 7: Spring Data @DomainEvents (Convention-Based Harvesting)

### Description

Spring Data JPA provides a convention-based approach: annotate a method on the aggregate with `@DomainEvents` and the framework automatically calls it during `repository.save()` to collect events. A companion `@AfterDomainEventPublication` method clears the collection.

### How It Achieves Automation

```java
@Entity
public class Runbook extends AbstractAggregateRoot<Runbook> {
    
    public void assignTask(String taskId, String assigneeId, String userId) {
        validateIsOwner(userId);
        getTask(taskId).assign(assigneeId);
        registerEvent(new TaskAssigned(runbookId, taskId, assigneeId));
    }
}

// The AbstractAggregateRoot base class provides:
// @DomainEvents -> returns collected events
// @AfterDomainEventPublication -> clears the collection
```

When `runbookRepository.save(runbook)` is called, Spring Data:
1. Calls the `@DomainEvents` method to get pending events
2. Persists the entity
3. Publishes events via `ApplicationEventPublisher`
4. Calls `@AfterDomainEventPublication` to clear events

### Trade-offs

| Dimension | Assessment |
|-----------|------------|
| **Automation level** | Fully automatic -- zero infrastructure code per repository |
| **Developer cognitive load** | Very low -- just extend base class and call `registerEvent()` |
| **Testability** | High -- events inspectable on aggregate |
| **Framework coupling** | High -- Spring Data specific |
| **Performance** | Minimal |

### Relevance to Memento Separation

The principle is highly relevant: the repository implementation is the integration point where events are harvested. In a memento-separated architecture, the equivalent would be a repository base class that harvests events from the domain object before converting to memento:

```csharp
public abstract class RepositoryBase<TAggregate, TMemento>
{
    protected abstract TMemento ToMemento(TAggregate aggregate);

    public async Task SaveAsync(TAggregate aggregate)
    {
        var events = aggregate.DomainEvents.ToList();  // harvest before conversion
        var memento = ToMemento(aggregate);
        await PersistMementoAsync(memento);
        await DispatchEventsAsync(events);
        aggregate.ClearDomainEvents();
    }
}
```

**Source**: [Baeldung - DDD aggregates and @DomainEvents](https://www.baeldung.com/spring-data-ddd), [paucls - Aggregate Roots and Domain Events publication](https://paucls.wordpress.com/2018/05/31/ddd-aggregate-roots-and-domain-events-publication/)

---

## Pattern 8: Outbox Pattern (Event Persistence as First-Class Concern)

### Description

Instead of dispatching events in-memory, persist them to an outbox table in the same database transaction as the aggregate state change. A background worker later reads the outbox and publishes events to handlers/message brokers.

### How It Achieves Automation

```csharp
public async Task SaveAsync(TAggregate aggregate)
{
    using var transaction = await _dbContext.Database.BeginTransactionAsync();
    
    // 1. Persist the memento
    var memento = aggregate.Snapshot();
    _dbContext.Set<TMemento>().Update(memento);
    
    // 2. Persist events to outbox (same transaction)
    foreach (var evt in aggregate.DomainEvents)
    {
        _dbContext.Set<OutboxMessage>().Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = evt.GetType().AssemblyQualifiedName,
            Payload = JsonSerializer.Serialize(evt),
            OccurredOn = DateTime.UtcNow,
            ProcessedOn = null
        });
    }
    
    await _dbContext.SaveChangesAsync();
    await transaction.CommitAsync();
    
    aggregate.ClearDomainEvents();
}
```

### Frameworks Supporting This

- **NServiceBus Outbox**: Simulates atomic transaction across data store and message queue. Events stored alongside business data, delivered by framework.
- **MassTransit Transactional Outbox**: Persists messages in database, delivered by hosted service. Two modes: in-memory (buffered until consumer completes) and transactional (database-persisted).
- **Wolverine + Marten**: Events forwarded through persistent outbox, published after transaction commits.

### Trade-offs

| Dimension | Assessment |
|-----------|------------|
| **Automation level** | Medium-High -- requires outbox infrastructure but collection can be automated |
| **Developer cognitive load** | Medium -- must understand outbox mechanics |
| **Testability** | High -- events are persisted and queryable |
| **Framework coupling** | Medium -- outbox table is generic; can use any messaging framework |
| **Performance** | Higher latency (async delivery) but higher reliability |

### Relevance to Memento Separation

Fully compatible. The outbox pattern is persistence-model-agnostic -- it just needs the events and a database transaction. Events can be harvested from the domain object before memento conversion. The pattern actually *benefits* from memento separation because it already treats event persistence as a separate concern from state persistence.

**Source**: [Enterprise Craftsmanship - Domain events simple and reliable solution](https://enterprisecraftsmanship.com/posts/domain-events-simple-reliable-solution/), [MassTransit - Transactional Outbox](https://masstransit.io/documentation/patterns/transactional-outbox), [NServiceBus - Outbox](https://docs.particular.net/nservicebus/outbox/)

---

## Pattern 9: Marten's Explicit Event Stream Appending

### Description

Marten (PostgreSQL document/event store for .NET) takes a different approach: the aggregate explicitly maintains uncommitted events, and the repository explicitly appends them to an event stream via `session.Events.Append()`.

### How It Achieves Automation

```csharp
public abstract class AggregateBase
{
    [JsonIgnore]
    private readonly List<object> _uncommittedEvents = new();

    public IEnumerable<object> GetUncommittedEvents() => _uncommittedEvents;
    public void ClearUncommittedEvents() => _uncommittedEvents.Clear();
    protected void AddUncommittedEvent(object @event) => _uncommittedEvents.Add(@event);
}

// Repository
public async Task StoreAsync(AggregateBase aggregate, CancellationToken ct = default)
{
    await using var session = await store.LightweightSerializableSessionAsync(token: ct);
    
    var events = aggregate.GetUncommittedEvents().ToArray();
    session.Events.Append(aggregate.Id, aggregate.Version, events);
    await session.SaveChangesAsync(ct);
    aggregate.ClearUncommittedEvents();
}
```

Key: Marten does NOT use entity change tracking. The aggregate manages its own events, and the repository explicitly forwards them to the event store.

### Trade-offs

| Dimension | Assessment |
|-----------|------------|
| **Automation level** | Medium -- repository must explicitly call `GetUncommittedEvents()` and `Append()` |
| **Developer cognitive load** | Medium -- explicit but predictable |
| **Testability** | Very high -- no ORM, no change tracker; pure domain logic |
| **Framework coupling** | Medium -- Marten-specific API, but conceptually portable |
| **Performance** | Good -- lightweight sessions without change tracking overhead |

### Relevance to Memento Separation

Highly compatible. Marten already works without ORM entity tracking. The aggregate tracks its own events, and the repository is the integration point that forwards them. This pattern translates directly to a memento-separated architecture where the repository harvests events before converting to memento.

**Source**: [Marten - Aggregates, Events, Repositories](https://martendb.io/scenarios/aggregates-events-repositories)

---

## Pattern 10: Axon Framework's Managed Aggregate Lifecycle

### Description

Axon Framework (Java) provides a fully managed aggregate lifecycle. The framework loads aggregates from the event store, invokes command handlers, automatically collects events published via `AggregateLifecycle.apply()`, and persists them. The developer never touches persistence or event collection.

### How It Achieves Automation

```java
@Aggregate
public class GiftCard {

    @AggregateIdentifier
    private String id;
    
    @CommandHandler
    public GiftCard(IssueCardCommand cmd) {
        // apply() publishes to EventBus AND stores automatically
        apply(new CardIssuedEvent(cmd.getCardId(), cmd.getAmount()));
    }
    
    @EventSourcingHandler
    public void on(CardIssuedEvent evt) {
        this.id = evt.getCardId();
    }
}
```

The framework:
1. Loads events for the aggregate identifier
2. Replays them on an empty instance to reconstruct state
3. Invokes the command handler
4. Collects events published via `apply()`
5. Persists events to the event store
6. Dispatches events to subscribers

### Trade-offs

| Dimension | Assessment |
|-----------|------------|
| **Automation level** | Fully automatic -- zero infrastructure code |
| **Developer cognitive load** | Low once learned -- high initial learning curve |
| **Testability** | High -- Axon provides test fixtures for aggregate testing |
| **Framework coupling** | Very high -- deeply integrated into Axon |
| **Performance** | Varies -- event replay can be costly for long-lived aggregates |

### Relevance to Memento Separation

The concept of a managed lifecycle (framework controls loading, command execution, and event persistence) is applicable. The equivalent for a non-event-sourced system would be a "command handler pipeline" that: loads aggregate, executes business logic, harvests events, persists state (as memento), dispatches events.

**Source**: [Axon Framework - Aggregates documentation](https://docs.axoniq.io/axon-framework-reference/4.11/axon-framework-commands/modeling/aggregate/)

---

## Pattern 11: Wolverine's EF Core Domain Event Harvesting

### Description

Wolverine 5.6+ introduced automatic domain event harvesting from EF Core entities. The framework scans the ChangeTracker for entities with events and publishes them through the outbox.

### How It Achieves Automation

```csharp
// Configuration
opts.PublishDomainEventsFromEntityFrameworkCore<Entity>(x => x.Events);
opts.UseEntityFrameworkCoreTransactions();

// Entity base class
public abstract class Entity
{
    public List<object> Events { get; } = new();
    
    public void Publish(object @event)
    {
        Events.Add(@event);
    }
}
```

The middleware automatically:
1. Intercepts `SaveChangesAsync()`
2. Scans ChangeTracker for entities with events
3. Enqueues events as outgoing messages (via outbox)
4. Commits the transaction (entity state + outbox messages atomically)

### Trade-offs

| Dimension | Assessment |
|-----------|------------|
| **Automation level** | Fully automatic -- zero per-repository code |
| **Developer cognitive load** | Low -- just call `Publish()` in domain logic |
| **Testability** | Medium -- requires Wolverine test harness |
| **Framework coupling** | High -- Wolverine + EF Core |
| **Performance** | Good -- integrated with outbox for reliability |

### Relevance to Memento Separation

Like Pattern 2, this relies on EF Core ChangeTracker seeing the domain entities. It does NOT directly work with memento separation. However, the *configuration approach* (pointing the framework at a specific property via lambda `x => x.Events`) is interesting -- it shows how framework configuration can make event harvesting generic.

**Source**: [Jeremy D. Miller - Classic .NET domain events with Wolverine and EF Core](https://jeremydmiller.com/2025/12/04/classic-net-domain-events-with-wolverine-and-ef-core/)

---

## Comparative Analysis

### Compatibility with Memento Separation

| Pattern | Works with Memento Separation? | Adaptation Needed |
|---------|-------------------------------|-------------------|
| 1. Aggregate Tracker in UoW | **Yes** | UoW tracks domain object, not persistence model |
| 2. ORM ChangeTracker | **No** | ChangeTracker only sees mementos/EF entities |
| 3. Bogard's Deferred Dispatch | **No (original)** | Needs alternative collection mechanism |
| 4. AsyncLocal Collector | **Yes** | Domain must call static tracker (adds coupling) |
| 5. Functional/Returns Events | **Yes** | Requires architectural shift to CQRS-style |
| 6. Repository Decorator | **Yes** | Decorator intercepts before memento conversion |
| 7. Spring @DomainEvents | **Yes (principle)** | Repository base harvests before memento conversion |
| 8. Outbox Pattern | **Yes** | Orthogonal to collection mechanism; enhances any pattern |
| 9. Marten Explicit Append | **Yes** | Already works without ORM tracking |
| 10. Axon Managed Lifecycle | **Yes (concept)** | Requires managed command handler pipeline |
| 11. Wolverine EF Core | **No** | Relies on ChangeTracker |

### Automation Level Comparison

| Pattern | Per-Repository Code | One-Time Setup | Runtime Automation |
|---------|--------------------|--------------|--------------------|
| 1. Aggregate Tracker in UoW | None (if base class used) | UoW + repo base class | Full |
| 4. AsyncLocal Collector | None | Middleware + tracker | Full |
| 5. Functional/Returns Events | Handler returns events | Framework pipeline | Full |
| 6. Repository Decorator | None | Decorator registration | Full |
| 7. Spring @DomainEvents-style | None (if base class used) | Repo base class | Full |
| 8. Outbox Pattern | None (if combined with above) | Outbox infra | Full |
| 9. Marten Explicit | `GetUncommittedEvents` + `Append` | Repository method | Partial |

### Testability Comparison

| Pattern | Unit Test Approach | Complexity |
|---------|-------------------|------------|
| 1. Aggregate Tracker in UoW | Mock UoW, inspect aggregate events | Low |
| 5. Functional/Returns Events | Assert handler return value | Very Low |
| 6. Repository Decorator | Test decorator independently | Low |
| 4. AsyncLocal Collector | Must set up scope in tests | Medium |
| 8. Outbox Pattern | Query outbox table/mock | Medium |

---

## Patterns Most Applicable to YAF's Constraints

Given YAF's constraints (zero-dependency domain layer, memento pattern stays, explicit dispatch per ADR, minimize developer effort):

### Tier 1: Directly Applicable

1. **Aggregate Tracker in UoW (Pattern 1)** -- The UoW maintains references to loaded/modified domain aggregates. The repository base class automatically registers aggregates during `GetByIdAsync`/`Add`. At commit time, the UoW harvests events from all tracked aggregates. This requires zero per-repository code and works perfectly with memento separation because the UoW tracks domain objects, not mementos.

2. **Repository Decorator (Pattern 6)** -- A generic decorator wraps all repositories, intercepting `GetByIdAsync`/`Save` to register aggregates with a scoped event collector. Combined with Scrutor, this is a one-time DI registration. The decorator sees the domain object before the inner repository converts to memento.

3. **Repository Base Class Harvesting (Pattern 7 adapted)** -- An abstract repository base class that harvests events from the aggregate before calling the memento conversion. This is the simplest adaptation and can be combined with Pattern 1.

### Tier 2: Conceptually Useful but Require Trade-offs

4. **Outbox Pattern (Pattern 8)** -- Excellent for reliability and can be layered on top of any Tier 1 pattern. Adds infrastructure complexity but solves the "dispatch after commit" requirement naturally.

5. **Functional Returns-Events (Pattern 5)** -- Highly testable and eliminates the collection problem entirely. However, requires a significant architectural shift that may not align with YAF's entity-based DDD approach.

### Tier 3: Not Compatible

6. **ORM ChangeTracker patterns (Patterns 2, 3, 11)** -- These depend on the ORM tracking domain objects directly, which is incompatible with memento separation.

7. **AsyncLocal Collector (Pattern 4)** -- Adds static/ambient dependencies to the domain layer, potentially violating the zero-dependency constraint.

---

## Sources

- [Steven Giesel - Domain events and the Unit of Work pattern](https://steven-giesel.com/blogPost/ae55581a-9722-4735-8d0e-bfcfe4f6ad5a)
- [Microsoft Learn - Domain events design and implementation](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation)
- [Jimmy Bogard - A better domain events pattern](https://lostechies.com/jimmybogard/2014/05/13/a-better-domain-events-pattern/)
- [Enterprise Craftsmanship - Domain events simple and reliable solution](https://enterprisecraftsmanship.com/posts/domain-events-simple-reliable-solution/)
- [Ken van Grinsven - Minimal impact domain events](https://medium.com/@kenvgrinsven/minimal-impact-domain-events-313deb1af20e)
- [Dev.to - A more functional approach to event sourcing and DDD](https://dev.to/n1ckdm/a-more-function-approach-to-event-sourcing-and-ddd-491d)
- [Milan Jovanovic - Building a custom domain events dispatcher](https://www.milanjovanovic.tech/blog/building-a-custom-domain-events-dispatcher-in-dotnet)
- [Milan Jovanovic - Decorator pattern in ASP.NET Core](https://www.milanjovanovic.tech/blog/decorator-pattern-in-asp-net-core)
- [Andrew Lock - Adding decorated classes using Scrutor](https://andrewlock.net/adding-decorated-classes-to-the-asp.net-core-di-container-using-scrutor/)
- [Marten - Aggregates, Events, Repositories](https://martendb.io/scenarios/aggregates-events-repositories)
- [Axon Framework - Aggregates documentation](https://docs.axoniq.io/axon-framework-reference/4.11/axon-framework-commands/modeling/aggregate/)
- [Wolverine - Aggregate handlers and event sourcing](https://wolverinefx.net/guide/durability/marten/event-sourcing.html)
- [Jeremy D. Miller - Classic .NET domain events with Wolverine and EF Core](https://jeremydmiller.com/2025/12/04/classic-net-domain-events-with-wolverine-and-ef-core/)
- [Baeldung - DDD aggregates and @DomainEvents](https://www.baeldung.com/spring-data-ddd)
- [paucls - Aggregate Roots and Domain Events publication](https://paucls.wordpress.com/2018/05/31/ddd-aggregate-roots-and-domain-events-publication/)
- [MassTransit - Transactional Outbox](https://masstransit.io/documentation/patterns/transactional-outbox)
- [NServiceBus - Outbox](https://docs.particular.net/nservicebus/outbox/)
- [Martin Fowler - Domain Event](https://martinfowler.com/eaaDev/DomainEvent.html)
