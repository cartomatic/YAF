using Yaf.Domain.Interfaces;

namespace Yaf.Domain;

/// <summary>
/// Abstract base class for tenant-scoped mementos, providing default property
/// implementations for <see cref="ITenantMementoBase"/>: identity, accountability,
/// timestamps, version info, and tenant.
/// </summary>
/// <remarks>
/// <para>
/// Consumers extend this class and add entity-specific properties.
/// For non-tenant-scoped mementos, use <see cref="MementoBase"/>.
/// </para>
/// <para>
/// For mementos requiring soft-delete or version history, also implement
/// <see cref="IHasSoftDelete"/> or <see cref="IHasVersionHistory"/> directly.
/// </para>
/// </remarks>
public abstract class TenantMementoBase : MementoBase, ITenantMementoBase
{
    /// <inheritdoc />
    public Guid? TenantId { get; set; }
}
