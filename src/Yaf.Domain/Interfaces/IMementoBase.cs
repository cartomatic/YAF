namespace Yaf.Domain.Interfaces;

/// <summary>
/// Composite memento interface combining the standard cross-cutting concerns
/// for non-tenant-scoped entities: identity, accountability, timestamps, and version info.
/// </summary>
/// <remarks>
/// <para>
/// Use <see cref="ITenantMementoBase"/> for tenant-scoped entities.
/// </para>
/// <para>
/// Consumers can implement this interface directly or extend <see cref="MementoBase"/>
/// to get default property implementations. Soft-delete (<see cref="IHasSoftDelete"/>)
/// and version history (<see cref="IHasVersionHistory"/>) are independent opt-in concerns
/// and are not included in this composite.
/// </para>
/// </remarks>
public interface IMementoBase : IHasIdentity, IHasAccountability, IHasTimestamps, IHasVersionInfo;
