# Domain Events and Integration Events

- **Timestamp:** 2026-03-24 11:13
- **Status:** under review
- **Scope:** domain
- **Stakeholders:** Proposed by: Claude Code, Decided by: @cartomatic

---

## Summary

YAF distinguishes two event types: domain events (in-process signals within a bounded context) and integration events (cross-service communication via message broker). Both automatically carry a context envelope (TenantId, IdentityId, CorrelationId, ActivityId) for full traceability. Domain events are dispatched in-process after persistence. Integration events are deferred to a future messaging module. Domain event dispatch on `SaveChanges` is explicit, not implicit.

## Drivers

1. **Side-effect orchestration** — Domain events trigger in-process side effects (update read models, enforce policies, log business events) without coupling the aggregate to those concerns.
2. **Traceability** — Every event must carry enough context to trace it back to the originating operation, user, tenant, and correlation chain.
3. **Bounded context isolation** — Domain events stay within a bounded context. Cross-context communication needs a different mechanism (integration events) with different guarantees.
4. **Explicit behavior** — Implicit event dispatch on `SaveChanges` creates hidden side effects that are hard to reason about, test, and debug. Dispatch should be explicit and visible.
5. **Future messaging readiness** — The event model should accommodate async messaging (outbox, broker) when the messaging module is built, without changing the domain layer.

## Options

### Domain Event Dispatch

| Option | Assessment |
|--------|------------|
| **Explicit dispatch within the Unit of Work transaction, before commit** | **Selected.** Infrastructure dispatches accumulated domain events after `SaveChanges` but before `CommitAsync`. Event handlers (including policy validation) participate in the same transaction — if a handler fails, the entire UoW rolls back. The dispatch call is explicit in the UoW — visible in code, predictable in behavior. |
| Dispatch after commit | Events fire outside the transaction. If an event handler fails (e.g., policy validation), the original save cannot be rolled back — leaves the system in an inconsistent state. |
| Implicit dispatch via EF Core `SaveChanges` override | Convenient but hides side effects. Developers don't see that saving triggers event handlers. Hard to control ordering and error handling. |
| No framework dispatch — consumer handles it | Too much burden on consumers. Every project would reinvent the same pattern. |

### Event Context

| Option | Assessment |
|--------|------------|
| **Automatic context envelope on all events** | **Selected.** Every event carries TenantId, IdentityId, CorrelationId, ActivityId — populated from context providers at dispatch time. No opt-in needed. |
| Manual context attachment | Error-prone. Developers forget to attach context. Events become untraceable. |
| No context on domain events (only on integration events) | Loses traceability for in-process event chains. Debugging and auditing become harder. |

### Integration Events

| Option | Assessment |
|--------|------------|
| **Define contract in Domain, defer implementation** | **Selected.** `IIntegrationEvent` is defined in Yaf.Domain. Publishing infrastructure (outbox, broker adapter) is deferred to the Yaf.Messaging module. Domain events can be explicitly mapped to integration events when the module is built. |
| Implement messaging now | Premature. The initial release focuses on in-process behavior. Adding a broker, outbox, and retry logic before the core is stable creates unnecessary risk. |
| No integration event concept yet | Loses the ability to design domain events with eventual cross-service communication in mind. |

## Recommendation

Domain events dispatched explicitly after save, with automatic context envelope. Integration events defined as a contract but implementation deferred. This gives a working in-process event system from day one with a clear upgrade path to distributed messaging.

## Consequences

**Positive:**
- Aggregates stay decoupled from side-effect logic — they raise events, handlers react
- Full traceability from any event back to the originating operation, user, and tenant
- Explicit dispatch within the transaction means no hidden side effects — developers can see and reason about when events fire, and handler failures trigger rollback
- Integration event contract in Domain allows designing with cross-service communication in mind from the start
- Event handlers are independently testable — given an event, verify the handler's behavior

**Negative:**
- Explicit dispatch requires the infrastructure layer to coordinate save + dispatch — slightly more code than implicit
- Context envelope adds a small overhead to every event (acceptable for traceability benefits)
- Integration events are contract-only until the messaging module is built — consumers wanting cross-service messaging must wait or build their own adapter

## Conclusion

### Event Types

| Type | Interface | Scope | Dispatch | Package |
|------|-----------|-------|----------|---------|
| **Domain Event** | `IDomainEvent` | In-process, within bounded context | Explicit, after successful save | Yaf.Domain |
| **Integration Event** | `IIntegrationEvent` | Cross-service, across bounded contexts | Deferred (future Yaf.Messaging module) | Yaf.Domain (contract) |

### Context Envelope

> **Amended (2026-03-28):** Context is now part of the `IDomainEvent` contract itself, not attached
> separately at dispatch time. The event *carries* the context; infrastructure *populates* it.

Every `IDomainEvent` carries these fields as interface members:

| Field | Interface | Type | Source |
|-------|-----------|------|--------|
| `EventId` | `IDomainEvent` | `Guid` | Generated at event creation |
| `OccurredAtUtc` | `IDomainEvent` | `DateTimeOffset` | System clock |
| `CorrelationId` | `ICorrelated` | `Guid` | `ICorrelationIdProvider` |
| `TenantId` | `ITenantScoped<TenantId>` | `TenantId` | `ITenantContextProvider` |
| `UserId` | `IUserScoped` | `Guid` | `IIdentityContextProvider` |
| `ActivityId` | `IActivityScoped` | `string?` | `IActivityIdProvider` |

`ICorrelated`, `IUserScoped`, and `IActivityScoped` are standalone interfaces — commands, queries, and integration events can implement them too. `IDomainEvent<out T>` extends `IDomainEvent` with a covariant typed data payload.

Infrastructure populates context fields from context providers when the event is created or dispatched. The context providers remain Application-layer contracts, Infrastructure-implemented.

### Domain Event Lifecycle

```
1. Domain method executes business logic
2. Domain method calls AddDomainEvent(new OrderPlaced(...))
   → event accumulated on AggregateRoot's internal collection
3. UnitOfWork.SaveChangesAsync() flushes changes (within transaction)
4. Before CommitAsync(), infrastructure dispatches accumulated events
   → context envelope attached from context providers
   → handlers execute in-process, within the same transaction
   → if any handler fails → entire transaction rolls back
5. UnitOfWork.CommitAsync() finalizes the transaction
6. Domain events cleared from aggregate
```

### Event Handling

| Concept | Layer | Description |
|---------|-------|-------------|
| `IDomainEvent` | Domain | Full contract: EventId, OccurredAtUtc, inherits ICorrelated, ITenantScoped, IUserScoped, IActivityScoped |
| `IDomainEvent<out T>` | Domain | Extends IDomainEvent with a covariant typed data payload |
| `IDomainEventHandler<TEvent>` | Application | Handles a specific domain event type |
| Event dispatch | Infrastructure | Dispatches events after save, attaches context |
| `IIntegrationEvent` | Domain | Marker interface for cross-service events (contract only) |

Domain event handlers live in the Application layer — they orchestrate side effects (update read models, trigger policies, append to business event log) in response to domain events.

### What Domain Events Are NOT

- **Not persistence triggers** — events are dispatched *after* save but *before* commit, not *instead of* save
- **Not integration events** — domain events are in-process only; they don't cross service boundaries
- **Not reactive streams** — simple dispatch to registered handlers, not pub/sub with backpressure
- **Not implicitly dispatched** — the dispatch call is explicit and visible in infrastructure code

### Integration Events (Deferred)

`IIntegrationEvent` is defined in Yaf.Domain as a contract. When the Yaf.Messaging module is built, it will provide:
- Mapping from domain events to integration events (explicit, not automatic)
- Outbox pattern for reliable publishing
- Broker adapters (RabbitMQ, Azure Service Bus, etc.)
- Retry and dead-letter handling

Until then, consumers who need cross-service messaging can implement their own adapter against the `IIntegrationEvent` contract.

## More Information

- [YAF DDD Concepts Brainstorm](../../brainstorms/20260322-2036-yaf-ddd-concepts-brainstorm.md) — events and context envelope (sections 5, 21)
- [ADR: Domain Building Blocks](20260324-1032-domain-building-blocks.md) — AggregateRoot owns event collection
- [ADR: Application Layer Patterns](../architecture/20260324-1146-application-layer-patterns.md) — event handlers, context providers
