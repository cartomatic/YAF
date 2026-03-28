namespace Yaf.Domain.Interfaces;

/// <summary>
/// Enforces tenant context on entities. The tenant identifier is set at creation
/// time and is immutable by convention — infrastructure validates this on save.
/// </summary>
/// <remarks>
/// <para>
/// Entity constructors or factory methods are responsible for accepting and setting
/// <see cref="TenantId"/> at creation time. Consumers handle the tenant ID in their
/// <c>SnapshotCore</c>/<c>HydrateCore</c> implementations.
/// </para>
/// <para>
/// Uses the framework-provided <see cref="Yaf.Domain.TenantId"/> type.
/// </para>
/// </remarks>
public interface ITenantScoped
{
    /// <summary>
    /// The tenant this entity belongs to. Set at creation, immutable by convention.
    /// </summary>
    TenantId TenantId { get; }
}
