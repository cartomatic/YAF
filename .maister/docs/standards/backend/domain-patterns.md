## Domain Patterns

### Domain Entity Encapsulation
No public constructors, no public setters. Factory methods enforce invariants.

```csharp
public class Order : Entity<OrderId>
{
    private Order() { } // EF Core / memento only

    public static Result<Order> Create(string name, CustomerId customerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new Order { Name = name, CustomerId = customerId };
    }
}
```

### Memento Property Accessors
Use `{ get; private set; }` for properties that participate in memento snapshot/restore. No `init` or positional records for entities.

### Result Pattern
`Result<T>` for business errors, exceptions for infrastructure and programming failures. Never throw for expected business outcomes.

```csharp
public static Result<Order> Create(string name)
{
    if (string.IsNullOrWhiteSpace(name))
        return Error.Validation("Order.Name", "Name is required");

    return new Order { Name = name };
}
```

### Strongly-Typed IDs
All entity IDs use `TypedId` (abstract record) backed by `Guid`. External IDs are remapped at the boundary layer.

```csharp
public record OrderId(Guid Value) : TypedId(Value);

public static OrderId New() => new(Guid.NewGuid());
```

### Memento Pattern
`IMemento<TSelf, TMemento>` for snapshot/restore. Infrastructure owns concrete memento types. Domain defines the contract.

### Three-Level Validation
1. **Domain self-validation** -- invariants enforced in factory methods and state transitions
2. **Application pipeline** -- `IValidator<T>` for cross-cutting validation rules
3. **API boundary** -- FluentValidation for request shape validation

### Factory Methods
Static factory methods for simple creation. `Builder<TEntity, TBuilder>` for complex aggregates. Both return `Result<T>`.

### Application-Generated IDs
`Guid.NewGuid()` in factory methods. No database-generated IDs. IDs are known before persistence.

```csharp
public static Result<Order> Create(string name)
    => new Order { Id = OrderId.New(), Name = name };
```

### CRTP for Memento
`Entity<TId, TSelf, TMemento>` with `TSelf` constraint enables static `Restore` methods with correct return types.

```csharp
public class Order : Entity<OrderId, Order, OrderMemento>
{
    public static Order Restore(OrderMemento memento) => ...;
}
```
