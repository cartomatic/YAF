## C# Conventions

### Microsoft C# Naming Conventions
PascalCase for types, methods, and properties. `_camelCase` for all private fields (instance and static). `I` prefix for interfaces. `T` prefix for type parameters.

```csharp
public class OrderService
{
    private readonly IOrderRepository _repository;
    private static readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(30);
}
```

### Allman Brace Style
Opening braces on a new line for all constructs, including `else`, `catch`, and `finally`.

```csharp
if (condition)
{
    DoSomething();
}
else
{
    DoOther();
}
```

### File-Scoped Namespaces
Use `namespace X;` not `namespace X { }`.

```csharp
namespace Yaf.Domain.ValueObjects;

public abstract record ValueObject { }
```

### PascalCase File Naming
One type per file, PascalCase matching the primary type. Generics use braces: `Entity{TId}.cs`.

### Pattern Matching Preferred
Use `is`, `is not`, and switch expressions over type casts and `== null`.

```csharp
if (result is { IsSuccess: true, Value: var value })
    return value;

return error switch
{
    NotFoundError => StatusCodes.Status404NotFound,
    ValidationError => StatusCodes.Status400BadRequest,
    _ => StatusCodes.Status500InternalServerError
};
```

### Guard Clauses
Use static throw helpers for argument validation.

```csharp
ArgumentNullException.ThrowIfNull(entity);
ArgumentException.ThrowIfNullOrWhiteSpace(name);
```

### Records for Value Semantics
Records for value equality (TypedId, ValueObject, Error). Classes for entities with identity.

```csharp
public abstract record TypedId(Guid Value);
public record OrderId(Guid Value) : TypedId(Value);
```

### Expression-Bodied Members
Use `=>` for single-expression methods and properties. Block bodies for multi-statement logic.

```csharp
public string FullName => $"{FirstName} {LastName}";
public bool IsValid => Errors.Count == 0;
```

### Collection Expression Syntax
Use C# 12 `[]` syntax for empty and inline collections.

```csharp
IReadOnlyList<Error> errors = [];
int[] values = [1, 2, 3];
```

### String Defaults
String properties initialized to `string.Empty` with nullable reference types.

```csharp
public string Name { get; private set; } = string.Empty;
```

### Nullable Reference Types
Use `= default!` for deferred initialization, `?` annotations for nullable, and null-forgiving (`!`) only when provably safe.

```csharp
private ILogger _logger = default!; // Set in Configure()
public string? MiddleName { get; private set; }
```

### Read-Write Interface Separation
Public read-only interfaces for external consumers, internal writer interfaces for cross-cutting concerns.

```csharp
public interface IHasTimestamps
{
    DateTimeOffset CreatedAt { get; }
    DateTimeOffset? UpdatedAt { get; }
}

internal interface ITimestampWriter
{
    void SetCreatedAt(DateTimeOffset value);
}
```

### Marker Interface Semicolon Body
Use `interface IFoo;` for marker interfaces, not `interface IFoo { }`.

```csharp
public interface IAggregateRoot;
```
