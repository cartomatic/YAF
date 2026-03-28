namespace Yaf.Domain.Interfaces;

/// <summary>
/// Enforces a tenant identity property on memento types, enabling base entity classes
/// to handle tenant snapshot, restore, and hydrate automatically.
/// </summary>
/// <remarks>
/// When present on a memento, infrastructure auto-applies an EF Core global query filter:
/// <c>WHERE TenantId = @current</c>.
/// The tenant identity is stored as a <see cref="Guid"/> value. The typed ID wrapper
/// (e.g., <c>TenantId</c>) is reconstructed during hydration.
/// </remarks>
public interface IHasTenantId
{
    /// <summary>
    /// The tenant identity value. <see langword="null"/> when the tenant has not been set.
    /// </summary>
    Guid? TenantId { get; set; }
}
