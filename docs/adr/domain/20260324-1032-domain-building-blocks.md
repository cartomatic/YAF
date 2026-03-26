# Domain Building Blocks

- **Timestamp:** 2026-03-24 10:32
- **Status:** under review
- **Scope:** domain
- **Stakeholders:** Proposed by: Claude Code, Decided by: @cartomatic

---

## Summary

Yaf.Domain provides a set of DDD tactical building blocks as base types and interfaces: `Entity<TId>`, `AggregateRoot<TId>`, `ValueObject`, `TypedId<T>`, `Enumeration<TEnum>`, and construction patterns (static factory methods + generic builder). These types enforce identity semantics, encapsulation, and invariant protection with zero external dependencies.

## Drivers

1. **DDD foundation** — Consumers need correct, well-designed base types to build domain models. Getting Entity equality, aggregate boundaries, and value object semantics wrong is a common source of subtle bugs.
2. **Type safety** — Strongly-typed IDs prevent accidental ID mix-ups at compile time (`OrderId` vs `CustomerId`).
3. **Encapsulation** — Domain objects must protect their state. No public constructors, no public setters. Public getters (with private setters) are allowed for read access by the application layer — mutation is controlled through explicit domain methods.
4. **Zero dependencies** — Yaf.Domain must be pure .NET. Base types cannot bring in any NuGet packages.
5. **Consistency** — All YAF-based domain models share the same base type semantics, making them predictable across projects.

## Options

### Entity Identity

| Option | Assessment |
|--------|------------|
| **`Entity<TId>` with generic typed ID** | **Selected.** Consumer defines their own ID type (e.g., `OrderId : TypedId<Guid>`). Equality by ID. Generic allows any backing type. |
| `Entity` with `Guid` ID | Simpler but loses type safety. `Guid orderId` and `Guid customerId` are interchangeable at compile time. |
| `Entity` with `string` ID | Too loose. No structural guarantees. |

### Strongly-Typed IDs

| Option | Assessment |
|--------|------------|
| **`TypedId<T>` as an abstract record class with `ITypedId`/`ITypedId<T>` interface hierarchy** | **Selected.** Record class allows inheritance (`OrderId : TypedId<Guid>`), which record structs do not support. `ITypedId` non-generic marker enables `where TId : ITypedId` constraints on `Entity<TId>` without requiring a second type parameter. `ITypedId<T>` exposes `T Value` for infrastructure access. `T` is constrained to `IEquatable<T>` for proper equality semantics. Heap allocation is an acceptable tradeoff — typed IDs are typically short-lived parameters or entity properties. |
| Record struct typed IDs | Value type avoids heap allocation, but record structs cannot be inherited — `OrderId : TypedId<Guid>` would not compile. Also cannot serve as a constraint base without an interface. |
| Source-generated typed IDs (e.g., StronglyTypedId library) | External dependency in Domain — violates zero-dependency rule. |

### Value Objects

| Option | Assessment |
|--------|------------|
| **C# records** | **Selected.** Records provide immutability and structural equality out of the box — no manual `Equals`/`GetHashCode` needed. YAF provides a marker interface or minimal base record for polymorphic handling where needed. |
| Abstract `ValueObject` base with `GetEqualityComponents()` | Unnecessary boilerplate. The framework already handles equality for records. Manual equality components are error-prone and add code for no benefit. |

### Smart Enums

| Option | Assessment |
|--------|------------|
| **`Enumeration<TEnum>` base — Id + Name + behavior** | **Selected.** Replaces raw C# enums for domain concepts that carry logic. Type-safe, serializable, comparable. |
| Raw C# enums | No behavior, no validation beyond range check. Sufficient for simple flags but not for domain concepts with logic. |
| SmartEnum library | External dependency. YAF can provide the same pattern with minimal code. |

### Construction Patterns

| Option | Assessment |
|--------|------------|
| **Static factory methods + generic `Builder<TEntity, TBuilder>`** | **Selected.** Factory methods for simple creation; builder for complex aggregates with many properties. Both call private constructors, both enforce invariants. |
| Public constructors | Allows creation of invalid objects. No invariant enforcement at construction time. |
| Factory methods only | Insufficient for aggregates with many optional properties — method signatures become unwieldy. |
| Builder only | Too heavy for simple entities with 2-3 properties. |

## Recommendation

All options above as a cohesive set. The building blocks work together: `Entity<TId>` uses `TypedId<T>`, `AggregateRoot<TId>` extends `Entity<TId>`, value objects and enumerations are used as properties within entities, and construction patterns control how all of these are created.

## Consequences

**Positive:**
- Compile-time type safety for IDs — `OrderId` and `CustomerId` are distinct types
- Consistent equality semantics across all domain objects (identity-based for entities, structural for value objects)
- Encapsulation enforced by design — no public constructors, no public setters; public getters with private setters for application layer read access
- Zero external dependencies — base types are pure .NET
- Builder pattern enables readable, fluent construction for complex aggregates

**Negative:**
- More boilerplate than "just use a class" — every entity needs a typed ID, factory method, etc.
- Learning curve for consumers unfamiliar with DDD tactical patterns
- `TypedId<T>` requires EF Core value converters in Infrastructure (mapping concern stays in the right layer)
- Builder base class adds a generic type parameter that can feel verbose

## Conclusion

### Type Catalog

| Type | Purpose | Equality | Construction |
|------|---------|----------|-------------|
| **`Entity<TId>`** | Base for all entities. Carries typed identity. | By ID | Private constructor, static factory or builder |
| **`AggregateRoot<TId>`** | Entity that is a consistency boundary. Owns domain event collection. | By ID | Private constructor, static factory or builder |
| **`ValueObject` (record)** | Immutable value. No identity. | By value (record equality) | Constructor or static factory |
| **`TypedId<T>`** | Strongly-typed ID wrapper. Abstract record class with `ITypedId`/`ITypedId<T>` interfaces. | By value (record equality) | Constructor: `new OrderId(guid)` or `id.Value` for extraction |
| **`Enumeration<TEnum>`** | Smart enum — Id + Name + behavior. | By Id | Static instances (sealed, predefined set) |

### IHasIdentity\<T\>

- `IHasIdentity<T> where T : IEquatable<T>` — interface enforcing a `T Id { get; set; }` property on mementos
- Mementos for entities and aggregate roots must implement `IHasIdentity<T>` so that the base memento classes (`Entity<TId, T, TSelf, TMemento>`, `AggregateRoot<TId, T, TSelf, TMemento>`) can handle identity snapshot/restore/hydrate without abstract methods
- Consumer usage: `interface IOrderMemento : IHasIdentity<Guid> { string Name { get; set; } }`

### Entity\<TId\>

- Generic `TId` constrained to `ITypedId` — prevents raw primitives like `Entity<Guid>`. Application-generated IDs (e.g., `Guid.NewGuid()` in factory methods) are the mandated pattern — database-generated sequential IDs are not a first-class pattern
- Memento-capable variant: `Entity<TId, T, TSelf, TMemento>` adds `T` (the backing value type) so that `TId : ITypedId<T>` and `TMemento : IHasIdentity<T>` can be enforced — the base class handles Id directly via `memento.Id = Id.Value` without abstract Get/Set methods
- Equality by ID — two entities with the same ID are equal regardless of other property values
- Protected constructor — only accessible to subclasses and factory methods
- Public getters with private setters — application layer can read state for DTO projection; mutation only through explicit domain methods that enforce invariants

### AggregateRoot\<TId\>

- Extends `Entity<TId>`
- Owns a `IReadOnlyCollection<IDomainEvent>` for domain event accumulation
- Methods to add/clear events (`AddDomainEvent`, `ClearDomainEvents`)
- Serves as the repository boundary — `IRepository<T> where T : AggregateRoot<TId>`
- **Does not carry a concurrency token itself** — optimistic concurrency is opt-in via `IHasVersionInfo` on the memento (see [Cross-Cutting Infrastructure ADR](../infrastructure/20260324-1249-cross-cutting-infrastructure.md))

### ValueObject

- C# `record` type — structural equality and immutability provided by the language
- YAF provides a `ValueObject` marker base record for polymorphic scenarios (e.g., generic constraints)
- No manual `Equals`/`GetHashCode` — records handle this automatically
- All properties are positional or init-only
- Consumer defines: `public record Address(string Street, string City, string PostalCode) : ValueObject;`

### TypedId\<T\>

- `ITypedId` — non-generic marker interface for use as a generic constraint (`where TId : ITypedId`)
- `ITypedId<T> : ITypedId where T : IEquatable<T>` — generic interface exposing `T Value` for infrastructure access (e.g., EF Core value converters)
- `abstract record TypedId<T>(T Value) : ITypedId<T> where T : IEquatable<T>` — abstract record class providing value equality and `ToString()` via record semantics
- Consumer creates their own: `public record OrderId(Guid Value) : TypedId<Guid>(Value)`
- No implicit/explicit conversion operators — consumers use `id.Value` or `new OrderId(guid)`
- Infrastructure maps to primitive types via EF Core value converters

### Enumeration\<TEnum\>

- Abstract base with `int Id` and `string Name`
- Static instances define the valid set: `public static readonly Status Active = new(1, "Active")`
- Supports lookup by Id or Name
- Can carry behavior (methods, computed properties)
- Serializable to/from Id for persistence

### Construction Patterns

**Static factory methods** — for simple creation:
```
public static Result<Order> Create(CustomerId customerId, IReadOnlyList<OrderItem> items)
{
    // validate invariants
    // return Result.Success(new Order(...)) or Result.Failure(error)
}
```

**`Builder<TEntity, TBuilder>`** — for complex aggregates:
```
Order.Builder()
    .WithCustomer(customerId)
    .WithItems(items)
    .WithShippingAddress(address)
    .Build();  // returns Result<Order>
```

Both patterns:
- Call private/protected constructors internally
- Enforce invariants before object creation
- Return `Result<T>` to signal success or domain errors (no exceptions for business rule violations)

## More Information

- [YAF DDD Concepts Brainstorm](../../brainstorms/20260322-2036-yaf-ddd-concepts-brainstorm.md) — detailed concept catalog
- [ADR: Core Architecture Style](../architecture/20260324-0921-core-architecture-style.md) — Domain as innermost layer
- [ADR: Solution Structure](../architecture/20260324-0953-solution-structure-and-package-layering.md) — Yaf.Domain package
