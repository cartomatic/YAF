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
| **`TypedId<T>` as a record struct** | **Selected.** Records give value equality and `ToString()` for free. Struct avoids heap allocation. Generic backing type supports Guid, int, long, string. |
| Class-based typed IDs | Unnecessary heap allocation for what is essentially a value wrapper. |
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
| **`AggregateRoot<TId>`** | Entity that is a consistency boundary. Owns domain event collection. Carries optimistic concurrency token. | By ID | Private constructor, static factory or builder |
| **`ValueObject` (record)** | Immutable value. No identity. | By value (record equality) | Constructor or static factory |
| **`TypedId<T>`** | Strongly-typed ID wrapper. Record struct. | By value | Implicit/explicit conversion from `T` |
| **`Enumeration<TEnum>`** | Smart enum — Id + Name + behavior. | By Id | Static instances (sealed, predefined set) |

### Entity\<TId\>

- Generic `TId` constrained to `TypedId<T>` (or `IEquatable<TId>` for flexibility)
- Equality by ID — two entities with the same ID are equal regardless of other property values
- Protected constructor — only accessible to subclasses and factory methods
- Public getters with private setters — application layer can read state for DTO projection; mutation only through explicit domain methods that enforce invariants

### AggregateRoot\<TId\>

- Extends `Entity<TId>`
- Owns a `IReadOnlyCollection<IDomainEvent>` for domain event accumulation
- Methods to add/clear events (`AddDomainEvent`, `ClearDomainEvents`)
- Carries an optimistic concurrency token (version/rowversion)
- Serves as the repository boundary — `IRepository<T> where T : AggregateRoot<TId>`

### ValueObject

- C# `record` type — structural equality and immutability provided by the language
- YAF provides a `ValueObject` marker base record for polymorphic scenarios (e.g., generic constraints)
- No manual `Equals`/`GetHashCode` — records handle this automatically
- All properties are positional or init-only
- Consumer defines: `public record Address(string Street, string City, string PostalCode) : ValueObject;`

### TypedId\<T\>

- `record struct TypedId<T>(T Value)` where T is the backing type (typically `Guid`)
- Consumer creates their own: `public record struct OrderId(Guid Value) : TypedId<Guid>(Value)`
- Provides `ToString()`, equality, and hashing for free via record semantics
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
