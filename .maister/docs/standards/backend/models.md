## Models

### Clear Naming
Use singular names for models and plural for tables (or follow framework conventions).

### Timestamps
Include created and updated timestamps for auditing and debugging.

### Database Constraints
Enforce data rules at the database level (NOT NULL, UNIQUE, foreign keys).

### Appropriate Types
Choose data types that match purpose and size requirements.

### Index Foreign Keys
Index foreign key columns and frequently queried fields.

### Multi-Layer Validation
Validate at both model and database levels for defense in depth.

### Clear Relationships
Define relationships with appropriate cascade behaviors and naming.

### Practical Normalization
Balance normalization with query performance needs.

### XML Documentation Required
All public API must have XML doc comments. CS1591 enforced as error via EditorConfig. Test projects are exempt. Use `<inheritdoc />` for interface implementations.

```csharp
/// <summary>
/// Represents a uniquely identified domain entity with snapshot/restore capability.
/// </summary>
/// <typeparam name="TId">The strongly-typed identifier type.</typeparam>
/// <remarks>
/// Entities compare by identity, not by value. Use <see cref="ValueObject"/>
/// for value-equality semantics.
/// </remarks>
public abstract class Entity<TId> where TId : TypedId { }
```

### Comprehensive XML Docs
`<summary>` describes what, not how. No filler text. `<remarks>` for design rationale. `<see cref>` for cross-references between related types.
