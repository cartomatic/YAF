# Codebase Findings: YAF Domain Event and Memento Infrastructure

## Research Question

How can domain events be automatically collected from aggregate roots when the infrastructure layer only interacts with mementos (DTOs)?

---

## 1. Current Event Infrastructure

### AggregateRoot<TId> (src/Yaf.Domain/AggregateRoot.cs)

The base aggregate root owns a private domain event collection:

```csharp
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : ITypedId
{
    private List<IDomainEvent>? _domainEvents;

    public IReadOnlyCollection<IDomainEvent> DomainEvents =>
        (IReadOnlyCollection<IDomainEvent>?)_domainEvents ?? [];

    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents ??= [];
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents() => _domainEvents?.Clear();
}
```

**Key observations:**
- `_domainEvents` is a private `List<IDomainEvent>?` with lazy initialization (line 24).
- `DomainEvents` is public read-only (line 30-31).
- `AddDomainEvent` is protected -- only the aggregate itself can raise events (line 53).
- `ClearDomainEvents` is public -- intended for infrastructure to call after dispatch (line 63).
- Events are NOT included in any memento snapshot/restore cycle.

**Source:** `src/Yaf.Domain/AggregateRoot.cs:24-63`

### AggregateRoot<TId, TSelf, TMemento> (src/Yaf.Domain/AggregateRoot{TId,TSelf,TMemento}.cs)

The memento-enabled variant extends `AggregateRoot<TId>`:

```csharp
public abstract class AggregateRoot<TId, TSelf, TMemento>
    : AggregateRoot<TId>, IMemento<TSelf, TMemento>, IHydratable<TMemento>, IValidatable
```

The `Snapshot` method (lines 28-34) delegates to:
1. `MementoHelper.WriteIdentity` -- writes Id to memento
2. `MementoHelper.SnapshotCrossCutting` -- writes timestamps, accountability, soft-delete
3. `SnapshotCore(memento)` -- abstract, consumer-defined

The `Restore` method (lines 37-43) creates an uninitialized instance via `RuntimeHelpers.GetUninitializedObject`, then calls `Hydrate`.

**Critical finding: Domain events are NOT part of the snapshot/restore cycle.** The `Snapshot` method writes identity + cross-cutting + entity-specific state. It never touches `_domainEvents`. The `Restore` method creates an uninitialized instance (no constructor runs), so `_domainEvents` starts as null.

**Source:** `src/Yaf.Domain/AggregateRoot{TId,TSelf,TMemento}.cs:1-76`

### IDomainEvent and IDomainEvent<T> (src/Yaf.Domain/Interfaces/IDomainEvent.cs)

```csharp
public interface IDomainEvent : ICorrelated, ITenantScoped, IActorScoped, IActivityScoped
{
    Guid EventId { get; }
    DateTimeOffset OccurredAtUtc { get; }
}

public interface IDomainEvent<out T> : IDomainEvent
{
    T Data { get; }
}
```

Each domain event carries a self-describing context envelope: EventId, OccurredAtUtc, CorrelationId, TenantId, ActorId, ActivityId. The `out T` covariance on `IDomainEvent<T>` enables polymorphic handling.

**Source:** `src/Yaf.Domain/Interfaces/IDomainEvent.cs:1-56`

---

## 2. The Memento Boundary -- What Crosses and What Does Not

### What infrastructure SEES (memento types)

The infrastructure layer interacts exclusively with memento DTOs. These are what cross the domain-to-infrastructure boundary:

| Interface | Properties | Purpose |
|-----------|-----------|---------|
| `IHasIdentity` | `Guid? Id` | Entity identity bridging |
| `IHasAccountability` | `Guid? CreatedBy`, `Guid? ModifiedBy` | Audit fields |
| `IHasTimestamps` | `DateTimeOffset? CreatedAtUtc`, `DateTimeOffset? ModifiedAtUtc` | Temporal tracking |
| `IHasVersionInfo` | `Guid Version` | Optimistic concurrency token |
| `IHasSoftDelete` | `DateTimeOffset? DeletedAtUtc`, `Guid? DeletedBy` | Soft-delete tracking |
| `IHasTenantId` | `Guid? TenantId` | Multi-tenant scoping |
| `IMementoBase` | Composite: `IHasIdentity + IHasAccountability + IHasTimestamps + IHasVersionInfo` | Non-tenant base |
| `ITenantMementoBase` | `IMementoBase + IHasTenantId` | Tenant-scoped base |

Concrete base classes `MementoBase` and `TenantMementoBase` provide default property implementations.

**Source:** `src/Yaf.Domain/Interfaces/IMementoBase.cs`, `src/Yaf.Domain/Interfaces/ITenantMementoBase.cs`, `src/Yaf.Domain/MementoBase.cs`

### What infrastructure does NOT see

- **Domain events** (`_domainEvents` list) -- private field on `AggregateRoot<TId>`, never written to memento.
- **Rich domain types** -- typed IDs, value objects, enumerations -- these are flattened to primitives in the memento.
- **Behavioral methods** -- domain logic is inaccessible via the memento interface.

### MementoHelper (src/Yaf.Domain/Helpers/MementoHelper.cs)

The `MementoHelper<TId, TSelf, TMemento>` is an internal static class that bridges cross-cutting concerns between entities and mementos. It handles:

1. `WriteIdentity` / `ReadIdentity` -- TypedId to/from Guid via `IHasIdentity`
2. `SnapshotCrossCutting` -- copies ITimestamped, IAccountable, ISoftDeletable from entity to memento
3. `HydrateCrossCutting` -- copies IHasTimestamps, IHasAccountability, IHasSoftDelete from memento to entity

**The helper uses interface detection** (`if (entity is ITimestamped ts && memento is IHasTimestamps hts)`) to auto-map matching interfaces. This is the established pattern for bridging domain-to-memento concerns.

**Source:** `src/Yaf.Domain/Helpers/MementoHelper.cs:1-100`

### IMemento<TSelf, TMemento> contract (src/Yaf.Domain/Interfaces/IMemento.cs)

```csharp
public interface IMemento<TSelf, TMemento>
    where TSelf : IMemento<TSelf, TMemento>
    where TMemento : class
{
    void Snapshot(TMemento memento);
    static abstract TSelf Restore(TMemento memento);
}
```

**Source:** `src/Yaf.Domain/Interfaces/IMemento.cs:1-24`

### IHydratable<TMemento> (src/Yaf.Domain/Interfaces/IHydratable.cs)

```csharp
public interface IHydratable<TMemento>
    where TMemento : class
{
    void Hydrate(TMemento memento);
}
```

Used for in-place reloading of mutable entities. Value objects (immutable) only implement `IMemento`, not `IHydratable`.

**Source:** `src/Yaf.Domain/Interfaces/IHydratable.cs:1-16`

---

## 3. Cross-Cutting Interfaces -- How YAF Bridges Domain-to-Memento Concerns

YAF uses a read/write interface split pattern for cross-cutting concerns:

| Domain Read Interface | Internal Write Interface | Memento Interface |
|-----------------------|------------------------|--------------------|
| `IAccountable` (public) | `IAccountableWriter` (internal) | `IHasAccountability` |
| `ITimestamped` (public) | `ITimestampedWriter` (internal) | `IHasTimestamps` |
| `ISoftDeletable` (public) | `ISoftDeletableWriter` (internal) | `IHasSoftDelete` |

The pattern:
- **Domain consumers** see only the read-only interfaces (`IAccountable`, `ITimestamped`, `ISoftDeletable`).
- **Infrastructure hydration** uses the internal writer interfaces (`IAccountableWriter`, `ITimestampedWriter`, `ISoftDeletableWriter`).
- **Memento types** implement the flat data interfaces (`IHasAccountability`, `IHasTimestamps`, `IHasSoftDelete`).
- `MementoHelper` bridges them via interface detection in `SnapshotCrossCutting` and `HydrateCrossCutting`.

This is the existing precedent for bridging domain state into the memento boundary. Domain events could potentially follow a similar pattern.

**Source:** `src/Yaf.Domain/Interfaces/IAccountable.cs`, `src/Yaf.Domain/Interfaces/ITimestamped.cs`, `src/Yaf.Domain/Interfaces/ISoftDeletable.cs`

---

## 4. ADR Constraints on Domain Event Dispatch

### ADR: Domain Events and Integration Events (docs/adr/domain/20260324-1113-domain-events-and-integration-events.md)

**Domain Event Lifecycle** (from ADR):
```
1. Domain method executes business logic
2. Domain method calls AddDomainEvent(new OrderPlaced(...))
   -> event accumulated on AggregateRoot's internal collection
3. UnitOfWork.SaveChangesAsync() flushes changes (within transaction)
4. Before CommitAsync(), infrastructure dispatches accumulated events
   -> context envelope attached from context providers
   -> handlers execute in-process, within the same transaction
   -> if any handler fails -> entire transaction rolls back
5. UnitOfWork.CommitAsync() finalizes the transaction
6. Domain events cleared from aggregate
```

**Key constraints:**
- Dispatch is **explicit, not implicit** -- no hidden `SaveChanges` override magic.
- Dispatch happens **after SaveChanges but before CommitAsync** within the transaction.
- Handler failures cause **transaction rollback**.
- Infrastructure **populates context fields** (CorrelationId, TenantId, ActorId, ActivityId) from context providers at dispatch time.

**Source:** `docs/adr/domain/20260324-1113-domain-events-and-integration-events.md:96-109`

### ADR: Data Consistency (docs/adr/infrastructure/20260324-1256-data-consistency.md)

The UoW lifecycle confirms event dispatch placement:

```
unitOfWork.ExecuteAsync(async ctx =>
{
    // 1. Load aggregate via repository
    // 2. Execute domain logic (events accumulate)
    // 3. SaveChangesAsync() -- flushes to DB within transaction
    // 4. Return Result<T>
    // After operation returns successfully:
    //   -> Domain events dispatched (within transaction)
    //   -> If handler fails -> entire UoW rolls back
    //   -> On success -> auto-commit
});
```

**Source:** `docs/adr/infrastructure/20260324-1256-data-consistency.md:125-158`

### ADR: Persistence Strategy (docs/adr/infrastructure/20260324-1229-persistence-strategy.md)

**Persistence Flow** (from ADR, step 3-4):
```
3. Save via ctx.SaveChangesAsync()
   -> Infrastructure calls Snapshot(memento) on modified aggregates
   -> Encrypts [Encryptable] properties
   -> Auto-populates audit/accountability fields
   -> EF Core flushes changes (within the UoW transaction)

4. Operation returns Result<T>
   -> If success -> domain events dispatched (within the transaction)
     -> Event handlers execute
     -> If any handler fails -> transaction rolls back
   -> On success -> auto-commit
```

The critical observation: **Step 3 calls `Snapshot(memento)` on the aggregate** -- at this point, infrastructure still has a reference to the domain aggregate object (not just the memento). But after Snapshot, what happens to that reference?

**Source:** `docs/adr/infrastructure/20260324-1229-persistence-strategy.md:179-202`

### ADR: Domain Building Blocks (docs/adr/domain/20260324-1032-domain-building-blocks.md)

Defines the repository boundary:
- `IRepository<T> where T : AggregateRoot<TId>` -- repositories operate on aggregate roots.
- This means repository methods (`Add`, `GetByIdAsync`) work with `AggregateRoot<TId>` instances directly.

**Source:** `docs/adr/domain/20260324-1032-domain-building-blocks.md:112-117`

### ADR: State Management -- Memento Pattern (docs/adr/domain/20260324-1104-state-management-memento-pattern.md)

**Save flow:**
```
Repository creates/obtains memento instance
  -> passes to domain object via Snapshot(memento)
    -> domain populates memento with flattened primitives
      -> infrastructure encrypts [Encryptable] properties
        -> EF Core persists the memento
```

**Load flow (new instance):**
```
EF Core loads memento from database
  -> infrastructure decrypts [Encryptable] properties
    -> new domain object created via TSelf.Restore(memento)
```

The memento pattern does NOT specify where the aggregate root reference goes after Snapshot. The ADR focuses on data flow, not object lifecycle.

**Source:** `docs/adr/domain/20260324-1104-state-management-memento-pattern.md:126-151`

### ADR: Application Layer Patterns (docs/adr/architecture/20260324-1146-application-layer-patterns.md)

Defines:
- `IDomainEventHandler<TEvent>` lives in the Application layer.
- Context providers (`ITenantContextProvider`, `IIdentityContextProvider`, `ICorrelationIdProvider`, `IActivityIdProvider`) populate event context at dispatch time.
- Domain event dispatch reads from providers to populate the context envelope.

**Source:** `docs/adr/architecture/20260324-1146-application-layer-patterns.md:66-81`

---

## 5. Test Patterns for Domain Events

### AggregateRootEventTests (tests/Yaf.Domain.Tests/AggregateRootTests.cs:6-119)

Tests verify:
- `DomainEvents_AfterCreation_ContainsCreationEvent` -- factory method raises an event (line 46-53)
- `DomainEvents_WhenNoEventsRaised_ReturnsEmptyCollection` -- after clear, empty (line 56-64)
- `DomainEvents_MultipleEvents_PreservesInsertionOrder` -- insertion order maintained (line 67-76)
- `ClearDomainEvents_RemovesAllEvents` -- clear works (line 79-88)
- `ClearDomainEvents_WhenEmpty_DoesNotThrow` -- idempotent clear (line 91-100)

Test uses a simple `TestOrder : AggregateRoot<OrderId>` (non-memento variant).

### AggregateRootMementoTests (tests/Yaf.Domain.Tests/AggregateRootTests.cs:121-306)

Tests verify memento round-trips. **Critically:**

```csharp
[Fact]
public void Restore_DomainEventsCollectionIsEmpty()
{
    var memento = new InvoiceMemento { Id = Guid.NewGuid(), Customer = "Acme", Total = 100m };
    var invoice = Invoice.Restore(memento);
    invoice.DomainEvents.Should().BeEmpty();
}
```

This test (line 244-251) explicitly confirms that **restored aggregates have no domain events**. Events are transient -- they exist only on the in-memory aggregate instance during the current operation. Restoration from a memento starts with a clean event slate.

The test `Invoice` class uses `AggregateRoot<InvoiceId, Invoice, IInvoiceMemento>` and its factory method raises `InvoiceCreated` event, but this event is lost after snapshot/restore.

**Source:** `tests/Yaf.Domain.Tests/AggregateRootTests.cs:121-306`

### DomainEventInterfaceTests (tests/Yaf.Domain.Tests/DomainEventTests.cs)

Tests verify the `IDomainEvent` interface contract: EventId, OccurredAtUtc, ICorrelated, IActorScoped, IActivityScoped, ITenantScoped. Also verifies `IDomainEvent<T>` covariance.

These tests operate on standalone event instances -- they do not test the dispatch/collection mechanism.

**Source:** `tests/Yaf.Domain.Tests/DomainEventTests.cs:1-151`

---

## 6. The Gap: Where Aggregate Root Reference is Lost

### The Problem Statement

The ADRs define a clear lifecycle:
1. Repository loads/creates aggregate root (infrastructure has an `AggregateRoot<TId>` reference)
2. Domain logic runs on the aggregate (events accumulate on `_domainEvents`)
3. `SaveChangesAsync` calls `Snapshot(memento)` (memento gets data, aggregate still exists)
4. EF Core persists the memento
5. **Events must be dispatched** -- but from where?

### The Exact Gap

The gap exists at the **boundary between what EF Core tracks and what the domain layer provides**:

- **EF Core's change tracker** tracks memento instances (`TMemento` DTOs), NOT aggregate root instances.
- **The repository** mediates between aggregates and mementos. When `Add(aggregate)` or `GetByIdAsync` is called, the repository must:
  - For Add: create a memento, call `Snapshot`, add the memento to the DbContext
  - For Get: load the memento from DbContext, call `Restore` to create the aggregate

After `Snapshot(memento)`, EF Core tracks the memento. The aggregate root reference may or may not be retained by the repository implementation.

**The question reduces to:** Does the repository (or UoW) retain a reference to the aggregate root instances that participated in the current operation?

### How Other Concerns Solve This

The cross-cutting concerns (timestamps, accountability) do NOT face this problem because they are handled differently:
- **Timestamps/Accountability** are auto-populated by `YafDbContext` during `SaveChanges` -- they operate on the **memento** (which IS tracked by EF Core). The DbContext intercepts `SaveChanges`, iterates tracked memento entries, and sets `CreatedAtUtc`/`ModifiedBy` etc.
- **Domain events** are on the **aggregate root** (which is NOT tracked by EF Core). The DbContext cannot iterate change-tracked entries to find domain events because it tracks mementos, not aggregates.

### Possible Collection Points

Based on the codebase analysis, there are several architectural points where events could be collected:

1. **Repository tracks aggregates alongside mementos** -- the repository implementation maintains a parallel collection of aggregate root references alongside the memento instances it manages for the DbContext. When `Add(aggregate)` is called, the repository stores both the memento (for EF Core) and the aggregate reference (for event collection). When `SaveChangesAsync` triggers snapshot, the aggregate reference is still available.

2. **UoW collects from repositories** -- the `IUnitOfWork.ExecuteAsync` implementation, after `SaveChangesAsync` succeeds, asks each repository for the aggregate roots it touched, then collects their domain events for dispatch.

3. **Change tracker extension** -- a custom EF Core mechanism that associates aggregate references with their memento entries in the change tracker, allowing event collection at save time.

4. **Explicit event collection at the handler level** -- the command handler explicitly provides the aggregates to the UoW for event dispatch. This contradicts the ADR's "automatic" dispatch intent.

### Confidence Assessment

- **High (100%)**: Domain events live on `AggregateRoot<TId>._domainEvents` and are NOT part of the memento snapshot/restore cycle.
- **High (100%)**: EF Core tracks mementos, not aggregate roots.
- **High (100%)**: The ADR mandates dispatch after SaveChanges, before CommitAsync, within the transaction.
- **High (100%)**: Cross-cutting concerns (timestamps, accountability) are handled at the memento/DbContext level, not the aggregate level -- so they are NOT a precedent for event collection.
- **Medium (70%)**: The repository is the natural place to retain aggregate references, since it already mediates between aggregates and mementos. But no repository implementation exists yet -- the infrastructure layer is not built.

---

## 7. Summary of Key Interfaces and Type Hierarchy

```
Entity<TId>
  |
  +-- AggregateRoot<TId>                     # owns _domainEvents, DomainEvents, ClearDomainEvents
        |
        +-- AggregateRoot<TId, TSelf, TMemento>  # adds IMemento, IHydratable, IValidatable
              |                                   # Snapshot/Restore/Hydrate do NOT touch events
              |
              implements:
                IMemento<TSelf, TMemento>     # Snapshot(memento), static Restore(memento)
                IHydratable<TMemento>         # Hydrate(memento)
                IValidatable                  # GetValidationErrors()
```

```
Memento side (infrastructure DTOs):
  IMementoBase = IHasIdentity + IHasAccountability + IHasTimestamps + IHasVersionInfo
  ITenantMementoBase = IMementoBase + IHasTenantId
  MementoBase : IMementoBase (concrete base class)
  TenantMementoBase : ITenantMementoBase (concrete base class)
```

```
Domain event side:
  IDomainEvent : ICorrelated, ITenantScoped, IActorScoped, IActivityScoped
    + EventId, OccurredAtUtc
  IDomainEvent<out T> : IDomainEvent
    + T Data
```

**The gap is structural**: `AggregateRoot` owns events. Mementos do not carry events. EF Core tracks mementos. Infrastructure needs events from aggregates. The bridge between "aggregate that has events" and "memento that EF Core persists" is the repository -- which does not yet exist.
