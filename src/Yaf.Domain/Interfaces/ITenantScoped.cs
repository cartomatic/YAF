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
/// The framework provides <see cref="Yaf.Domain.TenantId"/> as a convenience default,
/// but consumers may define their own tenant identifier type.
/// </para>
/// </remarks>
/// <typeparam name="TTenantId">
/// The strongly-typed tenant identifier. Must implement <see cref="ITypedId"/>.
/// </typeparam>
public interface ITenantScoped<TTenantId>
    where TTenantId : ITypedId
{
    /// <summary>
    /// The tenant this entity belongs to. Set at creation, immutable by convention.
    /// </summary>
    TTenantId TenantId { get; }
}
