# Data Consistency

- **Timestamp:** 2026-03-24 12:56
- **Status:** under review
- **Scope:** infrastructure
- **Stakeholders:** Proposed by: Claude Code, Decided by: @cartomatic

---

## Summary

YAF enforces data consistency through three mechanisms: aggregate boundaries as the unit of consistency, optimistic concurrency via version tokens on aggregate roots, and explicit transaction boundaries managed by the Unit of Work. Within a single bounded context, consistency is strong (ACID via EF Core + database). Across bounded contexts, consistency is eventual (via integration events, deferred to the messaging module).

## Drivers

1. **Aggregate as consistency boundary** — DDD aggregates define what must be consistent within a single transaction. Cross-aggregate operations do not share a transaction by default.
2. **Concurrent access** — Multiple users or services may attempt to modify the same aggregate simultaneously. Last-write-wins silently loses data.
3. **Predictable transaction scope** — Developers should know exactly what is in a transaction. Implicit transactions that span multiple aggregates or services create hidden coupling and deadlocks.
4. **Eventual consistency readiness** — Cross-bounded-context consistency will be eventual (via integration events). The data model should be designed for this from the start.
5. **Simplicity** — Avoid distributed transactions (2PC). They are fragile, slow, and rarely necessary when aggregates are well-designed.

## Options

### Concurrency Control

| Option | Assessment |
|--------|------------|
| **Optimistic concurrency via `IHasVersionInfo`** | **Selected.** Mementos that implement `IHasVersionInfo` carry a `Version` property (Guid). EF Core auto-configures it as a concurrency token. On conflict, EF Core throws `DbUpdateConcurrencyException` — the application retries or reports the conflict. Opt-in per aggregate, not baked into `AggregateRoot`. |
| Pessimistic locking (SELECT FOR UPDATE) | Blocks concurrent readers. Increases contention and deadlock risk. Appropriate only for specific high-contention scenarios, not as a default. |
| Last-write-wins (no concurrency control) | Silently loses data. Unacceptable for business-critical aggregates. |

### Transaction Boundaries

| Option | Assessment |
|--------|------------|
| **Unit of Work with `ExecuteAsync` and explicit rollback request** | **Selected.** `IUnitOfWork.ExecuteAsync(operation)` wraps the operation in a transaction automatically. The operation may call `SaveChangesAsync` multiple times within the scope (e.g., for ID generation). `RequestTransactionRollback()` allows the operation to request a rollback without throwing exceptions — aligning with the Result pattern. Domain events are dispatched within the transaction. If the operation returns a faulted result or requests rollback, the entire transaction rolls back. On success, it auto-commits. |
| Manual Begin/Commit/Rollback | Burden on the consumer. Easy to forget to commit or handle exceptions. Transaction lifecycle leaks into handler code. |
| One SaveChanges = one transaction | Too limited. Some operations need intermediate saves (e.g., database-generated IDs needed for subsequent logic) before the process is complete. |
| Transaction per aggregate | Too granular. If a command legitimately needs to modify two aggregates atomically (within the same bounded context), this forces eventual consistency where strong consistency is simpler. |
| Ambient transactions (TransactionScope) | Implicit, easy to accidentally escalate to distributed transactions. Hard to reason about scope. |
| Distributed transactions (2PC) | Fragile, slow, not supported by many databases/brokers. Avoid entirely. |

### Cross-Aggregate Consistency (Same Bounded Context)

| Option | Assessment |
|--------|------------|
| **Same transaction when modified in the same command** | **Selected.** If a command handler modifies multiple aggregates within the same bounded context, `SaveChangesAsync` commits them atomically. This is acceptable within a bounded context but should be infrequent — well-designed aggregates minimize cross-aggregate transactions. |
| Always eventual via domain events | Too strict. Forces async patterns where a simple transaction would suffice within one context. |

### Cross-Bounded-Context Consistency

| Option | Assessment |
|--------|------------|
| **Eventual consistency via integration events** | **Selected.** Cross-bounded-context operations communicate through integration events. Each context commits its own transaction independently. The messaging module (future) will provide outbox pattern for reliable delivery. |
| Distributed transactions | Rejected. See above. |
| Synchronous cross-context calls | Creates tight coupling between contexts. Violates bounded context autonomy. |

## Recommendation

Optimistic concurrency opt-in via `IHasVersionInfo` on mementos, Unit of Work per command for transaction scope, same-transaction for same-context multi-aggregate operations, eventual consistency across bounded contexts. No distributed transactions.

## Consequences

**Positive:**
- Optimistic concurrency prevents silent data loss with minimal performance overhead
- Transaction scope is explicit and predictable — `ExecuteAsync` wraps the entire business process automatically
- Cross-aggregate transactions within a bounded context are possible when genuinely needed
- Eventual consistency across bounded contexts avoids distributed transaction complexity
- Well-designed aggregates naturally minimize the need for cross-aggregate transactions

**Negative:**
- Optimistic concurrency conflicts must be handled — callers need retry or conflict resolution logic
- Multi-aggregate commands within one context can create larger transaction scopes (mitigated: should be infrequent with good aggregate design)
- Eventual consistency across contexts requires idempotent event handlers and compensation logic (handled by future messaging module)
- No built-in retry mechanism for concurrency conflicts (consumer decides retry strategy)

## Conclusion

### Consistency Model

| Scope | Consistency | Mechanism |
|-------|------------|-----------|
| **Within an aggregate** | Strong (ACID) | Single transaction, optimistic concurrency via `IHasVersionInfo` (opt-in) |
| **Across aggregates, same bounded context** | Strong (ACID) | Same Unit of Work transaction (when modified in the same command) |
| **Across bounded contexts** | Eventual | Integration events (future messaging module) |
| **Across services** | Eventual | Integration events over message broker (future) |

### Optimistic Concurrency

Mementos that implement `IHasVersionInfo` carry a `Version` property (Guid), auto-configured as an EF Core concurrency token:

- **Opt-in:** not baked into `AggregateRoot<TId>` — only mementos implementing `IHasVersionInfo` get concurrency. `IVersionable` extends `IHasVersionInfo`, so versionable aggregates get it automatically.
- **EF Core mapping:** infrastructure auto-configures the `Version` property as a concurrency token (application-managed Guid, portable across databases)
- **On save:** infrastructure generates a new Guid for `Version`. EF Core includes the old `Version` in the `WHERE` clause of the `UPDATE` statement.
- **On conflict:** `DbUpdateConcurrencyException` is thrown
- **Handling:** the application layer catches the exception and either retries the operation (reload aggregate, re-apply logic, save again) or returns a conflict error to the caller

```
Conflict flow:
  1. User A loads Order (version: abc-...)
  2. User B loads Order (version: abc-...)
  3. User A saves Order → success (version: def-...)
  4. User B saves Order → DbUpdateConcurrencyException (version mismatch)
  5. User B: retry or report conflict
```

### Unit of Work Contract

```
// Yaf.Domain
public interface IUnitOfWork
{
    Task<Result<T>> ExecuteAsync<T>(
        Func<IUnitOfWorkContext, Task<Result<T>>> operation,
        CancellationToken ct = default);
}

public interface IUnitOfWorkContext
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    void RequestTransactionRollback();
}
```

**`ExecuteAsync`** is the single entry point. It:
1. Opens a transaction
2. Invokes the operation delegate, passing an `IUnitOfWorkContext`
3. The operation may call `SaveChangesAsync()` multiple times (intermediate flushes within the transaction)
4. The operation returns `Result<T>` — no exceptions needed for business failures
5. After the operation completes, dispatches accumulated domain events (within the transaction)
6. If the result is a failure, rollback was requested, or any event handler fails → rolls back the entire transaction
7. On success → auto-commits

The consumer never manages transactions directly. The UoW owns the entire lifecycle.

### How It Works

```
unitOfWork.ExecuteAsync(async ctx =>
{
    // 1. Load aggregate
    var order = await orderRepository.GetByIdAsync(orderId);

    // 2. Execute domain logic
    var result = order.AddItem(product, quantity);
    if (result.IsFailure)
        return Result.Failure<OrderDto>(result.Error);  // → auto rollback

    // 3. Intermediate save (e.g., generate database IDs)
    await ctx.SaveChangesAsync();

    // 4. Further logic using generated state
    var items = order.CreateShipmentGroups();
    await ctx.SaveChangesAsync();

    // 5. Return success — UoW dispatches events, then auto-commits
    return Result.Success(order.ToDto());
});
```

**Rollback scenarios:**
- **Faulted Result returned** — operation returns `Result.Failure(...)` → UoW rolls back, returns the failure result to the caller
- **Explicit rollback request** — operation calls `ctx.RequestTransactionRollback()` → UoW rolls back after the operation completes (useful when the operation needs to inspect state before deciding to abort)
- **Domain event handler failure** — an event handler returns a faulted result (e.g., policy validation fails) → UoW rolls back
- **Unhandled exception** — unexpected infrastructure failure → UoW rolls back and propagates the exception

### Domain Event Handlers and Consistency

Domain event handlers execute **within** the UoW transaction, after the operation completes but before commit:

- Handlers can validate policies, enforce cross-aggregate rules, or trigger side effects
- If a handler returns a faulted result (e.g., policy validation rejects the operation), the entire UoW rolls back — the original saves and all intermediate saves are undone
- No exceptions needed — handlers return `Result`, and the UoW inspects it
- This ensures the system never enters an inconsistent state where the primary operation succeeded but a required side effect failed

**Example — policy-triggered rollback:**
```
unitOfWork.ExecuteAsync(async ctx =>
{
    var order = Order.Create(customerId, items);
    orderRepository.Add(order);
    await ctx.SaveChangesAsync();  // generates OrderId

    order.AddShippingDetails(address);
    await ctx.SaveChangesAsync();

    return Result.Success(order.ToDto());

    // After this returns successfully:
    // → Domain event OrderPlaced dispatched (within transaction)
    // → Policy handler checks: "customer credit limit exceeded across all orders"
    //   → Requires cross-aggregate query that couldn't be checked on Order alone
    //   → Policy returns Result.Failure(...) → entire UoW rolls back
});
```

### CQRS Pipeline Integration

The UoW integrates naturally as a CQRS pipeline behavior:

```
Request arrives
  → Sanitization behavior (clean ISanitizable inputs)
    → Validation behavior (short-circuit on failure — no transaction opened)
      → UoW behavior (wraps handler execution in ExecuteAsync)
        → Handler executes (within transaction)
        → Domain events dispatched (within transaction)
        → Auto-commit or rollback
      ← UoW returns Result<T>
    ← Sanitization
  ← Validation
Response returned
```

The CQRS adapter (Wolverine, MediatR) registers a pipeline behavior that automatically wraps command handlers in `IUnitOfWork.ExecuteAsync`. Query handlers skip the UoW (read-only, no transaction needed).

### Transaction Boundary Rules

1. **One aggregate per command** is the ideal — keeps transactions small and focused
2. **Multiple aggregates in one command** is acceptable within the same bounded context when atomicity is genuinely required
3. **Multiple `SaveChangesAsync` within one `ExecuteAsync`** is expected when intermediate persistence is needed
4. **Domain events are part of the transaction** — handler failures cause rollback via Result, not exceptions
5. **Cross-bounded-context atomicity** is never achieved via transactions — always eventual consistency via integration events
6. **Query handlers do not use UoW** — read-only operations need no transaction wrapper

### Concurrency Conflict Resolution

YAF does not prescribe a single retry strategy. The application layer decides:

| Strategy | When |
|----------|------|
| **Automatic retry** | Idempotent operations — reload, re-apply, save again. Limit retry count. |
| **Return conflict error** | Non-idempotent operations or when the user should decide. Maps to 409 Conflict ProblemDetails. |
| **Merge** | Rare — application-specific logic to merge concurrent changes. Complex and domain-dependent. |

### What This ADR Does NOT Cover

- **Outbox pattern** — deferred to Yaf.Messaging module. Required for reliable integration event publishing (avoid dual-write problem between database and message broker).
- **Saga / process manager** — orchestrated multi-step workflows across bounded contexts. Future concern.
- **Read model consistency** — CQRS read store synchronization. Future concern if a separate read model is introduced.

## More Information

- [ADR: Domain Building Blocks](../domain/20260324-1032-domain-building-blocks.md) — AggregateRoot as consistency boundary
- [ADR: Cross-Cutting Infrastructure](20260324-1249-cross-cutting-infrastructure.md) — `IHasVersionInfo` and `IVersionable` interfaces, versioning snapshots, graveyard
- [ADR: Persistence Strategy](20260324-1229-persistence-strategy.md) — IUnitOfWork, SaveChangesAsync flow
- [ADR: Domain Events](../domain/20260324-1113-domain-events-and-integration-events.md) — dispatch after commit, integration events for cross-context
