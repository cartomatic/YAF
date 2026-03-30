using Yaf.Domain.Interfaces;

namespace Yaf.Domain;

/// <summary>
/// Abstract base class for non-tenant-scoped mementos, providing default property
/// implementations for <see cref="IMementoBase"/>: identity, accountability,
/// timestamps, and version info.
/// </summary>
/// <remarks>
/// <para>
/// Consumers extend this class and add entity-specific properties.
/// For tenant-scoped mementos, use <see cref="TenantMementoBase"/>.
/// </para>
/// <para>
/// For mementos requiring soft-delete or version history, also implement
/// <see cref="IHasSoftDelete"/> or <see cref="IHasVersionHistory"/> directly.
/// </para>
/// </remarks>
public abstract class MementoBase : IMementoBase
{
    /// <inheritdoc />
    public Guid? Id { get; set; }

    /// <inheritdoc />
    public Guid? CreatedBy { get; set; }

    /// <inheritdoc />
    public Guid? ModifiedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <inheritdoc />
    public Guid Version { get; set; }
}
