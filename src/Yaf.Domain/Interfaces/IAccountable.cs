namespace Yaf.Domain.Interfaces;

/// <summary>
/// Enforces accountability tracking on entities — who created and who last modified them.
/// </summary>
/// <remarks>
/// <para>
/// Both properties are nullable. <see langword="null"/> indicates the entity has not yet
/// completed a persistence round-trip. Infrastructure auto-populates these from
/// <c>IIdentityContextProvider</c> during <c>SaveChanges</c> and must throw if actor context
/// is not available when saving an accountable entity.
/// </para>
/// <para>
/// Deletion tracking is a separate concern — see <see cref="ISoftDeletable"/>.
/// </para>
/// <para>
/// Uses the framework-provided <see cref="Yaf.Domain.ActorId"/> type.
/// </para>
/// </remarks>
public interface IAccountable
{
    /// <summary>
    /// The actor who created this entity. <see langword="null"/> before first persistence.
    /// </summary>
    ActorId? CreatedBy { get; }

    /// <summary>
    /// The actor who last modified this entity. <see langword="null"/> until first modification.
    /// </summary>
    ActorId? ModifiedBy { get; }
}

/// <summary>
/// Internal write-side complement to <see cref="IAccountable"/> for infrastructure hydration.
/// Entities implement both; domain consumers see only the read-only <see cref="IAccountable"/>.
/// </summary>
internal interface IAccountableWriter
{
    /// <inheritdoc cref="IAccountable.CreatedBy"/>
    ActorId? CreatedBy { get; set; }

    /// <inheritdoc cref="IAccountable.ModifiedBy"/>
    ActorId? ModifiedBy { get; set; }
}
