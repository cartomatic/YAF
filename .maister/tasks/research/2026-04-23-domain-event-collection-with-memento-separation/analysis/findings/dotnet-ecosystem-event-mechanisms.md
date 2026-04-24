# .NET Ecosystem: Domain Event Collection and Dispatch Mechanisms

## Research Focus

.NET-specific mechanisms for collecting domain events from aggregate roots when domain objects are NOT directly tracked by EF Core. This is the key constraint for YAF, where EF Core tracks memento DTOs, not domain objects.

---

## Mechanism 1: EF Core SaveChanges Override / SaveChangesInterceptor (ChangeTracker-Based)

### How It Works

The standard .NET pattern overrides `SaveChangesAsync` in DbContext (or uses `SaveChangesInterceptor`) to scan the ChangeTracker for entities that have accumulated domain events, then dispatches them before or after the actual save.

### Code Pattern (eShopOnContainers / Microsoft Reference)

```csharp
// DbContext implements IUnitOfWork
public class OrderingContext : DbContext, IUnitOfWork
{
    public async Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default)
    {
        // Dispatch domain events BEFORE or AFTER save
        await _mediator.DispatchDomainEventsAsync(this);
        var result = await base.SaveChangesAsync(cancellationToken);
        return true;
    }
}

// MediatorExtension -- collects events from ChangeTracker
public static class MediatorExtension
{
    public static async Task DispatchDomainEventsAsync(this IMediator mediator, DbContext ctx)
    {
        var domainEntities = ctx.ChangeTracker
            .Entries<Entity>()
            .Where(x => x.Entity.DomainEvents != null && x.Entity.DomainEvents.Any());

        var domainEvents = domainEntities
            .SelectMany(x => x.Entity.DomainEvents)
            .ToList();

        domainEntities.ToList().ForEach(entity => entity.Entity.ClearDomainEvents());

        foreach (var domainEvent in domainEvents)
            await mediator.Publish(domainEvent);
    }
}
```

**Source**: [Microsoft Learn - Domain events: Design and implementation](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation)

### SaveChangesInterceptor Variant

```csharp
public sealed class DomainEventInterceptor : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var domainEvents = eventData.Context.ChangeTracker
            .Entries<Entity>()
            .Select(entry => entry.Entity)
            .SelectMany(entity =>
            {
                var events = entity.GetDomainEvents();
                entity.ClearDomainEvents();
                return events;
            })
            .ToList();

        // Dispatch events...
        foreach (var domainEvent in domainEvents)
            await mediator.Publish(domainEvent, cancellationToken);

        return result;
    }
}
```

**Source**: [Milan Jovanovic - How To Use Domain Events To Build Loosely Coupled Systems](https://www.milanjovanovic.tech/blog/how-to-use-domain-events-to-build-loosely-coupled-systems)

### Dispatch Timing Options

| Timing | Transaction Behavior | Trade-Off |
|--------|---------------------|-----------|
| Before `SaveChangesAsync` | Side effects in same transaction | Simpler; if save fails, everything rolls back |
| After `SaveChangesAsync` | Side effects in separate transaction | Need eventual consistency / compensatory actions |

**Source**: Microsoft Learn docs state: "Deciding if you send the domain events right before or right after committing the transaction is important, since it determines whether you will include the side effects as part of the same transaction or in different transactions."

### Compatibility with YAF's Memento Pattern

**INCOMPATIBLE as-is.** The ChangeTracker only contains memento DTOs in YAF. The `Entries<AggregateRoot>()` call will return zero results because aggregate root classes are never tracked. This is the fundamental gap this research aims to bridge.

### Who Uses This Pattern
- eShopOnContainers / eShop (Microsoft reference app)
- Jason Taylor's Clean Architecture template
- Most MediatR-based .NET DDD samples
- Milan Jovanovic's tutorials

**Confidence**: High (100%) -- well-documented, widely used, definitively incompatible with memento separation.

---

## Mechanism 2: Scoped Domain Event Collector Service (DI-Based)

### How It Works

A scoped service (`IDomainEventCollector`) is registered in the DI container. Repositories explicitly register aggregates (or their events) with this collector after loading or saving them. The Unit of Work reads from the collector at commit time.

### Code Pattern (Synthesized from Multiple Sources)

```csharp
// Domain-layer interface (or application-layer)
public interface IDomainEventCollector
{
    void RegisterAggregate(IAggregateRoot aggregate);
    IReadOnlyList<IDomainEvent> GetAllEvents();
    void ClearEvents();
}

// Infrastructure implementation -- registered as Scoped
public class DomainEventCollector : IDomainEventCollector
{
    private readonly List<IAggregateRoot> _trackedAggregates = new();

    public void RegisterAggregate(IAggregateRoot aggregate)
    {
        if (!_trackedAggregates.Contains(aggregate))
            _trackedAggregates.Add(aggregate);
    }

    public IReadOnlyList<IDomainEvent> GetAllEvents()
    {
        return _trackedAggregates
            .SelectMany(a => a.DomainEvents)
            .ToList()
            .AsReadOnly();
    }

    public void ClearEvents()
    {
        foreach (var aggregate in _trackedAggregates)
            aggregate.ClearDomainEvents();
    }
}

// Repository usage
public class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _context;
    private readonly IDomainEventCollector _eventCollector;

    public async Task<Order> GetByIdAsync(OrderId id, CancellationToken ct)
    {
        var memento = await _context.Orders.FindAsync(id.Value, ct);
        var order = Order.Restore(memento);
        _eventCollector.RegisterAggregate(order);  // <-- key step
        return order;
    }

    public async Task SaveAsync(Order order, CancellationToken ct)
    {
        var memento = order.Snapshot();
        _context.Orders.Update(memento);
        _eventCollector.RegisterAggregate(order);  // <-- ensures new aggregates registered too
    }
}

// UoW dispatches at commit time
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private readonly IDomainEventCollector _eventCollector;
    private readonly IDomainEventDispatcher _dispatcher;

    public async Task CommitAsync(CancellationToken ct)
    {
        var events = _eventCollector.GetAllEvents();
        await _context.SaveChangesAsync(ct);
        await _dispatcher.DispatchAsync(events, ct);
        _eventCollector.ClearEvents();
    }
}
```

### Key Characteristics
- Scoped lifetime matches HTTP request / command handler scope
- Repositories must explicitly call `RegisterAggregate()` -- this is the developer burden
- Works regardless of ORM tracking -- completely independent of ChangeTracker
- Events live on domain objects; collector merely holds references

### ABP Framework Precedent (for Non-ChangeTracker Case)

ABP Framework explicitly handles the MongoDB case (no ChangeTracker). Per ABP docs:

> "For MongoDB, [events are] published when you call repository's InsertAsync, UpdateAsync or DeleteAsync methods (since MongoDB has not a change tracking system)."

This confirms the pattern: when there is no change tracker, the **repository** is the collection point. ABP's repository methods trigger event publication directly because there's no SaveChanges hook.

**Source**: [ABP.IO - Event Bus Documentation](https://abp.io/docs/latest/framework/infrastructure/event-bus)

### Compatibility with YAF's Memento Pattern

**COMPATIBLE.** This pattern does not depend on ChangeTracker. The collector holds references to domain objects (not mementos), so events can be extracted at any point. The main cost is that each repository must call `RegisterAggregate()`.

### Reducing Developer Burden

This pattern's weakness is the manual `RegisterAggregate()` call. This can be mitigated with a **generic repository base class** (see Mechanism 4 below).

**Confidence**: High (90%) -- pattern is well-established conceptually, ABP confirms the approach for non-ChangeTracker scenarios, but specific "scoped collector as separate service" implementations are less commonly documented than the ChangeTracker approach.

---

## Mechanism 3: AsyncLocal-Based Domain Event Tracker (Static, Ambient)

### How It Works

Uses `AsyncLocal<T>` to create an ambient, thread-safe, request-scoped event collection that domain objects write to via a static method. No DI injection into domain objects is needed. A middleware or behavior creates the scope and reads events at the end.

### Code Pattern

```csharp
// Can live in domain layer (zero dependencies) or shared kernel
internal static class DomainEventTracker
{
    private static readonly AsyncLocal<List<IDomainEvent>> _domainEvents = new();

    public static void CreateScope()
    {
        _domainEvents.Value ??= [];
    }

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

// Aggregate usage -- no DI needed
internal class Order : AggregateRoot
{
    public void Submit()
    {
        // business logic...
        DomainEventTracker.Raise(new OrderSubmitted(this.Id));
    }
}

// Middleware wrapping the pipeline
public class DomainEventDispatchingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        DomainEventTracker.CreateScope();
        var response = await next();
        var events = DomainEventTracker.GetDomainEvents();
        // dispatch events...
        return response;
    }
}
```

**Source**: [Ken van Grinsven - Minimal Impact Domain Events](https://medium.com/@kenvgrinsven/minimal-impact-domain-events-313deb1af20e)

### Key Characteristics
- Zero impact on aggregate design -- no `AddDomainEvent()` or events collection needed on the entity
- Thread-safe via `AsyncLocal` -- values propagate down async call stack
- Automatic cleanup when execution context exits
- Supports nested scopes
- No DI injection into domain objects

### Critical Concern: Static Coupling in Domain Layer

The aggregate calls `DomainEventTracker.Raise()` which is a **static dependency**. This creates coupling:
- Domain layer depends on the tracker class (even if internal)
- Harder to unit test aggregates in isolation (static state)
- Conflicts with YAF's zero-dependency domain layer principle if the tracker lives elsewhere

However, if `DomainEventTracker` lives within the domain layer itself, the zero-dependency constraint is preserved. The question becomes whether static ambient context is acceptable.

### Compatibility with YAF's Memento Pattern

**COMPATIBLE** -- completely independent of ChangeTracker or ORM. Events exist only in the async execution context. However, it conflicts with YAF's current pattern where aggregates use `AddDomainEvent()` to accumulate events on the aggregate itself. Adopting this would require changing the event raising mechanism.

**Confidence**: Medium (70%) -- the AsyncLocal pattern is proven for scoping, but using it for domain events specifically is less mainstream. The static coupling concern is significant for a DDD framework.

---

## Mechanism 4: Generic Repository Base Class with Automatic Event Registration

### How It Works

An abstract repository base class encapsulates the memento-to-domain mapping AND the event collector registration, so concrete repositories inherit the behavior with zero additional boilerplate.

### Code Pattern (Synthesized for YAF's Memento Pattern)

```csharp
// Abstract base -- infrastructure layer
public abstract class MementoRepository<TAggregate, TMemento, TId>
    : IRepository<TAggregate, TId>
    where TAggregate : AggregateRoot<TId, TAggregate, TMemento>
    where TMemento : class, IMemento
    where TId : TypedId
{
    private readonly AppDbContext _context;
    private readonly IDomainEventCollector _eventCollector;

    protected MementoRepository(AppDbContext context, IDomainEventCollector eventCollector)
    {
        _context = context;
        _eventCollector = eventCollector;
    }

    protected abstract DbSet<TMemento> DbSet { get; }

    public virtual async Task<TAggregate?> GetByIdAsync(TId id, CancellationToken ct = default)
    {
        var memento = await DbSet.FindAsync(new object[] { id.Value }, ct);
        if (memento is null) return null;

        var aggregate = TAggregate.Restore(memento);  // CRTP static method
        _eventCollector.RegisterAggregate(aggregate);   // automatic!
        return aggregate;
    }

    public virtual Task SaveAsync(TAggregate aggregate, CancellationToken ct = default)
    {
        var memento = aggregate.Snapshot();
        DbSet.Update(memento);
        _eventCollector.RegisterAggregate(aggregate);   // automatic!
        return Task.CompletedTask;
    }

    public virtual Task AddAsync(TAggregate aggregate, CancellationToken ct = default)
    {
        var memento = aggregate.Snapshot();
        DbSet.Add(memento);
        _eventCollector.RegisterAggregate(aggregate);   // automatic!
        return Task.CompletedTask;
    }
}

// Concrete repository -- minimal boilerplate
public class OrderRepository : MementoRepository<Order, OrderMemento, OrderId>, IOrderRepository
{
    public OrderRepository(AppDbContext context, IDomainEventCollector eventCollector)
        : base(context, eventCollector) { }

    protected override DbSet<OrderMemento> DbSet => Context.Orders;

    // Only add custom query methods, not standard CRUD
}
```

### Key Characteristics
- **Zero per-repository event registration code** -- base class handles it
- Developers only write concrete repositories for custom queries
- Combines naturally with the scoped collector (Mechanism 2)
- Template Method pattern provides consistency
- Each repository method that touches an aggregate automatically registers it

### Industry Precedent

CodeOpinion (Derek Comartin) describes the separated behavior/data pattern where the repository acts as translator between domain and persistence:

> "The Get() method retrieves persistence data and constructs a new Aggregate Root instance... The Save() method handles the opposite: it takes the modified Aggregate and persists changes back."

**Source**: [CodeOpinion - Aggregate Root Design: Behavior & Data](https://codeopinion.com/aggregate-root-design-behavior-data/)

Kamil Grzybek's modular monolith uses a similar pattern where the UoW collects events from tracked aggregates:

> "As part of Unit Of Work, the Aggregate Store adds events to the stream and messages are added to the Outbox."

**Source**: [Kamil Grzybek - Modular Monolith with DDD](https://github.com/kgrzybek/modular-monolith-with-ddd)

### Compatibility with YAF's Memento Pattern

**HIGHLY COMPATIBLE.** This is purpose-built for the memento separation. The base class knows about both the domain aggregate and the memento, performs the translation, and handles event collector registration as a natural part of the load/save cycle. This is the most promising pattern for YAF.

**Confidence**: High (95%) -- the generic repository base pattern is well-established; combining it with a scoped event collector is a natural composition.

---

## Mechanism 5: MediatR / Mediator-Based Event Dispatch

### How It Works

MediatR provides the `INotification` / `INotificationHandler<T>` infrastructure for publishing and handling domain events. Domain events implement `INotification`, and handlers implement `INotificationHandler<TEvent>`.

### Code Pattern

```csharp
// Domain event
public class OrderStartedDomainEvent : INotification
{
    public Order Order { get; }
    public OrderStartedDomainEvent(Order order) => Order = order;
}

// Handler (application layer)
public class OrderStartedHandler : INotificationHandler<OrderStartedDomainEvent>
{
    private readonly IBuyerRepository _buyerRepository;

    public async Task Handle(OrderStartedDomainEvent notification, CancellationToken ct)
    {
        // Side-effect logic
    }
}

// DI registration
services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
```

**Source**: [Microsoft Learn - Domain Events](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation), [Wrapt - .NET Domain Events Using MediatR](https://wrapt.dev/blog/dotnet-domain-events)

### Key Insight for YAF

MediatR handles **dispatch** (publishing events to handlers), NOT **collection** (gathering events from aggregates). The collection mechanism is still needed -- MediatR doesn't solve the ChangeTracker gap. MediatR is the dispatch half; the scoped collector or repository base is the collection half.

YAF's ADR specifies mediator-agnostic CQRS abstractions. MediatR would be an implementation detail, not a domain dependency.

### Compatibility with YAF's Memento Pattern

**COMPATIBLE as dispatch mechanism** -- MediatR is indifferent to how events were collected. It just publishes `INotification` instances. The collection problem must be solved separately (Mechanisms 2 or 4).

**Confidence**: High (100%) -- MediatR is the most widely used domain event dispatch mechanism in .NET.

---

## Mechanism 6: Wolverine Framework Integration

### How It Works

Wolverine 5.6+ provides built-in domain event publishing from EF Core entities, with transactional outbox support. It scrapes events from ChangeTracker.

### Configuration

```csharp
opts.PublishDomainEventsFromEntityFrameworkCore<Entity>(x => x.Events);
```

### Entity Base Class

```csharp
public abstract class Entity
{
    public List<object> Events { get; } = new();
    public void Publish(object @event) { Events.Add(@event); }
}
```

**Source**: [Jeremy Miller - Classic .NET Domain Events with Wolverine](https://jeremydmiller.com/2025/12/04/classic-net-domain-events-with-wolverine-and-ef-core/), [Wolverine Docs - Publishing Domain Events](https://wolverinefx.net/guide/durability/efcore/domain-events)

### Compatibility with YAF's Memento Pattern

**INCOMPATIBLE as-is** -- Wolverine scrapes from ChangeTracker, same problem as Mechanism 1. The transactional outbox is valuable but requires ChangeTracker integration. Custom adaptation would be needed.

**Confidence**: High (100%) -- Wolverine's mechanism is clearly documented; its ChangeTracker dependency is explicit.

---

## Mechanism 7: Custom IDomainEventDispatcher (No MediatR Dependency)

### How It Works

A custom dispatcher resolves handlers from the DI container using reflection and generic type wrappers, avoiding a MediatR dependency while providing the same publish/subscribe semantics.

### Code Pattern

```csharp
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken ct = default);
}

public interface IDomainEventHandler<in T> where T : IDomainEvent
{
    Task Handle(T domainEvent, CancellationToken ct = default);
}

public class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private static readonly ConcurrentDictionary<Type, Type> _handlerTypes = new();

    public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken ct)
    {
        foreach (var domainEvent in domainEvents)
        {
            var eventType = domainEvent.GetType();
            var handlerType = _handlerTypes.GetOrAdd(eventType,
                t => typeof(IDomainEventHandler<>).MakeGenericType(t));

            using var scope = _serviceProvider.CreateScope();
            var handlers = scope.ServiceProvider.GetServices(handlerType);

            foreach (var handler in handlers)
            {
                // Invoke via cached wrapper or reflection
                await ((dynamic)handler).Handle((dynamic)domainEvent, ct);
            }
        }
    }
}
```

**Source**: [Milan Jovanovic - Building a Custom Domain Events Dispatcher in .NET](https://www.milanjovanovic.tech/blog/building-a-custom-domain-events-dispatcher-in-dotnet)

### DI Registration with Scrutor

```csharp
services.Scan(scan => scan
    .FromAssembliesOf(typeof(DependencyInjection))
    .AddClasses(classes => classes.AssignableTo(typeof(IDomainEventHandler<>)), publicOnly: false)
    .AsImplementedInterfaces()
    .WithScopedLifetime());
services.AddTransient<IDomainEventDispatcher, DomainEventDispatcher>();
```

### Compatibility with YAF's Memento Pattern

**COMPATIBLE** -- this is purely a dispatch mechanism. Like MediatR, it is indifferent to how events were collected. Combined with a scoped collector (Mechanism 2) and repository base (Mechanism 4), it provides a complete solution.

### Relevance for YAF

Given YAF's mediator-agnostic CQRS abstractions, a custom dispatcher aligns well. YAF could define `IDomainEventDispatcher` in the application layer and implement it in infrastructure, keeping the domain layer dependency-free.

**Confidence**: High (95%) -- straightforward DI-based pattern with clear implementation.

---

## Mechanism 8: Outbox Pattern with Domain Events

### How It Works

Domain events are serialized and persisted to an `OutboxMessages` table within the same database transaction as the business data. A background worker picks them up and dispatches to handlers.

### Code Pattern

```csharp
public sealed class OutboxInterceptor : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct)
    {
        var context = eventData.Context;
        // Collect events (from ChangeTracker or collector)
        var events = CollectDomainEvents(); // mechanism varies

        foreach (var @event in events)
        {
            context.Set<OutboxMessage>().Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = @event.GetType().AssemblyQualifiedName,
                Content = JsonSerializer.Serialize(@event, @event.GetType()),
                OccurredOnUtc = DateTime.UtcNow
            });
        }

        return result;
    }
}
```

**Source**: [DEV Community - Reliable Messaging: Domain Events and Outbox Pattern with EF Core Interceptors](https://dev.to/stevsharp/reliable-messaging-in-net-domain-events-and-the-outbox-pattern-with-ef-core-interceptors-pjp)

### Implementations in .NET
- **MassTransit**: Built-in EF Core outbox (`AddEntityFrameworkOutbox<T>()`)
- **Wolverine**: Transactional outbox via durable messaging
- **Custom**: Manual OutboxMessages table + background worker

### Compatibility with YAF's Memento Pattern

**COMPATIBLE with adaptation.** The outbox table persistence is compatible regardless of how events are collected. The key question is still HOW events reach the outbox -- which goes back to Mechanisms 2/4 (scoped collector + repository base). The outbox pattern is an orthogonal concern about reliable delivery, not about collection.

**Confidence**: High (90%) -- outbox is a well-established pattern for reliable event delivery; its combination with a scoped collector is straightforward.

---

## Comparison Matrix: Compatibility with YAF's Memento Pattern

| # | Mechanism | Works Without ChangeTracker? | Zero-Dep Domain? | Developer Effort per Repo | Explicit Dispatch? | Testability |
|---|-----------|------------------------------|-------------------|---------------------------|-------------------|-------------|
| 1 | EF Core SaveChanges/Interceptor | NO | Yes | None | Yes | High |
| 2 | Scoped Event Collector | YES | Yes | Low (RegisterAggregate call) | Yes | High |
| 3 | AsyncLocal Tracker | YES | Depends* | None | Yes | Medium |
| 4 | Generic Repository Base | YES | Yes | None (inherited) | Yes | High |
| 5 | MediatR Dispatch | N/A (dispatch only) | No (app-layer dep) | None | Yes | High |
| 6 | Wolverine Integration | NO | No | None | Yes | High |
| 7 | Custom Dispatcher | N/A (dispatch only) | Yes | None | Yes | High |
| 8 | Outbox Pattern | N/A (delivery layer) | Yes | None | Yes | High |

*AsyncLocal tracker: if the static class lives in domain layer, zero-dep is preserved but introduces static coupling.

---

## Recommended Composition for YAF

Based on this analysis, the optimal approach for YAF combines multiple mechanisms:

### Collection Layer (solves the ChangeTracker gap)
- **Mechanism 2 (Scoped Collector)** + **Mechanism 4 (Generic Repository Base)** = events are automatically collected from aggregates without per-repository boilerplate

### Dispatch Layer (publishes events to handlers)
- **Mechanism 7 (Custom Dispatcher)** preferred over Mechanism 5 (MediatR) to avoid framework coupling. YAF can define its own `IDomainEventDispatcher` interface

### Delivery Layer (optional, for reliability)
- **Mechanism 8 (Outbox)** can be added later as an infrastructure concern

### Architecture Flow

```
1. Application Service calls Repository.GetByIdAsync(id)
2. Repository (via base class) loads memento, restores aggregate, registers with collector
3. Application Service calls aggregate methods (events accumulate on aggregate)
4. Application Service calls Repository.SaveAsync(aggregate)
5. Repository (via base class) snapshots to memento, persists, re-registers with collector
6. UoW.CommitAsync() reads events from collector, dispatches, then commits transaction
```

This approach:
- Preserves zero-dependency domain layer (collector interface can live in application layer)
- Works with memento separation (collector tracks domain objects, not mementos)
- Supports explicit dispatch before/after SaveChanges per ADR
- Requires zero per-repository boilerplate (base class handles registration)
- Is fully unit-testable (collector and dispatcher are injectable)

---

## Sources

- [Microsoft Learn - Domain events: Design and implementation](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation)
- [Milan Jovanovic - How To Use Domain Events To Build Loosely Coupled Systems](https://www.milanjovanovic.tech/blog/how-to-use-domain-events-to-build-loosely-coupled-systems)
- [Milan Jovanovic - Building a Custom Domain Events Dispatcher in .NET](https://www.milanjovanovic.tech/blog/building-a-custom-domain-events-dispatcher-in-dotnet)
- [Jeremy Miller - Classic .NET Domain Events with Wolverine and EF Core](https://jeremydmiller.com/2025/12/04/classic-net-domain-events-with-wolverine-and-ef-core/)
- [Wolverine Docs - Publishing Domain Events from EF Core](https://wolverinefx.net/guide/durability/efcore/domain-events)
- [DEV Community - Reliable Messaging: Domain Events and Outbox Pattern](https://dev.to/stevsharp/reliable-messaging-in-net-domain-events-and-the-outbox-pattern-with-ef-core-interceptors-pjp)
- [Ken van Grinsven - Minimal Impact Domain Events (AsyncLocal)](https://medium.com/@kenvgrinsven/minimal-impact-domain-events-313deb1af20e)
- [Steven Giesel - Domain Events and the Unit of Work Pattern](https://steven-giesel.com/blogPost/ae55581a-9722-4735-8d0e-bfcfe4f6ad5a)
- [Kamil Grzybek - How to Publish and Handle Domain Events](https://www.kamilgrzybek.com/blog/posts/how-to-publish-handle-domain-events)
- [Kamil Grzybek - Modular Monolith with DDD (GitHub)](https://github.com/kgrzybek/modular-monolith-with-ddd)
- [ABP.IO - Event Bus Documentation](https://abp.io/docs/latest/framework/infrastructure/event-bus)
- [Enterprise Craftsmanship - Domain Events: Simple and Reliable Solution](https://enterprisecraftsmanship.com/posts/domain-events-simple-reliable-solution/)
- [CodeOpinion - Aggregate Root Design: Behavior & Data](https://codeopinion.com/aggregate-root-design-behavior-data/)
- [Ledjon Behluli - Change Tracking While Doing DDD](https://www.ledjonbehluli.com/posts/change_tracking_ddd/)
- [Wrapt - .NET Domain Events Using MediatR](https://wrapt.dev/blog/dotnet-domain-events)
- [Ardalis - Immediate Domain Event Salvation with MediatR](https://ardalis.com/immediate-domain-event-salvation-with-mediatr/)
- [EF Core Interceptors Documentation](https://learn.microsoft.com/en-us/ef/core/logging-events-diagnostics/interceptors)
