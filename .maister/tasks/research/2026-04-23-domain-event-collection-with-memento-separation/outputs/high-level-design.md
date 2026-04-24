# High-Level Design: Automated Domain Event Collection for Memento-Separated Infrastructure

## Design Overview

YAF's memento pattern separates domain aggregates from EF Core persistence, creating a "lost reference" problem where domain events on aggregate roots become invisible to the Unit of Work at commit time. This blocks the ADR-mandated explicit dispatch lifecycle (after SaveChanges, before CommitAsync). The infrastructure layer needs this capability before repositories and UoW can be built.

The chosen approach is a **three-component composition** in the infrastructure layer: a **scoped IAggregateTracker** that holds aggregate references across repository calls within a single UoW scope, a **generic MementoRepository base class** that auto-registers every aggregate it loads or saves, and a **custom IDomainEventDispatcher** that resolves handlers from DI without depending on MediatR. These compose at the UoW level: `SaveChanges` flushes mementos, the tracker yields tracked aggregates, the dispatcher fires events, aggregates are cleared, and the transaction commits.

**Key decisions:**
- **Track aggregate references, not copy events** -- deferred extraction preserves single-responsibility and handles events raised during handler execution
- **Abstract base class, not decorator** -- simpler, explicit, aligns with YAF's existing abstract base pattern (`AggregateRoot`, `Entity`, `ValueObject`)
- **All new interfaces in infrastructure, not application** -- the domain layer already provides everything needed via `AggregateRoot<TId>.DomainEvents`; no domain changes required
- **Custom dispatcher over MediatR** -- the ADR specifies mediator-agnostic CQRS abstractions; a custom dispatcher with DI-based handler resolution avoids framework coupling
- **Dispatch after SaveChanges, before CommitAsync** -- per ADR mandate; handler failures cause transaction rollback via Result pattern

## Architecture

### System Context (C4 Level 1)

```
                                +-------------------+
                                |   Consumer App    |
                                | (Command Handler) |
                                +--------+----------+
                                         |
                                    uses |
                                         v
+----------------+    restore    +-------------------+    flush     +----------+
| Yaf.Domain     |<-------------|  Yaf.Infra-       |------------>| Database |
| (AggregateRoot |   snapshot   |  structure         |  (EF Core)  | (SQL)    |
|  + Events)     |------------->| (Repos, UoW,      |<------------|          |
+----------------+              |  Tracker,          |             +----------+
                                |  Dispatcher)       |
                                +--------+----------+
                                         |
                                  resolves handlers
                                         v
                                +-------------------+
                                | Yaf.Application   |
                                | (IDomainEvent-    |
                                |  Handler<T>)      |
                                +-------------------+
```

**Actors and systems:**
- **Consumer App** -- the application using YAF, invokes command handlers that load and modify aggregates
- **Yaf.Domain** -- zero-dependency domain layer with `AggregateRoot<TId>` carrying `DomainEvents`; unchanged
- **Yaf.Infrastructure** -- new components: tracker, repository base, dispatcher, UoW event orchestration
- **Yaf.Application** -- owns `IDomainEventHandler<TEvent>` interface; handlers live here
- **Database** -- persistence target via EF Core; only sees mementos

### Container Overview (C4 Level 2)

```
+===========================================================================+
|  Yaf.Infrastructure                                                        |
|                                                                            |
|  +-------------------------+     +----------------------------------+     |
|  | IAggregateTracker       |     | MementoRepository<TAgg,TId,TMem>|     |
|  | (scoped service)        |<----|  (abstract base class)           |     |
|  |                         |     |                                  |     |
|  | .Track(aggregate)       |     | .GetByIdAsync() -- restore+track|     |
|  | .GetTrackedAggregates() |     | .AddAsync()     -- snapshot+track     |
|  +------------+------------+     | .SaveAsync()    -- snapshot+track|     |
|               |                  +---------------+------------------+     |
|               |                                  |                        |
|               v                                  | uses DbContext          |
|  +-------------------------+                     v                        |
|  | UnitOfWork              |     +----------------------------------+     |
|  | (IUnitOfWork impl)      |     | DbContext                        |     |
|  |                         |     | (tracks mementos only)            |     |
|  | .ExecuteAsync():        |     +----------------------------------+     |
|  |   1. SaveChangesAsync() |                                              |
|  |   2. GetTracked()       |                                              |
|  |   3. Harvest events     |     +----------------------------------+     |
|  |   4. DispatchAsync()  ------->| IDomainEventDispatcher            |     |
|  |   5. ClearDomainEvents()|     | (resolves handlers from DI)      |     |
|  |   6. CommitAsync()      |     +----------------------------------+     |
|  +-------------------------+                                              |
|                                                                            |
+===========================================================================+

+==========================+     +==========================+
|  Yaf.Application         |     |  Yaf.Domain              |
|                          |     |                          |
| IDomainEventHandler<T>   |     | AggregateRoot<TId>       |
|  (handler interface)     |     |  .DomainEvents           |
|                          |     |  .ClearDomainEvents()    |
| Context providers:       |     |  .AddDomainEvent()       |
|  ITenantContextProvider  |     |                          |
|  ICorrelationIdProvider  |     | IDomainEvent             |
|  IIdentityContextProvider|     |                          |
+==========================+     +==========================+
```

**Container responsibilities:**
- **IAggregateTracker** -- scoped service that holds `AggregateRoot` references for the duration of a UoW scope. Simple internal `List` with identity-based deduplication.
- **MementoRepository base class** -- abstract generic that encapsulates memento-to-domain mapping and auto-tracks every aggregate it touches. Concrete repos inherit and add custom queries only.
- **IDomainEventDispatcher** -- resolves `IDomainEventHandler<TEvent>` from DI, invokes them, returns aggregated `Result`. Caches handler type lookups for performance.
- **UnitOfWork** -- implements `IUnitOfWork.ExecuteAsync` per the Data Consistency ADR. After the operation completes, orchestrates: SaveChanges, harvest, dispatch, clear, commit/rollback.
- **DbContext** -- standard EF Core context tracking memento entities only. No awareness of domain events.

## Key Components

| Component | Purpose | Responsibilities | Key Interfaces | Dependencies |
|-----------|---------|-----------------|----------------|--------------|
| **IAggregateTracker** | Hold aggregate references within a UoW scope | - Accept aggregate registrations via `Track()` | `Track(AggregateRoot<ITypedId>)`, `GetTrackedAggregates()` | None (leaf component) |
| | | - Deduplicate by aggregate identity | | |
| | | - Return all tracked aggregates on demand | | |
| **MementoRepository<TAgg,TId,TMem>** | Automate memento mapping and event tracking | - Load memento from DbContext.Set<TMemento>(), restore to aggregate, track | `GetByIdAsync(TId)`, `AddAsync(TAgg)`, `SaveAsync(TAgg)` | IAggregateTracker, DbContext |
| | | - Snapshot aggregate to memento, persist, track | | |
| | | - Expose DbContext to concrete repos for contextual queries | | |
| **IDomainEventDispatcher** | Dispatch events to handlers without MediatR coupling | - Resolve handlers from IServiceProvider by event type | `DispatchAsync(IReadOnlyCollection<IDomainEvent>, CancellationToken)` | IServiceProvider |
| | | - Invoke handlers, aggregate Results | | |
| | | - Cache handler type resolution | | |
| **UnitOfWork** | Orchestrate transaction + event lifecycle | - Manage transaction lifecycle (begin/commit/rollback) | `IUnitOfWork.ExecuteAsync<T>(...)` | IAggregateTracker, IDomainEventDispatcher, DbContext |
| | | - Call SaveChanges, harvest events, dispatch, clear | | |
| | | - Rollback on failure (Result or exception) | | |
| **Concrete Repository** (consumer-written) | Domain-specific queries | - Inherit MementoRepository base | Domain-specific query methods | MementoRepository base |
| | | - Add custom query methods (using inherited DbContext access) | | |

## Interface Signatures

```csharp
// --- Infrastructure Layer (Yaf.Infrastructure) ---

/// <summary>
/// Tracks aggregate root instances within a Unit of Work scope for domain event harvesting.
/// Registered as a scoped service in DI.
/// </summary>
public interface IAggregateTracker
{
    /// <summary>Registers an aggregate for event harvesting at commit time.</summary>
    void Track<TId>(AggregateRoot<TId> aggregate) where TId : ITypedId;

    /// <summary>Returns all tracked aggregates with their pending domain events.</summary>
    IReadOnlyCollection<IHasDomainEvents> GetTrackedAggregates();
}

/// <summary>
/// Dispatches domain events to their registered handlers.
/// </summary>
public interface IDomainEventDispatcher
{
    /// <summary>Dispatches all events to resolved handlers. Returns failure on first handler failure.</summary>
    Task<Result> DispatchAsync(
        IReadOnlyCollection<IDomainEvent> domainEvents,
        CancellationToken ct = default);
}

/// <summary>
/// Abstract base class for repositories that use memento-based persistence.
/// Auto-registers aggregates with IAggregateTracker on every CRUD operation.
/// </summary>
public abstract class MementoRepository<TAggregate, TId, TMemento>
    where TAggregate : AggregateRoot<TId, TAggregate, TMemento>
    where TId : ITypedId
    where TMemento : class
{
    /// <summary>The DbContext, available to concrete repositories for contextual queries.</summary>
    protected DbContext DbContext { get; }

    public virtual async Task<TAggregate?> GetByIdAsync(TId id, CancellationToken ct = default);
    public virtual async Task AddAsync(TAggregate aggregate, CancellationToken ct = default);
    public virtual async Task SaveAsync(TAggregate aggregate, CancellationToken ct = default);
}
```

**Notes on generic constraints:**
- `TAggregate : AggregateRoot<TId, TAggregate, TMemento>` -- uses YAF's existing CRTP pattern, giving access to `Snapshot()`, `Restore()`, `DomainEvents`, and `ClearDomainEvents()`
- The tracker may need a non-generic `IHasDomainEvents` interface (or use the existing `AggregateRoot<TId>` base) to store aggregates with different `TId` types in a single collection. `AggregateRoot<TId>` already exposes `DomainEvents` and `ClearDomainEvents()` publicly, so a simple approach is to track as `object` and cast at harvest time, or introduce a small marker interface.

## Data Flow

### Complete Event Lifecycle

```
  Consumer Code              Repository Base           Tracker          UoW            Dispatcher
       |                          |                      |               |                |
  1.   |-- GetByIdAsync(id) ----->|                      |               |                |
       |                          |-- Find memento (EF)  |               |                |
       |                          |-- TAggregate.Restore()|              |                |
       |                          |-- Track(aggregate) -->|              |                |
       |<-- aggregate ------------|                      |               |                |
       |                                                 |               |                |
  2.   |-- aggregate.DoSomething()                       |               |                |
       |   (calls AddDomainEvent internally)             |               |                |
       |                                                 |               |                |
  3.   |-- SaveAsync(aggregate) ->|                      |               |                |
       |                          |-- Snapshot(memento)  |               |                |
       |                          |-- DbSet.Update()     |               |                |
       |                          |-- Track(aggregate) -->|              |                |
       |                                                 |               |                |
  4.   |-- return Result.Success  |                      |               |                |
       |                                                 |               |                |
  --- UoW orchestration begins (after operation delegate returns) -------|                |
       |                                                 |               |                |
  5.   |                                                 |   SaveChanges |                |
       |                                                 |  (mementos    |                |
       |                                                 |   flushed)    |                |
  6.   |                                                 |<- GetTracked--|                |
       |                                                 |-- aggregates->|                |
  7.   |                                                 |               |-- harvest ----->|
       |                                                 |               |   DomainEvents  |
  8.   |                                                 |               |   DispatchAsync |
       |                                                 |               |<-- Result ------|
  9.   |                                                 |               |                |
       |                                              ClearDomainEvents on each aggregate |
  10.  |                                                 |               |                |
       |                                                 |  CommitAsync  |                |
       |                                                 |  (transaction |                |
       |                                                 |   committed)  |                |
```

### Error Handling Flow

```
  Handler failure path:
    5. SaveChangesAsync()     -- succeeds (mementos written to DB within transaction)
    6-7. Harvest events       -- events collected from tracked aggregates
    8. DispatchAsync()        -- handler returns Result.Failure(...)
    9. UoW detects failure    -- does NOT call ClearDomainEvents
   10. RollbackAsync()        -- entire transaction rolled back (mementos reverted)
       Return failure Result to caller

  Infrastructure failure path:
    5. SaveChangesAsync()     -- throws DbUpdateConcurrencyException
       UoW catches exception  -- rolls back transaction
       Return failure Result or rethrow per ADR
```

## Integration Points

### Repository <-> EF Core DbContext

The `MementoRepository` base class receives the `DbContext` via constructor injection and accesses the memento set via `DbContext.Set<TMemento>()`. Concrete repositories inherit access to the full `DbContext` for contextual queries that may involve other entity sets beyond the primary memento. The repository never exposes `DbContext` to consumers (callers).

### Repository <-> IAggregateTracker

Every repository method that touches an aggregate calls `tracker.Track(aggregate)`. This is the critical bridge point -- it is automated by the base class so concrete repositories never need to remember it. The tracker deduplicates by aggregate identity (same aggregate loaded then saved = one entry).

### UoW <-> IAggregateTracker + IDomainEventDispatcher

The UoW receives both the tracker and dispatcher via constructor injection. At commit time, it calls `tracker.GetTrackedAggregates()`, flattens their `DomainEvents`, passes them to `dispatcher.DispatchAsync()`, then calls `ClearDomainEvents()` on each aggregate. This is a sequential pipeline, not parallel -- ordering matters for consistency.

### IDomainEventDispatcher <-> DI Container

The dispatcher receives `IServiceProvider` and uses it to resolve `IDomainEventHandler<TEvent>` implementations at runtime. For each event, it constructs `typeof(IDomainEventHandler<>).MakeGenericType(event.GetType())` and resolves all implementations from the container. Handler type lookups are cached in a `ConcurrentDictionary` for performance.

### UoW <-> CQRS Pipeline

Per the Data Consistency ADR, a CQRS pipeline behavior wraps command handler execution in `IUnitOfWork.ExecuteAsync()`. The UoW manages the transaction, and after the handler completes, it orchestrates the event dispatch lifecycle. Query handlers bypass the UoW entirely.

## Design Decisions

| ID | Decision | Rationale | ADR Reference |
|----|----------|-----------|---------------|
| ADR-001 | Track aggregate references, not copy events | Deferred extraction; handles events raised during dispatch | [decision-log.md#ADR-001](decision-log.md#adr-001-aggregate-tracking-vs-event-copying) |
| ADR-002 | Abstract base class, not repository decorator | Simpler, explicit, matches YAF patterns | [decision-log.md#ADR-002](decision-log.md#adr-002-base-class-vs-decorator-for-repository-automation) |
| ADR-003 | New interfaces in infrastructure layer | Domain needs zero changes; tracker is infrastructure concern | [decision-log.md#ADR-003](decision-log.md#adr-003-interface-placement-across-layers) |
| ADR-004 | Custom mediator-agnostic dispatcher | ADR mandates YAF-owned abstractions; avoids MediatR coupling | [decision-log.md#ADR-004](decision-log.md#adr-004-mediator-agnostic-dispatcher-vs-mediatr-dependency) |
| ADR-005 | Dispatch after SaveChanges, before CommitAsync | ADR mandate; handlers participate in transaction | [decision-log.md#ADR-005](decision-log.md#adr-005-dispatch-timing-within-the-unit-of-work) |

## Concrete Examples

### Example 1: Order placement with inventory reservation handler

**Given** an `OrderRepository : MementoRepository<Order, OrderId, OrderMemento>` and a registered `IDomainEventHandler<OrderPlacedEvent>` that reserves inventory,

**When** a command handler loads the order, calls `order.Place()` (which internally calls `AddDomainEvent(new OrderPlacedEvent(...))`), saves via `repository.SaveAsync(order)`, and the UoW commits,

**Then**:
1. `repository.SaveAsync` snapshots the order to its memento and calls `tracker.Track(order)`
2. UoW calls `SaveChangesAsync()` -- the order memento is flushed to the database within the transaction
3. UoW harvests `OrderPlacedEvent` from the tracked order's `DomainEvents`
4. UoW dispatches `OrderPlacedEvent` -- the inventory reservation handler executes within the same transaction
5. If the handler succeeds, `ClearDomainEvents()` is called, and the transaction commits
6. If the handler fails (e.g., insufficient stock), the entire transaction rolls back -- the order is not persisted

### Example 2: Multiple aggregates in a single UoW

**Given** a command that transfers an item between two warehouses (`WarehouseA` and `WarehouseB`), both loaded via their respective `MementoRepository` subclasses,

**When** the handler calls `warehouseA.RemoveItem(itemId)` and `warehouseB.AddItem(item)` (each raising domain events), then saves both,

**Then**:
1. Both `warehouseA` and `warehouseB` are tracked by the same scoped `IAggregateTracker` instance
2. UoW harvests events from both aggregates: `ItemRemovedEvent` from A, `ItemAddedEvent` from B
3. Events are dispatched in tracker registration order (A first, then B), preserving per-aggregate insertion order within each
4. Both aggregates' mementos are committed atomically in one transaction

### Example 3: Aggregate loaded but not modified

**Given** a command handler that loads an order to validate it but makes no state changes,

**When** the handler calls `repository.GetByIdAsync(orderId)` and then returns without calling any domain methods,

**Then**:
1. The order is tracked by `IAggregateTracker` (tracking happens on load, unconditionally)
2. At commit time, UoW harvests `DomainEvents` from the order -- the collection is empty
3. Nothing is dispatched, `ClearDomainEvents()` is a no-op on an empty collection
4. The transaction commits normally with zero overhead from the event pipeline

## Out of Scope

The following are explicitly excluded from this design and deferred to future work:

- **Recursive/cascading event dispatch** -- if a domain event handler modifies another aggregate that raises new events, whether those secondary events are dispatched in the same commit cycle is an open question. The tracker pattern supports both single-pass and iterative dispatch, but the decision is deferred to implementation specification.
- **Event ordering guarantees across aggregates** -- per-aggregate insertion order is preserved by `AggregateRoot._domainEvents`. Cross-aggregate ordering follows tracker registration order, but no formal guarantee is specified. A future ADR should document the ordering contract.
- **Integration event conversion** -- the mechanism for converting domain events to integration events (for cross-bounded-context communication) is a separate concern covered by the future Yaf.Messaging module.
- **Outbox pattern** -- reliable event delivery via an outbox table is orthogonal to collection. It can be layered on top of this design by having the dispatcher (or a decorator) write events to the outbox instead of dispatching in-process.
- **Concrete EF Core configuration** -- DbContext setup, memento-to-table mapping, migration strategy, and connection management are infrastructure concerns outside the event collection design.
- **Non-repository event sources** -- if aggregates are created or modified outside repositories (e.g., raw SQL, bulk operations), events from those operations will not be collected. This is a known trade-off of the repository-as-registrar pattern.
- **Handler execution strategy** -- whether handlers run sequentially or in parallel, and how partial handler failures are handled (stop-on-first-failure vs. collect-all-failures), is an implementation detail for the dispatcher specification.

## Success Criteria

1. **Zero per-repository boilerplate** -- concrete repositories that inherit `MementoRepository` need zero event-related code; tracking is fully automated by the base class
2. **Zero domain-layer changes** -- `AggregateRoot<TId>`, `IDomainEvent`, and all existing domain types remain unmodified
3. **All domain events dispatched within the transaction** -- events are dispatched after `SaveChangesAsync()` and before `CommitAsync()`, ensuring handler failures trigger rollback
4. **Handler failure causes complete rollback** -- if any event handler returns `Result.Failure`, the entire UoW transaction rolls back including the original persistence operation
5. **Multiple aggregates per UoW are supported** -- a single command that modifies N aggregates via N different repositories collects events from all N, dispatched in a single batch
6. **No external framework dependencies** -- the event dispatch mechanism does not depend on MediatR, Wolverine, or any third-party mediator library
