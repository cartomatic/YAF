# Decision Log

## ADR-001: Aggregate Tracking vs. Event Copying

### Status
Accepted

### Context
When the repository converts an aggregate to a memento for EF Core, the aggregate reference (and its domain events) could be discarded. The UoW needs access to those events at commit time. Two strategies exist: (1) hold references to aggregates and extract events lazily at commit time, or (2) copy events out of aggregates immediately when the repository interacts with them and store them in a separate collection.

### Decision Drivers
- Events raised during handler execution (cascading events) must also be collectable
- Single-responsibility: events belong to the aggregate until infrastructure needs them
- Simplicity of implementation and debugging

### Considered Options
1. **Aggregate tracking (hold references, deferred extraction)** -- the tracker stores aggregate references; UoW reads `DomainEvents` at commit time
2. **Event copying (eager extraction)** -- the repository copies events from the aggregate into a scoped collector immediately on save/load
3. **Hybrid (track + snapshot events)** -- track references but also snapshot events at registration time for safety

### Decision Outcome
Chosen option: **Option 1 (Aggregate tracking)**, because deferred extraction naturally handles events raised during dispatch (handlers modifying aggregates) without needing re-extraction logic. The aggregate remains the single source of truth for its events. The synthesis rates tracker at 90% confidence vs. collector at 85%.

### Consequences

#### Good
- Events raised after the initial `Track()` call (e.g., during handler execution) are automatically visible at the next harvest pass
- Single source of truth -- events are never duplicated between aggregate and collector
- Simpler implementation -- tracker is just a `List<object>` with deduplication

#### Bad
- The tracker holds strong references to aggregates for the UoW scope duration, preventing GC until the scope ends
- If the aggregate is mutated between Track and harvest in unexpected ways, the harvested events reflect the final state (acceptable since the UoW owns this lifecycle)

---

## ADR-002: Base Class vs. Decorator for Repository Automation

### Status
Accepted

### Context
Every repository must register aggregates with the tracker on load, add, and save operations. This cross-cutting concern can be automated in two ways: an abstract base class that all repositories inherit, or a generic decorator that wraps repository interfaces at the DI level (e.g., using Scrutor's `Decorate<>` method).

### Decision Drivers
- YAF repositories are being built from scratch (greenfield) -- no legacy repositories to wrap
- YAF already uses abstract base classes extensively (`AggregateRoot`, `Entity`, `ValueObject`)
- Debugging transparency -- developers should see tracking logic in the call stack without proxy layers
- Simplicity of DI registration

### Considered Options
1. **Abstract base class (`MementoRepository<TAggregate, TId, TMemento>`)** -- concrete repos inherit, base handles track calls in GetByIdAsync/AddAsync/SaveAsync
2. **Generic decorator via Scrutor** -- `services.Decorate(typeof(IRepository<,>), typeof(TrackingRepositoryDecorator<,>))` intercepts all repository calls
3. **Both (base class + decorator available)** -- base class as primary, decorator for third-party repos

### Decision Outcome
Chosen option: **Option 1 (Abstract base class)**, because YAF is building all repositories from scratch and the base class pattern is already established in the codebase. The base class is explicit (tracking logic is visible in the type hierarchy), simpler to debug, and requires no DI magic. The decorator can be introduced later if third-party repositories need event collection without modification.

### Consequences

#### Good
- Consistent with YAF's existing pattern of abstract base types
- Explicit -- developers see `MementoRepository` in the inheritance chain and know tracking is handled
- No DI registration complexity -- standard `services.AddScoped<IOrderRepository, OrderRepository>()` works
- Concrete repositories are minimal -- only custom query methods needed

#### Bad
- Requires inheritance -- concrete repositories must extend the base class (C# single-inheritance constraint)
- If a repository does not inherit the base class (e.g., hand-written for performance), events from that repository will not be tracked automatically
- Less flexible than the decorator pattern for cross-cutting concerns that evolve independently

---

## ADR-003: Interface Placement Across Layers

### Status
Accepted

### Context
The new interfaces (`IAggregateTracker`, `IDomainEventDispatcher`) need a layer assignment. Options include: domain layer (alongside `IDomainEvent`), application layer (alongside `IDomainEventHandler<T>`), or infrastructure layer (where the implementations live). The existing `AggregateRoot<TId>` already provides `DomainEvents` and `ClearDomainEvents()` publicly.

### Decision Drivers
- Zero-dependency domain layer principle -- domain must not reference infrastructure or application concerns
- The tracker accepts `AggregateRoot<TId>` instances -- this type is in the domain layer
- The dispatcher consumes `IDomainEvent` -- this type is in the domain layer
- Clean Architecture dependency rule -- inner layers define contracts, outer layers implement
- `IDomainEventHandler<T>` is already placed in the application layer per ADR

### Considered Options
1. **Domain layer** -- `IAggregateTracker` and `IDomainEventDispatcher` alongside `IDomainEvent`
2. **Application layer** -- alongside `IDomainEventHandler<T>` and context providers
3. **Infrastructure layer** -- alongside implementations, since these are infrastructure orchestration concerns
4. **Split: dispatcher interface in application, tracker in infrastructure** -- dispatcher is closer to handler orchestration (application concern), tracker is purely infrastructure

### Decision Outcome
Chosen option: **Option 4 (Split placement)**, because `IDomainEventDispatcher` orchestrates handler execution and is naturally consumed by the application layer (the UoW behavior in the CQRS pipeline), while `IAggregateTracker` is purely an infrastructure coordination mechanism between repositories and the UoW -- no application-layer code ever calls it directly.

Specifically:
- `IDomainEventDispatcher` lives in **Yaf.Application** (alongside `IDomainEventHandler<T>`)
- `IAggregateTracker` lives in **Yaf.Infrastructure** (internal infrastructure coordination)
- All implementations live in **Yaf.Infrastructure**

### Consequences

#### Good
- Domain layer stays at zero dependencies -- no changes needed
- Dispatcher interface in application layer allows different infrastructure implementations (in-process, outbox-based) without changing application code
- Tracker interface in infrastructure keeps it hidden from application code that should not use it directly
- Follows the existing pattern: `IDomainEventHandler<T>` in application, handler implementations in infrastructure/application

#### Bad
- The split may be surprising to developers expecting all event-related interfaces in one place
- If the UoW is refactored to live in the application layer, the tracker interface may need to move up

---

## ADR-004: Mediator-Agnostic Dispatcher vs. MediatR Dependency

### Status
Accepted

### Context
Domain events need to be dispatched to their handlers. The .NET ecosystem's most common approach is MediatR's `INotification`/`INotificationHandler<T>` pattern. YAF's existing ADRs specify mediator-agnostic CQRS abstractions with YAF-owned interfaces, bridged to specific implementations via adapter packages.

### Decision Drivers
- ADR for CQRS specifies YAF-owned `ICommand<T>`/`IQuery<T>` -- the same principle should apply to event dispatch
- Minimal dependencies -- YAF avoids framework coupling where a simple DI-based solution suffices
- MediatR's `INotification.Publish` semantics (fire-and-forget, no result aggregation) may not match YAF's need for Result-based handler outcomes
- The dispatcher implementation is straightforward (~50 lines of reflection + DI resolution)

### Considered Options
1. **Custom `IDomainEventDispatcher` with DI-based handler resolution** -- resolves `IDomainEventHandler<TEvent>` from `IServiceProvider` using `MakeGenericType`
2. **MediatR `IMediator.Publish` as the dispatcher** -- domain events implement `INotification`, handlers implement `INotificationHandler<T>`
3. **Adapter pattern** -- `IDomainEventDispatcher` interface with both a custom implementation and a MediatR adapter implementation, selectable via DI

### Decision Outcome
Chosen option: **Option 1 (Custom dispatcher)**, because it aligns with the ADR's mediator-agnostic principle, gives full control over handler invocation semantics (Result aggregation, ordering, error handling), and avoids adding MediatR as a framework dependency. A MediatR adapter (Option 3) can be added later as a separate package (`Yaf.Infrastructure.MediatR`) if consumers prefer it.

### Consequences

#### Good
- Full control over dispatch semantics -- can return `Result` from handlers, enforce ordering, implement stop-on-first-failure
- No external NuGet dependency for event dispatch
- Consistent with YAF's mediator-agnostic CQRS approach
- Simple implementation -- `IServiceProvider.GetServices(handlerType)` with type caching

#### Bad
- Must implement and maintain the dispatcher (small code surface, but still custom code)
- Developers familiar with MediatR may expect `INotification` patterns
- No built-in pipeline behaviors for event handlers (would need custom implementation if needed)

---

## ADR-005: Dispatch Timing Within the Unit of Work

### Status
Accepted

### Context
The Domain Events ADR mandates explicit dispatch "after SaveChanges, before CommitAsync, within the transaction." The Data Consistency ADR confirms this in the `ExecuteAsync` lifecycle. This decision documents the specific timing and its integration with the UoW's `ExecuteAsync` pattern.

### Decision Drivers
- ADR mandate: dispatch within the transaction so handler failures trigger rollback
- The `ExecuteAsync` pattern wraps the entire operation -- dispatch must happen after the operation delegate returns
- Multiple `SaveChangesAsync` calls are allowed within one `ExecuteAsync` -- events should be dispatched once after all saves, not after each save
- Handler side effects (database writes) must be part of the same transaction

### Considered Options
1. **After each SaveChangesAsync call** -- dispatch events immediately after each intermediate save
2. **After operation delegate returns, before CommitAsync** -- single dispatch pass after all business logic completes
3. **After CommitAsync (post-commit)** -- dispatch outside the transaction

### Decision Outcome
Chosen option: **Option 2 (After operation, before commit)**, because:
- It matches the ADR mandate exactly
- Dispatching after each `SaveChangesAsync` would fire events on intermediate states that may not represent the final business outcome
- Post-commit dispatch (Option 3) would prevent handler failures from triggering rollback, violating the consistency model
- A single dispatch pass after all business logic is cleaner and more predictable

The specific sequence within `ExecuteAsync`:
1. Begin transaction
2. Execute operation delegate (may call `SaveChangesAsync` multiple times)
3. If operation returned failure or requested rollback -> rollback, return
4. Final `SaveChangesAsync()` (flush any remaining changes)
5. Harvest events from tracked aggregates
6. `DispatchAsync(events)` -- handlers execute within the transaction
7. If dispatch returned failure -> rollback, return
8. `ClearDomainEvents()` on all tracked aggregates
9. `CommitAsync()` -- transaction committed
10. Return success result

### Consequences

#### Good
- Handlers always see the complete, final state of the database (all intermediate saves are flushed)
- Handler failures cause full rollback -- including all intermediate saves within the operation
- Single dispatch point is predictable and easy to reason about
- Events reflect the final business outcome, not intermediate states

#### Bad
- Events from intermediate saves are not dispatched until the end -- handlers cannot react to intermediate states (by design)
- Long-running operations accumulate events that are all dispatched at once -- may cause a burst of handler activity at commit time
- If the operation modifies many aggregates, the dispatch phase may be long (mitigated: well-designed aggregates minimize cross-aggregate operations)
