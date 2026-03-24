# Persistence Strategy

- **Timestamp:** 2026-03-24 12:29
- **Status:** under review
- **Scope:** infrastructure
- **Stakeholders:** Proposed by: Claude Code, Decided by: @cartomatic

---

## Summary

YAF uses EF Core 10 as its default persistence technology, accessed through a Repository + Unit of Work pattern defined in Yaf.Domain and implemented in Yaf.Infrastructure. Repositories operate on aggregate roots only. Persistence maps to memento DTOs, not domain objects directly. A base `YafDbContext` provides conventions for audit fields, concurrency tokens, tenant query filters, and EF Core configuration. Specifications encapsulate query logic and keep repositories generic.

## Drivers

1. **Aggregate-level persistence** — Repositories correspond to aggregate roots. The aggregate is the consistency boundary — loading and saving happen at the aggregate level.
2. **Memento-based mapping** — EF Core maps memento DTOs, not domain objects. Domain structure does not leak into persistence configuration (covered in the Memento ADR).
3. **Convention over configuration** — Common EF Core concerns (audit fields, concurrency, tenant filters, soft delete via graveyard) should be handled by the base DbContext, not repeated per entity.
4. **Specification pattern** — Query logic should be encapsulated and testable. Repositories stay generic; specifications carry the filtering, ordering, and inclusion logic.
5. **Migration management** — Database migrations should be managed through EF Core migrations, with clear ownership and a reproducible workflow.

## Options

### Repository Pattern

| Option | Assessment |
|--------|------------|
| **`IRepository<T> where T : AggregateRoot<TId>` + `IUnitOfWork`** | **Selected.** Generic repository constrained to aggregate roots. Unit of Work coordinates save across repositories. Specifications handle query customization. |
| Generic repository without aggregate constraint | Allows repositories for any entity, breaking the aggregate boundary. Leads to inconsistent persistence patterns. |
| No repository — DbContext used directly | Couples application code to EF Core. Violates the dependency rule. Makes testing harder. |
| Repository per aggregate (no generic base) | Excessive boilerplate. Each aggregate gets a custom repository interface with near-identical methods. |

### Query Strategy

| Option | Assessment |
|--------|------------|
| **Specification pattern (`ISpecification<T>`)** | **Selected.** Encapsulates filter, include, order, and pagination. Composable (And/Or/Not). Keeps `IRepository<T>` generic while enabling rich queries. Specification evaluator in Infrastructure translates to EF Core. |
| Method-per-query on repository | Repository interfaces grow endlessly. Every new query need adds a method. |
| LINQ expressions passed to repository | Leaks query concerns into application code. Hard to test and reuse. |
| Raw SQL / Dapper for reads | Viable for complex read models but doesn't integrate with the memento pattern. Better suited as a separate read-side strategy (future). |

### DbContext Design

| Option | Assessment |
|--------|------------|
| **Base `YafDbContext` with convention-based configuration** | **Selected.** Auto-applies audit field mapping, concurrency tokens, tenant global query filters, typed ID conversions, and enumeration conversions. Consumer's DbContext inherits and adds its own entity configurations. |
| No base DbContext — consumer configures everything | Too much burden. Every project repeats the same audit, concurrency, and filter setup. |
| Fully sealed DbContext | Too restrictive. Consumers need to add their own entities and configuration. |

### Migration Strategy

| Option | Assessment |
|--------|------------|
| **EF Core migrations in a dedicated migrations project or Infrastructure** | **Selected.** Standard EF Core migration workflow. Migrations live alongside the DbContext in Infrastructure (or a dedicated migrations project for larger solutions). |
| Manual SQL scripts | Loses the code-first workflow. Migration history tracking must be managed manually. |
| Third-party migration tool (FluentMigrator, DbUp) | Adds a dependency for something EF Core handles natively. |

## Recommendation

Generic repository constrained to aggregate roots, specification pattern for queries, base `YafDbContext` with conventions, and EF Core migrations. This balances structure with pragmatism — consumers get sensible defaults while retaining full control over their entity configuration.

## Consequences

**Positive:**
- Repository boundary matches aggregate boundary — enforces DDD consistency rules
- Specifications are testable and reusable — query logic doesn't scatter across repositories or handlers
- Base DbContext eliminates repetitive configuration — audit fields, concurrency, tenant filters work automatically
- EF Core migrations provide a proven, reproducible schema management workflow
- Memento-based mapping keeps domain objects free of EF Core concerns

**Negative:**
- Generic repository may not cover all query scenarios — complex read models may need a separate read-side strategy (acceptable: deferred)
- Base DbContext conventions assume certain patterns (audit fields, tenant ID) — consumers who don't want them must opt out
- Specification evaluator adds a translation layer between domain specifications and EF Core LINQ (mitigated: evaluator is tested in integration tests)
- EF Core migrations can be problematic in team environments with concurrent schema changes (mitigated: `DbContextModelSnapshot` treated as binary in `.gitattributes` — see Migration Conflict Prevention below)

## Conclusion

### Repository Contract (Yaf.Domain)

```
public interface IRepository<T> where T : AggregateRoot<TId>
{
    Task<T?> GetByIdAsync(TId id, CancellationToken ct = default);
    Task<T?> GetAsync(ISpecification<T> spec, CancellationToken ct = default);
    Task<IReadOnlyList<T>> ListAsync(ISpecification<T> spec, CancellationToken ct = default);
    Task<int> CountAsync(ISpecification<T> spec, CancellationToken ct = default);
    Task<bool> AnyAsync(ISpecification<T> spec, CancellationToken ct = default);
    void Add(T entity);
    void Remove(T entity);
}

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

- `Add` and `Remove` are synchronous (tracking only). `ExecuteAsync` wraps the operation in a transaction. `SaveChangesAsync` on the context flushes changes within the transaction. Rollback is triggered by returning a faulted `Result`, calling `RequestTransactionRollback()`, or an event handler failure. See the Data Consistency ADR for the full Unit of Work lifecycle.
- `GetByIdAsync` is a convenience shortcut — equivalent to a specification filtering by ID.
- `Remove` moves the aggregate to the graveyard (see Cross-Cutting Infrastructure ADR), not a hard delete.

### Specification Contract (Yaf.Domain)

```
public interface ISpecification<T>
{
    Expression<Func<T, bool>>? Criteria { get; }
    List<Expression<Func<T, object>>> Includes { get; }
    Expression<Func<T, object>>? OrderBy { get; }
    Expression<Func<T, object>>? OrderByDescending { get; }
    int? Take { get; }
    int? Skip { get; }
}
```

Composable via `And()`, `Or()`, `Not()` extension methods. Consumer defines specifications per aggregate:

```
public class ActiveOrdersForCustomer : Specification<Order>
{
    public ActiveOrdersForCustomer(CustomerId customerId)
    {
        Criteria = o => o.CustomerId == customerId && o.Status != OrderStatus.Cancelled;
        OrderByDescending = o => o.CreatedAtUtc;
    }
}
```

### Specification Evaluator (Yaf.Infrastructure)

Translates `ISpecification<T>` to EF Core `IQueryable<T>`:

```
IQueryable<T> query = dbContext.Set<TMemento>().AsQueryable();

if (spec.Criteria != null)
    query = query.Where(TranslateCriteria(spec.Criteria));

// apply includes, ordering, pagination...
```

The evaluator works on memento types (since EF Core maps mementos, not domain objects). Translation between domain specifications and memento queries is handled by the evaluator.

### YafDbContext (Yaf.Infrastructure)

Base DbContext that consumer inherits:

```
public class AppDbContext : YafDbContext
{
    // Consumer adds DbSets for their memento types
    public DbSet<OrderMemento> Orders => Set<OrderMemento>();
}
```

**YafDbContext auto-applies:**

| Convention | Description |
|-----------|-------------|
| **Audit fields** | `CreatedAtUtc`, `ModifiedAtUtc` auto-populated on `SaveChanges` |
| **Accountability fields** | `CreatedBy`, `ModifiedBy` auto-populated from `IIdentityContextProvider` |
| **Concurrency tokens** | Configures `RowVersion` / concurrency check on aggregate mementos |
| **Tenant query filters** | Global `WHERE TenantId = @current` on tenant-scoped mementos |
| **Typed ID conversions** | Value converters for `TypedId<T>` → primitive |
| **Enumeration conversions** | Value converters for `Enumeration<T>` → int |
| **UTC enforcement** | All `DateTimeOffset` properties stored as UTC |

### Persistence Flow

```
Application handler:
  1. Retrieve aggregate via IRepository<T>.GetByIdAsync(id)
     → Infrastructure loads memento from DB
     → Decrypts [Encryptable] properties
     → Creates domain object, calls Hydrate(memento)
     → Returns domain object

  2. Execute domain logic on aggregate
     → Domain raises events, modifies state

  3. Save via ctx.SaveChangesAsync() (one or more times within ExecuteAsync)
     → Infrastructure calls Snapshot(memento) on modified aggregates
     → Encrypts [Encryptable] properties
     → Auto-populates audit/accountability fields
     → EF Core flushes changes (within the UoW transaction)

  4. Operation returns Result<T>
     → If failure or rollback requested → transaction rolls back
     → If success → domain events dispatched (within the transaction)
       → Event handlers execute (policy validation, side effects)
       → If any handler fails → transaction rolls back
     → On success → auto-commit
```

### Package Layering

Persistence is split across multiple packages following a contracts → implementation → provider hierarchy:

| Package | Responsibility |
|---------|---------------|
| **Yaf.Infrastructure.Persistence** | Persistence contracts and abstractions — repository base, memento mapping contracts, specification evaluator interface, UoW infrastructure. ORM-agnostic. |
| **Yaf.Infrastructure.Persistence.EfCore** | EF Core implementation — `YafDbContext`, EF Core repository, specification evaluator, value converters. Database-agnostic within EF Core. |
| **Yaf.Infrastructure.Persistence.EfCore.PgSql** | PostgreSQL-specific — Npgsql provider configuration, PostgreSQL conventions, migration support. |
| **Yaf.Infrastructure.Persistence.EfCore.DbMigrator** | Migration runner — standalone executable for running EF Core migrations. |

This layering means:
- Swapping EF Core for Dapper only requires a new `Yaf.Infrastructure.Persistence.Dapper` package implementing the same contracts
- Swapping PostgreSQL for SQL Server only requires a new `Yaf.Infrastructure.Persistence.EfCore.SqlServer` package
- The consumer's application code references `Yaf.Infrastructure.Persistence` contracts and is unaware of the specific ORM or database

### Database Provider

YAF is architecturally database-agnostic — the base `YafDbContext` works with any EF Core provider. PostgreSQL is the provider of choice: the example application, integration tests (via TestContainers), and documentation all target PostgreSQL via `Yaf.Infrastructure.Persistence.EfCore.PgSql`. Consumers can use SQL Server, MySQL, or any supported provider by referencing the appropriate provider package.

### Database Migrator

API projects **never run migrations at startup**. Migrations are handled by a dedicated `DbMigrator` — a short-lived executable deployed as an init/startup container:

```
Deployment sequence:
  1. DbMigrator container starts
  2. Checks current migration state
  3. Applies pending migrations
  4. Exits (success or failure)
  5. API containers start (only after migrator succeeds)
```

**Why a separate migrator:**
- **Scale-out safety** — multiple API instances starting concurrently would race on migrations. A single migrator container eliminates this.
- **Separation of concerns** — the API's job is to serve requests, not to manage schema changes.
- **Deployment control** — migration failures prevent API startup. No half-migrated database with running API instances.
- **Orchestration integration** — works with Kubernetes init containers, Docker Compose `depends_on`, and Aspire lifecycle hooks.

The consumer's migrator project references their `DbContext` and migrations. `Yaf.Infrastructure.Persistence.EfCore.DbMigrator` provides the base runner infrastructure (startup, migration execution, health reporting, exit codes).

### Migration Conflict Prevention

EF Core migrations are problematic when multiple developers create migrations concurrently — the `DbContextModelSnapshot` file diverges and silently merges, potentially producing broken migrations. YAF prevents this by treating `DbContextModelSnapshot` as a binary file in `.gitattributes`:

```gitattributes
**/DbContextModelSnapshot.cs binary
```

**How this works:**
1. Developer A creates a migration on their branch — modifies `DbContextModelSnapshot.cs`
2. Developer B creates a different migration on their branch — also modifies `DbContextModelSnapshot.cs`
3. Developer A merges to main first — succeeds
4. Developer B attempts to merge — **git signals a binary conflict** that cannot be auto-merged
5. Developer B must: pull main, remove their migration, regenerate it on top of the current snapshot, then merge

**Why this is better than silent text merges:** A text merge of `DbContextModelSnapshot.cs` may succeed syntactically but produce an incorrect model snapshot — migrations run but the snapshot doesn't reflect the actual database state. A binary conflict forces explicit resolution, ensuring migrations are always generated against the correct baseline.

**Resolution workflow:**
1. Pull latest main
2. Delete your pending migration files
3. Regenerate migrations against the current snapshot (which includes all merged migrations)
4. Verify the migration is correct
5. Push and merge

### What This ADR Does NOT Cover

- **Read-side optimization** (dedicated read models, CQRS read stores) — deferred to a future ADR if needed
- **Graveyard, versioning, encryption** — covered in the Cross-Cutting Infrastructure ADR
- **Outbox pattern** — covered by the future Yaf.Messaging module

## More Information

- [ADR: State Management — Memento Pattern](../domain/20260324-1104-state-management-memento-pattern.md) — memento as persistence boundary
- [ADR: Domain Building Blocks](../domain/20260324-1032-domain-building-blocks.md) — AggregateRoot as repository boundary
- [ADR: Technology Stack](../architecture/20260324-0948-technology-stack.md) — EF Core selection
- [YAF DDD Concepts Brainstorm](../../brainstorms/20260322-2036-yaf-ddd-concepts-brainstorm.md) — repositories (section 8), specifications (section 8)
