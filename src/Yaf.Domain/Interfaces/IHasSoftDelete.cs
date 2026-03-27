namespace Yaf.Domain.Interfaces;

/// <summary>
/// Non-generic base interface for mementos that carry soft-deletion fields.
/// Provides runtime access to the deletion timestamp and boxed actor identity
/// for automatic soft-delete handling in entity base classes.
/// </summary>
/// <remarks>
/// When present on a memento, infrastructure auto-applies an EF Core global query filter:
/// <c>WHERE DeletedAtUtc IS NULL</c>.
/// </remarks>
public interface IHasSoftDelete
{
    /// <summary>
    /// When the entity was soft-deleted (UTC). <see langword="null"/> if not deleted.
    /// </summary>
    DateTimeOffset? DeletedAtUtc { get; set; }

    /// <summary>
    /// The <see cref="Type"/> of the actor identity value, or <see langword="null"/>
    /// if the non-generic <see cref="ISoftDeletable"/> marker is used without actor tracking.
    /// </summary>
    Type? ActorIdType { get; }

    /// <summary>
    /// The deleter's identity value boxed as <see cref="object"/>.
    /// <see langword="null"/> if not deleted or if actor tracking is not used.
    /// </summary>
    object? BoxedDeletedBy { get; set; }
}

/// <summary>
/// Enforces typed soft-deletion properties on memento types, enabling base entity classes
/// to handle soft-delete snapshot, restore, and hydrate automatically.
/// </summary>
/// <typeparam name="T">The backing value type of the actor identity (e.g., <see cref="Guid"/>).</typeparam>
public interface IHasSoftDelete<T> : IHasSoftDelete
    where T : struct, IEquatable<T>
{
    /// <summary>
    /// The deleter's identity value. <see langword="null"/> if not deleted.
    /// </summary>
    T? DeletedBy { get; set; }

    /// <inheritdoc />
    Type? IHasSoftDelete.ActorIdType => typeof(T);

    /// <inheritdoc />
    object? IHasSoftDelete.BoxedDeletedBy
    {
        get => DeletedBy;
        set => DeletedBy = Helpers.BoxingHelper.Unbox<T>(value);
    }
}
