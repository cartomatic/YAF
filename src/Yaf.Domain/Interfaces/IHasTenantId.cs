namespace Yaf.Domain.Interfaces;

/// <summary>
/// Non-generic base interface for mementos that carry a tenant identifier.
/// Provides runtime access to the tenant identity type and boxed value
/// for automatic tenant handling in entity base classes.
/// </summary>
/// <remarks>
/// When present on a memento, infrastructure auto-applies an EF Core global query filter:
/// <c>WHERE TenantId = @current</c>.
/// </remarks>
public interface IHasTenantId
{
    /// <summary>
    /// The <see cref="Type"/> of the tenant identity value (e.g., <c>typeof(Guid)</c>).
    /// </summary>
    Type TenantIdType { get; }

    /// <summary>
    /// The tenant identity value boxed as <see cref="object"/>.
    /// <see langword="null"/> when the tenant has not been set.
    /// </summary>
    object? BoxedTenantId { get; set; }
}

/// <summary>
/// Enforces a typed tenant identity property on memento types, enabling base entity classes
/// to handle tenant snapshot, restore, and hydrate automatically.
/// </summary>
/// <typeparam name="T">The backing value type of the tenant identity (e.g., <see cref="Guid"/>).</typeparam>
public interface IHasTenantId<T> : IHasTenantId
    where T : struct, IEquatable<T>
{
    /// <summary>
    /// The tenant identity value. <see langword="null"/> when the tenant has not been set.
    /// </summary>
    T? TenantId { get; set; }

    /// <inheritdoc />
    Type IHasTenantId.TenantIdType => typeof(T);

    /// <inheritdoc />
    object? IHasTenantId.BoxedTenantId
    {
        get => TenantId;
        set => TenantId = Helpers.BoxingHelper.Unbox<T>(value);
    }
}
