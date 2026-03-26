namespace Yaf.Domain.Interfaces;

/// <summary>
/// Non-generic base interface for mementos that carry an identity value.
/// Provides runtime access to the identity type and boxed value
/// for automatic identity handling in entity base classes.
/// </summary>
public interface IHasIdentity
{
    /// <summary>
    /// The <see cref="Type"/> of the identity value (e.g., <c>typeof(Guid)</c>).
    /// </summary>
    Type IdentityType { get; }

    /// <summary>
    /// The identity value boxed as <see cref="object"/>.
    /// </summary>
    object BoxedId { get; set; }
}

/// <summary>
/// Enforces a typed identity property on memento types, enabling base entity classes
/// to handle identity snapshot, restore, and hydrate automatically.
/// </summary>
/// <typeparam name="T">The backing value type of the identity (e.g., <see cref="Guid"/>).</typeparam>
public interface IHasIdentity<T> : IHasIdentity
    where T : IEquatable<T>
{
    /// <summary>
    /// The identity value. Maps to the entity's <c>TypedId&lt;T&gt;.Value</c> in the domain layer.
    /// </summary>
    T Id { get; set; }

    /// <inheritdoc />
    Type IHasIdentity.IdentityType => typeof(T);

    /// <inheritdoc />
    object IHasIdentity.BoxedId
    {
        get => Id!;
        set => Id = value is T typed
            ? typed
            : throw new ArgumentException($"Expected {typeof(T).Name}, got {value?.GetType().Name ?? "null"}.", nameof(value));
    }
}
