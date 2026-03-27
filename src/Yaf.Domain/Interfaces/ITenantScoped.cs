namespace Yaf.Domain.Interfaces;

/// <summary>
/// Non-generic marker interface for entities that are scoped to a tenant.
/// Enables runtime discovery by infrastructure for automatic EF Core
/// global query filter application (<c>WHERE TenantId = @current</c>).
/// </summary>
/// <remarks>
/// Implement the generic <see cref="ITenantScoped{TTenantId}"/> on concrete entities.
/// This non-generic base exists solely for runtime <c>is</c> checks.
/// </remarks>
public interface ITenantScoped;

/// <summary>
/// Enforces tenant context on entities. The tenant identifier is set at creation
/// time and is immutable by convention — infrastructure validates this on save.
/// </summary>
/// <remarks>
/// <para>
/// Entity constructors or factory methods are responsible for accepting and setting
/// <see cref="TenantId"/> at creation time. The memento auto-handling in
/// <see cref="Helpers.MementoHelper{TId,TSelf,TMemento}"/> only covers the persistence
/// round-trip (Snapshot/Restore/Hydrate).
/// </para>
/// <para>
/// The framework provides <see cref="Yaf.Domain.TenantId"/> as a convenience default,
/// but consumers may define their own tenant identifier type.
/// </para>
/// </remarks>
/// <typeparam name="TTenantId">
/// The strongly-typed tenant identifier. Must implement <see cref="ITypedId"/>.
/// </typeparam>
public interface ITenantScoped<TTenantId> : ITenantScoped
    where TTenantId : ITypedId
{
    /// <summary>
    /// The tenant this entity belongs to. Set at creation, immutable by convention.
    /// </summary>
    TTenantId TenantId { get; }
}
