namespace Yaf.Domain.Interfaces;

/// <summary>
/// Non-generic marker interface for entities that track who created and modified them.
/// Enables runtime discovery by infrastructure and <see cref="Helpers.MementoHelper{TId,TSelf,TMemento}"/>
/// without requiring knowledge of the actor identifier type.
/// </summary>
/// <remarks>
/// Implement the generic <see cref="IAccountable{TActorId}"/> on concrete entities.
/// This non-generic base exists solely for runtime <c>is</c> checks.
/// </remarks>
public interface IAccountable;

/// <summary>
/// Enforces accountability tracking on entities — who created and who last modified them.
/// </summary>
/// <remarks>
/// <para>
/// Both properties are nullable. <see langword="null"/> indicates the entity has not yet
/// completed a persistence round-trip. Infrastructure auto-populates these from
/// <c>IIdentityContextProvider</c> during <c>SaveChanges</c> and must throw if user context
/// is not available when saving an accountable entity.
/// </para>
/// <para>
/// Deletion tracking is a separate concern — see <see cref="ISoftDeletable{TActorId}"/>.
/// </para>
/// </remarks>
/// <typeparam name="TActorId">
/// The strongly-typed actor identifier (e.g., <c>UserId</c>, <c>EmployeeId</c>).
/// Must implement <see cref="ITypedId"/> for compile-time safety and runtime bridging.
/// </typeparam>
public interface IAccountable<TActorId> : IAccountable
    where TActorId : ITypedId
{
    /// <summary>
    /// The actor who created this entity. <see langword="null"/> before first persistence.
    /// </summary>
    TActorId? CreatedBy { get; }

    /// <summary>
    /// The actor who last modified this entity. <see langword="null"/> until first modification.
    /// </summary>
    TActorId? ModifiedBy { get; }
}
