namespace Yaf.Domain.Interfaces;

/// <summary>
/// Non-generic base interface for mementos that carry accountability fields.
/// Provides runtime access to the actor identity type and boxed values
/// for automatic accountability handling in entity base classes.
/// </summary>
public interface IHasAccountability
{
    /// <summary>
    /// The <see cref="Type"/> of the actor identity value (e.g., <c>typeof(Guid)</c>).
    /// </summary>
    Type ActorIdType { get; }

    /// <summary>
    /// The creator's identity value boxed as <see cref="object"/>.
    /// <see langword="null"/> before first persistence.
    /// </summary>
    object? BoxedCreatedBy { get; set; }

    /// <summary>
    /// The last modifier's identity value boxed as <see cref="object"/>.
    /// <see langword="null"/> until first modification.
    /// </summary>
    object? BoxedModifiedBy { get; set; }
}

/// <summary>
/// Enforces typed accountability properties on memento types, enabling base entity classes
/// to handle accountability snapshot, restore, and hydrate automatically.
/// </summary>
/// <typeparam name="T">The backing value type of the actor identity (e.g., <see cref="Guid"/>).</typeparam>
public interface IHasAccountability<T> : IHasAccountability
    where T : struct, IEquatable<T>
{
    /// <summary>
    /// The creator's identity value. <see langword="null"/> before first persistence.
    /// </summary>
    T? CreatedBy { get; set; }

    /// <summary>
    /// The last modifier's identity value. <see langword="null"/> until first modification.
    /// </summary>
    T? ModifiedBy { get; set; }

    /// <inheritdoc />
    Type IHasAccountability.ActorIdType => typeof(T);

    /// <inheritdoc />
    object? IHasAccountability.BoxedCreatedBy
    {
        get => CreatedBy;
        set => CreatedBy = Unbox(value);
    }

    /// <inheritdoc />
    object? IHasAccountability.BoxedModifiedBy
    {
        get => ModifiedBy;
        set => ModifiedBy = Unbox(value);
    }

    /// <summary>
    /// Converts a boxed value to <typeparamref name="T"/>?, with null propagation and type checking.
    /// Shared by all DIM property setters on this interface.
    /// </summary>
    private static T? Unbox(object? value) => value switch
    {
        null => null,
        T typed => typed,
        _ => throw new ArgumentException(
            $"Expected {typeof(T).Name}, got {value.GetType().Name}.", nameof(value))
    };
}
