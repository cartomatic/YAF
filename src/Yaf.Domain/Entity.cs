using Yaf.Domain.Interfaces;

namespace Yaf.Domain;

/// <summary>
/// Base class for domain entities with identity-based equality.
/// </summary>
/// <remarks>
/// Two entities are equal if and only if they are the same runtime type and have the same ID.
/// Cross-type comparison always returns false, even with the same ID type and value.
/// </remarks>
/// <typeparam name="TId">The strongly-typed identifier type, constrained to <see cref="ITypedId"/>.</typeparam>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : ITypedId
{
    /// <summary>
    /// The unique identifier of this entity.
    /// </summary>
    public TId Id { get; protected set; } = default!;

    /// <summary>
    /// Initializes a new instance of the entity with the specified identifier.
    /// </summary>
    /// <param name="id">The entity identifier.</param>
    protected Entity(TId id)
    {
        ArgumentNullException.ThrowIfNull(id);
        Id = id;
    }

    /// <summary>
    /// Parameterless constructor for memento restoration via
    /// <see cref="System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject"/>.
    /// </summary>
    protected Entity()
    {
    }

    /// <inheritdoc />
    public bool Equals(Entity<TId>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (GetType() != other.GetType())
        {
            return false;
        }

        return EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as Entity<TId>);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    /// <summary>
    /// Determines whether two entities are equal by identity.
    /// </summary>
    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) => Equals(left, right);

    /// <summary>
    /// Determines whether two entities are not equal by identity.
    /// </summary>
    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !Equals(left, right);
}
